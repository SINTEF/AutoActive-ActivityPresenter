using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlayerTreeView : ContentView, IDropCollector
    {
        public static readonly GridLength DefaultWidth = 200;

        private DataTree _tree;
        internal DataTree Tree
        {
            get => _tree;
            set
            {
                _tree = value;
                SetTree(_tree);
            }
        }

        private DataTree _prevTree;
        private void SetTree(DataTree tree)
        {
            TreeLayout.Children.Clear();
            if (_prevTree != null) _prevTree.ChildElementChanged -= TreeOnChildElementChanged;

            _prevTree = tree;
            if (tree == null) return;

            tree.ChildElementChanged += TreeOnChildElementChanged;

            foreach (var element in tree)
            {
                AddStructure(element);
            }
        }

        private void TreeOnChildElementChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public PlayerTreeView()
        {
            InitializeComponent();

            DataRegistry.ProviderAdded += ProviderAdded;
            DataRegistry.ProviderRemoved += ProviderRemoved;
            // Add current items
            foreach (var dataProvider in DataRegistry.Providers)
                ProviderAdded(dataProvider);
        }

        private void ProviderRemoved(IDataProvider dataprovider)
        {
            RemoveProvider(dataprovider);
        }

        private void RemoveProvider(IDataProvider dataprovider)
        {
            TreeLayout.Children.Remove(TreeLayout.Children.First(el => (el as BranchView)?.Element.DataStructure == dataprovider));
        }

        private void ProviderAdded(IDataProvider dataprovider)
        {
            AddStructure(dataprovider);
        }

        private void AddStructure(IDataStructure dataStructure)
        {
            TreeLayout.Children.Add(new BranchView
            {
                Element = new VisualizedStructure(dataStructure)
                {
                    IsExpanded = false
                },
                ParentTree = this
            });
        }

        public event EventHandler<IDataPoint> DataPointTapped;
        public event EventHandler<IDataPoint> UseInTimelineTapped;

        public void DataPointClicked(IDataPoint dataPoint)
        {
            DataPointTapped?.Invoke(this, dataPoint);
        }

        public void DataPointAction(IDataPoint dataPoint, TreeAction action)
        {
            switch (action)
            {
                case TreeAction.UseInTimeline:
                    UseInTimelineTapped?.Invoke(this, dataPoint);
                    break;
            }
        }

        public void ObjectDroppedOn(IDraggable item)
        {
            Debug.WriteLine($"{item} dropped on {this}");
        }
    }

    public enum TreeAction
    {
        UseInTimeline
    }

    public class VisualizedStructure
    {
        private bool _isExpanded;

        public VisualizedStructure(IDataStructure structure)
        {
            DataStructure = structure;

        }

        public VisualizedStructure(IDataPoint dataPoint)
        {
            DataPoint = dataPoint;
        }

        public IDataStructure DataStructure { get; }
        public IDataPoint DataPoint { get; }


        private List<VisualizedStructure> _children;

        public List<VisualizedStructure> Children
        {
            get
            {
                if (_children != null) return _children;

                _children = new List<VisualizedStructure>();

                if (DataStructure == null) return _children;

                foreach (var child in DataStructure.Children)
                {
                    _children.Add(new VisualizedStructure(child));
                }
                foreach (var child in DataStructure.DataPoints)
                {
                    _children.Add(new VisualizedStructure(child));
                }

                return _children;
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnExpandChanged?.Invoke(this, value);
            }
        }

        public string Name
        {
            get => DataStructure != null ? DataStructure.Name : DataPoint.Name;
            set
            {
                if (DataStructure != null)
                {
                    DataStructure.Name = value;
                    return;
                }

                DataPoint.Name = value;
            }
        }

        public event EventHandler<bool> OnExpandChanged;
    }

    public class DataTree : IEnumerable<IDataStructure>
    {
        public readonly ObservableCollection<IDataStructure> Children = new ObservableCollection<IDataStructure>();

        public DataTree()
        {
            Children.CollectionChanged += ChildrenOnCollectionChanged;
        }

        public event EventHandler<NotifyCollectionChangedEventArgs> ChildElementChanged;

        private void ChildrenOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ChildElementChanged?.Invoke(this, e);
        }

        public IEnumerator<IDataStructure> GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}