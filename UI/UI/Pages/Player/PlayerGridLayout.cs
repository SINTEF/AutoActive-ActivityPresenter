using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
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

        // FIXME : Implement this class, and also possibly restrict this to more specific views for data-renderers
        public PlayerGridLayout() { }

        public DataViewerContext ViewerContext { get; set; }

        public async void AddPlotFor(IDataPoint datapoint)
        {
            if (datapoint is ArchiveVideoVideo)
            {
                var video = await ImageView.Create(datapoint, ViewerContext as TimeSynchronizedContext);
                Children.Add(video);
            }
            else if (datapoint is TableColumn)
            {
                var plot = await LinePlot.Create(datapoint, ViewerContext as TimeSynchronizedContext);
                Children.Add(plot);
            }
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
            var cellWidth = width / (GRID_COLUMNS + 1);
            var cellHeight = height / (GRID_ROWS + 1);
            var cellSpacingX = cellWidth / (GRID_COLUMNS + 1);
            var cellSpacingY = cellHeight / (GRID_ROWS + 1);

            // Layout all the children
            int i = 0, j = 0;
            var cx = x + cellSpacingX;
            var cy = y + cellSpacingY;

            foreach (var figure in Children)
            {
                figure.Layout(new Rectangle(cx, cy, cellWidth, cellHeight));
                cx += cellWidth + cellSpacingX;
                if (++i == GRID_COLUMNS)
                {
                    j++;
                    i = 0;
                    cx = x + cellSpacingX;
                    cy += cellHeight + cellSpacingY;
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
