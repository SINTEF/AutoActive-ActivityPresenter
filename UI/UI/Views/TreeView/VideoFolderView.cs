using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    class VideoFolderViewButton : MovableObject, TreeButton
    {
        public VideoFolderViewButton() : base()
        {
            Element = new VisualizedStructure(new TemporaryVideoArchive("Add Video Folder"));
            setButtonSettings();
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            throw new NotImplementedException();
        }

        public MovableObject CreateNewView()
        {
            return new VideoFolderView() { Element = new VisualizedStructure(new TemporaryVideoArchive("New Video Folder")) };
        }

        public override void ObjectDroppedOn(IDraggable item)
        {
            throw new NotImplementedException();
        }
    }

    class VideoFolderView : MovableObject
    {

        public VideoFolderView(): base()
        {

        }

        public override void ObjectDroppedOn(IDraggable item)
        {

            if (item is VideoFolderView videoFolderItem)
            {
                if (videoFolderItem == this)
                {
                    return;
                }
            }

            if (!(item is DataPointView itemView))
            {
                throw new Exception("A Video Folder can only contain a video");
            }

            if (!(itemView.Element.DataPoint is ArchiveVideoVideo))
            {
                throw new Exception("A Video Folder can only contain a video");
            }

            if (this._element.Children.Count >= 1)
            {
                throw new Exception("A Video Folder can only contain a single video");
            }

            ParentTree?.ObjectDroppedOn(this, item);
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            return new DataPointView { ParentTree = ParentTree, Element = element };
        }

    }

    internal class TemporaryVideoArchive : ISaveable, IDataStructure
    {
        public TemporaryVideoArchive(string name)
        {
            Name = name;
            Meta = new JObject();
            User = new JObject();
        }

        public bool IsSaved { get; }
        public string Name { get; set; }

        protected JObject Meta { get; }
        protected JObject User { get; }

        public ObservableCollection<IDataStructure> Children => new ObservableCollection<IDataStructure>();
        public event DataStructureAddedHandler ChildAdded;
        public event DataStructureRemovedHandler ChildRemoved;

        private readonly ObservableCollection<IDataPoint> _dataPoints = new ObservableCollection<IDataPoint>();
        public ObservableCollection<IDataPoint> DataPoints => _dataPoints;
        public event DataPointAddedHandler DataPointAdded;
        public event DataPointRemovedHandler DataPointRemoved;
        public void AddDataPoint(IDataPoint dataPoint)
        {
            if (_dataPoints.Count == 0)
            {
                _dataPoints.Add(dataPoint);
                DataPointAdded?.Invoke(this, dataPoint);
                return;
            }
            else
            {
                throw new Exception("A Video folder can only contain one video");
            }
        }

        public void RemoveDataPoint(IDataPoint dataPoint)
        {
            _dataPoints.Remove(dataPoint);
            DataPointRemoved?.Invoke(this, dataPoint);
        }
        public void Close()
        {
            throw new NotImplementedException();
        }

        public void AddChild(IDataStructure dataStructure)
        {
            throw new NotImplementedException();
        }

        public void RemoveChild(IDataStructure dataStructure)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {

            Stream stream;
            string fileId;
            ArchiveVideoVideo video = (ArchiveVideoVideo)_dataPoints[0];
            (IReadSeekStreamFactory readerFactory, string _) = video.GetStreamFactory();

            if (readerFactory == null)
            {
                Archive.Archive archive = video.Archive;
                ZipEntry zipEntry = archive.FindFile(video.URI);
                stream = await archive.OpenFile(zipEntry);
            }
            else
            {
                stream = await readerFactory.GetReadStream();
            }


            fileId = "/videos" + "/" + Name + "." + Guid.NewGuid();
            writer.StoreFileId(stream, fileId);
            Meta["attachments"] = new JArray(new object[] { fileId });
            Meta["type"] = "no.sintef.video";
            root["meta"] = Meta;
            root["user"] = User;

            root["meta"]["start_time"] = video.VideoTime.Offset;
            root["meta"]["video_length"] = video.VideoTime.VideoLength;

            return true;
        }
    }



}
