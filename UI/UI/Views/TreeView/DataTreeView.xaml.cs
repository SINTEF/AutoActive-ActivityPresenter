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

//Data tree view is a tree view used for selecting data on all pages except of save page
//--------------------------------------------------------------------------------------
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

        private async void TreeOnChildElementChanged(object sender, NotifyCollectionChangedEventArgs e)
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
                    {
                        await XamarinHelpers.ShowOkMessage("Error", $"Only adding of data has been implemented");
                        return;
                    }
                    RemoveStructure(dataStructure);
                }

                return;
            }

            if (e.Action != NotifyCollectionChangedAction.Add)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"Only adding of data has been implemented");
                return;
            }

            foreach (var item in e.NewItems)
            {
                if ((item is TemporaryVideoArchive) || (item is TemporaryDataTable))
                {
                    await XamarinHelpers.ShowOkMessage("Error", $"The first folder type in a tree must be a Folder");
                    return;
                }
                if (!(item is IDataStructure dataStructure))
                {
                    await XamarinHelpers.ShowOkMessage("Error", $"Only data structure has been implemented");
                    return;
                }
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
            var movableObject = TreeLayout.Children.First(el => (el as MovableObject)?.Element.DataStructure == dataprovider);
            TreeLayout.Children.Remove(movableObject);
        }

        private void AddStructure(IDataStructure dataStructure)
        {
            var folderView = new FolderView
            {
                Element = new VisualizedStructure(dataStructure)
                {
                    IsExpanded = false
                },
                ParentTree = this
            };
            TreeLayout.Children.Add(folderView);
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