using System;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Views;
using System.Threading.Tasks;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.UI.Helpers;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Figures
{
    public class ImageView : FigureView
    {
        private VideoPlayer _player;

        public static async Task<ImageView> Create(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            // TODO: Check that this datapoint has a type that can be used
            var viewer = await context.GetDataViewerFor(datapoint);

            var view = new ImageView(context, datapoint);
            view.AddViewer(viewer);

            if (!(viewer is ArchiveVideoVideoViewer videoViewer))
                return view;

            var (streamFactory, mime) = videoViewer.Video.GetStreamFactory();
            view.SetStreamFactory(streamFactory, mime);

            if (viewer.DataPoint.Time is ArchiveVideoTime time)
            {
                view.TimeOffset = time.Offset;
                time.OffsetChanged += (s, offset) => view.TimeOffset = offset;
            }

            context.SelectedTimeRangeChanged += view.OnSelectedTimeRangeChanged;
            context.IsPlayingChanged += view.IsPlayingChanged;
            context.PlaybackRateChanged += view.PlaybackRateChanged;
            view.IsPlayingChanged(null, context.IsPlaying);
            view.PlaybackRateChanged(null, context.PlaybackRate);

            return view;
        }

        public long TimeOffset { get; set; }

        private void IsPlayingChanged(object sender, bool isPlaying)
        {
            _player.IsPlaying = isPlaying;
        }

        private void PlaybackRateChanged(object sender, double playbackRate)
        {
            _player.PlaybackRate = playbackRate;
        }

        private void OnSelectedTimeRangeChanged(SingleSetDataViewerContext sender, long from, long to)
        {
            var diff = TimeFormatter.SecondsFromTime(from - TimeOffset);
            try
            {
                _player.Position = TimeSpan.FromSeconds(diff);
            }
            catch(OverflowException)
            {
                //TODO: add warning?
            }
        }

        private void SetStreamFactory(IReadSeekStreamFactory streamFactory, string mime)
        {
            Canvas.IsVisible = false;

            _player = new VideoPlayer();

            GridLayout.Children.Add(_player);
            Grid.SetColumn(_player, Grid.GetColumn(Canvas));
            Grid.SetRow(_player, Grid.GetRow(Canvas));
            Grid.SetRowSpan(_player, Grid.GetRowSpan(Canvas));
            Grid.SetColumnSpan(_player, Grid.GetColumnSpan(Canvas));

            _player.Source = streamFactory;
            _player.MimeType = mime;
            _player.Position = TimeSpan.Zero;
        }

        protected ImageView(TimeSynchronizedContext context, IDataPoint dataPoint) : base(context, dataPoint)
        {
        }

        /// Remove datapoint (by removing this image view) if present here.
        protected override void RemoveDataPoint(IDataPoint datapoint)
        {
            if (DataPoints.Contains(datapoint))
                RemoveThisView();
        }
    }
}
