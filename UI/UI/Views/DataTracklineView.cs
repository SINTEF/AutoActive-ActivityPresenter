using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.Pages.Player;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views
{
    [DesignTimeVisible(false)]
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public class DataTracklineView : SKCanvasView
    {
        private readonly List<(ITimeViewer, TimeSynchronizedContext, string)> _timeViewers = new List<(ITimeViewer, TimeSynchronizedContext, string)>();
        private readonly List<(IDataPoint, ITimeViewer, IDataViewer)> _dataTimeList = new List<(IDataPoint, ITimeViewer, IDataViewer)>();

        private readonly SKPaint _currentLinePaint = new SKPaint
        {
            Color = SKColor.Parse("#F1304D"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true
        };

        private void ContextOnSelectedTimeRangeChanged(SingleSetDataViewerContext sender, long @from, long to)
        {
            InvalidateSurface();
        }

        private void ViewerOnTimeChanged(ITimeViewer sender, long start, long end)
        {
            InvalidateSurface();
        }

        public PlaybarView Playbar { get; set; }

        public DataTracklineView() : base()
        {
            PaintSurface += OnPaintSurface;
            WidthMargins = 10;
            Touch += OnTouch;
            EnableTouchEvents = true;

        }


        private bool _syncIsSetMaster;
        private bool _syncIsSetSlave;
        private Page _currentPage;

        NavigationPage GetNavigationPage()
        {
            var mainPage = Application.Current.MainPage;

            if (mainPage is MasterDetailPage page)
            {
                mainPage = page.Detail;
            }

            return (NavigationPage)mainPage;
        }

        private void OnTouch(object sender, SKTouchEventArgs e)
        {
            if (_timeViewers.Count == 0) { return; }
            if (e.MouseButton == SKMouseButton.Left)
            {
                if (_currentPage is PlayerPage)
                {
                    onTouchPlayerPage(sender, e);
                }
                else if (_currentPage is Pages.Synchronization.PointSynchronizationPage)
                {
                    
                    onTouchSyncPage(sender, e);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            if (e.MouseButton == SKMouseButton.Right && e.ActionType == SKTouchAction.Released)
            {
                Playbar.GetTimeStepper.PlayButton_Clicked(this, new EventArgs());
            }
        }

        private void onTouchPlayerPage(object sender, SKTouchEventArgs e)
        {
            var mouseClickLocationX = e.Location.X;
            var totalWindowLength = this.Width;
            var relativeMouseClickLocationX = mouseClickLocationX / totalWindowLength;
            var maximumSliderValue = Playbar.GetTimeSlider.Maximum;
            Playbar.GetTimeSlider.Value = maximumSliderValue * relativeMouseClickLocationX;
        }

        private void onTouchSyncPage(object sender, SKTouchEventArgs e)
        {
            var mouseClickLocationX = e.Location.X;
            var times = new List<(long, long, string)>();

            foreach (var (viewer, context, label) in _timeViewers)
            {
                int counterMaster = times.Where(x => x.Item3.Contains("Master")).Count();
                int counterSlave = times.Where(x => x.Item3.Contains("Slave")).Count();
                if (counterMaster == 1 && counterSlave == 1) { break; }
                try
                {
                    SynchronizationContext slaveContext = (SynchronizationContext)context;
                    long offset = slaveContext.Offset;
                    long start = viewer.Start - offset;
                    long end = viewer.End - offset;
                    string newLabel = label + "_Slave";
                    times.Add((start, end, label));
                }
                catch
                {
                    if (counterMaster == 0)
                    {
                        string newLabel = label + "_Master";
                        times.Add((viewer.Start, viewer.End, newLabel));
                    }
                }
            }

            double xMin = times.Min(el => el.Item1);
            double xMax = times.Max(el => el.Item2);

            foreach (var (start, end, label) in times)
            {
                double scaleX = (this.Width / (xMax - xMin));
                double rectStartX = (start - xMin) * scaleX;
                double rectEndX = (end - xMin) * scaleX;

                if (label.Contains("Master"))
                {
                    Slider slider = Playbar.GetTimeSlider;
                    setSliderValue(rectStartX, rectEndX, mouseClickLocationX, slider);
                }
            }
        }

        private void setSliderValue(double rectStartX, double rectEndX, double mouseClickLocationX, Slider slider)
        {
            double totalWindowLength = rectEndX - rectStartX;
            double relativeMouseClick = (mouseClickLocationX - rectStartX) / totalWindowLength;
            slider.Value = slider.Maximum * relativeMouseClick;
        }

        public int WidthMargins { get; set; }

        protected void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {

            _currentPage = GetNavigationPage().Navigation.NavigationStack.LastOrDefault();
            if (_currentPage is Pages.Synchronization.PointSynchronizationPage) { Playbar.GetTimeStepper.IsVisible = false; }
            var canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.White);

            var height = e.Info.Height;
            var width = e.Info.Width;

            var drawRect = new SKRect(0, 0, width, height);

            var borderPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            drawRect.Right -= 1;
            drawRect.Bottom -= 1;

            canvas.DrawRect(drawRect, borderPaint);

            drawRect.Left = WidthMargins;
            drawRect.Right -= WidthMargins;
            drawRect.Top = 2;
            drawRect.Bottom -= 2;

            var trans = SKMatrix.MakeTranslation(drawRect.Left, drawRect.Top);
            canvas.SetMatrix(trans);

            var (xMin, xScale) = DrawDataSegments(e.Surface.Canvas, drawRect, _timeViewers);
            ActivateDeactivateTimeStepper(_timeViewers);
            EnsurePlayerIsDeactivatedIfEmpty(_timeViewers);
            if (!_timeViewers.Any()) return;
            canvas.SetMatrix(SKMatrix.MakeIdentity());
            DrawCurrentTime(canvas, xMin, xScale);
            
        }

        private void ActivateDeactivateTimeStepper(IEnumerable<(ITimeViewer, TimeSynchronizedContext, string)> timeViewers)
        {
            var nrOfTimeViewers = _timeViewers.Count();
            if (nrOfTimeViewers == 0)
            {
                Playbar.GetTimeStepper.AreButtonsEnabled = false;
            }
            else
            {
                Playbar.GetTimeStepper.AreButtonsEnabled = true;
            }
        }

        private void EnsurePlayerIsDeactivatedIfEmpty(IEnumerable<(ITimeViewer, TimeSynchronizedContext, string)> timeViewers)
        {
            var nrOfTimeViewers = _timeViewers.Count();
            if (nrOfTimeViewers == 0 && Playbar.GetTimeStepper.GetPlayButton.Text == "STOP")
            {
                Playbar.GetTimeStepper.PlayButton_Clicked(this, new EventArgs());
            }
        }

        private void DrawCurrentTime(SKCanvas canvas, long xMin, float xScale)
        {
            var timeViewerItem = _timeViewers.First();
            var currentTimePos = timeViewerItem.Item2.SelectedTimeFrom;

            var xPos = (currentTimePos - xMin) * xScale;
            canvas.DrawLine(xPos, 0, xPos, canvas.LocalClipBounds.Height, _currentLinePaint);
        }

        private static (List<(long, long, string)>, long, float) GetMinTimeAndScale(IEnumerable<(ITimeViewer, TimeSynchronizedContext, string)> timeViewers, SKRect drawRect)
        {
            var times = new List<(long, long, string)>();
            foreach (var (viewer, context, label) in timeViewers)
            {
                var (start, end) = context.GetAvailableTimeInContext(viewer);
                times.Add((start, end, label));
            }

            var xMin = times.Min(el => el.Item1);
            var xMax = times.Max(el => el.Item2);
            var xDiff = xMax - xMin;
            var xScale = drawRect.Width / xDiff;

            return (times, xMin, xScale);
        }

        private const float MaxTrackHeight = 30f;
        private const float YMargin = 2f;
        private float _previousHeight;

        private float GetTracklineHeight(float plotHeight)
        {
            var yHeight = (plotHeight) / _timeViewers.Count - YMargin;
            if (yHeight > MaxTrackHeight) yHeight = MaxTrackHeight;
            return yHeight;
        }

        private (long, float) DrawDataSegments(SKCanvas canvas, SKRect drawRect, IReadOnlyCollection<(ITimeViewer, TimeSynchronizedContext, string)> timeViewers)
        {
            if (timeViewers.Count == 0) return (0,0);
            _previousHeight = drawRect.Height;

            const float labelXMargin = 4f;
            const float boxRoundnessX = 5f;
            const float boxRoundnessY = boxRoundnessX;
            const float minTextSize = 10f;

            var (times, xMin, xScale) = GetMinTimeAndScale(timeViewers, drawRect);

            var yHeight = GetTracklineHeight(drawRect.Height);

            var fontHeight = _textPaint.FontMetrics.Bottom - _textPaint.FontMetrics.Top;
            var textSize = _textPaint.TextSize;
            if (yHeight < Math.Abs(fontHeight))
            {
                textSize = Math.Abs(yHeight / fontHeight * _textPaint.TextSize);
            }

            _textPaint.TextSize = Math.Max(textSize, minTextSize);

            var fontBottom =
                Math.Abs(_textPaint.FontMetrics.Bottom - _textPaint.FontMetrics.Top) < yHeight
                    ? yHeight / 2 - _textPaint.FontMetrics.Bottom
                    : 0f;

            var yPos = YMargin;

            if (_currentPage is PlayerPage)
            {
                foreach (var (start, end, label) in times)
                {
                    var xPos = (start - xMin) * xScale;
                    var width = Math.Max((end - start) * xScale, 1f);
                    canvas.DrawRoundRect(xPos, yPos, width, yHeight, boxRoundnessX, boxRoundnessY, _dataTrackPaint);
                    canvas.DrawText(label, xPos + labelXMargin, yPos + yHeight - fontBottom, _textPaint);
                    yPos += yHeight + YMargin;
                }
            }
            else if (_currentPage is Pages.Synchronization.PointSynchronizationPage)
            {
                foreach (var tup in times.Zip(timeViewers, (i1, i2) => Tuple.Create(i1, i2)))
                {
                    var (start, end, label) = tup.Item1;
                    var (timeViwer, context, name) = tup.Item2;
                    SKPaint trackPaint = DecideColor(context);
                    var xPos = (start - xMin) * xScale;
                    var width = Math.Max((end - start) * xScale, 1f);
                    canvas.DrawRoundRect(xPos, yPos, width, yHeight, boxRoundnessX, boxRoundnessY, trackPaint);
                    canvas.DrawText(label, xPos + labelXMargin, yPos + yHeight - fontBottom, _textPaint);
                    yPos += yHeight + YMargin;
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            return (xMin, xScale);
        }

        private void ChangeColor(object sender, bool value)
        {

            if (sender is SynchronizationContext)
            {
                _syncIsSetSlave = value;
            }
            else
            {
                _syncIsSetMaster = value;
            }

            InvalidateSurface();

        }

        private SKPaint DecideColor(TimeSynchronizedContext context)
        {
            if (context is SynchronizationContext)
            {
                if (_syncIsSetSlave == true)
                {
                    return _dataTrackPaintOnSync;
                }
                else
                {
                    return _dataTrackPaintNoSync;
                }
            }
            else
            {
                if (_syncIsSetMaster == true)
                {
                    return _dataTrackPaintOnSync;
                }
                else
                {
                    return _dataTrackPaintNoSync;
                }
            }

        }

        private readonly SKPaint _dataTrackPaint = new SKPaint
        {
            Color = new SKColor(100, 108, 119),
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        private readonly SKPaint _dataTrackPaintOnSync = new SKPaint
        {
            Color = new SKColor(29, 185, 84),
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        private readonly SKPaint _dataTrackPaintNoSync = new SKPaint
        {
            Color = new SKColor(241, 48, 77),
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        private readonly SKPaint _textPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            SubpixelText = true,
            Typeface = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold)
        };

        private void ContextOnAvailableTimeRangeChanged(DataViewerContext sender, long @from, long to)
        {
            InvalidateSurface();
        }

        public async Task AddDataPoint(IDataPoint dataPoint, TimeSynchronizedContext context)
        {
            if (!_dataTimeList.Any())
            {
                context.AvailableTimeRangeChanged += ContextOnAvailableTimeRangeChanged;
            }

            var dataViewer = await context.GetDataViewerFor(dataPoint);
            var timeViewer = await dataViewer.DataPoint.Time.CreateViewer();
            _dataTimeList.Add((dataPoint, timeViewer, dataViewer));
            _timeViewers.Add((timeViewer, context, dataPoint.Name));
            timeViewer.TimeChanged += ViewerOnTimeChanged;
            context.SelectedTimeRangeChanged += ContextOnSelectedTimeRangeChanged;
            context.SyncIsSetChanged += ChangeColor;
            InvalidateSurface();
        }

        public void RemoveDataPoint(IDataPoint dataPoint, TimeSynchronizedContext context)
        {
            var found = false;
            int index;
            for (index = 0; index < _dataTimeList.Count; index++)
            {
                if (_dataTimeList[index].Item1 != dataPoint) continue;
                if (_timeViewers[index].Item2 != context) continue;

                found = true;
                break;
            }

            if (!found)
                return;

            var(_, timeViewer, dataViewer) = _dataTimeList[index];
            _dataTimeList.RemoveAt(index);
            if (_dataTimeList.Count == 0)
            {
                context.AvailableTimeRangeChanged -= ContextOnAvailableTimeRangeChanged;
            }

            timeViewer.TimeChanged -= ViewerOnTimeChanged;
            context.Remove(dataViewer);
            context.SelectedTimeRangeChanged -= ContextOnSelectedTimeRangeChanged;
            context.SyncIsSet = false;
            context.SyncIsSetChanged -= ChangeColor;
            _timeViewers.RemoveAt(index);
            int masterCount = _timeViewers.Where(x => x.Item2 is TimeSynchronizedContext).Count();
            int slaveCount = _timeViewers.Where(x => x.Item2 is SynchronizationContext).Count();
            if (masterCount == 0){ _syncIsSetMaster = false;}
            if (slaveCount == 0) { _syncIsSetSlave = false; }
            InvalidateSurface();
        }

        private async void DataPointAddedHandler(object sender, (IDataPoint, DataViewerContext) args)
        {
            var (datapoint, context) = args;
            if (context is TimeSynchronizedContext timeContext)
            {
                try
                {
                    await AddDataPoint(datapoint, timeContext);
                }
                catch (Exception ex)
                {
                    await XamarinHelpers.ShowOkMessage("Error", ex.Message, XamarinHelpers.GetCurrentPage(Navigation));
                }
            }
        }
        private void DataPointRemovedHandler(object sender, (IDataPoint, DataViewerContext) args)
        {
            var (datapoint, context) = args;
            if (context is TimeSynchronizedContext timeContext)
                RemoveDataPoint(datapoint, timeContext);
        }

        public void RegisterFigureContainer(IFigureContainer container)
        {
            container.DatapointAdded += DataPointAddedHandler;
            container.DatapointRemoved += DataPointRemovedHandler;
        }

        public void DeregisterFigureContainer(IFigureContainer container)
        {
            container.DatapointAdded -= DataPointAddedHandler;
            container.DatapointRemoved -= DataPointRemovedHandler;
        }
    }
}