using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views.DynamicLayout
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlaceableItem : ContentView, ISerializableView
    {
        public string ViewType => "no.sintef.ui.placeableitem";
        public Guid ViewId = Guid.NewGuid();
        public PlaceableItem()
        {
            InitializeComponent();
            PlacementLocations = PlacementLocationsCenterSmall;

            PlacementLocationVisible = false;

            SizeChanged += OnSizeChanged;

        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            var size = Math.Min(MasterLayout.Width, MasterLayout.Height);
            if (size < 30 * 3 + 10)
            {
                PlacementLocations = PlacementLocationsCenterTiny;
            }
            else if (size < 60 * 3 + 10)
            {
                PlacementLocations = PlacementLocationsCenterSmall;
            }
            else
            {
                PlacementLocations = PlacementLocationsCenter;
            }
        }

        public FigureView Item { get; set; }
        public void SetItem(FigureView item)
        {
            if (item == null)
            {
                if (Item == null) return;

                MasterLayout.Children.RemoveAt(0);
                Item = null;
                return;
            }

            Item = item;
            AbsoluteLayout.SetLayoutFlags(item, AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(item, new Rectangle(0, 0, 1, 1));
            MasterLayout.Children.Insert(0, item);
        }

        private Grid PlacementLocations
        {
            get => _placementLocations;
            set
            {
                if (_placementLocations == value)
                {
                    return;
                }

                var prev = _placementLocations;
                _placementLocations = value;

                if (prev == null) return;

                var wasVisible = prev.IsVisible;
                prev.IsVisible = false;
                PlacementLocationVisible = wasVisible;
            }
        }

        public event EventHandler<PlaceableLocation> LocationSelected;

        public bool PlacementLocationVisible
        {
            get => PlacementLocations.IsVisible;
            set
            {
                if (value)
                {
                    foreach (var child in PlacementLocations.Children)
                    {
                        if (!(child is Button button)) continue;

                        child.IsVisible = Item != null || button.Text == "▢";
                    }
                }

                PlacementLocations.IsVisible = value;
            }
        }

        public override string ToString()
        {
            return $"Placeable item: {Item}";
        }

        protected virtual void OnButtonPressed(PlaceableLocation e)
        {
            LocationSelected?.Invoke(this, e);
        }

        private void UpButton_OnClicked(object sender, EventArgs e)
        {
            OnButtonPressed(PlaceableLocation.Up);
        }
        private void RightButton_OnClicked(object sender, EventArgs e)
        {
            OnButtonPressed(PlaceableLocation.Right);
        }
        private void DownButton_OnClicked(object sender, EventArgs e)
        {
            OnButtonPressed(PlaceableLocation.Down);
        }
        private void LeftButton_OnClicked(object sender, EventArgs e)
        {
            OnButtonPressed(PlaceableLocation.Left);
        }
        private void CenterButton_OnClicked(object sender, EventArgs e)
        {
            OnButtonPressed(PlaceableLocation.Center);
        }

        public List<(PlaceableItem, PlaceableLocation)> PlaceableItems = new List<(PlaceableItem, PlaceableLocation)>();
        private Grid _placementLocations;

        public void PlaceRelative(PlaceableItem visualizer, PlaceableLocation location)
        {
            var verticalLayout = VerticalLayout;
            var horizontalLayout = HorizontalLayout;

            PlaceableItems.Add((visualizer, location));

            if (location == PlaceableLocation.Right)
            {
                horizontalLayout.InsertChild(horizontalLayout.Children.IndexOf(MasterLayout) + 1, visualizer);
                return;
            }
            if (location == PlaceableLocation.Left)
            {
                horizontalLayout.InsertChild(horizontalLayout.Children.IndexOf(MasterLayout), visualizer);
                return;
            }

            var newLayout = new ResizableStackLayout
            {
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
            };

            newLayout.InsertChild(0, visualizer);

            if (location == PlaceableLocation.Up)
            {
                verticalLayout.InsertChild(verticalLayout.Children.IndexOf(horizontalLayout), newLayout);
            }
            else if (location == PlaceableLocation.Down)
            {
                verticalLayout.InsertChild(verticalLayout.Children.IndexOf(horizontalLayout) + 1, newLayout);
            }
        }

        public void RemoveItem(PlaceableItem placeableSender)
        {
            foreach (var el in PlaceableItems)
            {
                if (el.Item1 != placeableSender) continue;

                PlaceableItems.Remove(el);
                return;
            }
        }

        public TimeSynchronizedContext Context { get; set; }

        public Task DeserializeView(JObject root, IDataStructure archive = null)
        {
            return DeserializeView(root, archive, this);
        }

        private async Task DeserializeView(JObject root, IDataStructure archive, PlaceableItem rootParent)
        {
            if (root["type"].ToString() != ViewType)
            {
                Debug.WriteLine("Unknown type for view.");
                return;
            }

            var item = await FigureView.DeserializeView((JObject) root["item"], Context, archive);
            SetItem(item);

            ResizableStackLayout.SetSizeWeight(this, root["weight"].Value<double>());
            ResizableStackLayout.SetSizeWeight(MasterLayout, root["master_layout_weight"].Value<double>());

            ResizableStackLayout.SetSizeWeight(HorizontalLayout, root["size_weight_horizontal"].Value<double>());
            ResizableStackLayout.SetSizeWeight(VerticalLayout, root["size_weight_vertical"].Value<double>());

            if (rootParent == this)
            {
                rootParent.ItemDeserialized?.Invoke(this, (this, PlaceableLocation.Center));
            }

            var children = ((JArray)root["children"]).Cast<JObject>();
            foreach (var child in children)
            {
                var plItem = new PlaceableItem {Context = Context};
                await plItem.DeserializeView((JObject)child["item"], archive, rootParent);
                if(plItem.Item == null) continue;

                var locString = child["location"].Value<string>();
                var location = JsonConvert.DeserializeObject<PlaceableLocation>(locString);
                PlaceRelative(plItem, location);

                var horizParent = XamarinHelpers.GetTypedElementFromParents<ResizableStackLayout>(plItem.Parent);
                var vertParent = XamarinHelpers.GetTypedElementFromParents<ResizableStackLayout>(horizParent.Parent);
                var horizWeight = child["item"]["parent_size_weight_horizontal"]?.Value<double>();
                var vertWeight = child["item"]["parent_size_weight_vertical"]?.Value<double>();
                if(horizWeight.HasValue)
                    ResizableStackLayout.SetSizeWeight(horizParent, horizWeight.Value);
                if(vertWeight.HasValue)
                    ResizableStackLayout.SetSizeWeight(vertParent, vertWeight.Value);

                rootParent.ItemDeserialized?.Invoke(this, (plItem, location));
            }


        }

        public event EventHandler<(PlaceableItem item, PlaceableLocation location)> ItemDeserialized;

        public JObject SerializeView(JObject root = null)
        {
            root = SerializableViewHelper.SerializeDefaults(root, this);

            root["item"] = Item?.SerializeView();
            root["view_id"] = ViewId.ToString();
            root["weight"] = ResizableStackLayout.GetSizeWeight(this);
            root["master_layout_weight"] = ResizableStackLayout.GetSizeWeight(MasterLayout);

            var items = new JArray();
            foreach (var (item, location) in PlaceableItems)
            {
                items.Add(new JObject
                {
                    ["item"] = item.SerializeView(),
                    ["location"] = JsonConvert.SerializeObject(location)
            });
            }

            root["children"] = items;

            var horizParent = XamarinHelpers.GetTypedElementFromParents<ResizableStackLayout>(Parent);
            var vertParent = XamarinHelpers.GetTypedElementFromParents<ResizableStackLayout>(horizParent.Parent);
            root["parent_size_weight_horizontal"] = ResizableStackLayout.GetSizeWeight(horizParent);
            root["parent_size_weight_vertical"] = ResizableStackLayout.GetSizeWeight(vertParent);
            root["size_weight_horizontal"] = ResizableStackLayout.GetSizeWeight(HorizontalLayout);
            root["size_weight_vertical"] = ResizableStackLayout.GetSizeWeight(VerticalLayout);

            return root;
        }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PlaceableLocation
    {
        Up, Down, Left, Right, Center
    }
}