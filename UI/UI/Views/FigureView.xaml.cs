using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Mqtt;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Pages.Player;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
	public partial class FigureView : ContentView
	{
        private IDataViewer Viewer { get; set; }
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

	    protected readonly SKPaint TextPaint = new SKPaint
	    {
	        Color = SKColors.Black,
	        Style = SKPaintStyle.Fill,
	        IsAntialias = true,
	        SubpixelText = true,
	    };

        public FigureView()
	    {
	        InitializeComponent();
	        //Canvas.PaintSurface += Canvas_PaintSurface;
        }

        protected FigureView(IDataViewer viewer, TimeSynchronizedContext context)
        {
            InitializeComponent();

            Viewer = viewer;
            Context = context;

            if (viewer != null)
                viewer.Changed += Viewer_Changed;

            // Redraw canvas when data changes, size of figure changes, or range updates
            // FIXME: This usually causes at least double updates. Maybe we can sort that out somehow
            // It really doesn't make sense to redraw more often than the screen refresh-rate either way
            SizeChanged += FigureView_SizeChanged;
            Context.SelectedTimeRangeChanged += Context_SelectedTimeRangeChanged;
            Canvas.PaintSurface += Canvas_PaintSurface;
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

        private void Viewer_Changed(IDataViewer sender)
        {
            //Debug.WriteLine("FigureView::Viewer_Changed ");
            Canvas.InvalidateSurface();
            Viewer_Changed_Hook();
        }

        protected virtual void Viewer_Changed_Hook()
        {
            // Hook method to be overridden by sub classes if special handling needed
        }

        private void Context_SelectedTimeRangeChanged(SingleSetDataViewerContext sender, long from, long to)
        {
            //Debug.WriteLine("FigureView::Context_RangeUpdated " + from + " " + to );
            Canvas.InvalidateSurface();
        }

        private void Canvas_PaintSurface(object sender, SkiaSharp.Views.Forms.SKPaintSurfaceEventArgs e)
        {
            // GUI becomes sluggish and unresponsive at large window length with debug output here.
            //Debug.WriteLine("FigureView::Canvas_PaintSurface ");
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

	    protected virtual void OnHandleMenuResult(Page page, string action)
	    {
	        DefaultOnHandleMenuResult(page, action);
        }

	    protected void DefaultOnHandleMenuResult(Page page, string action)
	    {
	        switch (action)
	        {
                case null:
	            case CancelText:
	                return;
                case SelectText:
                    switch (Parent)
                    {
                        case PlayerGridLayout playerGridLayout:
                            playerGridLayout.Selected = this;
                            break;
                        default:
                            throw new ArgumentException("Layout not recognized");
                    }

                    break;
                case DeselectText:
                    switch (Parent)
                    {
                        case PlayerGridLayout playerGridLayout:
                            playerGridLayout.Selected = null;
                            break;
                        default:
                            throw new ArgumentException("Layout not recognized");
                    }
                    break;
	            case RemoveText:
	                switch (Parent)
	                {
	                    case PlayerGridLayout playerGridLayout:
	                        playerGridLayout.RemoveChild(this);
	                        break;
	                    case Layout<FigureView> parentLayout:
	                        parentLayout.Children.Remove(this);
	                        break;
	                    default:
	                        throw new ArgumentException("Layout not recognized");
	                }

	                break;
                default:
                    throw new ArgumentException($"Unknown action: {action}");
	        }
	    }

	    public virtual async Task AddDataPoint(IDataPoint datapoint, TimeSynchronizedContext timeContext)
	    {
	        throw new NotImplementedException();
	    }
	}
}