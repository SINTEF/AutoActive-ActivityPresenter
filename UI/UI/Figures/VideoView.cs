﻿using System;
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
    public class VideoView : FigureView, IDisposable
    {
        private VideoPlayer _player;
        public static bool PlaybackErrorsHidden = false;

        public static async Task<VideoView> Create(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            // TODO: Check that this datapoint has a type that can be used
            var viewer = await context.GetDataViewerFor(datapoint);

            var view = new VideoView(context, datapoint);
            view.AddViewer(viewer);

            if (!(viewer is ArchiveVideoVideoViewer videoViewer))
                return view;

            var (streamFactory, mime) = videoViewer.Video.GetStreamFactory();
            view.SetStreamFactory(streamFactory, mime);

            if (viewer.DataPoint.Time is ArchiveVideoTime time)
            {
                view.StartTime = time.Offset;
                time.OffsetChanged += (s, offset) => view.StartTime = offset;
                view._player.OffsetChanged += (s, offset) =>
                    time.VideoPlaybackOffset = TimeFormatter.TimeFromSeconds(offset);
            }

            context.SelectedTimeRangeChanged += view.OnSelectedTimeRangeChanged;
            context.IsPlayingChanged += view.IsPlayingChanged;
            context.PlaybackRateChanged += view.PlaybackRateChanged;
            view.IsPlayingChanged(null, context.IsPlaying);
            view.PlaybackRateChanged(null, context.PlaybackRate);

            return view;
        }

        public long StartTime { get; set; }

        private void IsPlayingChanged(object sender, bool isPlaying)
        {
            _player.IsPlaying = isPlaying;
        }

        private void PlaybackRateChanged(object sender, double playbackRate)
        {
            _player.PlaybackRate = playbackRate;
        }

        private void OnSelectedTimeRangeChanged(SingleSetDataViewerContext sender, long currentStart, long to)
        {
            var diff = TimeFormatter.SecondsFromTime(currentStart - StartTime);
            try
            {
                _player.Position = TimeSpan.FromSeconds(diff);
            }
            catch (OverflowException)
            {
                //TODO: add warning?
            }
        }

        private void SetStreamFactory(IReadSeekStreamFactory streamFactory, string mime)
        {
            Canvas.IsVisible = false;

            _player = new VideoPlayer
            {
                Label = new Label
                {
                    Text = "0",
                    TextColor = Color.Lime,
                    HorizontalOptions = new LayoutOptions(LayoutAlignment.End, false),
                    VerticalOptions = new LayoutOptions(LayoutAlignment.Start, false)
                }
            };
            _player.PlaybackError += PlayerOnPlaybackError;

            Grid.SetColumn(_player.Label, Grid.GetColumn(Canvas));
            Grid.SetRow(_player.Label, Grid.GetRow(Canvas));
            Grid.SetRowSpan(_player.Label, Grid.GetRowSpan(Canvas));
            Grid.SetColumnSpan(_player.Label, Grid.GetColumnSpan(Canvas));

            Grid.SetColumn(_player, Grid.GetColumn(Canvas));
            Grid.SetRow(_player, Grid.GetRow(Canvas));
            Grid.SetRowSpan(_player, Grid.GetRowSpan(Canvas));
            Grid.SetColumnSpan(_player, Grid.GetColumnSpan(Canvas));

            GridLayout.Children.Add(_player);
            GridLayout.Children.Add(_player.Label);

            _player.Source = streamFactory;
            _player.MimeType = mime;
            _player.Position = TimeSpan.Zero;
        }

        private async void PlayerOnPlaybackError(object sender, string e)
        {
            if (PlaybackErrorsHidden) return;

            const string dontShowAgain = "Don't show again";
            var page = XamarinHelpers.GetCurrentPage(Navigation);
            var res2 = await page.DisplayAlert("Video playback error detected", $"{e}", "OK", dontShowAgain);
            if (!res2)
                PlaybackErrorsHidden = true;
        }

        protected VideoView(TimeSynchronizedContext context, IDataPoint dataPoint) : base(context, dataPoint)
        {
        }

        /// Remove datapoint (by removing this image view) if present here.
        protected override void RemoveDataPoint(IDataPoint datapoint)
        {
            if (DataPoints.Contains(datapoint))
                RemoveThisView();
        }


        public void Dispose()
        {
            if (_player == null) return;

            _player.PlaybackError -= PlayerOnPlaybackError;
        }
    }
}
