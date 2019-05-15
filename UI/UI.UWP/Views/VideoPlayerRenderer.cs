using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI;
using SINTEF.AutoActive.UI.UWP.FileSystem;
using SINTEF.AutoActive.UI.UWP.Views;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(VideoPlayer), typeof(VideoPlayerRenderer))]

namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class VideoPlayerRenderer : ViewRenderer<VideoPlayer, MediaElement>
    {
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

                    _mediaElement.MediaOpened += OnMediaElementMediaOpened;
                    _mediaElement.PlaybackRate = 0.1;
                    _mediaElement.AutoPlay = false;
                    _mediaElement.IsMuted = true;
                    _mediaElement.Volume = 0;
                }

                var videoPlayer = args.NewElement;
                if (videoPlayer.Source is Archive.Archive.ArchiveFileBoundFactory streamFactory)
                {
                    var stream = await streamFactory.GetBoundedStream();
                    Control.SetSource(stream.AsRandomAccessStream(), videoPlayer.MimeType);
                    /*var binding = new Binding {Source = videoPlayer, Path = new PropertyPath("Position"), Mode = BindingMode.OneWay};
                    Control.SetBinding(MediaElement.PositionProperty, binding);*/

                    videoPlayer.PositionChanged += VideoPlayerOnPositionChanged;
                    videoPlayer.PlayingChanged += VideoPlayerOnPlayingChanged;
                    videoPlayer.PlaybackRateChanged += PlaybackRateChanged;
                }
                else
                {
                    throw new ArgumentException("Video player must be stream factory");
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

        private void VideoPlayerOnPositionChanged(object sender, PositionChangedEventArgs args)
        {
            var allowedOffset = 3.0;
            if (sender is VideoPlayer player)
            {
                allowedOffset = player.AllowedOffset;
            }

            var offset = Math.Abs((_mediaElement.Position - args.Time).TotalSeconds);

            if (offset > allowedOffset)
            {
                _mediaElement.Position = args.Time;
            }

            // Only ensure play state if the time is not later than the duration
            if (_currentlyPlaying && args.Time < _mediaElement.NaturalDuration)
            {
                _mediaElement.Play();
            }
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == VideoPlayer.SourceProperty.PropertyName)
            {

            }
        }

        private void OnMediaElementMediaOpened(object sender, RoutedEventArgs e)
        {
            // TODO: provide length
        }
    }
}
