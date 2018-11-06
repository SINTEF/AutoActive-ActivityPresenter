using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Mqtt;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    //public class PlayerGridLayout : Layout<View>
    public class PlayerGridLayout : Layout<FigureView>
    {
        private static readonly int GRID_COLUMNS = 4;
        private static readonly int GRID_ROWS = 4;

        private static readonly int BIG_GRID_COLUMNS = 2;
        private static readonly int BIG_GRID_ROWS = 1;

        // FIXME : Implement this class, and also possibly restrict this to more specific views for data-renderers
        public PlayerGridLayout() { }

        public DataViewerContext ViewerContext { get; set; }

        public async void AddPlotFor(IDataPoint datapoint)
        {
            FigureView newView;
            if (datapoint is ArchiveVideoVideo)
            {
                //var video = await ImageView.Create(datapoint, ViewerContext as TimeSynchronizedContext);
                //Children.Add(video);
                newView = await ImageView.Create(datapoint, ViewerContext as TimeSynchronizedContext);
            }
            else if (datapoint is TableColumn)
            {
                //var plot = await LinePlot.Create(datapoint, ViewerContext as TimeSynchronizedContext);
                //Children.Add(plot);
                newView = await LinePlot.Create(datapoint, ViewerContext as TimeSynchronizedContext);
            }
            else if (datapoint is TableColumnDyn)
            {
                //var plot = await LinePlot.Create(datapoint, ViewerContext as TimeSynchronizedContext);
                //Children.Add(plot);
                newView = await LinePlot.Create(datapoint, ViewerContext as TimeSynchronizedContext);
            }
            else
            {
                throw new NotSupportedException();
            }

            {
                var tgr = new TapGestureRecognizer();
                tgr.NumberOfTapsRequired = 1;
                tgr.Tapped += newView.Viewer_Tapped;
                newView.GestureRecognizers.Add(tgr);
            }
            {
                var pagr = new PanGestureRecognizer();
                pagr.PanUpdated += newView.Viewer_Panned;
                newView.GestureRecognizers.Add(pagr);
            }

            Children.Add(newView);

        }

    private void UseInTimelineClicked(object sender, EventArgs e)
    {
        var dataPointItem = BindingContext as DataPointItem;
        dataPointItem?.OnUseInTimelineTapped();
    }

    /* -- Grid layout operations -- */
    protected override SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            Debug.WriteLine("GRID: OnMeasure");
            // We want to use the full size available
            var size = new Size(widthConstraint, heightConstraint);

            // FIXME: Call layout on the children

            return new SizeRequest(size);
        }

        protected override void LayoutChildren(double x, double y, double width, double height)
        {
            Debug.WriteLine("GRID: LayoutChildren");
            // Leave spacing equal to one free cell

            var cellSpacingX = 10;
            var cellSpacingY = cellSpacingX;

            var smallCellWidth = (width - cellSpacingX * (GRID_COLUMNS + 1)) / GRID_COLUMNS;
            var smallCellHeight = (height - cellSpacingY * (GRID_ROWS + 1)) / GRID_ROWS;

            var bigCellWidth = smallCellWidth * 2 + cellSpacingX;
            var bigCellHeight = smallCellHeight * 2 + cellSpacingY;

            double cellWidth;
            double cellHeight;
            int nColumns;
            int nRows;

            if (BIG_GRID_COLUMNS > 0)
            {
                nColumns = BIG_GRID_COLUMNS;
                nRows = BIG_GRID_ROWS;
                cellWidth = bigCellWidth;
                cellHeight = bigCellHeight;
            }
            else
            {
                nColumns = GRID_COLUMNS;
                nRows = GRID_ROWS;
                cellWidth = smallCellWidth;
                cellHeight = smallCellHeight;
            }


            // Layout all the children
            int i = 0, j = 0;
            var cx = x + cellSpacingX;
            var cy = y + cellSpacingY;

            foreach (var figure in Children)
            {
                figure.Layout(new Rectangle(cx, cy, cellWidth, cellHeight));
                cx += cellWidth + cellSpacingX;
                if (++i == nColumns)
                {
                    j++;
                    i = 0;
                    cx = x + cellSpacingX;
                    cy += cellHeight + cellSpacingY;

                    if (j == nRows)
                    {
                        nColumns = GRID_COLUMNS;
                        nRows = GRID_ROWS;
                        cellHeight = smallCellHeight;
                        cellWidth = smallCellWidth;
                    }
                }
            }
        }

        protected override void InvalidateLayout()
        {
            Debug.WriteLine("GRID: InvalidateLayout");
            // When children are added or removed
        }

        protected override void OnChildMeasureInvalidated()
        {
            Debug.WriteLine("GRID: OnChildMeasureInvalidated");
            // When a child's size changes
        }
    }
}
