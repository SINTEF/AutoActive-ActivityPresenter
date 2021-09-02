using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    class FolderViewButton : MovableObject, TreeButton
    {
        public FolderViewButton() : base()
        {
            Element = new VisualizedStructure(new TemporaryFolder("Add Folder"));
            setButtonSettings();
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            throw new NotImplementedException();
        }

        public override void ObjectDroppedOn(IDraggable item)
        {
            throw new NotImplementedException();
        }

        public MovableObject CreateNewView()
        {
            return new FolderView() { Element = new VisualizedStructure(new TemporaryFolder("New Folder")) };
        }
    }

    class FolderView : MovableObject
    {
        public FolderView(): base()
        {

        }
        public override void ObjectDroppedOn(IDraggable item)
        {
            ParentTree?.ObjectDroppedOn(this, item);
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            if (element.DataStructure != null)
            {
                if (element.DataStructure.GetType() == typeof(TemporaryDataTable))
                {
                    return new DataFolderView { ParentTree = ParentTree, Element = element};
                }
                else if (element.DataStructure.GetType() == typeof(TemporaryVideoArchive))
                {
                    return new VideoFolderView { ParentTree = ParentTree, Element = element};
                }
                else if (element.Children.Count != 0)
                {
                    var childDataPoint = element.Children[0].DataPoint;
                    if (childDataPoint is Databus.Implementations.TabularStructure.TableColumn)
                    {
                        return new DataFolderView { ParentTree = ParentTree, Element = element };
                    }
                    else
                    {
                        return new VideoFolderView { ParentTree = ParentTree, Element = element };
                    }
                }
                else
                {
                    return new FolderView { ParentTree = ParentTree, Element = element};
                }
            }
            else
            {
                return new FolderView { ParentTree = ParentTree, Element = element };
            }

        }
    }

    internal class TemporaryFolder : IDataStructure, ISaveable
    {
        public TemporaryFolder(string name)
        {
            Name = name;
            Children.CollectionChanged += ChildrenOnCollectionChanged;
        }

        private void ChildrenOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var it in e.NewItems)
                {
                    if (it is IDataStructure dataStructure)
                        ChildAdded?.Invoke(this, dataStructure);
                }

            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var it in e.OldItems)
                {
                    if (it is IDataStructure dataStructure)
                        ChildRemoved?.Invoke(this, dataStructure);
                }
            }
        }

        public string Name { get; set; }
        public void Close()
        {
        }

        public ObservableCollection<IDataStructure> Children { get;} = new ObservableCollection<IDataStructure>();

        public event DataStructureAddedHandler ChildAdded;
        public event DataStructureRemovedHandler ChildRemoved;

        public void AddChild(IDataStructure item)
        {
            Children.Add(item);
        }

        public void RemoveChild(IDataStructure item)
        {
            Children.Remove(item);
        }

        private readonly ObservableCollection<IDataPoint> _dataPoints = new ObservableCollection<IDataPoint>();
        public ObservableCollection<IDataPoint> DataPoints => _dataPoints;
        public event DataPointAddedHandler DataPointAdded;
        public event DataPointRemovedHandler DataPointRemoved;

        public void AddDataPoint(IDataPoint dataPoint)
        {
            throw new Exception("");
        }

        public void RemoveDataPoint(IDataPoint dataPoint)
        {
            throw new Exception("");
        }

        public bool IsSaved { get; }
        public Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
                root["meta"] = new JObject
                {
                    ["type"] = ArchiveFolder.PluginType
                };
                root["user"] = new JObject();

                return Task.FromResult(true);
        }
    }
}
