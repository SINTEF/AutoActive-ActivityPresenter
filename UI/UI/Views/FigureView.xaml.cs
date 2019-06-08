using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Mqtt;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Pages.Player;
using SINTEF.AutoActive.UI.Pages.Synchronization;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
	public partial class FigureView : ContentView
	{
        public List<IDataPoint> DataPoints { get; set; } = new List<IDataPoint>();

        protected TimeSynchronizedContext Context { get; private set; }
	    protected static readonly SKPaint FramePaint = new SKPaint
	    {
	        Color = SKColors.LightSlateGray,
	        Style = SKPaintStyle.Stroke,
	        StrokeWidth = 1,
	        StrokeJoin = SKStrokeJoin.Miter,
	        IsAntialias = false,
	    };

	    public bool Selected
	    {
	        get => SelectionFrame.BorderColor == Color.Red;
	        set => SelectionFrame.BorderColor = value ? Color.Red : Color.Black;
	    }

	    public bool ContextButtonIsVisible
	    {
	        get => ContextButton.IsVisible;
	        set => ContextButton.IsVisible = value;
	    }

        protected readonly SKPaint TextPaint = new SKPaint
	    {
	        Color = SKColors.Black,
	        Style = SKPaintStyle.Fill,
	        IsAntialias = true,
	        SubpixelText = true,
	    };

        private readonly List<IDataViewer> _viewers = new List<IDataViewer>();

        protected void AddViewer(IDataViewer viewer)
        {
            _viewers.Add(viewer);
        }

        protected void RemoveViewer(IDataViewer viewer)
        {
            Context.Remove(viewer);
            _viewers.Remove(viewer);
        }

        public FigureView()
	    {
	        InitializeComponent();
	        //Canvas.PaintSurface += Canvas_PaintSurface;

            SINTEF.AutoActive.Databus.DataRegistry.DataPointRemoved += DataRegistry_DataPointRemoved;
        }

        protected FigureView(TimeSynchronizedContext context, IDataPoint dataPoint)
        {
            InitializeComponent();
            DataPoints.Add(dataPoint);

            Context = context;

            // Redraw canvas when data changes, size of figure changes, or range updates
            // FIXME: This usually causes at least double updates. Maybe we can sort that out somehow
            // It really doesn't make sense to redraw more often than the screen refresh-rate either way
            SizeChanged += FigureView_SizeChanged;
            Context.SelectedTimeRangeChanged += Context_SelectedTimeRangeChanged;
            Canvas.PaintSurface += Canvas_PaintSurface;

            SINTEF.AutoActive.Databus.DataRegistry.DataPointRemoved += DataRegistry_DataPointRemoved;
            /// The DataRegistry_DataPointRemoved callback is removed again in RemoveThisView().
        }

        /// Called when datapoint is removed from DataRegistry, i.e. session closed.
        private void DataRegistry_DataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            RemoveDataPoint(datapoint);
        }

        private void FigureView_SizeChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("FigureView::FigureView_SizeChanged ");
            Canvas.InvalidateSurface();
        }

        public void Viewer_Tapped(object sender, EventArgs e)
        {
            Debug.WriteLine("FigureView::Viewer_tapped");
        }

        public void Viewer_Panned(object sender, PanUpdatedEventArgs e)
        {
            var debugText = "FigureView::Viewer_Panned ";
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    debugText += "STARTED";
                    break;
                case GestureStatus.Running:
                    debugText += "RUNNING";
                    break;
                case GestureStatus.Completed:
                    debugText += "COMPLETED";
                    break;
                case GestureStatus.Canceled:
                    debugText += "CANCELED";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            Debug.WriteLine(debugText);
            Debug.WriteLine($"X:{e.TotalX} Y:{e.TotalY}");

        }

        private void Context_SelectedTimeRangeChanged(SingleSetDataViewerContext sender, long from, long to)
        {
            Canvas.InvalidateSurface();
        }

        private void Canvas_PaintSurface(object sender, SkiaSharp.Views.Forms.SKPaintSurfaceEventArgs e)
        {
            /// \todo Investigate why a \c Debug.WriteLine() output here makes
            /// the GUI sluggish and unresponsive at large windows length.
            /// Why is it correlated with the data window length?
            RedrawCanvas(e.Surface.Canvas, e.Info);
        }

	    protected virtual void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
	    {
	        // Clear background and draw frame
	        canvas.Clear(SKColors.White);
	        canvas.DrawRect(0, 0, info.Width - 1, info.Height - 1, FramePaint);

            const string text = "Override RedrawCanvas";
	        var textHeight = TextPaint.FontMetrics.CapHeight;
	        var maxTextWidth = TextPaint.MeasureText(text);

            canvas.DrawText(text, info.Width/2f - maxTextWidth/2, info.Height/2f - textHeight/2, TextPaint);
        }

        /// Create new view of the proper type to visualize datapoint.
        /// \todo Add new argument "ISelecting selectingView" and
        /// store it as _selectingView for later usage.
        /// Declare the new interface ISelecting to contain the functions
        /// Select(FigureView view) and RemoveChild(FigureView view).
        /// PlayerGridLayout and SynchronizationPage must implement ISelecting.
	    public static async Task<FigureView> GetView(IDataPoint datapoint, TimeSynchronizedContext context)
	    {
	        FigureView view;
	        switch (datapoint)
	        {
	            case ArchiveVideoVideo _:
	                view = await ImageView.Create(datapoint, context);
	                break;
	            case TableColumn _:
	            case TableColumnDyn _:
	                view = await LinePlot.Create(datapoint, context);
	                break;
	            default:
	                throw new NotSupportedException();
	        }

	        return view;
	    }

	    protected const string CancelText = "Cancel";
	    protected const string RemoveText = "Remove";
	    protected const string SelectText = "Select";
	    protected const string DeselectText = "Deselect";

        protected async void MenuButton_OnClicked(object sender, EventArgs e)
	    {
	        var page = Navigation.NavigationStack.LastOrDefault();
	        if (page == null) return;


	        var parameters = new List<string> {Selected ? DeselectText : SelectText};

	        GetExtraMenuParameters(parameters);

	        var action = await page.DisplayActionSheet("Modify View", CancelText, RemoveText, parameters.ToArray());
	        try
	        {
	            OnHandleMenuResult(page, action);
	        }
	        catch (ArgumentException ex)
	        {
	            await page.DisplayAlert("Illegal argument", $"Could not handle menu input: {ex.Message}", "Ok");
	        }
	    }

	    protected virtual bool GetExtraMenuParameters(List<string> parameters)
	    {
	        return false;
	    }

        /// Remove this view from the selecting view that contains it.
        protected void RemoveThisView()
        {
            /// \todo Call _selectingView.RemoveChild(this) instead of this indirect call.
            OnHandleMenuResult(XamarinHelpers.GetCurrentPage(this), RemoveText);
        }

	    protected virtual void OnHandleMenuResult(Page page, string action)
	    {
	        DefaultOnHandleMenuResult(page, action);
        }

        /// Handle menu action.
	    protected void DefaultOnHandleMenuResult(Page page, string action)
	    {
	        switch (action)
	        {
                case null:
	            case CancelText:
	                return;
                case SelectText:
                    /// \todo Call _selectingView.Select(this) instead of this switch.
                    switch (Parent)
                    {
                        case PlayerGridLayout playerGridLayout:
                            playerGridLayout.Selected = this;
                            break;
                        default:
                            if (page is SynchronizationPage syncPage)
                            {
                                syncPage.Selected = this;
                                break;
                            }
                            throw new ArgumentException("Layout not recognized");
                    }

                    break;
                case DeselectText:
                    /// \todo Call _selectingView.Select(null) instead of this switch.
                    switch (Parent)
                    {
                        case PlayerGridLayout playerGridLayout:
                            playerGridLayout.Selected = null;
                            break;
                        default:
                            if (page is SynchronizationPage syncPage)
                            {
                                syncPage.Selected = null;
                                break;
                            }
                            throw new ArgumentException("Layout not recognized");
                    }
                    break;
	            case RemoveText:
                    /// \todo When the switch below is replaced with a call to
                    /// _selectingView.RemoveChild(this), then move all code for
                    /// this RemoveText case into RemoveThisView() and call that
                    /// function from here instead of the other way around.
                    foreach (var viewer in _viewers)
                    {
                        Context.Remove(viewer);
                    }
                    /// \todo Call _selectingView.RemoveChild(this) instead of this switch.
                    switch (Parent)
	                {
	                    case PlayerGridLayout playerGridLayout:
	                        playerGridLayout.RemoveChild(this);
	                        break;
	                    case Layout<FigureView> parentLayout:
	                        parentLayout.Children.Remove(this);
	                        break;
	                    default:
	                        if (page is SynchronizationPage syncPage)
	                        {
	                            syncPage.RemoveChild(this);
	                            break;
	                        }

	                        throw new ArgumentException("Layout not recognized");
	                }
                    /// Remove callback to this view when this view is removed.
                    SINTEF.AutoActive.Databus.DataRegistry.DataPointRemoved -= DataRegistry_DataPointRemoved;
                    /// \todo End of case to move into RemoveThisView(). See above.
	                break;
                default:
                    throw new ArgumentException($"Unknown action: {action}");
	        }
	    }

        /// Add new datapoint to view, or remove it if already present here.
	    public virtual Task ToggleDataPoint(IDataPoint datapoint, TimeSynchronizedContext timeContext)
	    {
	        throw new NotImplementedException();
	    }

        /// Remove datapoint from view if present here.
        protected virtual void RemoveDataPoint(IDataPoint datapoint)
        {
            throw new NotImplementedException();
        }
    }
}