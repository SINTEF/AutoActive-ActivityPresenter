﻿using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.Plugins.Import.Json;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public class PlayerTreeView : ListView
    {
        public static readonly GridLength DefaultWidth = 180;

        public PlayerTreeView () : base(ListViewCachingStrategy.RecycleElementAndDataTemplate)
		{
            BackgroundColor = Color.FromHex("#1D2637");
            SelectionMode = ListViewSelectionMode.None;

            ItemTemplate = new DataItemTemplateSelector();

            var registryTree = new DataRegistryTree(this);
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
        public readonly Dictionary<object, uint> TreeLevel = new Dictionary<object, uint>();
    }

    internal class DataItemTemplateSelector : DataTemplateSelector
    {
        private readonly DataTemplate _providerTemplate = new DataTemplate(typeof(DataProviderCell));
        private readonly DataTemplate _structureTemplate = new DataTemplate(typeof(DataStructureCell));
        private readonly DataTemplate _pointTemplate = new DataTemplate(typeof(DataPointCell));
        private readonly DataTemplate _emptyTemplate = new DataTemplate(typeof(TextCell));

        protected override DataTemplate OnSelectTemplate(object obj, BindableObject container)
        {
            if (!(container is PlayerTreeView treeView))
            {
                throw new ArgumentException("Container not TreeView");
            }

            var treeLevel = treeView.TreeLevel;

            if (!treeLevel.TryGetValue(obj, out var level))
            {
                level = 0;
            }

            {
                if (obj is DataItem item)
                {
                    item.Indentation = level;
                }
            }
            switch (obj)
            {
                case DataProviderItem item:
                    foreach (var it in item.ChildItems)
                    {
                        treeLevel[it] = level + 1;
                    }

                    return _providerTemplate;
                case DataStructureItem item:
                    foreach (var it in item.ChildItems)
                    {
                        treeLevel[it] = level + 1;
                    }
                    return _structureTemplate;
                case DataPointItem item:
                    return _pointTemplate;
                default:
                    return _emptyTemplate;
            }
        }
    }

    // View data cell class
    public abstract class DataItemCell : ViewCell
    {
        // Create datacell at top level, no indentation
        public readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string),
            typeof(DataItemCell),
            propertyChanged: (boundObject, _, value) =>
            {
                if (!(boundObject is DataItemCell cell))
                    return;

                var text = value as string;
                cell._label.Text = text;
                // Set text color for session name
                cell._label.TextColor = Color.White;
                cell._frame.BackgroundColor = Color.FromHex("23A2B1");
                cell._frame.CornerRadius = 0;
                cell._frame.BorderColor = Color.Transparent;
                cell._frame.HorizontalOptions = LayoutOptions.FillAndExpand;
            });

        // Create datacell with indentation
        public readonly BindableProperty UIntProperty = BindableProperty.Create(nameof(Indentation), typeof(uint), typeof(DataItemCell),
            propertyChanged: (boundObject, _, value) =>
            {
                if (!(boundObject is DataItemCell cell))
                    return;
                var indent = value as uint?;
                if (!indent.HasValue) return;
                cell._frame.Margin = new Thickness(10 * indent.Value, 0, 0, 0);
                // Set text color for session data
                cell._label.TextColor = Color.White;
                cell._frame.BackgroundColor = Color.Transparent;
                cell._frame.CornerRadius = 0;
                cell._frame.BorderColor = Color.Transparent;
            });

        protected readonly Label _label = new Label();
        private readonly Frame _frame;

        protected DataItemCell()
        {
            this.SetBinding(TextProperty, "Text");
            this.SetBinding(UIntProperty, "Indentation");
            var infoAction = new MenuItem { Text = "Info" };
            infoAction.Clicked += InfoClicked;
            ContextActions.Add(infoAction);

            var layout = new StackLayout
            {
                Orientation = StackOrientation.Horizontal

            };
            _frame = new Frame
            {
                BorderColor = Color.FromHex("#1D2637"), Content = layout, HorizontalOptions = LayoutOptions.Start,
                Padding = 5, Margin = 0
            };

            // TODO: Add checkbox
            layout.Children.Add(_label);
            View = _frame;
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
        public uint Indentation
        {
            get => (uint)GetValue(UIntProperty);
            set => SetValue(UIntProperty, value);
        }

        public string Detail { get; set; }

        private void InfoClicked(object sender, EventArgs e)
        {
            Debug.WriteLine($"DataItem info clicked");
        }

        protected override void OnTapped()
        {
            var dataItem = BindingContext as DataItem;
            try
            {
                dataItem?.OnTapped();
            }
            catch (Exception ex)
            {
                XamarinHelpers.GetCurrentPage().DisplayAlert("Unknown error", ex.Message, "OK");
            }
        }
    }

    // Class for cell at top level, data provider
    internal class DataProviderCell : DataStructureCell
    {
        private readonly IFileBrowser _browser;

        public DataProviderCell()
        {
            _browser = DependencyService.Get<IFileBrowser>();
            Detail = "DataProvider";

            var saveViewAction = new MenuItem { Text = "Save View", IsDestructive = true };
            saveViewAction.Clicked += SaveViewClicked;
            ContextActions.Add(saveViewAction);

            var loadViewAction = new MenuItem { Text = "Load View", IsDestructive = true };
            loadViewAction.Clicked += LoadViewClicked;
            ContextActions.Add(loadViewAction);

            var exportAnnotationAction = new MenuItem { Text = "Export Annotations", IsDestructive = true };
            exportAnnotationAction.Clicked += exportAnnotationClicked;
            ContextActions.Add(exportAnnotationAction);

            /*
            TBD - Save and load annotation info, do not load to correct provider
            var exportAnnotationInfoAction = new MenuItem { Text = "Save Annotation Info", IsDestructive = true };
            exportAnnotationInfoAction.Clicked += saveAnnotationInfoClicked;
            ContextActions.Add(exportAnnotationInfoAction);

            var loadAnnotationInfoAction = new MenuItem { Text = "Load Annotation Info", IsDestructive = true };
            loadAnnotationInfoAction.Clicked += LoadAnnotationInfoClicked;
            ContextActions.Add(loadAnnotationInfoAction);
            */

            var closeAction = new MenuItem { Text = "Close", IsDestructive = true };
            closeAction.Clicked += CloseClicked;
            ContextActions.Add(closeAction);
        }

        private void CloseClicked(object sender, EventArgs e)
        {
            var dataProviderItem = BindingContext as DataProviderItem;
            dataProviderItem?.DataProvider.Unregister();
        }

        private async void LoadViewClicked(object sender, EventArgs e)
        {
            var dataProviderItem = BindingContext as DataProviderItem;
            IReadSeekStreamFactory file = await _browser.BrowseForLoad((".aav", "AutoActive View"));

            if (file == null)
            {
                return;
            }

            JObject root;
            var stream = await file.GetReadStream();
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                var serializer = new JsonSerializer();
                root = serializer.Deserialize(reader) as JObject;
            }
            var page = XamarinHelpers.GetCurrentPage();

            if (root == null)
            {
                await XamarinHelpers.ShowOkMessage("Error reading file", "Could not parse JSON file.", page);
                return;
            }

            var version = root["serializer_version"].Value<string>();
            if (version != SerializableViewHelper.Version)
            {
                var message =
                    $"Stored view version ({version}) is not the same as current version ({SerializableViewHelper.Version})";
                // await XamarinHelpers.ShowOkMessage("Deserialization warning", message, page);
                //TODO(sigurdal): this should probably be shown to the user in some way
                Debug.WriteLine(message);
            }

            if (page is ISerializableView view)
            {
                await view.DeserializeView(root, dataProviderItem?.DataProvider);
            }
            else
            {
                await XamarinHelpers.ShowOkMessage("Error", "Current page does not support loading views yet.", page);
            }
        }

        private async void SaveViewClicked(object sender, EventArgs e)
        {
            var dataProviderItem = BindingContext as DataProviderItem;
            IReadWriteSeekStreamFactory file = await _browser.BrowseForSave((".aav", "AutoActive View"));

            if (file == null)
            {
                return;
            }

            var page = XamarinHelpers.GetCurrentPage();
            if (!(page is ISerializableView view))
            {
                await XamarinHelpers.ShowOkMessage("Error", "Current page does not support saving views yet.", page);
                return;
            }

            var root = view.SerializeView();
            root["serializer_version"] = SerializableViewHelper.Version;

            var stream = await file.GetReadWriteStream();
            using (var streamWriter = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(streamWriter))
            {
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(writer, root);
            }
        }

        private async void exportAnnotationClicked(object sender, EventArgs e)
        {
            var dataProviderItem = BindingContext as DataProviderItem;
            IReadWriteSeekStreamFactory file = await _browser.BrowseForSave((".json", "Export Annotations"));

            if (file == null)
            {
                return;
            }

            var provider = AnnotationProvider.GetAnnotationProvider(false);
            var annotationSet = provider.AnnotationSet;

            var stream = await file.GetReadWriteStream();
            using (var streamWriter = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(streamWriter))
            {
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(writer, annotationSet);
            }
        }

        /*
        private class AnnotationInfoFile
        {
            public AnnotationInfoFile()
            {
                AutoActiveType = "Annotation";
                Version = "1.0.0";

                AnnotationInfo = new Dictionary<int, AnnotationInfo>();
            }
            public string AutoActiveType { get; set; }

            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("annotation_info")]
            public Dictionary<int, AnnotationInfo> AnnotationInfo { get; set; }
        }

        private async void saveAnnotationInfoClicked(object sender, EventArgs e)
        {
            var dataProviderItem = BindingContext as DataProviderItem;
            IReadWriteSeekStreamFactory file = await _browser.BrowseForSave((".aai", "AutoActive Annotation Info"));

            if (file == null)
            {
                return;
            }

            var provider = AnnotationProvider.GetAnnotationProvider(false);
            var annotationSet = provider.AnnotationSet;
            var annotationInfoJsonFile = new AnnotationInfoFile();
            annotationInfoJsonFile.AnnotationInfo = annotationSet.AnnotationInfo;

            var stream = await file.GetReadWriteStream();
            using (var streamWriter = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(streamWriter))
            {
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(writer, annotationInfoJsonFile);
            }
        }

        private async void LoadAnnotationInfoClicked(object sender, EventArgs e)
        {
            var dataProviderItem = BindingContext as DataProviderItem;
            IReadSeekStreamFactory file = await _browser.BrowseForLoad((".aai", "AutoActive Annotation Info"));

            if (file == null)
            {
                return;
            }

            var provider = AnnotationProvider.GetAnnotationProvider(false);
            var stream = await file.GetReadStream();
            provider.ParseFile(stream);
        }
        */
    }

    // Class for cell with no data
    internal class DataStructureCell : DataItemCell
    {
        public DataStructureCell()
        {
            Detail = "DataStructure";
        }
    }

    // Class for cell with data
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

            // Toggle text colour if used as timeline
            if (_label.TextColor == Color.White)
            {
                _label.TextColor = Color.FromHex("#1F77B4");
                _label.FontAttributes = FontAttributes.Bold;
            }
            else
            {
                _label.TextColor = Color.White;
                _label.FontAttributes = FontAttributes.None;
            }
        }

    }

    /* ---- Classes for building the tree view ---- */
    internal class DataRegistryTree : ObservableCollection<DataItem>
    {
        private readonly Dictionary<IDataProvider, DataProviderItem> _providerItems = new Dictionary<IDataProvider, DataProviderItem>();

        private readonly PlayerTreeView _view;
        public Dictionary<object, uint> TreeLevel => _view.TreeLevel;

        internal DataRegistryTree(PlayerTreeView view)
        {
            _view = view;
            // The top level always show all available dataproviders
            // Listen for changes
            DataRegistry.ProviderAdded += ProviderAdded;
            DataRegistry.ProviderRemoved += ProviderRemoved;
            // Add current items
            foreach (var dataProvider in DataRegistry.Providers)
                ProviderAdded(dataProvider);
        }

        private void ProviderAdded(IDataProvider dataProvider)
        {
            if (_providerItems.ContainsKey(dataProvider)) return;
            var item = new DataProviderItem(dataProvider, this);
            Add(item);
            _providerItems.Add(dataProvider, item);
        }

        private void ProviderRemoved(IDataProvider dataProvider)
        {
            if (!_providerItems.TryGetValue(dataProvider, out var item)) return;

            Remove(item);
            item.HideChildItems();
            item.Dispose();
            _providerItems.Remove(dataProvider);
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
        public uint Indentation { get; set; }

        public abstract void OnTapped();


    }

    internal class DataProviderItem : DataStructureItem, IDisposable
    {
        internal DataProviderItem(IDataProvider dataProvider, DataRegistryTree tree) : base(dataProvider, tree)
        {
            DataProvider = dataProvider;
            IsShown = true;
        }
        public override void Dispose()
        {
            base.Dispose();
            DataProvider = null;
        }

        public IDataProvider DataProvider { get; private set; }
    }

    internal class DataStructureItem : DataItem, IDisposable
    {
        internal DataStructureItem(IDataStructure dataStructure, DataRegistryTree tree) : base(tree)
        {
            DataStructure = dataStructure;

            dataStructure.ChildAdded += ChildAdded;
            dataStructure.ChildRemoved += ChildRemoved;
            dataStructure.DataPointAdded += DataPointAdded;
            dataStructure.DataPointRemoved += DataPointRemoved;

            foreach (var child in dataStructure.Children) ChildAdded(dataStructure, child);
            foreach (var point in dataStructure.DataPoints) DataPointAdded(dataStructure, point);
        }

        public virtual void Dispose()
        {
            DataStructure.ChildAdded -= ChildAdded;
            DataStructure.ChildRemoved -= ChildRemoved;
            DataStructure.DataPointAdded -= DataPointAdded;
            DataStructure.DataPointRemoved -= DataPointRemoved;

            foreach (var child in DataStructure.Children) ChildRemoved(DataStructure, child);
            foreach (var point in DataStructure.DataPoints) DataPointRemoved(DataStructure, point);

            DataStructure = null;
        }

        public IDataStructure DataStructure { get; private set; }

        public override string Text => DataStructure.Name;

        public bool IsExpanded { get; private set; } = false;
        public bool IsShown { get; protected set; } = false;

        public override void OnTapped()
        {
            Debug.WriteLine("DataStructureItem tapped");
            ToggleExpanded();
        }

        // --- Keep track of the children and datapoints ---
        private readonly Dictionary<IDataStructure, DataStructureItem> _structureItems = new Dictionary<IDataStructure, DataStructureItem>();
        private readonly Dictionary<IDataPoint, DataPointItem> _pointItems = new Dictionary<IDataPoint, DataPointItem>();
        public List<DataItem> ChildItems { get; private set; } = new List<DataItem>();

        // FIXME: Update the following four methods to handle if the DataStructure IsExpanded
        private void ChildAdded(IDataStructure sender, IDataStructure dataStructure)
        {
            if (_structureItems.ContainsKey(dataStructure)) return;
            var item = new DataStructureItem(dataStructure, Tree);
            ChildItems.Add(item);
            _structureItems.Add(dataStructure, item);
        }

        private void ChildRemoved(IDataStructure sender, IDataStructure dataStructure)
        {
            if (!_structureItems.TryGetValue(dataStructure, out var item)) return;

            _structureItems.Remove(dataStructure);
            item.Dispose();
            ChildItems.Remove(item);
        }

        // Build three structure data points
        private void DataPointAdded(IDataStructure sender, IDataPoint datapoint)
        {
            if (_pointItems.ContainsKey(datapoint)) return;

            var item = new DataPointItem(datapoint, Tree);
            ChildItems.Add(item);
            _pointItems.Add(datapoint, item);
        }

        private void DataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            if (!_pointItems.TryGetValue(datapoint, out var item)) return;

            _pointItems.Remove(datapoint);
            item.Dispose();
            ChildItems.Remove(item);
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
            if (!IsExpanded) return;

            foreach (var child in ChildItems)
            {
                Tree.Remove(child);
                if (!(child is DataStructureItem structureItem)) continue;

                structureItem.IsShown = true;
                structureItem.HideChildItems();
            }
        }

        private void ShowChildItems(ref int index)
        {
            if (!IsExpanded) return;

            foreach (var child in ChildItems)
            {
                index++;
                Tree.Insert(index, child);
                if (!(child is DataStructureItem structureItem)) continue;

                structureItem.IsShown = true;
                structureItem.ShowChildItems(ref index);
            }
        }
    }

    internal class DataPointItem : DataItem, IDisposable
    {
        internal DataPointItem(IDataPoint datapoint, DataRegistryTree tree) : base(tree)
        {
            DataPoint = datapoint;
        }

        public void Dispose()
        {
            DataPoint = null;
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