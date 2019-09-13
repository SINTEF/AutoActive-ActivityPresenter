using SINTEF.AutoActive.UI.UWP.Video;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using System.Threading.Tasks;
using Xamarin.Forms;
using Windows.Storage.Streams;
using Windows.Media.Playback;
using Windows.Media.Core;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.UWP.Views;


[assembly: Dependency(typeof(VideoLengthExtractorFactory))]
namespace SINTEF.AutoActive.UI.UWP.Video
{
    public class VideoLengthExtractor : IVideoLengthExtractor
    {
        private long? _videoLength;
        private readonly TaskCompletionSource<long> _videoLengthTask = new TaskCompletionSource<long>();

        public VideoLengthExtractor(IRandomAccessStream stream, string mime, long reportedLength)
        {
            if(_videoLength.HasValue) _videoLengthTask.SetResult(_videoLength.Value);

            ReportedLength = reportedLength;

            var source = MediaSource.CreateFromStream(stream, mime);

            var item = new MediaPlaybackItem(source);

            var mediaPlayer = new MediaPlayer
            {
                AutoPlay = false,
                IsMuted = true,
                Source = item,
            };
            mediaPlayer.MediaOpened += MediaPlayerOnMediaOpened;
        }

        private void MediaPlayerOnMediaOpened(MediaPlayer sender, object args)
        {
            var videoLength = TimeFormatter.TimeFromTimeSpan(sender.PlaybackSession.NaturalDuration);
            sender.MediaOpened -= MediaPlayerOnMediaOpened;
            
            if (videoLength == 0)
            {
                sender.CurrentStateChanged += CurrentStateChanged;
                return;
            }

            _videoLength = videoLength;
            _videoLengthTask.SetResult(_videoLength.Value);

            sender.Dispose();
        }

        private void CurrentStateChanged(MediaPlayer sender, object args)
        {
            var videoLength = TimeFormatter.TimeFromTimeSpan(sender.PlaybackSession.NaturalDuration);
            if (videoLength == 0)
                return;
            sender.CurrentStateChanged -= CurrentStateChanged;

            _videoLength = videoLength;
            _videoLengthTask.SetResult(_videoLength.Value);

            sender.Dispose();
        }

        public Task<long> GetLengthAsync()
        {
            return _videoLengthTask.Task;
        }

        public long ReportedLength { get; }
    }

    public class VideoLengthExtractorFactory : IVideoLengthExtractorFactory
    {
        public async Task<IVideoLengthExtractor> CreateVideoDecoder(IReadSeekStreamFactory file, string mime, long suggestedLength)
        {
            return new VideoLengthExtractor(await VideoPlayerRenderer.GetVideoStream(file), mime, suggestedLength);
        }
    }
}
