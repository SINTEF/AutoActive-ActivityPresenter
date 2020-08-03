using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Mqtt;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
	public partial class FigureView : ContentView, ISerializableView
    {
        public static Color ElementBackgroundColor = Color.CornflowerBlue.MultiplyAlpha(0.7d);
        public List<IDataPoint> DataPoints { get; set; } = new List<IDataPoint>();

        public static string StaticViewType => "no.sintef.ui.figureview";
        public string ViewType => StaticViewType;

        public TimeSynchronizedContext Context { get; }
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

        public FigureView()
        {
            InitializeComponent();
        }

        protected void AddViewer(IDataViewer viewer)
        {
            _viewers.Add(viewer);
        }

        protected void RemoveViewer(IDataViewer viewer)
        {
            Context.Remove(viewer);
            _viewers.Remove(viewer);
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

            // The DataRegistry_DataPointRemoved callback is removed in RemoveThisView().
            Databus.DataRegistry.DataPointRemoved += DataRegistry_DataPointRemoved;
        }

        /// Called when datapoint is removed from DataRegistry, i.e. session closed.
        private void DataRegistry_DataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            RemoveDataPoint(datapoint);
        }

        private void FigureView_SizeChanged(object sender, EventArgs e)
        {
            Canvas.InvalidateSurface();
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
            // Clear background and draw frame
            e.Surface.Canvas.Clear(SKColors.White);

            // \todo Investigate why a \c Debug.WriteLine() output here makes
            // the GUI sluggish and unresponsive at large windows length.
            // Why is it correlated with the data window length?
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
	    public static async Task<FigureView> GetView(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            try
            {
                FigureView view;
                var parents = DataRegistry.GetParents(datapoint);
                switch (datapoint)
                {
                    case ArchiveVideoVideo _:
                        view = await VideoView.Create(datapoint, context);
                        if (parents != null && parents.Any())
                        {
                            view.Title.Text = parents.First().Name;
                        }
                        return view;
                    case TableColumn _:
                    case TableColumnDyn _:
                        view = await LinePlot.Create(datapoint, context);
                        if (parents != null && parents.Any())
                        {
                            var titleText = parents.Count > 2 ? parents[2].Name + " - " : "";
                            titleText += parents.Count > 1 ? parents[1].Name + " - " : "";
                            titleText += parents[0].Name;
                            view.Title.Text = titleText;
                        }
                        return view;
                    default:
                        throw new NotSupportedException();
                }
            }  catch(Exception ex)
            {
                if (datapoint.DataType == typeof(string))
                {
                    await XamarinHelpers.ShowOkMessage("Error", $"Visualizing columns containing strings is not yet implemented.\n\n{ex.Message}");
                }
                else
                {
                    await XamarinHelpers.ShowOkMessage("Error", $"Could not add plot.\n\n{ex.Message}");
                }
            }
            return null;
        }

	    protected const string CancelText = "Cancel";
	    protected const string RemoveText = "Remove";
	    protected const string SelectText = "Select";
	    protected const string DeselectText = "Deselect";
        protected const string ToggleTitleText = "Toggle title";

        protected async void MenuButton_OnClicked(object sender, EventArgs e)
	    {
	        var page = Navigation.NavigationStack.LastOrDefault();
	        if (page == null) return;

	        var parameters = new List<string> {Selected ? DeselectText : SelectText};

            GetExtraMenuParameters(parameters);

            if (Title.Text != "")
            {
                parameters.Add(ToggleTitleText);
            }

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
        public void RemoveThisView()
        {
            var figureContainer = XamarinHelpers.GetFigureContainerFromParents(Parent);
            figureContainer.RemoveChild(this);

            foreach (var viewer in _viewers)
            {
                Context.Remove(viewer);
            }
            DataPoints.Clear();

            // Remove all callbacks registered in constructor
            SizeChanged -= FigureView_SizeChanged;
            Context.SelectedTimeRangeChanged -= Context_SelectedTimeRangeChanged;
            Canvas.PaintSurface -= Canvas_PaintSurface;

            // Remove callback to this view when this view is removed.
            Databus.DataRegistry.DataPointRemoved -= DataRegistry_DataPointRemoved;
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
                    XamarinHelpers.GetFigureContainerFromParents(Parent).Selected = this;
                    break;
                case DeselectText:
                    XamarinHelpers.GetFigureContainerFromParents(Parent).Selected = null;
                    break;
                case RemoveText:
                    RemoveThisView();
                    break;
                case ToggleTitleText:
                    Title.IsVisible ^= true;
                    break;
                default:
                    throw new ArgumentException($"Unknown action: {action}");
	        }
	    }

	    public virtual Task<ToggleResult> ToggleDataPoint(IDataPoint datapoint, TimeSynchronizedContext timeContext)
	    {
            if (!DataPoints.Contains(datapoint))
            {
                throw new NotImplementedException();
            }

            RemoveThisView();
            return Task.FromResult(ToggleResult.Removed);
        }

        /// Remove datapoint from view if present here.
        protected virtual void RemoveDataPoint(IDataPoint datapoint)
        {
            throw new NotImplementedException();
        }

        private static IDataPoint GetDataPointsFromIdentifierFromDataProvider(JArray indexes,
            IDataStructure dataStructure)
        {
            for (var i = 0; i < indexes.Count - 1; i++)
            {
                var ix = indexes[i].Value<int>();
                dataStructure = dataStructure.Children.Skip(ix).First();
            }
            var dataPoint = dataStructure.DataPoints[indexes[indexes.Count - 1].Value<int>()];

            // TODO(sigurdal): Check the datapoint, not only the indexes.

            return dataPoint;
        }


        private static IList<IDataPoint> GetDataPointsFromIdentifier(JToken serializedObject)
        {
            var dataPoints = new List<IDataPoint>();
            var indexesList = ((JArray)serializedObject["data_point_indexes"]).Cast<JArray>();

            foreach(var indexes in indexesList) {
                //IDataStructure dataStructure = DataRegistry.Providers.First();
                var dataPoint = DataRegistry.Providers.Select(dataStructure => GetDataPointsFromIdentifierFromDataProvider(indexes, dataStructure)).FirstOrDefault();

                if (dataPoint == null)
                {
                    //TODO(sigurdal): Show warning message
                }

                dataPoints.Add(dataPoint);
            }

            return dataPoints;
        }

        public static bool DeserializationFailedWarned;

        private static async Task ShowDeserializationWarning(string message)
        {
            // Make sure the warning is only shown once per deserialization step
            if (!DeserializationFailedWarned)
            {
                await XamarinHelpers.ShowOkMessage("Deserialization failed.", message);
                DeserializationFailedWarned = true;
            }
        }

        public static async Task<FigureView> DeserializeView(JObject root, TimeSynchronizedContext context, IDataStructure archive=null)
        {
            if (root["type"].Value<string>() != StaticViewType)
            {
                Debug.WriteLine("Illegal type");
                return null;
            }

            try
            {
                var dataPoints = GetDataPointsFromIdentifier(root, archive);
                if (dataPoints.Count == 0)
                {
                    await ShowDeserializationWarning("Could not find any DataPoints for figure during deserialization");
                    return null;
                }

                var dataPoint = dataPoints.First();
                var view = await GetView(dataPoint, context);
                if (dataPoints.Count <= 1) return view;

                if (!(view is LinePlot linePlot))
                {
                    await ShowDeserializationWarning("View trying to add multiple data points to unsupported view.");
                    return null;
                }

                foreach (var extraDataPoint in dataPoints.Skip(1))
                {
                    await linePlot.AddLine(extraDataPoint);
                }

                return view;
            }
            catch (Exception ex)
            {
                await ShowDeserializationWarning($"Could not get DataPoint: {ex}");
                return null;
            }
        }

        Task ISerializableView.DeserializeView(JObject root)
        {
            throw new NotImplementedException();
        }

        public JObject SerializeView(JObject root = null)
        {
            if (root == null)
            {
                root = new JObject();
            }

            var dataPoints = new JArray();
            var dataPointIndexes = new JArray();
            foreach (var dataPoint in DataPoints)
            {
                var dataPointIndex = new JArray();
                dataPointIndexes.Add(dataPointIndex);
                var parentNames = new JArray();
                dataPoints.Add(parentNames);
                var parents = DataRegistry.GetParents(dataPoint);
                parents.Reverse();
                var prev = parents.First();
                Debug.WriteLine(dataPoint.Name);
                if (prev is ArchiveSession session)
                {
                    Debug.WriteLine($"  {prev.Name} - {session.Id}");
                }

                foreach (var parent in parents.Skip(1))
                {
                    var ix = prev.Children.IndexOf(parent);
                    dataPointIndex.Add(ix);
                    var line = $"  {ix} - {parent.Name}";
                    if (parent is ArchiveStructure structure)
                    {
                        line += $" - {structure.Type}";
                    }
                    parentNames.Add(parent.Name);
                    Debug.WriteLine(line);

                    prev = parent;
                }

                {
                    var ix = prev.DataPoints.IndexOf(dataPoint);
                    dataPointIndex.Add(ix);
                    var line = $"  {ix} - {dataPoint.Name}";
                    Debug.WriteLine(line);

                    parentNames.Add(dataPoint.Name);
                }
                Debug.WriteLine("");
            }
            root["data_points"] = dataPoints;
            root["data_point_indexes"] = dataPointIndexes;
            root["type"] = ViewType;

            return root;
        }
    }
}