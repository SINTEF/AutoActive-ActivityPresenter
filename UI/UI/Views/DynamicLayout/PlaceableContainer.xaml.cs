﻿using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views.DynamicLayout
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlaceableContainer : ContentView, IFigureContainer, ISerializableView
    {
        public string ViewType => "no.sintef.ui.placeablecontainer";
        private readonly List<(PlaceableItem item, PlaceableLocation location)> _placeableItems = new List<(PlaceableItem, PlaceableLocation)>();

        public bool HasAnyItems => !(_placeableItems.Count == 1 && _placeableItems.First().Item1.Item == null);
        public bool AutoAddIfEmpty = true;

        public PlaceableContainer()
        {
            InitializeComponent();
            foreach (var el in XamarinHelpers.GetAllChildElements<PlaceableItem>(this))
            {
                _placeableItems.Add((el, PlaceableLocation.Center));
            }
        }

        private bool PlacementLocationVisible
        {
            set
            {
                foreach (var (item, _) in _placeableItems)
                    item.PlacementLocationVisible = value;
            }
        }

        private IDataPoint _selectedItem;
        private TimeSynchronizedContext _selectedContext;

        public void DataPointSelected(IDataPoint dataPoint, TimeSynchronizedContext context)
        {
            SelectItem(dataPoint, context);
        }

        public void SelectItem(IDataPoint item, TimeSynchronizedContext context)
        {
            // Toggle if the same item is selected
            if (_selectedItem == item)
            {
                DeselectItem();
                return;
            }

            _selectedContext = context;
            PlacementLocationVisible = true;
            _selectedItem = item;

            // If it is the first datapoint, add it automatically if this is enabled
            if (!HasAnyItems && AutoAddIfEmpty)
            {
                PlaceableItem_OnLocationSelected(_placeableItems.First().Item1, PlaceableLocation.Center);
            }
        }

        public (IDataPoint, TimeSynchronizedContext) DeselectItem()
        {
            var item = _selectedItem;
            var context = _selectedContext;
            _selectedItem = null;
            PlacementLocationVisible = false;
            _selectedContext = null;
            return (item, context);
        }

        private async void PlaceableItem_OnLocationSelected(object sender, PlaceableLocation e)
        {
            var (item, context) = DeselectItem();

            if (item == null)
            {
                Debug.WriteLine("No item selected.");
                return;
            }

            if (!(sender is PlaceableItem placeableSender))
            {
                Debug.WriteLine($"Unknown sender: {sender}");
                return;
            }


            Debug.WriteLine(placeableSender.Item != null
                ? $"Placing: {item} @ {e} : {placeableSender.Item}"
                : $"Placing: {item} @ {e}");

            try
            {
                await PlaceItem(placeableSender, item, context, e);
            }
            catch (Exception ex)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"Could not add figure: {ex.Message}", XamarinHelpers.GetCurrentPage(Navigation));
            }
        }

        private async Task<FigureView> CreateFigureView(IDataPoint item, TimeSynchronizedContext context)
        {
            var figureView = await FigureView.GetView(item, context);
            if (figureView != null)
            {
                DatapointAdded?.Invoke(this, (item, context));
            }
            return figureView;
        }

        public async Task<PlaceableItem> PlaceItem(PlaceableItem placeableSender, IDataPoint item, TimeSynchronizedContext context,
            PlaceableLocation e)
        {
            if (e != PlaceableLocation.Center)
            {
                return await AddItem(placeableSender, item, context, e);
            }

            if (placeableSender.Item != null)
            {
                await ToggleDataPoint(placeableSender, item, context);
                return placeableSender;
            }

            placeableSender.SetItem(await CreateFigureView(item, context));
            return placeableSender;
        }

        private async Task<PlaceableItem> AddItem(PlaceableItem placeableSender, IDataPoint item, TimeSynchronizedContext context,
            PlaceableLocation location)
        {
            var visualizer = await CreateFigureView(item, context);
            var placeableItem = new PlaceableItem();
            placeableItem.LocationSelected += PlaceableItem_OnLocationSelected;
            placeableItem.SetItem(visualizer);
            _placeableItems.Add((placeableItem, location));
            placeableItem.HorizontalOptions = LayoutOptions.FillAndExpand;
            placeableItem.VerticalOptions = LayoutOptions.FillAndExpand;
            placeableSender.PlaceRelative(placeableItem, location);
            return placeableItem;
        }

        private async Task ToggleDataPoint(PlaceableItem container, IDataPoint item, TimeSynchronizedContext context)
        {
            await container.Item.ToggleDataPoint(item, context);

            if (container.Item != null && container.Item.DataPoints.Any()) return;

            RemoveItem(container, context);
        }

        private PlaceableItem RemoveItem(PlaceableItem container, TimeSynchronizedContext context)
        {
            if (container.Item != null)
            {
                foreach (var itemDataPoint in container.Item.DataPoints)
                {
                    DatapointRemoved?.Invoke(this, (itemDataPoint, context));
                }
            }
            container.SetItem(null);

            var ix = _placeableItems.FindIndex(it => it.item == container);
            if (ix != -1)
            {
                _placeableItems.RemoveAt(ix);
            }

            var placeableParent = XamarinHelpers.GetTypedElementFromParents<PlaceableItem>(container.Parent);
            if (!(container.Parent is ResizableStackLayout layout))
            {
                return placeableParent;
            }
            placeableParent?.RemoveItem(container);

            {
                var index = layout.Children.IndexOf(container);
                layout.RemoveChild(container);

                var verticalIndex = MainVerticalStackLayout.Children.IndexOf(layout);

                var leftIx = 0;
                // Move the children to the removed item's parent
                foreach (var childItemPair in container.PlaceableItems)
                {
                    var (childItem, location) = childItemPair;
                    if (placeableParent != null)
                    {
                        placeableParent.PlaceRelative(childItem, location);
                    }
                    else
                    {
                        switch (location)
                        {
                            case PlaceableLocation.Left:
                                MainHorizontalStackLayout.InsertChild(index++ - ++leftIx, childItem);
                                break;
                            case PlaceableLocation.Right:
                                MainHorizontalStackLayout.InsertChild(index, childItem);
                                break;
                            case PlaceableLocation.Up:
                                if (!MainHorizontalStackLayout.Children.Any())
                                {
                                    MainHorizontalStackLayout.InsertChild(0, childItem);
                                    break;
                                }
                                MainVerticalStackLayout.InsertChild(verticalIndex++, childItem);
                                break;
                            case PlaceableLocation.Down:
                                if (!MainHorizontalStackLayout.Children.Any())
                                {
                                    MainHorizontalStackLayout.InsertChild(0, childItem);
                                    break;
                                }
                                MainVerticalStackLayout.InsertChild(verticalIndex + 1, childItem);
                                break;
                            case PlaceableLocation.Center:
                                throw new ArgumentException("Center should have been handled elsewhere");
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }

            // Hack to remove empty StackLayouts
            if (!layout.Children.Any() && layout.Parent is ResizableStackLayout stackParent)
            {
                if (layout != MainHorizontalStackLayout && layout != MainVerticalStackLayout)
                {
                    stackParent.RemoveChild(layout);
                }
            }

            if (_placeableItems.Count != 0) return placeableParent;

            // Ensure at least one item
            var view = new PlaceableItem();
            _placeableItems.Add((view, PlaceableLocation.Center));
            view.LocationSelected += PlaceableItem_OnLocationSelected;
            MainHorizontalStackLayout.InsertChild(0, view);
            return view;
        }



        public FigureView Selected { get; set; }

        public void RemoveChild(FigureView figureView)
        {
            var item = XamarinHelpers.GetTypedElementFromParents<PlaceableItem>(figureView);
            RemoveItem(item, figureView.Context);
        }

        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointAdded;
        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointRemoved;
        public void InvokeDatapointRemoved(IDataPoint dataPoint, DataViewerContext context)
        {
            DatapointRemoved?.Invoke(this, (dataPoint, context));
        }

        public void InvokeDatapointAdded(IDataPoint dataPoint, DataViewerContext context)
        {
            DatapointAdded?.Invoke(this, (dataPoint, context));
        }

        public async Task DeserializeView(JObject root, IDataStructure archive)
        {
            await SerializableViewHelper.EnsureViewType(root, this);

            var children = (JObject) root["children"];
            var verticalChildren = (JArray) children["vertical"];

            ResizableStackLayout.SetSizeWeight(MainHorizontalStackLayout, root["size_weight_horizontal"].Value<double>());
            ResizableStackLayout.SetSizeWeight(MainVerticalStackLayout, root["size_weight_vertical"].Value<double>());

            foreach (var vertical in verticalChildren)
            {
                if (vertical.ToString() == "horizontal") continue;

                var pItem = new PlaceableItem {Context = ViewerContext};
                pItem.ItemDeserialized += PlaceableItemOnItemDeserialized;
                await pItem.DeserializeView((JObject)vertical, archive);
                pItem.ItemDeserialized -= PlaceableItemOnItemDeserialized;
                if (pItem.Item == null)
                {
                    continue;
                }
                MainVerticalStackLayout.AddChild(pItem);
            }

            var horizontalChildren = (JArray)children["horizontal"];
            foreach (var horizontal in horizontalChildren)
            {
                PlaceableItem pItem;
                bool shouldAdd;
                if (MainHorizontalStackLayout.Children.Count == 1 &&
                    ((PlaceableItem)MainHorizontalStackLayout.Children.First()).Item == null)
                {
                    pItem = (PlaceableItem)MainHorizontalStackLayout.Children.First();
                    shouldAdd = false;
                }
                else
                {
                    pItem = new PlaceableItem();
                    shouldAdd = true;
                }

                pItem.LocationSelected += PlaceableItem_OnLocationSelected;

                pItem.Context = ViewerContext;

                pItem.ItemDeserialized += PlaceableItemOnItemDeserialized;
                await pItem.DeserializeView((JObject)horizontal, archive);
                pItem.ItemDeserialized -= PlaceableItemOnItemDeserialized;

                if (pItem.Item != null && shouldAdd)
                {
                    MainHorizontalStackLayout.AddChild(pItem);
                }
            }

        }

        private void PlaceableItemOnItemDeserialized(object sender, (PlaceableItem item, PlaceableLocation location) args)
        {
            var view = args.item.Item;
            if (view == null)
                return;
            args.item.LocationSelected += PlaceableItem_OnLocationSelected;
            if (!(_placeableItems.Count == 1 && _placeableItems.First().item == args.item))
            {
                _placeableItems.Add(args);
            }

            foreach (var dataPoint in view.DataPoints)
            {
                InvokeDatapointAdded(dataPoint, view.Context);
            }
        }


        public JObject SerializeView(JObject root = null)
        {
            root = SerializableViewHelper.SerializeDefaults(root, this);

            var arr = new JArray();
            root["container"] = arr;
            root["size_weight_horizontal"] = ResizableStackLayout.GetSizeWeight(MainHorizontalStackLayout);
            root["size_weight_vertical"] = ResizableStackLayout.GetSizeWeight(MainVerticalStackLayout);

            var vertical = new JArray();

            foreach (var el in MainVerticalStackLayout.Children)
            {
                if (el == MainHorizontalStackLayout)
                {
                    vertical.Add("horizontal");
                    continue;
                }

                if (!(el is PlaceableItem placeableItem)) continue;

                vertical.Add(placeableItem.SerializeView());
            }

            var horizontal = new JArray();
            foreach (var el in MainHorizontalStackLayout.Children)
            {
                if (!(el is PlaceableItem placeableItem)) continue;

                horizontal.Add(placeableItem.SerializeView());
            }

            root["children"] = new JObject { ["vertical"] = vertical, ["horizontal"] = horizontal };

            return root;
        }

        public TimeSynchronizedContext ViewerContext { get; set; }
    }
}