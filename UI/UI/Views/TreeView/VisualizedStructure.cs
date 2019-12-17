using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using SINTEF.AutoActive.Databus.Interfaces;
using Xamarin.Forms.Internals;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    public class VisualizedStructure
    {
        private bool _isExpanded;

        public VisualizedStructure(IDataStructure structure)
        {
            DataStructure = structure;
            structure.Children.CollectionChanged += DataStructureOnCollectionChanged;
            structure.DataPoints.CollectionChanged += DataPointsOnCollectionChanged;
        }

        private void DataPointsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _children = null;
        }

        private void DataStructureOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_children == null) return;

            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var child in e.OldItems)
                {
                    _children.RemoveAt(_children.IndexOf(el => el.DataStructure == child));
                }
            }

            _children = null;
        }

        private void StructureOnChildRemoved(IDataStructure sender, IDataStructure datastructure)
        {
            if (_children == null) return;
            var child = _children.First(el => el.DataStructure == datastructure);
            _children.Remove(child);
        }

        private void StructureOnDataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            if (_children == null) return;
            var child = _children.First(el => el.DataPoint == datapoint);
            _children.Remove(child);
        }

        private void StructureOnChildAdded(IDataStructure sender, IDataStructure datastructure)
        {
            // TODO: implement ensure children instead
            _children = null;
        }

        private void StructureOnDataPointAdded(IDataStructure sender, IDataPoint datapoint)
        {
            // TODO implement ensure children instead
            _children = null;
        }

        public VisualizedStructure(IDataPoint dataPoint)
        {
            DataPoint = dataPoint;
        }

        public IDataStructure DataStructure { get; }
        public IDataPoint DataPoint { get; }


        private ObservableCollection<VisualizedStructure> _children;

        public ObservableCollection<VisualizedStructure> Children
        {
            get
            {
                if (_children != null) return _children;

                _children = new ObservableCollection<VisualizedStructure>();

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
}