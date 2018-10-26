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
        readonly IVideoDecoder decoder;

        readonly uint minFramesAfter;
        readonly uint batchFramesToRead;

        // A circular buffer of frames
        readonly SemaphoreSlim bufferLock;
        bool isDecodingFrames;
        readonly uint frameBufferLength;
        readonly VideoDecoderFrame[] frameBuffer;
        uint frameBufferNextWritePosition;

        byte[] dataBuffer;
        uint dataBufferFrameStride;

        public BufferedVideoDecoder(IVideoDecoder decoder)
        {
            this.decoder = decoder;
            bufferLock = new SemaphoreSlim(1, 1);
            isDecodingFrames = false;

            minFramesAfter = 10;
            batchFramesToRead = 10;

            frameBufferLength = minFramesAfter + batchFramesToRead + 1;
            frameBuffer = new VideoDecoderFrame[frameBufferLength];
            frameBufferNextWritePosition = 0;

            CreateDataBuffer(0, 0);
        }

        bool TryFindFrameInBuffer(long time, out VideoDecoderFrame frame, out uint framesAfter, out double approxFramerate, out long lastBufferedFrameTime)
        {
            lock (frameBuffer)
            {
                approxFramerate = double.Epsilon;
                frame = new VideoDecoderFrame();
                framesAfter = 0;

                var framesInBuffer = 0;
                var foundInBuffer = false;
                VideoDecoderFrame lastBufferedFrame = new VideoDecoderFrame();

                // Loop through all frames in circular buffer
                for (var i = 0; i < frameBufferLength-1; i++)
                {
                    var j = (frameBufferNextWritePosition + 1 + i) % frameBufferLength;
                    var bufferedFrame = frameBuffer[j];
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

        void ClearBuffer()
        {
            for (var i = 0; i < frameBufferLength; i++)
            {
                frameBuffer[i] = new VideoDecoderFrame();
            }
            frameBufferNextWritePosition = 0;
        }

        void CreateDataBuffer(uint width, uint height)
        {
            dataBufferFrameStride = width * height * 4;
            Debug.WriteLine($"CREATING DATA BUFFER SIZE: {width}x{height} - {dataBufferFrameStride * frameBufferLength}");
            dataBuffer = new byte[dataBufferFrameStride*frameBufferLength];
        }

        async Task LoadMoreFrames()
        {
            try
            {
                Int32 timeoutMs = 3000;
                // Wait for the lock to do some work
                await bufferLock.WaitAsync();

                isDecodingFrames = true;

                // Start loading a set of new frames
                for (var i = 0; i < batchFramesToRead; i++)
                {
                    var nextSegment = new ArraySegment<byte>(dataBuffer, (int)(frameBufferNextWritePosition * dataBufferFrameStride), (int)dataBufferFrameStride);
                    var task = decoder.DecodeNextFrameAsync(nextSegment);
                    if (await Task.WhenAny(task, Task.Delay(timeoutMs)) != task)
                    {
                        //TODO: We likely need to reinstanciate the decoder if this happens
                        break;
                    }
                    var frame = await task;
                    lock (frameBuffer)
                    {
                        frameBuffer[frameBufferNextWritePosition] = frame;
                        frameBufferNextWritePosition = (frameBufferNextWritePosition + 1) % frameBufferLength;
                    }
                }

                isDecodingFrames = false;
            }
            finally
            {
                // We are done, release the lock
                bufferLock.Release();
            }
        }

        /* -- Public API -- */

        public Task<long> GetLengthAsync()
        {
            return decoder.GetLengthAsync();
        }

        // TODO: TOKEN??
        public async Task SetSizeAsync(uint width, uint height)
        {
            // Wait for loading to finish
            await bufferLock.WaitAsync();
            try
            {
                // Clear the buffer, and keep track of which frames we had
                long? firstFrameTime = null;
                lock (frameBuffer)
                {
                    var j = (frameBufferNextWritePosition + 1) % frameBufferLength;
                    var previousFirstFrame = frameBuffer[j];
                    if (previousFirstFrame.Loaded)
                        firstFrameTime = previousFirstFrame.Time;

                    ClearBuffer();
                }
                // Resize the decoder, and our databuffer
                var (actualWidth, actualHeight) = await decoder.SetSizeAsync(width, height);
                CreateDataBuffer(actualWidth, actualHeight);
                // Seek to the first frame we had
                if (firstFrameTime.HasValue)
                {
                    await decoder.SeekToAsync(firstFrameTime.Value);
                }
            }
            finally
            {
                bufferLock.Release();
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
                if (after < minFramesAfter && !isDecodingFrames)
                {
                    // Start loading some more frames
                    Debug.WriteLine($"BUFFEREDVIDEODECODER - 1");
                    LoadMoreFrames();
                }
                // Return the frame
                return frame;
            }
            else
            {
                Debug.WriteLine($"BUFFEREDVIDEODECODER - NOT FOUND {time},{after},{framerate},{last}");
                // We need to load this frame somehow
                var needToDecode = (time - last) / framerate;
                if (needToDecode >= 0 && needToDecode < 2*batchFramesToRead)
                {
                    // We can just continue to load more frames sequentially, it shouldn't be too long until we get it
                    if (!isDecodingFrames)
                    {
                        // We are currently not loading, so we need to trigger it
                        Debug.WriteLine($"BUFFEREDVIDEODECODER - 2 - {needToDecode}");
                        await LoadMoreFrames();
                    }
                    else
                    {
                        // Wait for the loading to finish
                        await bufferLock.WaitAsync(cancellationToken);
                        bufferLock.Release();
                    }
                    // Then re-run ourself to finish the work
                    return await GetFrameAtAsync(time, cancellationToken);
                }
                else
                {
                    // The frame is far away from the buffer, it is safer to scrap the buffer, and start from scratch
                    // Wait for anything to finish
                    var seeked = time;
                    await bufferLock.WaitAsync(cancellationToken);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        // Clear the buffer
                        lock (frameBuffer)
                        {
                            ClearBuffer();
                        }
                        // Seek to the selected time
                        seeked = await decoder.SeekToAsync(time, cancellationToken);

                        // Load some more frames
                        bufferLock.Release();
                        Debug.WriteLine($"BUFFEREDVIDEODECODER - 3");
                        await LoadMoreFrames();
                        
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
