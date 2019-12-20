using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class DataTreeView : ContentView, IDropCollector
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
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                var el = Tree.Children[e.OldStartingIndex];
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    if (!(item is IDataStructure dataStructure))
                        throw new NotImplementedException("Only data structure has been implemented");
                    RemoveStructure(dataStructure);
                }

                return;
            }

            if (e.Action != NotifyCollectionChangedAction.Add)
            {
                throw new NotImplementedException("Only adding of data has been implemented");
            }

            foreach (var item in e.NewItems)
            {
                if (!(item is IDataStructure dataStructure))
                    throw new NotImplementedException("Only data structure has been implemented");

                AddStructure(dataStructure);
            }
        }

        public DataTreeView()
        {
            InitializeComponent();
            Tree = new DataTree();
        }

        private void RemoveStructure(IDataStructure dataprovider)
        {
            var branchView = TreeLayout.Children.First(el => (el as BranchView)?.Element.DataStructure == dataprovider);
            TreeLayout.Children.Remove(branchView);
        }

        private void AddStructure(IDataStructure dataStructure)
        {
            var branchView = new BranchView
            {
                Element = new VisualizedStructure(dataStructure)
                {
                    IsExpanded = false
                },
                ParentTree = this
            };
            TreeLayout.Children.Add(branchView);
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
            ObjectDroppedOn(this, item);
        }

        public event EventHandler<(DataTreeView parent, IDropCollector container, IDraggable item)> ItemDroppedOn;

        internal void ObjectDroppedOn(IDropCollector collector, IDraggable item)
        {
            var parent = XamarinHelpers.GetTypedElementFromParents<DataTreeView>(item as Element);
            ItemDroppedOn?.Invoke(this, (parent, collector, item));
        }
    }

    public enum TreeAction
    {
        UseInTimeline
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