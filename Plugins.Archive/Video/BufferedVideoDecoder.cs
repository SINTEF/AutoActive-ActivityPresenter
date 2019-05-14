using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Video
{
    public class BufferedVideoDecoder
    {
        private readonly IVideoDecoder _decoder;

        private const uint MinFramesAfter = 10;
        private const uint BatchFramesToRead = 10;
        private const uint FrameBufferLength = MinFramesAfter + BatchFramesToRead + 1;

        // A circular buffer of frames
        private readonly SemaphoreSlim _bufferLock = new SemaphoreSlim(1, 1);
        private bool _isDecodingFrames;
        private readonly VideoDecoderFrame[] _frameBuffer = new VideoDecoderFrame[FrameBufferLength];
        private uint _frameBufferNextWritePosition;

        private byte[] _dataBuffer;
        private uint _dataBufferFrameStride;
        readonly bool DEBUG_OUTPUT = false;

        public BufferedVideoDecoder(IVideoDecoder decoder)
        {
            _decoder = decoder;
            CreateDataBuffer(0, 0);
        }

        private bool TryFindFrameInBuffer(long time, out VideoDecoderFrame frame, out uint framesAfter, out double approxFramerate, out long lastBufferedFrameTime)
        {
            lock (_frameBuffer)
            {
                approxFramerate = double.Epsilon;
                frame = new VideoDecoderFrame();
                framesAfter = 0;

                var framesInBuffer = 0;
                var foundInBuffer = false;
                var lastBufferedFrame = new VideoDecoderFrame();

                // Loop through all frames in circular buffer
                for (var i = 0; i < FrameBufferLength-1; i++)
                {
                    var j = (_frameBufferNextWritePosition + 1 + i) % FrameBufferLength;
                    var bufferedFrame = _frameBuffer[j];
                    if (!bufferedFrame.Loaded)
                    {
                        // This frame is not loaded
                        continue;
                    }

                    // We need more than one frame, so don't do anything for the first
                    if (framesInBuffer > 0)
                    {
                        if (lastBufferedFrame.Time <= time && bufferedFrame.Time > time)
                        {
                            // The last frame is the frame we want
                            frame = lastBufferedFrame;
                            foundInBuffer = true;
                        }
                        if (foundInBuffer) framesAfter++;

                        approxFramerate += bufferedFrame.Time - lastBufferedFrame.Time;
                    }

                    // Keep the last one in memory
                    lastBufferedFrame = bufferedFrame;
                    framesInBuffer++;
                }

                if (framesInBuffer > 2)
                {
                    approxFramerate /= framesInBuffer - 1;
                }

                if (framesInBuffer > 0)
                {
                    lastBufferedFrameTime = lastBufferedFrame.Time;
                }
                else
                {
                    lastBufferedFrameTime = long.MinValue;
                }

                return foundInBuffer;
            }
        }

        private void ClearBuffer()
        {
            for (var i = 0; i < FrameBufferLength; i++)
            {
                _frameBuffer[i] = new VideoDecoderFrame();
            }
            _frameBufferNextWritePosition = 0;
        }

        private void CreateDataBuffer(uint width, uint height)
        {
            _dataBufferFrameStride = width * height * 4;
            if (DEBUG_OUTPUT) Debug.WriteLine($"CREATING DATA BUFFER SIZE: {width}x{height} - {_dataBufferFrameStride * FrameBufferLength}");
            _dataBuffer = new byte[_dataBufferFrameStride * FrameBufferLength];
        }

        private async Task LoadMoreFrames()
        {
            try
            {
                Int32 timeoutMs = 5000;
                // Wait for the lock to do some work
                await _bufferLock.WaitAsync();
                if (DEBUG_OUTPUT) Debug.WriteLine("A - Locked");

                _isDecodingFrames = true;

                // Start loading a set of new frames
                for (var i = 0; i < BatchFramesToRead; i++)
                {
                    var nextSegment = new ArraySegment<byte>(_dataBuffer, (int)(_frameBufferNextWritePosition * _dataBufferFrameStride), (int)_dataBufferFrameStride);
                    var task = _decoder.DecodeNextFrameAsync(nextSegment);
                    if (await Task.WhenAny(task, Task.Delay(timeoutMs)) != task)
                    {
                        //TODO: We likely need to reinstanciate the decoder if this happens
                        continue;
                    }
                    var frame = await task;
                    lock (_frameBuffer)
                    {
                        _frameBuffer[_frameBufferNextWritePosition] = frame;
                        _frameBufferNextWritePosition = (_frameBufferNextWritePosition + 1) % FrameBufferLength;
                    }
                }

                _isDecodingFrames = false;
            }
            finally
            {
                // We are done, release the lock
                if (DEBUG_OUTPUT) Debug.WriteLine("A - Unlock");
                _bufferLock.Release();
            }
        }

        /* -- Public API -- */

        public Task<long> GetLengthAsync()
        {
            return _decoder.GetLengthAsync();
        }

        // TODO: TOKEN??
        public async Task SetSizeAsync(uint width, uint height)
        {
            // Wait for loading to finish
            await _bufferLock.WaitAsync();
            try
            {
                if (DEBUG_OUTPUT) Debug.WriteLine("B - Locked");
                // Clear the buffer, and keep track of which frames we had
                long? firstFrameTime = null;
                lock (_frameBuffer)
                {
                    var j = (_frameBufferNextWritePosition + 1) % FrameBufferLength;
                    var previousFirstFrame = _frameBuffer[j];
                    if (previousFirstFrame.Loaded)
                        firstFrameTime = previousFirstFrame.Time;

                    ClearBuffer();
                }
                // Resize the decoder, and our databuffer
                var (actualWidth, actualHeight) = await _decoder.SetSizeAsync(width, height);
                CreateDataBuffer(actualWidth, actualHeight);
                // Seek to the first frame we had
                if (firstFrameTime.HasValue)
                {
                    await _decoder.SeekToAsync(firstFrameTime.Value);
                }
            }
            finally
            {
                if (DEBUG_OUTPUT) Debug.WriteLine("B - Unlock");
                _bufferLock.Release();
            }
            // Then let's load some more frames
            LoadMoreFrames();
        }

        public async Task<VideoDecoderFrame> GetFrameAtAsync(long time, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryFindFrameInBuffer(time, out var frame, out var after, out var framerate, out var last))
            {
                // We already have this frame :)
                if (after < MinFramesAfter && !_isDecodingFrames)
                {
                    // Start loading some more frames
                    //Debug.WriteLine($"BUFFEREDVIDEODECODER - 1");
                    LoadMoreFrames();
                }
                // Return the frame
                return frame;
            }
            else
            {
                //Debug.WriteLine($"BUFFEREDVIDEODECODER - NOT FOUND {time},{after},{framerate},{last}");
                // We need to load this frame somehow
                var needToDecode = (time - last) / framerate;
                if (needToDecode >= 0 && needToDecode < 2 * BatchFramesToRead)
                {
                    // We can just continue to load more frames sequentially, it shouldn't be too long until we get it
                    if (!_isDecodingFrames)
                    {
                        // We are currently not loading, so we need to trigger it
                        if (DEBUG_OUTPUT) Debug.WriteLine($"BUFFEREDVIDEODECODER - 2 - {needToDecode}");
                        await LoadMoreFrames();
                    }
                    else
                    {
                        // Wait for the loading to finish
                        await _bufferLock.WaitAsync(cancellationToken);
                        if (DEBUG_OUTPUT) Debug.WriteLine($"C - Locked ({_bufferLock.CurrentCount})");
                        _bufferLock.Release();
                        if (DEBUG_OUTPUT) Debug.WriteLine("C - Unlocked");
                    }
                    // Then re-run ourself to finish the work
                    return await GetFrameAtAsync(time, cancellationToken);
                }
                else
                {
                    // The frame is far away from the buffer, it is safer to scrap the buffer, and start from scratch
                    // Wait for anything to finish
                    var seeked = time;
                    await _bufferLock.WaitAsync(cancellationToken);
                    if (DEBUG_OUTPUT) Debug.WriteLine($"D - Locked ({_bufferLock.CurrentCount})");
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        // Clear the buffer
                        lock (_frameBuffer)
                        {
                            ClearBuffer();
                        }
                        // Seek to the selected time
                        seeked = await _decoder.SeekToAsync(time, cancellationToken);

                        // Load some more frames
                        _bufferLock.Release();
                        if (DEBUG_OUTPUT) Debug.WriteLine("D - Unlocked 1");
                        if (DEBUG_OUTPUT) Debug.WriteLine($"BUFFEREDVIDEODECODER - 3");
                        await LoadMoreFrames();
                    }
                    else
                    {
                        if (DEBUG_OUTPUT) Debug.WriteLine($"BUFFEREDVIDEODECODER - X");
                        //TODO: this is likely a race condition
                        if (_bufferLock.CurrentCount == 0)
                        {
                            _bufferLock.Release();
                            if (DEBUG_OUTPUT) Debug.WriteLine("D - Unlocked 2");
                        }
                        else
                        {
                            if (DEBUG_OUTPUT) Debug.WriteLine($"BUFFEREDVIDEODECODER - Y");
                        }
                    }
                    // Then re-run ourself to finish the work, but with the resulting seeked time as the new time
                    // When we seek, we might not have a frame exactly where we asked for, and we can only find the time for the next one
                    // So that has to be close enough
                    return await GetFrameAtAsync(seeked, cancellationToken);

                }
            }
        }
    }
}
