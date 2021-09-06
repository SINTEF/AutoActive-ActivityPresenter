using Newtonsoft.Json.Linq;
using Parquet.Data;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Plugins.Import;
using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    class DataFolderViewButton : MovableObject, TreeButton
    {

        public DataFolderViewButton():base()
        {

            Element = new VisualizedStructure(new TemporaryDataTable("Add Data Folder"));
            setButtonSettings();
        }


        public override void ObjectDroppedOn(IDraggable item)
        {
            throw new NotImplementedException();
            //ParentTree?.ObjectDroppedOn(this, item);
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            throw new NotImplementedException();
        }

        public MovableObject CreateNewView()
        {
            return new DataFolderView() { Element = new VisualizedStructure(new TemporaryDataTable("New Data Folder")) };
        }
    }

    class DataFolderView: MovableObject
    {

        public DataFolderView(): base()
        {

        }

        public override void ObjectDroppedOn(IDraggable item)
        {

            if (item is DataFolderView dataFolderItem)
            {
                if (dataFolderItem == this)
                {
                    return;
                }
            }

            if (!(item is DataPointView itemView))
            {
                throw new Exception("A Data Folder can only contain 1d signals");
            }

            if (!(itemView.Element.DataPoint is Databus.Implementations.TabularStructure.TableColumn))
            {
                throw new Exception("A Data Folder can only contain 1d signals");
            }

            if (this._element.Children.Count > 0)
            {
                if (!(this._element.Children[0].DataPoint.Time == itemView.Element.DataPoint.Time))
                {
                    throw new Exception("Data in the same Data Folder must reference the same timeline");
                }
            }

            ParentTree?.ObjectDroppedOn(this, item);
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            return new DataPointView { ParentTree = ParentTree, Element = element };
        }

    }

    internal class TemporaryDataTable : IDataStructure, ISaveable
    {

        public TemporaryDataTable(string name)
        {
            Name = name;
        }

        public bool IsSaved { get; }
        public string Name { get; set; }

        public ObservableCollection<IDataStructure> Children { get; } = new ObservableCollection<IDataStructure>();
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

            if (_dataPoints.First().Time.Equals(dataPoint.Time))
            {
                _dataPoints.Add(dataPoint);
                DataPointAdded?.Invoke(this, dataPoint);
                return;
            }
            else
            {
                throw new Exception("Columns in table must reference the same timeline");
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

        public void AddChild(IDataStructure dataStrcture)
        {
            throw new Exception("You can only add DataPoints to {}");
        }

        public void RemoveChild(IDataStructure dataStructure)
        {
            throw new NotImplementedException();
        }

        public Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            (Dictionary<string, Array> data, List<string> units) = TransformData();
            var fileId = "/Data" + "/" + Name + "." + Guid.NewGuid();

            // Make table object
            var metaTable = new JObject
            {
                ["type"] = "no.sintef.table",
                ["attachments"] = new JArray(new object[] { fileId }),
                ["units"] = new JArray(units.ToArray()),
                ["is_world_clock"] = _dataPoints.First().Time.IsSynchronizedToWorldClock,
                ["version"] = 1
            };

            var userTable = new JObject { };

            // Place objects into root
            root["meta"] = metaTable;
            root["user"] = userTable;

            return WriteTable(fileId, writer, data);
        }

        private (Dictionary<string, Array>, List<string>) TransformData()
        {
            Dictionary<string, Array> data = new Dictionary<string, Array>();
            List<string> units = new List<string>();

            foreach (IDataPoint dataPoint in _dataPoints)
            {
                (long[] time, double[] dataArray) = ReadData(dataPoint);
                if (data.Count() == 0)
                {
                    data.Add("time", time);
                    units.Add("us");
                }

                if (dataPoint.Name.ToLower() != "time")
                {
                    data.Add(dataPoint.Name, dataArray);
                    units.Add("-");
                }
            }

            return (data, units);
        }

        private (long[], double[]) ReadData(IDataPoint inputData)
        {
            Task dataViewTask = inputData.CreateViewer();
            Task timeViewTask = inputData.Time.CreateViewer();
            Task[] tasks = new Task[] { dataViewTask, timeViewTask };
            var dataView = Task.Run(() => inputData.CreateViewer()).Result;
            var timeView = Task.Run(() => inputData.Time.CreateViewer()).Result;
            Task.WaitAll(tasks);
            ITimeSeriesViewer viewer = (ITimeSeriesViewer)dataView;
            viewer.SetTimeRange(timeView.Start, timeView.End);
            var genericConstructor = typeof(DataReader<>).MakeGenericType(inputData.DataType)
            .GetConstructor(new[] { typeof(ITimeSeriesViewer) });
            var dataReader = (IDataReader)genericConstructor.Invoke(new object[] { viewer });
            (long[] timeArray, double[] dataArray, bool[] isNaNArray) = dataReader.DataAsArrays();
            return (timeArray, dataArray);
        }

        private DataColumnAndSchema MakeDataColumnAndSchema(Dictionary<string, Array> data)
        {
            var fields = new List<Field>();
            var datacols = new List<DataColumn>();

            foreach (KeyValuePair<string, Array> entry in data)
            {
                DataColumn column;
                switch (entry.Value)
                {
                    case bool[] _:
                        column = new DataColumn(new DataField<bool>(entry.Key), entry.Value);
                        break;
                    case byte[] _:
                        column = new DataColumn(new DataField<byte>(entry.Key), entry.Value);
                        break;
                    case int[] _:
                        column = new DataColumn(new DataField<int>(entry.Key), entry.Value);
                        break;
                    case long[] _:
                        column = new DataColumn(new DataField<long>(entry.Key), entry.Value);
                        break;
                    case float[] _:
                        column = new DataColumn(new DataField<float>(entry.Key), entry.Value);
                        break;
                    case double[] _:
                        column = new DataColumn(new DataField<double>(entry.Key), entry.Value);
                        break;
                    case string[] _:
                        column = new DataColumn(new DataField<string>(entry.Key), entry.Value);
                        break;
                    default:
                        continue;
                }
                fields.Add(column.Field);
                datacols.Add(column);
            }

            return new DataColumnAndSchema(datacols, new Schema(fields));
        }

        private Task<bool> WriteTable(string fileId, ISessionWriter writer, Dictionary<string, Array> data)
        {
            // This stream will be disposed by the sessionWriter
            var ms = new MemoryStream();

            var dataColAndSchema = MakeDataColumnAndSchema(data);

            using (var tableWriter = new Parquet.ParquetWriter(dataColAndSchema.Schema, ms))
            {

                using (var rowGroup = tableWriter.CreateRowGroup())  // Using construction assure correct storage of final rowGroup details in parquet file
                {
                    foreach (var dataCol in dataColAndSchema.DataColumns)
                    {
                        rowGroup.WriteColumn(dataCol);
                    }
                }
            }

            ms.Position = 0;
            writer.StoreFileId(ms, fileId);

            return Task.FromResult(true);
        }
    }


}
