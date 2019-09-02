using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using System;
using System.IO;

namespace SINTEF.AutoActive.Databus.Implementations
{
    public abstract class BaseDataProvider : BaseDataStructure, IDataProvider
    {
        protected override void OnChildAdded(IDataStructure sender, IDataStructure datastructure)
        {
            // Emit the original event
            base.OnChildAdded(sender, datastructure);
            // Emit recursive tree events
            OnDataStructureAddedToTree(sender, datastructure);
        }

        protected override void OnChildRemoved(IDataStructure sender, IDataStructure datastructure)
        {
            // Emit the original event
            base.OnChildRemoved(sender, datastructure);
            // Emit recursive tree events
            OnDataStructureRemovedFromTree(sender, datastructure);
        }

        protected override void OnDataPointAdded(IDataStructure sender, IDataPoint datapoint)
        {
            // Emit the original event
            base.OnDataPointAdded(sender, datapoint);
            // Emit recursive tree events
            OnDataPointAddedToTree(sender, datapoint);
        }

        protected override void OnDataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            // Emit the original event
            base.OnDataPointRemoved(sender, datapoint);
            // Emit recursive tree events
            OnDataPointRemovedFromTree(sender, datapoint);
        }

        // ----- Global tree events -----
        public event DataStructureAddedHandler DataStructureAddedToTree;
        public event DataStructureRemovedHandler DataStructureRemovedFromTree;
        public event DataPointAddedHandler DataPointAddedToTree;
        public event DataPointRemovedHandler DataPointRemovedFromTree;
        private Stream _stream;

        private void OnDataStructureAddedToTree(IDataStructure sender, IDataStructure datastructure)
        {
            // Emit tree event
            DataStructureAddedToTree?.Invoke(sender, datastructure);

            // Subscribe to changes down the tree
            datastructure.ChildAdded += OnDataStructureAddedToTree;
            datastructure.ChildRemoved += OnDataStructureRemovedFromTree;
            datastructure.DataPointAdded += OnDataPointAddedToTree;
            datastructure.DataPointRemoved += OnDataPointRemovedFromTree;

            // Emit events about the already existing tree
            foreach (var child in datastructure.Children)
            {
                OnDataStructureAddedToTree(datastructure, child);
            }
            foreach (var point in datastructure.DataPoints)
            {
                OnDataPointAddedToTree(datastructure, point);
            }
        }

        private void OnDataStructureRemovedFromTree(IDataStructure sender, IDataStructure datastructure)
        {
            // Unsubscribe from the changes down the tree
            datastructure.ChildAdded -= OnDataStructureAddedToTree;
            datastructure.ChildRemoved -= OnDataStructureRemovedFromTree;
            datastructure.DataPointAdded -= OnDataPointAddedToTree;
            datastructure.DataPointRemoved -= OnDataPointRemovedFromTree;

            // Emit events bout the existing tree
            foreach (var child in datastructure.Children)
            {
                OnDataStructureRemovedFromTree(datastructure, child);
            }
            foreach (var point in datastructure.DataPoints)
            {
                OnDataPointRemovedFromTree(datastructure, point);
            }

            // Emit tree event
            DataStructureRemovedFromTree?.Invoke(sender, datastructure);
        }

        private void OnDataPointAddedToTree(IDataStructure sender, IDataPoint datapoint)
        {
            DataPointAddedToTree?.Invoke(sender, datapoint);
        }

        private void OnDataPointRemovedFromTree(IDataStructure sender, IDataPoint datapoint)
        {
            DataPointRemovedFromTree?.Invoke(sender, datapoint);
        }

        protected abstract void DoParseFile(Stream stream);

        public void ParseFile(Stream stream)
        {
            _stream = stream;
            DoParseFile(stream);
        }
        public void Close()
        {
            _stream?.Close();
        }
    }
}
