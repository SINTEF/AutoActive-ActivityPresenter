using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public class PlayerTreeView : ListView
    {
        public static readonly GridLength DefaultWidth = 200;

        public PlayerTreeView () : base(ListViewCachingStrategy.RecycleElementAndDataTemplate)
		{
            BackgroundColor = Color.White;
            SelectionMode = ListViewSelectionMode.None;

            ItemTemplate = new DataItemTemplateSelector();

            var registryTree = new DataRegistryTree();
            registryTree.DataPointTapped += OnDataPointTapped;
            registryTree.UseInTimelineTapped += OnUseInTimelineTapped;

            ItemsSource = registryTree;
        }

        public event EventHandler<IDataPoint> DataPointTapped;
        private void OnDataPointTapped(object sender, IDataPoint datapoint)
        {
            DataPointTapped?.Invoke(this, datapoint);
        }

        public event EventHandler<IDataPoint> UseInTimelineTapped;
        private void OnUseInTimelineTapped(object sender, IDataPoint datapoint)
        {
            UseInTimelineTapped?.Invoke(this, datapoint);
        }

    }

    internal class DataItemTemplateSelector : DataTemplateSelector
    {
        DataTemplate providerTemplate = new DataTemplate(typeof(DataProviderCell));
        DataTemplate structureTemplate = new DataTemplate(typeof(DataStructureCell));
        DataTemplate pointTemplate = new DataTemplate(typeof(DataPointCell));
        DataTemplate emptyTemplate = new DataTemplate(typeof(TextCell));

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is DataProviderItem) return providerTemplate;
            if (item is DataStructureItem) return structureTemplate;
            if (item is DataPointItem) return pointTemplate;
            return emptyTemplate;
        }
    }

    internal abstract class DataItemCell : TextCell
    {
        public DataItemCell()
        {
            this.SetBinding(TextProperty, "Text");

            var infoAction = new MenuItem { Text = "Info" };
            infoAction.Clicked += InfoClicked;
            ContextActions.Add(infoAction);
        }

        private void InfoClicked(object sender, EventArgs e)
        {
            Debug.WriteLine($"DataItem info clicked");
        }

        protected override void OnTapped()
        {
            var dataItem = BindingContext as DataItem;
            dataItem?.OnTapped();
        }
    }

    internal class DataProviderCell : DataStructureCell
    {
        public DataProviderCell()
        {
            Detail = "DataProvider";

            var closeAction = new MenuItem { Text = "Close", IsDestructive = true };
            closeAction.Clicked += CloseClicked;
            ContextActions.Add(closeAction);
        }

        private void CloseClicked(object sender, EventArgs e)
        {
            var dataProviderItem = BindingContext as DataProviderItem;
            dataProviderItem?.DataProvider.Unregister();
        }
    }

    internal class DataStructureCell : DataItemCell
    {
        public DataStructureCell()
        {
            Detail = "DataStructure";
        }
    }

    internal class DataPointCell : DataItemCell
    {
        public DataPointCell()
        {
            Detail = "DataPoint";

            // FIXME: Only for compatible data item types
            var useInTimelineAction = new MenuItem { Text = "Timeline" };
            useInTimelineAction.Clicked += UseInTimelineClicked;
            ContextActions.Add(useInTimelineAction);
        }

        private void UseInTimelineClicked(object sender, EventArgs e)
        {
            var dataPointItem = BindingContext as DataPointItem;
            dataPointItem?.OnUseInTimelineTapped();
        }
    }

    /* ---- Classes for building the tree view ---- */
    internal class DataRegistryTree : ObservableCollection<DataItem>
    {
        private readonly Dictionary<IDataProvider, DataProviderItem> providerItems = new Dictionary<IDataProvider, DataProviderItem>();

        internal DataRegistryTree()
        {
            // The top level always show all available dataproviders
            // Listen for changes
            DataRegistry.ProviderAdded += ProviderAdded;
            DataRegistry.ProviderRemoved += ProviderRemoved;
            // Add current items
            foreach (var dataprovider in DataRegistry.Providers)
                ProviderAdded(dataprovider);
        }

        private void ProviderAdded(IDataProvider dataprovider)
        {
            if (providerItems.ContainsKey(dataprovider)) return;
            var item = new DataProviderItem(dataprovider, this);
            Add(item);
            providerItems.Add(dataprovider, item);
        }

        private void ProviderRemoved(IDataProvider dataprovider)
        {
            if (providerItems.TryGetValue(dataprovider, out var item))
            {
                Remove(item);
                item.HideChildItems();
                providerItems.Remove(dataprovider);
            }
        }

        // Events for the view to listen to

        public event EventHandler<IDataPoint> UseInTimelineTapped;
        internal void OnUseInTimelineTapped(IDataPoint datapoint)
        {
            UseInTimelineTapped?.Invoke(this, datapoint);
        }

        public event EventHandler<IDataPoint> DataPointTapped;
        internal void OnDataPointTapped(IDataPoint datapoint)
        {
            DataPointTapped?.Invoke(this, datapoint);
        }
    }

    internal abstract class DataItem
    {
        internal DataItem(DataRegistryTree tree)
        {
            Tree = tree;
        }

        protected DataRegistryTree Tree { get; private set; }

        public abstract string Text { get; }

        public abstract void OnTapped();
    }

    internal class DataProviderItem : DataStructureItem
    {
        internal DataProviderItem(IDataProvider dataprovider, DataRegistryTree tree) : base(dataprovider, tree)
        {
            DataProvider = dataprovider;
            IsShown = true;
        }

        public IDataProvider DataProvider { get; private set; }
    }

    internal class DataStructureItem : DataItem
    {
        internal DataStructureItem(IDataStructure datastructure, DataRegistryTree tree) : base(tree)
        {
            DataStructure = datastructure;

            datastructure.ChildAdded += ChildAdded;
            datastructure.ChildRemoved += ChildRemoved;
            datastructure.DataPointAdded += DataPointAdded;
            datastructure.DataPointRemoved += DataPointRemoved;

            foreach (var child in datastructure.Children) ChildAdded(datastructure, child);
            foreach (var point in datastructure.DataPoints) DataPointAdded(datastructure, point);
        }

        public IDataStructure DataStructure { get; private set; }

        public override string Text => DataStructure.Name;

        public bool IsExpanded { get; private set; } = false;
        public bool IsShown { get; protected set; } = false;

        public override void OnTapped()
        {
            Debug.WriteLine($"DataStructureItem tapped");
            ToggleExpanded();
        }

        // --- Keep track of the children and datapoints ---
        private readonly Dictionary<IDataStructure, DataItem> structureItems = new Dictionary<IDataStructure, DataItem>();
        private readonly Dictionary<IDataPoint, DataItem> pointItems = new Dictionary<IDataPoint, DataItem>();
        protected List<DataItem> ChildItems { get; private set; } = new List<DataItem>();

        private void ChildAdded(IDataStructure sender, IDataStructure datastructure)
        {
            if (structureItems.ContainsKey(datastructure)) return;
            var item = new DataStructureItem(datastructure, Tree);
            ChildItems.Add(item);
            structureItems.Add(datastructure, item);
        }

        private void ChildRemoved(IDataStructure sender, IDataStructure datastructure)
        {
            if (structureItems.TryGetValue(DataStructure, out var item))
            {
                structureItems.Remove(datastructure);
                ChildItems.Remove(item);
            }
        }

        private void DataPointAdded(IDataStructure sender, IDataPoint datapoint)
        {
            if (pointItems.ContainsKey(datapoint)) return;
            var item = new DataPointItem(datapoint, Tree);
            ChildItems.Add(item);
            pointItems.Add(datapoint, item);
        }

        private void DataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            if (pointItems.TryGetValue(datapoint, out var item))
            {
                pointItems.Remove(datapoint);
                ChildItems.Remove(item);
            }
        }

        // --- Toggle expanded ---
        internal void ToggleExpanded()
        {
            if (IsExpanded)
            {
                // Remove our children from the list
                HideChildItems();
                IsExpanded = false;
            }
            else
            {
                IsExpanded = true;
                // Add our children to the list
                var index = Tree.IndexOf(this);
                ShowChildItems(ref index);
            }
        }

        internal void HideChildItems()
        {
            if (IsExpanded)
            {
                foreach (var child in ChildItems)
                {
                    Tree.Remove(child);
                    if (child is DataStructureItem structureItem)
                    {
                        structureItem.IsShown = true;
                        structureItem.HideChildItems();
                    }
                }
            }
        }

        private void ShowChildItems(ref int index)
        {
            if (IsExpanded)
            {
                foreach (var child in ChildItems)
                {
                    index++;
                    Tree.Insert(index, child);
                    if (child is DataStructureItem structureItem)
                    {
                        structureItem.IsShown = true;
                        structureItem.ShowChildItems(ref index);
                    }
                }
            }
        }
    }

    internal class DataPointItem : DataItem
    {
        internal DataPointItem(IDataPoint datapoint, DataRegistryTree tree) : base(tree)
        {
            DataPoint = datapoint;
        }

        public IDataPoint DataPoint { get; private set; }

        public override string Text => DataPoint.Name;

        public override void OnTapped()
        {
            Tree.OnDataPointTapped(DataPoint);
        }

        public void OnUseInTimelineTapped()
        {
            Tree.OnUseInTimelineTapped(DataPoint);
        }
    }
}