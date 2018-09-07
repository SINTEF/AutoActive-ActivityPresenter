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
        public uint MaxFramesBefore { get; set; }
        public uint MinFramesAfter { get; set; }
        public uint BatchFramesToRead { get; set; }

        readonly IVideoDecoder decoder;

        readonly SemaphoreSlim bufferLock;
        bool isDecodingFrames;
        readonly LinkedList<VideoDecoderFrame> frameBuffer;

        public BufferedVideoDecoder(IVideoDecoder decoder)
        {
            this.decoder = decoder;
            bufferLock = new SemaphoreSlim(1, 1);
            isDecodingFrames = false;
            frameBuffer = new LinkedList<VideoDecoderFrame>();

            MaxFramesBefore = 24;
            MinFramesAfter = 24;
            BatchFramesToRead = 24;
        }

        bool TryFindFrameInBuffer(double time, out VideoDecoderFrame frame, out uint framesBefore, out uint framesAfter, out double approxFramerate, out double lastBufferedFrameTime)
        {
            lock (frameBuffer)
            {
                framesBefore = 0;
                framesAfter = 0;

                // Start looking
                var bufferedFrame = frameBuffer.First;

                // The buffer is empty, or only has one frame (so it is impossible to decide if this is the right frame)
                if (bufferedFrame == null || bufferedFrame.Next == null)
                {
                    frame = new VideoDecoderFrame();
                    approxFramerate = double.Epsilon;
                    lastBufferedFrameTime = bufferedFrame?.Value.Time ?? double.MinValue;
                    return false;
                }

                // There are two or more elements in the list
                var foundInBuffer = false;
                frame = new VideoDecoderFrame();
                
                while (bufferedFrame.Next != null)
                {
                    if (bufferedFrame.Value.Time <= time && bufferedFrame.Next.Value.Time > time)
                    {
                        // This is the frame we want
                        frame = bufferedFrame.Value;
                        foundInBuffer = true;
                    }
                    if (!foundInBuffer) framesBefore++;
                    else framesAfter++;
                    bufferedFrame = bufferedFrame.Next;
                }

                // Set the rest of the stats
                approxFramerate = (framesBefore+framesAfter)/(frameBuffer.Last.Value.Time - frameBuffer.First.Value.Time);
                lastBufferedFrameTime = frameBuffer.Last.Value.Time;

                return foundInBuffer; 
            }
        }

        async Task LoadMoreFrames()
        {
            // Wait for the lock to do some work
            await bufferLock.WaitAsync();

            isDecodingFrames = true;

            // Start loading a set of new frames
            for (var i = 0; i < BatchFramesToRead; i++)
            {
                var frame = await decoder.DecodeNextFrameAsync();
                lock (frameBuffer)
                {
                    frameBuffer.AddLast(frame);
                }
            }

            isDecodingFrames = false;

            // We are done, release the lock
            bufferLock.Release();
        }

        /* -- Public API -- */

        public Task<double> GetLengthAsync()
        {
            return decoder.GetLengthAsync();
        }

        // TODO: TOKEN??
        public async Task SetSizeAsync(uint width, uint height, bool flushOldBuffered = false)
        {
            if (flushOldBuffered)
            {
                // Wait for loading to finish
                await bufferLock.WaitAsync();
                // Clear the buffer, and keep track of which frames we had
                double? firstFrameTime;
                lock (frameBuffer)
                {
                    firstFrameTime = frameBuffer.First?.Value.Time;
                    frameBuffer.Clear();
                }
                // Resize the decoder
                await decoder.SetSizeAsync(width, height);
                // Seek to the first frame we had
                if (firstFrameTime.HasValue)
                {
                    await decoder.SeekToAsync(firstFrameTime.Value);
                }
                // Then let's load some more frames
                bufferLock.Release();
                LoadMoreFrames();
            }
            else
            {
                await decoder.SetSizeAsync(width, height);
            }
        }

        public async Task<VideoDecoderFrame> GetFrameAtAsync(double time, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (TryFindFrameInBuffer(time, out var frame, out var before, out var after, out var framerate, out var last))
            {
                // We already have this frame :)
                if (before > MaxFramesBefore)
                {
                    // Clear up the frames before the current
                    lock (frameBuffer)
                    {
                        while (before > 0)
                        {
                            frameBuffer.RemoveFirst();
                            before--;
                        }
                    }
                }
                if (after < MinFramesAfter)
                {
                    // Also start loading some more
                    LoadMoreFrames();
                }
                // Return the frame
                return frame;
            }
            else
            {
                // We need to load this frame somehow
                var needToDecode = (time - last) * framerate;
                if (needToDecode >= 0 && needToDecode < 2*BatchFramesToRead)
                {
                    // We can just continue to load more frames sequentially, it shouldn't be too long until we get it
                    if (!isDecodingFrames)
                    {
                        // We are currently not loading, so we need to trigger it
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
                            frameBuffer.Clear();
                        }
                        // Seek to the selected time
                        seeked = await decoder.SeekToAsync(time, cancellationToken);

                        // Load some more frames
                        bufferLock.Release();
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
