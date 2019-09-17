﻿using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public class PlayerGridLayout : Layout<FigureView>, ISerializableView, IFigureContainer
    {
        //TODO: re-layout if these changes
        public int GridColumns { get; set; } = 4;
        public int GridRows { get; set; } = 4;

        public int BigGridColumns { get; set; } = 2;
        public int BigGridRows { get; set; } = 1;

        private FigureView _currentlySelected;

        public FigureView Selected
        {
            get => _currentlySelected;
            set
            {
                if (_currentlySelected != null) _currentlySelected.Selected = false;
                _currentlySelected = value;
                if (_currentlySelected != null) _currentlySelected.Selected = true;
            }
        }

        // FIXME : Implement this class, and also possibly restrict this to more specific views for data-renderers
        public PlayerGridLayout()
        {
            DatapointAdded += (sender, args) =>
            {
                _contexts.Add(args);
            };
        }

        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointAdded;
        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointRemoved;
        private readonly List<(IDataPoint, DataViewerContext)> _contexts = new List<(IDataPoint, DataViewerContext)>();

        public async Task<ToggleResult> TogglePlotFor(IDataPoint datapoint, TimeSynchronizedContext timeContext)
        {
            if (Selected != null)
            {
                try
                {
                   var result = await Selected.ToggleDataPoint(datapoint, timeContext);
                   switch (result)
                   {
                       case ToggleResult.Added:
                           DatapointAdded?.Invoke(this, (datapoint, timeContext));
                           break;
                       case ToggleResult.Removed:
                           DatapointRemoved?.Invoke(this, (datapoint, timeContext));
                           break;
                       case ToggleResult.Cancelled:
                           break;
                       default:
                           throw new ArgumentOutOfRangeException();
                   }
                   return result;
                }
                catch (Exception ex)
                {
                    //TODO: put this in a helper method?
                    var page = XamarinHelpers.GetCurrentPage(this);
                    if (page != null)
                    {
                        await page.DisplayAlert("Error", $"Could not toggle data point for {datapoint.Name}: {ex.Message}",
                            "Ok");
                    }
                }

                return ToggleResult.Cancelled;
            }
            var view = await FigureView.GetView(datapoint, timeContext);
            {
                var tgr = new TapGestureRecognizer {NumberOfTapsRequired = 1};
                tgr.Tapped += view.Viewer_Tapped;
                view.GestureRecognizers.Add(tgr);
            }
            {
                var pgr = new PanGestureRecognizer();
                pgr.PanUpdated += view.Viewer_Panned;
                view.GestureRecognizers.Add(pgr);
            }

            Children.Add(view);
            DatapointAdded?.Invoke(this, (datapoint, timeContext));

            return ToggleResult.Added;
        }

        /* -- Grid layout operations -- */
        protected override SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            //Debug.WriteLine("GRID: OnMeasure");
            // We want to use the full size available
            var size = new Size(widthConstraint, heightConstraint);

            // FIXME: Call layout on the children

            return new SizeRequest(size);
        }

        protected override void LayoutChildren(double x, double y, double width, double height)
        {
            //Debug.WriteLine("GRID: LayoutChildren");
            // Leave spacing equal to one free cell

            const int cellSpacingX = 10;
            const int cellSpacingY = cellSpacingX;

            var smallCellWidth = (width - cellSpacingX * (GridColumns + 1)) / GridColumns;
            var smallCellHeight = (height - cellSpacingY * (GridRows + 1)) / GridRows;

            var bigCellWidth = smallCellWidth * 2 + cellSpacingX;
            var bigCellHeight = smallCellHeight * 2 + cellSpacingY;

            double cellWidth;
            double cellHeight;
            int nColumns;
            int nRows;

            if (BigGridColumns > 0)
            {
                nColumns = BigGridColumns;
                nRows = BigGridRows;
                cellWidth = bigCellWidth;
                cellHeight = bigCellHeight;
            }
            else
            {
                nColumns = GridColumns;
                nRows = GridRows;
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
                if (++i != nColumns) continue;

                j++;
                i = 0;
                cx = x + cellSpacingX;
                cy += cellHeight + cellSpacingY;

                if (j != nRows) continue;

                nColumns = GridColumns;
                nRows = GridRows;
                cellHeight = smallCellHeight;
                cellWidth = smallCellWidth;
            }
        }

        protected override void InvalidateLayout()
        {
            //Debug.WriteLine("GRID: InvalidateLayout");
            // When children are added or removed
        }

        protected override void OnChildMeasureInvalidated()
        {
            //Debug.WriteLine("GRID: OnChildMeasureInvalidated");
            // When a child's size changes
        }

        public void RemoveChild(FigureView figureView)
        {
            if (Selected == figureView)
                Selected = null;

            Children.Remove(figureView);
            foreach(var datapoint in figureView.DataPoints)
            {
                var elIx = _contexts.FindLastIndex(s => s.Item1 == datapoint);
                var el = _contexts[elIx];
                DatapointRemoved?.Invoke(this, el);
                _contexts.RemoveAt(elIx);
            }
        }

        public JObject SerializeView(JObject root = null)
        {
            if (root == null)
            {
                root = new JObject();
            }

            var uris = (from child in Children from dataPoint in child.DataPoints select dataPoint.URI).ToList();
            root["Children"] = new JArray(uris);
            return root;
        }

        public void DeserializeView(JObject root)
        {
            var children = root["Children"];
            return;
        }
    }
}
