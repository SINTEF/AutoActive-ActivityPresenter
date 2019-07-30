using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI;
using SINTEF.AutoActive.UI.UWP.Views;
using Xamarin.Forms.Platform.UWP;

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
        protected override async void OnElementChanged(ElementChangedEventArgs<VideoPlayer> args)
        {
            base.OnElementChanged(args);

            if (args.NewElement != null)
            {
                if (Control == null)
                {
                    _mediaElement = new MediaElement();
                    SetNativeControl(_mediaElement);

                    _mediaElement.PlaybackRate = 1d;
                    _mediaElement.AutoPlay = false;
                    _mediaElement.IsMuted = true;
                    _mediaElement.Volume = 0d;
                }

                var videoPlayer = args.NewElement;

                _mediaElement.AutoPlay = videoPlayer.IsPlaying;
                Control.SetSource(await GetVideoStream(videoPlayer.Source), videoPlayer.MimeType);


                videoPlayer.PositionChanged += VideoPlayerOnPositionChanged;
                videoPlayer.PlayingChanged += VideoPlayerOnPlayingChanged;
                videoPlayer.PlaybackRateChanged += PlaybackRateChanged;
                PlaybackRateChanged(this, videoPlayer.PlaybackRate);

                if (videoPlayer.IsPlaying)
                {
                    _mediaElement.Play();
                }

            }

            if (args.OldElement != null)
            {
                var oldVideoPlayer = args.OldElement;
                oldVideoPlayer.PositionChanged -= VideoPlayerOnPositionChanged;
                oldVideoPlayer.PlayingChanged -= VideoPlayerOnPlayingChanged;
                oldVideoPlayer.PlaybackRateChanged -= PlaybackRateChanged;
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

        private void SetVideoPosition(TimeSpan wantedPosition, double allowedOffset)
        {
            if (!_currentlyPlaying)
            {
                _mediaElement.Position = wantedPosition;
                return;
            }

            var offset = Math.Abs((_mediaElement.Position - wantedPosition).TotalSeconds);

            // A possibility here would be to estimate the expected offset and compensate for it
            if (offset > allowedOffset)
            {
                _mediaElement.Position = wantedPosition;
            }

            // Only ensure play state if the time is not later than the duration
            if (_currentlyPlaying && wantedPosition < _mediaElement.NaturalDuration)
            {
                _mediaElement.Play();
            }
        }

        private void VideoPlayerOnPositionChanged(object sender, PositionChangedEventArgs args)
        {
            var allowedOffset = 3.0;
            if (sender is VideoPlayer player)
            {
                allowedOffset = player.AllowedOffset;
            }

            XamarinHelpers.EnsureMainThread(() => SetVideoPosition(args.Time, allowedOffset));
        }
    }
}
