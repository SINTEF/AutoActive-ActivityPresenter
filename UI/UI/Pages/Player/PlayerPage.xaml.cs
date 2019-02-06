using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SINTEF.AutoActive.UI.Pages.Synchronization;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Player
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class PlayerPage : ContentPage
	{
        static readonly double SPLIT_VIEW_WIDTH_MIN = 1000;
        static readonly double OVERLAY_MODE_WIDTH = 0.9;
        static readonly double OVERLAY_MODE_SHADE_OPACITY = 0.5;

        public DataViewerContext ViewerContext { get; } = new TimeSynchronizedContext();

        public PlaybarView Playbar { get; private set; }

        public PlayerPage ()
		{
			InitializeComponent ();

            (ViewerContext as TimeSynchronizedContext)?.SetSynchronizedToWorldClock(false);

            Playbar = new PlaybarView(ViewerContext);
            PageGrid.Children.Add(Playbar, 0, 3, 2, 3);
            //Playbar.ViewerContext = ViewerContext;
            PlayerGrid.ViewerContext = ViewerContext;

            Splitter.DragStart += Splitter_DragStart;
            Splitter.Dragged += Splitter_Dragged;

            NavigationBar.MenuButtonClicked += NavigationBar_MenuButtonClicked;

            TreeView.DataPointTapped += TreeView_DataPointTapped1;
            TreeView.UseInTimelineTapped += TreeView_UseInTimelineTapped;
        }

        private void TreeView_DataPointTapped1(object sender, IDataPoint datapoint)
        {
            PlayerGrid.AddPlotFor(datapoint);
        }

        private void TreeView_UseInTimelineTapped(object sender, IDataPoint datapoint)
        {
            Playbar.UseDataPointForTimelinePreview(datapoint);
        }

        /* -- Show or hide the tree based on window size -- */
        static readonly GridLength gridZeroLength = new GridLength(0, GridUnitType.Absolute);
        enum TreeViewState { SplitMode, OverlayModeHidden, OverlayModeShown }
        TreeViewState treeViewState = TreeViewState.SplitMode;
        
        void UpdateTreeView(TreeViewState nextTreeViewState)
        {
            if (nextTreeViewState == treeViewState) return;
            var prevTreeViewState = treeViewState;
            treeViewState = nextTreeViewState;

            // Hide or show the menu button
            NavigationBar.MenuButtonShown = nextTreeViewState != TreeViewState.SplitMode;
            
            // Deal with the tree
            if (nextTreeViewState == TreeViewState.SplitMode)
            {
                // Remove it from the overlay
                OverlayShading.IsVisible = false;
                TreeView.IsVisible = false;
                OverlayLayout.Children.Remove(TreeView);
                // Move the tree into the grid
                var grid = Content as Grid;
                ColumnSplitter.Width = PlayerSplitterView.DefaultWidth;
                ColumnTree.Width = treeViewWidth;
                grid.Children.Add(TreeView, 2, 1);
                TreeView.IsVisible = true;
            }
            else if (nextTreeViewState == TreeViewState.OverlayModeShown || nextTreeViewState == TreeViewState.OverlayModeHidden)
            {
                // We should prepare the overlay
                if (prevTreeViewState == TreeViewState.SplitMode)
                {
                    // Remove it from the split
                    var grid = Content as Grid;
                    ColumnSplitter.Width = gridZeroLength;
                    ColumnTree.Width = gridZeroLength;
                    grid.Children.Remove(TreeView);
                    // Show it in the overlay
                    OverlayLayout.Children.Add(TreeView, new Rectangle(1, 1, OVERLAY_MODE_WIDTH, 1), AbsoluteLayoutFlags.All);

                    // Show or hide the view
                    if (nextTreeViewState == TreeViewState.OverlayModeShown)
                    {
                        TreeView.IsVisible = true;
                        OverlayShading.IsVisible = true;
                    }
                    else
                    {
                        TreeView.IsVisible = false;
                        OverlayShading.IsVisible = false;
                    }
                }
                else
                {
                    // Animate the opening or closing of the overlay
                    if (nextTreeViewState == TreeViewState.OverlayModeShown)
                    {
                        OverlayShading.Opacity = 0;
                        OverlayShading.IsVisible = true;
                        // Animate the TreeView with a fixed position (so it doesn't scale)
                        AbsoluteLayout.SetLayoutFlags(TreeView, AbsoluteLayoutFlags.SizeProportional);
                        AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(0, 0, OVERLAY_MODE_WIDTH, 1));
                        TreeView.IsVisible = true;
                        var animation = new Animation(v =>
                        {
                            OverlayShading.Opacity = v * OVERLAY_MODE_SHADE_OPACITY;
                            AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(Width-v*TreeView.Width, 0, OVERLAY_MODE_WIDTH, 1));
                        });
                        animation.Commit(this, "SlideTreeOverlayIn", rate: 10, length: 100, easing: Easing.SinIn, finished: (v, c) =>
                        {
                            OverlayShading.Opacity = OVERLAY_MODE_SHADE_OPACITY;
                            AbsoluteLayout.SetLayoutFlags(TreeView, AbsoluteLayoutFlags.All);
                            AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(1, 0, OVERLAY_MODE_WIDTH, 1));
                        });
                    }
                    else
                    {
                        // Animate the TreeView with a fixed position (so it doesn't scale)
                        AbsoluteLayout.SetLayoutFlags(TreeView, AbsoluteLayoutFlags.SizeProportional);
                        AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(Width-OVERLAY_MODE_WIDTH*TreeView.Width, 0, OVERLAY_MODE_WIDTH, 1));
                        var animation = new Animation(v =>
                        {
                            OverlayShading.Opacity = v * OVERLAY_MODE_SHADE_OPACITY;
                            AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(Width - v * TreeView.Width, 0, OVERLAY_MODE_WIDTH, 1));
                        }, start: 1, end: 0);
                        animation.Commit(this, "SlideTreeOverlayOut", rate: 10, length: 100, easing: Easing.SinOut, finished: (v, c) =>
                        {
                            OverlayShading.IsVisible = false;
                            TreeView.IsVisible = false;
                        });
                    }

                }
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            var shouldShowSplit = width >= SPLIT_VIEW_WIDTH_MIN;
            if (shouldShowSplit && treeViewState != TreeViewState.SplitMode)
            {
                UpdateTreeView(TreeViewState.SplitMode);
            }
            else if (!shouldShowSplit && treeViewState == TreeViewState.SplitMode)
            {
                UpdateTreeView(TreeViewState.OverlayModeHidden);
            }
        }

        private void NavigationBar_MenuButtonClicked(object sender, EventArgs e)
        {
            if (treeViewState == TreeViewState.OverlayModeHidden)
            {
                UpdateTreeView(TreeViewState.OverlayModeShown);
            }
            else if (treeViewState == TreeViewState.OverlayModeShown)
            {
                UpdateTreeView(TreeViewState.OverlayModeHidden);
            }
        }

        /* -- Resizing the tree view -- */
        GridLength treeViewWidth = PlayerTreeView.DefaultWidth;
        GridLength splitterStartDragWidth;

        private void Splitter_DragStart()
        {
            splitterStartDragWidth = ColumnTree.Width;
        }

        private void Splitter_Dragged(double x, double y)
        {
            var newWidth = splitterStartDragWidth.Value - x;
            if (newWidth >= 0 && newWidth + ColumnSplitter.Width.Value <= Width)
            {
                ColumnTree.Width = treeViewWidth = new GridLength(newWidth);
            }
        }
	}
}