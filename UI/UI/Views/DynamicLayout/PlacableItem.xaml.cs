using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
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

        public async Task DeserializeView(JObject root)
        {
            if (root["type"].ToString() != ViewType)
            {
                Debug.WriteLine("Unknown type for view.");
                return;
            }

            var guidString = root["id"]?.Value<string>();
            ViewId = guidString != null ? Guid.Parse(guidString) : Guid.Empty;

            //Item = await FigureView.DeserializeView((JObject)root["item"], Context);
            SetItem(await FigureView.DeserializeView((JObject)root["item"], Context));

            return;
            foreach (var vertChild in (JArray)root["vertical"])
            {

                var layout = HorizontalLayout.Children.Count == 0 ? HorizontalLayout : new ResizableStackLayout();

                foreach (var horChild in (JArray) vertChild)
                {
                    var item = new PlaceableItem {Context = Context};
                    await item.DeserializeView((JObject)horChild);

                    layout.AddChild(item);
                }
                VerticalLayout.AddChild(layout);
            }
        }

        public JObject SerializeView(JObject root = null)
        {
            if (root == null) root = new JObject();
            root["type"] = ViewType;
            root["item"] = Item.SerializeView();
            root["id"] = ViewId.ToString();

            return root;

            var vertical = new JArray();

            var children = new JObject();
            root["vertical"] = vertical;

            foreach (var el in VerticalLayout.Children)
            {
                if (el is DraggableSeparator) continue;

                if (!(el is ResizableStackLayout stackLayout)) continue;
                var horizontal = new JArray();
                if (stackLayout.Children.Count == 1 && stackLayout.Children[0] is AbsoluteLayout)
                {
                    continue;
                }

                foreach (var el2 in stackLayout.Children)
                {

                    if (el2 is DraggableSeparator) continue;

                    if (!(el2 is PlaceableItem placableItem))
                    {
                        continue;
                    }

                    horizontal.Add(placableItem.SerializeView());
                }

                vertical.Add(horizontal);
            }


            children["vertical"] = vertical;

            return root;
        }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PlaceableLocation
    {
        Up, Down, Left, Right, Center
    }
}