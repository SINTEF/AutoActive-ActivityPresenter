using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Views.DynamicLayout;
using System;
using System.Threading;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public partial class PlayerPage : ContentPage
    {
        private const double SplitViewWidthMin = 1000;
        private const double OverlayModeWidth = 0.9;
        private const double OverlayModeShadeOpacity = 0.5;
        private static bool _fistStart = true;
        public TimeSynchronizedContext ViewerContext { get; } = new TimeSynchronizedContext();

        public PlayerPage()
        {
            InitializeComponent();

            ViewerContext?.SetSynchronizedToWorldClock(true);

            Appearing += OnAppearing;
            Disappearing += OnDisappearing;
        }

        private void OnAppearing(object sender, EventArgs e)
        {
            Playbar.ViewerContext = ViewerContext;

            Splitter.DragStart += Splitter_DragStart;
            Splitter.Dragged += Splitter_Dragged;

            TreeView.DataPointTapped += TreeView_DataPointTapped;
            TreeView.UseInTimelineTapped += TreeView_UseInTimelineTapped;

            Playbar.DataTrackline.RegisterFigureContainer(PlayerContainer);

            if (!_fistStart) return;
            _fistStart = false;

            var loaderThread = new Thread(async () =>
            {
                // Load Archive in the background
                var browser = DependencyHandler.GetInstance<IFileBrowser>();
                var file = await browser.LoadFromUri("D:\\data\\v0.6.0\\testSine.aaz");
                var archive = await Archive.Archive.Open(file);
                XamarinHelpers.EnsureMainThread(() =>
                {
                    foreach (var session in archive.Sessions)
                    {
                        session.Register();
                    }
                });

            });
            loaderThread.Start();
        }

        private void OnDisappearing(object sender, EventArgs e)
        {
            Splitter.DragStart -= Splitter_DragStart;
            Splitter.Dragged -= Splitter_Dragged;

            TreeView.DataPointTapped -= TreeView_DataPointTapped;
            TreeView.UseInTimelineTapped -= TreeView_UseInTimelineTapped;

            Playbar.DataTrackline.DeregisterFigureContainer(PlayerContainer);
        }

        private void TreeView_DataPointTapped(object sender, IDataPoint dataPoint)
        {
            PlayerContainer.DataPointSelected(dataPoint, ViewerContext);
        }

        private void TreeView_UseInTimelineTapped(object sender, IDataPoint datapoint)
        {
            Playbar.UseDataPointForTimelinePreview(datapoint);
        }

        /* -- Show or hide the tree based on window size -- */
	    private static readonly GridLength GridZeroLength = new GridLength(0, GridUnitType.Absolute);

	    private enum TreeViewState { SplitMode, OverlayModeHidden, OverlayModeShown }
	    private TreeViewState _treeViewState = TreeViewState.SplitMode;

	    private void UpdateTreeView(TreeViewState nextTreeViewState)
        {
            if (nextTreeViewState == _treeViewState) return;
            var prevTreeViewState = _treeViewState;
            _treeViewState = nextTreeViewState;

            // Deal with the tree
            if (nextTreeViewState == TreeViewState.SplitMode)
            {
                // Remove it from the overlay
                OverlayShading.IsVisible = false;
                TreeView.IsVisible = false;
                OverlayLayout.Children.Remove(TreeView);
                // Move the tree into the grid
                var grid = Content as Grid;
                ColumnSplitter.Width = 2d;
                ColumnTree.Width = _treeViewWidth;
                grid?.Children.Add(TreeView, 2, 1);
                TreeView.IsVisible = true;
            }
            else if (nextTreeViewState == TreeViewState.OverlayModeShown || nextTreeViewState == TreeViewState.OverlayModeHidden)
            {
                // We should prepare the overlay
                if (prevTreeViewState == TreeViewState.SplitMode)
                {
                    // Remove it from the split
                    var grid = Content as Grid;
                    ColumnSplitter.Width = GridZeroLength;
                    ColumnTree.Width = GridZeroLength;
                    grid?.Children.Remove(TreeView);
                    // Show it in the overlay
                    OverlayLayout.Children.Add(TreeView, new Rectangle(1, 1, OverlayModeWidth, 1), AbsoluteLayoutFlags.All);

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
                        AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(0, 0, OverlayModeWidth, 1));
                        TreeView.IsVisible = true;
                        var animation = new Animation(v =>
                        {
                            OverlayShading.Opacity = v * OverlayModeShadeOpacity;
                            AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(Width-v*TreeView.Width, 0, OverlayModeWidth, 1));
                        });
                        animation.Commit(this, "SlideTreeOverlayIn", rate: 10, length: 100, easing: Easing.SinIn, finished: (v, c) =>
                        {
                            OverlayShading.Opacity = OverlayModeShadeOpacity;
                            AbsoluteLayout.SetLayoutFlags(TreeView, AbsoluteLayoutFlags.All);
                            AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(1, 0, OverlayModeWidth, 1));
                        });
                    }
                    else
                    {
                        // Animate the TreeView with a fixed position (so it doesn't scale)
                        AbsoluteLayout.SetLayoutFlags(TreeView, AbsoluteLayoutFlags.SizeProportional);
                        AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(Width-OverlayModeWidth*TreeView.Width, 0, OverlayModeWidth, 1));
                        var animation = new Animation(v =>
                        {
                            OverlayShading.Opacity = v * OverlayModeShadeOpacity;
                            AbsoluteLayout.SetLayoutBounds(TreeView, new Rectangle(Width - v * TreeView.Width, 0, OverlayModeWidth, 1));
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
            // Disable hiding overlay
            UpdateTreeView(TreeViewState.SplitMode);
            return;
            var shouldShowSplit = width >= SplitViewWidthMin;
            if (shouldShowSplit && _treeViewState != TreeViewState.SplitMode)
            {
                UpdateTreeView(TreeViewState.SplitMode);
            }
            else if (!shouldShowSplit && _treeViewState == TreeViewState.SplitMode)
            {
                UpdateTreeView(TreeViewState.OverlayModeHidden);
            }
        }

        private void NavigationBar_MenuButtonClicked(object sender, EventArgs e)
        {
            if (_treeViewState == TreeViewState.OverlayModeHidden)
            {
                UpdateTreeView(TreeViewState.OverlayModeShown);
            }
            else if (_treeViewState == TreeViewState.OverlayModeShown)
            {
                UpdateTreeView(TreeViewState.OverlayModeHidden);
            }
        }

        /* -- Resizing the tree view -- */
        GridLength _treeViewWidth = PlayerTreeView.DefaultWidth;
        GridLength _splitterStartDragWidth;

        private void Splitter_DragStart(DraggableSeparator sender, double x, double y)
        {
            _splitterStartDragWidth = ColumnTree.Width;
        }

        private void Splitter_Dragged(DraggableSeparator sender, double x, double y, double dx, double dy)
        {
            var newWidth = _splitterStartDragWidth.Value - x;
            if (newWidth >= 0 && newWidth + ColumnSplitter.Width.Value <= Width)
            {
                ColumnTree.Width = _treeViewWidth = new GridLength(newWidth);
            }
        }
	}
}