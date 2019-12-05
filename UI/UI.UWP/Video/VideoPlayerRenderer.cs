using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI;
using SINTEF.AutoActive.UI.UWP.Views;
using Xamarin.Forms.Platform.UWP;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

[assembly: ExportRenderer(typeof(VideoPlayer), typeof(VideoPlayerRenderer))]

namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class VideoPlayerRenderer : ViewRenderer<VideoPlayer, MediaElement>
    {
        public static async Task<IRandomAccessStream> GetVideoStream(IReadSeekStreamFactory factory)
        {
            var stream = await factory.GetReadStream();
            return stream.AsRandomAccessStream();
        }

        private MediaElement _mediaElement;
        private VideoPlayer _videoPlayer;
        private DateTime _lastUpdate;

        protected override async void OnElementChanged(ElementChangedEventArgs<VideoPlayer> args)
        {
            base.OnElementChanged(args);

            if (args.OldElement != null)
            {
                var oldVideoPlayer = args.OldElement;
                oldVideoPlayer.PositionChanged -= VideoPlayerOnPositionChanged;
                oldVideoPlayer.PlayingChanged -= VideoPlayerOnPlayingChanged;
                oldVideoPlayer.PlaybackRateChanged -= PlaybackRateChanged;
            }

            if (args.NewElement == null) return;

            if (Control == null)
            {
                _mediaElement = new MediaElement();
                SetNativeControl(_mediaElement);

                _mediaElement.PlaybackRate = 1d;
                _mediaElement.AutoPlay = false;
                _mediaElement.IsMuted = true;
                _mediaElement.Volume = 0d;
            }

            _videoPlayer = args.NewElement;

            _mediaElement.AutoPlay = _videoPlayer.IsPlaying;
            Control.SetSource(await GetVideoStream(_videoPlayer.Source), _videoPlayer.MimeType);


            _videoPlayer.PositionChanged += VideoPlayerOnPositionChanged;
            _videoPlayer.PlayingChanged += VideoPlayerOnPlayingChanged;
            _videoPlayer.PlaybackRateChanged += PlaybackRateChanged;
            PlaybackRateChanged(this, _videoPlayer.PlaybackRate);

            if (_videoPlayer.IsPlaying)
            {
                _mediaElement.Play();
            }
        }

        private void PlaybackRateChanged(object sender, double rate)
        {
            if (rate == 0d) rate = 1d;
            _mediaElement.DefaultPlaybackRate = rate;
            _mediaElement.PlaybackRate = rate;
        }

        private bool _currentlyPlaying;
        private void VideoPlayerOnPlayingChanged(object sender, bool isPlaying)
        {
            _currentlyPlaying = isPlaying;

            if (isPlaying) _mediaElement.Play();
            else _mediaElement.Pause();
        }

        private double _offsetCompensator;
        private double _lastOffsetCompensator;
        private const int OffsetQueueElements = 120;
        private const double OffsetEqualComp = 0.1d;
        private readonly Queue<double> _offsetQueue = new Queue<double>(OffsetQueueElements);

        private void SetVideoPosition(TimeSpan wantedPosition, double allowedOffset)
        {
            var duration = _mediaElement.NaturalDuration.TimeSpan;
            if (wantedPosition > duration)
            {
                _mediaElement.Pause();
                if (_mediaElement.Position < duration)
                {
                    _mediaElement.Position = duration;
                }
                return;
            }

            if (wantedPosition.TotalSeconds < 0)
            {
                _mediaElement.Position = TimeSpan.Zero;
                _mediaElement.Pause();
                return;
            }

            if (!_currentlyPlaying)
            {
                _mediaElement.Position = wantedPosition;
                _videoPlayer.CurrentOffset = (_mediaElement.Position - wantedPosition).TotalSeconds;
                return;
            }

            var now = DateTime.Now;

            var expectedDiff = now - _lastUpdate;
            var diff = (_mediaElement.Position - wantedPosition).TotalSeconds - expectedDiff.TotalSeconds;

            _videoPlayer.CurrentOffset = diff;

            var offset = Math.Abs(diff);

            if (_offsetQueue.Count >= OffsetQueueElements)
            {
                _offsetQueue.Dequeue();
            }
            _offsetQueue.Enqueue(diff);

            var offsetChanged = false;

            if(_offsetQueue.Count == OffsetQueueElements)
            {
                var first = _offsetQueue.Peek();
                if (_offsetQueue.All(el => Math.Abs(first - el) < OffsetEqualComp*3)) {
                    var variance = Math.Sqrt(_offsetQueue.Sum(el => (el - first) * (el - first)));
                    var newOffset = _offsetQueue.Average();
                    if (variance < OffsetEqualComp)
                    {
                        Debug.WriteLine($"Variance ${variance}");
                        _offsetCompensator += (newOffset / 2);
                        if(Math.Abs(_offsetCompensator) > _videoPlayer.AllowedOffset)
                        {
                            _offsetCompensator = 0;
                        }
                        if (Math.Abs(_lastOffsetCompensator - _offsetCompensator) > OffsetEqualComp)
                        {
                            offsetChanged = true;
                        }
                        Debug.WriteLine("Offset compensator changed.");
                    }
                }
            }

            // A possibility here would be to estimate the expected offset and compensate for it
            if (offsetChanged || offset > allowedOffset * _mediaElement.DefaultPlaybackRate)
            {
                _offsetQueue.Clear();
                _lastOffsetCompensator = _offsetCompensator;
                _mediaElement.Position = wantedPosition.Add(new TimeSpan((long)(_offsetCompensator * -10000000L)));
            }

            // Only ensure play state if the time is not later than the duration
            if (_currentlyPlaying && wantedPosition < _mediaElement.NaturalDuration)
            {
                _mediaElement.Play();
            } else
            {
                _mediaElement.Pause();
            }

            _lastUpdate = DateTime.Now;
        }

        private void VideoPlayerOnPositionChanged(object sender, PositionChangedEventArgs args)
        {
            var allowedOffset = _videoPlayer.AllowedOffset;
            XamarinHelpers.EnsureMainThread(() => SetVideoPosition(args.Time, allowedOffset));
        }
    }
}
