using Newtonsoft.Json;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Mqtt;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Plugins.Import.Mqtt
{
    class ImportMqtt
    {
    }
    [ImportPlugin(".mqtt")]
    public class MqttImportPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory)
        {
            var importer = new MqttImporter(readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    public class MqttImporter : BaseDataProvider
    {
        internal MqttImporter(string name)
        {
            Name = name;
        }

        internal void ParseFile(Stream s)
        {
            Debug.WriteLine("Starting MqttImporter ");


            AddChild(new MqttTable("MqttDummy"));
        }
    }

    internal class MqttValue
    {
        public string name { get; set; }
        public double time { get; set; }
        public double val { get; set; }
    }

    public class MqttTable : BaseDataStructure
    {
        internal MqttTable(string id)
        {
            Name = id;

            // Start MQTT listener thread
            MakeMqttListener();
        }

        ~MqttTable()
        {
            if (mqttListener != null) mqttListener.Abort();
        }

        private Thread mqttListener = null;
        private bool runListener = true;

        private Dictionary<string, MqttColumn> mqttDict = new Dictionary<string, MqttColumn>();

        internal class MqttColumn
        {
            internal string name;
            internal TableIndexDyn timeCol;
            internal DoubleColumnDyn dataCol;
            internal double minTime = 0;
            internal double maxTime = 0;
            public MqttColumn(string name, MqttTable mt)
            {
                this.name = name;
                Debug.WriteLine($"MqttColumn name: {name} ");
                timeCol = new TableIndexDyn(name+"Time");
                dataCol = new DoubleColumnDyn(name, timeCol);
                mt.AddDataPoint(dataCol);
            }

            public void RxValue(MqttValue rx)
            {
                Debug.WriteLine($"RxValue name: {rx.name} time: {rx.time} val: {rx.val}");
                timeCol.AddData(rx.time);
                dataCol.AddData(rx.val);
                if (minTime > rx.time) minTime = rx.time;
                if (maxTime < rx.time) maxTime = rx.time;
                dataCol.UpdateDataRange(minTime, maxTime);
            }
        }

        private void rxMqtt(MqttApplicationMessage msg)
        {
            string payloadStr = Encoding.UTF8.GetString(msg.Payload);
            Debug.WriteLine($"Message received in topic: {msg.Topic} msg: {payloadStr}");
            var rx = JsonConvert.DeserializeObject<MqttValue>(payloadStr);
            Debug.WriteLine($"Json Message received name: {rx.name} time: {rx.time} val: {rx.val}");

            if (!mqttDict.ContainsKey(rx.name))
                mqttDict.Add(rx.name, new MqttColumn(rx.name, this));

            mqttDict[rx.name].RxValue(rx); 
        }

        /* Open an existing gamin file */
        private void MakeMqttListener()
        {

            mqttListener = new Thread(() =>
            {
                var configuration = new MqttConfiguration();
                //var client = await MqttClient.CreateAsync("test.mosquitto.org", configuration);
                var client = Task.Run(async () => await MqttClient.CreateAsync("test.mosquitto.org", configuration)).Result;
                //var sessionState = await client.ConnectAsync(new MqttCredentials(clientId: "no-sintef-autoactive"));
                var sessionState = Task.Run(async () => await client.ConnectAsync(new MqttClientCredentials(clientId: ""), cleanSession: true)).Result;

                Task.Run(async () => await client.SubscribeAsync("sintef/test/0", MqttQualityOfService.AtMostOnce)); //QoS0

                client
                      .MessageStream
                      .Subscribe(msg => rxMqtt(msg));

            // Create the time index
            var timeCol1 = new TableIndexDyn("Time1");
                var dataCol1 = new DoubleColumnDyn("Data1", timeCol1);
                this.AddDataPoint(dataCol1);

                var timeCol2 = new TableIndexDyn("Time2");
                var dataCol2 = new DoubleColumnDyn("Data2", timeCol2);
                this.AddDataPoint(dataCol2);


                int count = 0;
                // timeCol1.AddData((double)count++);
                // dataCol1.AddData((double)1);
                // timeCol2.AddData((double)count/2);
                // dataCol2.AddData((double)1);

                // timeCol1.AddData((double)count++);
                // dataCol1.AddData((double)-1);
                // timeCol2.AddData((double)count/2);
                // dataCol2.AddData((double)-1);
                while (runListener)
                {
                    Debug.WriteLine("Tick " + count++);
                    timeCol1.AddData((double)count);
                    dataCol1.AddData((double)Math.Sin((double)count / 10));
                    dataCol1.UpdateDataRange(0, count);

                    timeCol2.AddData((double)count/2);
                    dataCol2.AddData((double)Math.Sin((double)count / 10));
                    dataCol2.UpdateDataRange(0, count/2);
                    Thread.Sleep(1000);
                }
            });
            mqttListener.IsBackground = true;
            mqttListener.Start();


            // Create the time index
            //var timeCol = new TableIndex("Time", GenerateLoader(tpList, entry => (double)entry.TimeEpoch / 1000));

            // Add other columns
            //this.AddColumn("AltitudeMeters", GenerateLoader(tpList, entry => (float)entry.AltitudeMeters), timeCol);
        }

        public class TableIndexDyn : DoubleColumnDyn
        {
            public TableIndexDyn(string name) : base(name, null) { }

            internal int FindIndex(int current, double value)
            {
                // FIXME: This is far from perfect
                if (current >= 0 && data[current] == value) return current;

                // Do a binary search starting at the previous index
                int first = 0;
                int last = length - 1;

                if (current < 0) current = (first + last) / 2;

                while (first < last)
                {
                    if (value < data[first]) return first;
                    if (value > data[last]) return last;

                    if (value > data[current]) first = current + 1;
                    else last = current - 1;
                    current = (last + first) / 2;

                }
                return current;
            }

            internal override double HasDataFrom => data[0];
            internal override double HasDataTo => data[length - 1];
        }

        public class DoubleColumnDyn : TableColumnDyn
        {
            internal double[] data;
            internal int length = 0;

            public DoubleColumnDyn(string name, TableIndexDyn index) : base(typeof(double), name, index)
            {
                data = new double[100];
                length = 0;
            }

            public void AddData(double val)
            {
                if(length == 0)
                {
                    MinValueHint = val;
                    MaxValueHint = val;
                } else
                {
                    if (val < MinValueHint) MinValueHint = val;
                    if (val > MaxValueHint) MaxValueHint = val;
                }

                if(length >= data.Length )
                    Array.Resize(ref data, data.Length * 2);

                data[length] = val;
                length++;

                if (index != null && index.length != length) throw new Exception($"Column {Name} is not the same length as Index");
            }

            protected override TableColumnDynViewer CreateDoubleDynViewer(TableIndexDyn index, DataViewerContext context)
            {
                return new DoubleColumnDynViewer(index, this, context);
            }
        }

        public class DoubleColumnDynViewer : TableColumnDynViewer
        {
            private DoubleColumnDyn column;

            internal DoubleColumnDynViewer(TableIndexDyn index, DoubleColumnDyn column, DataViewerContext context) : base(index, column, context)
            {
                this.column = column;
            }

            public override SpanPair<double> GetCurrentDoubles()
            {
                //Debug.WriteLine("GetCurrentDoubles " + this.Column.Name + " " + startIndex + " " + length);

                return new SpanPair<double>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
            }
        }

        public abstract class TableColumnDyn : IDataPoint
        {
            protected TableIndexDyn index;
            private readonly List<TableColumnDynViewer> viewers = new List<TableColumnDynViewer>();

            public Type DataType { get; private set; }
            public string Name { get; set; }

            internal virtual double HasDataFrom => index.HasDataFrom;
            internal virtual double HasDataTo => index.HasDataTo;

            internal double? MinValueHint { get;  set; }
            internal double? MaxValueHint { get;  set; }

            internal TableColumnDyn(Type type, string name, TableIndexDyn index)
            {
                DataType = type;
                Name = name;
                this.index = index;
            }

            public void UpdateDataRange(double from, double to)
            {
                foreach (var viewer in viewers)
                {
                    viewer.UpdatedData(from, to);
                }
            }

            public async Task<IDataViewer> CreateViewerIn(DataViewerContext context)
            {
                TableColumnDynViewer newViewer;
                switch (this)
                {
                    // case BoolColumnDyn c:
                    //     newViewer = CreateBoolDynViewer(index, context);
                    //     break;
                    // case ByteColumnDyn c:
                    //     newViewer = CreateByteDynViewer(index, context);
                    //     break;
                    // case IntColumnDyn c:
                    //     newViewer = CreateIntDynViewer(index, context);
                    //     break;
                    // case LongColumnDyn c:
                    //     newViewer = CreateLongDynViewer(index, context);
                    //     break;
                    // case FloatColumnDyn c:
                    //     newViewer = CreateFloatDynViewer(index, context);
                    //     break;
                    case DoubleColumnDyn c:
                        newViewer = CreateDoubleDynViewer(index, context);
                        break;
                    // case StringColumnDyn c:
                    //     newViewer = CreateStringDynViewer(index, context);
                    // break;
                    default:
                        throw new NotSupportedException();
                }
                viewers.Add(newViewer);
                return newViewer;
            }

            //protected abstract (double? min, double? max) GetDataMinMax();

            protected virtual TableColumnDynViewer CreateBoolDynViewer(TableIndexDyn index, DataViewerContext context) { throw new NotSupportedException(); }
            protected virtual TableColumnDynViewer CreateByteDynViewer(TableIndexDyn index, DataViewerContext context) { throw new NotSupportedException(); }
            protected virtual TableColumnDynViewer CreateIntDynViewer(TableIndexDyn index, DataViewerContext context) { throw new NotSupportedException(); }
            protected virtual TableColumnDynViewer CreateLongDynViewer(TableIndexDyn index, DataViewerContext context) { throw new NotSupportedException(); }
            protected virtual TableColumnDynViewer CreateFloatDynViewer(TableIndexDyn index, DataViewerContext context) { throw new NotSupportedException(); }
            protected virtual TableColumnDynViewer CreateDoubleDynViewer(TableIndexDyn index, DataViewerContext context) { throw new NotSupportedException(); }
            protected virtual TableColumnDynViewer CreateStringDynViewer(TableIndexDyn index, DataViewerContext context) { throw new NotSupportedException(); }
        }

        public abstract class TableColumnDynViewer : ITimeSeriesViewer
        {
            protected TableIndexDyn index;
            protected int startIndex = -1;
            protected int endIndex = -1;
            protected double lastFrom = 0;
            protected double lastTo = 0;
            protected int length = -1;

            protected TableColumnDynViewer(TableIndexDyn index, TableColumnDyn column, DataViewerContext context)
            {
                this.index = index;
                Column = column;
                context.RangeUpdated += RangeUpdated;
                RangeUpdated(context.RangeFrom, context.RangeTo);
            }

            public void RangeUpdated(double from, double to)
            {
                var start = index.FindIndex(startIndex, from);
                var end = index.FindIndex(endIndex, to);
                lastTo = to;
                lastFrom = from;
                //Debug.WriteLine("TableColumnDynViewer::RangeUpdated " + this.Column.Name + " " + from + " " + to + " " + startIndex + " " + endIndex);
                if (start != startIndex || end != endIndex)
                {
                    startIndex = start;
                    endIndex = end;
                    length = endIndex - startIndex + 1;
                    //Debug.WriteLine("TableColumnDynViewer::RangeUpdated   Changed " + this.Column.Name);
                    Changed?.Invoke();
                }
            }

            public void UpdatedData(double from, double to)
            {
                // Inform context
                HasDataRangeChanged?.Invoke(from, to);

                // Update visible data range if changed
                var start = index.FindIndex(startIndex, lastFrom);
                var end = index.FindIndex(endIndex, lastTo);
                if (start != startIndex || end != endIndex)
                {
                    startIndex = start;
                    endIndex = end;
                    length = endIndex - startIndex + 1;
                    //Debug.WriteLine("TableColumnDynViewer::UpdatedData   Changed " + this.Column.Name);
                    Changed?.Invoke();
                }
            }

            public TableColumnDyn Column { get; private set; }
            public IDataPoint DataPoint => Column;

            public event DataViewWasChangedHandler Changed;

            public double HasDataFrom => Column.HasDataFrom;
            public double HasDataTo => Column.HasDataTo;

            public double? MinValueHint => Column.MinValueHint;
            public double? MaxValueHint => Column.MaxValueHint;

            public event DataViewHasDataRangeChangedHandler HasDataRangeChanged; 

            public virtual SpanPair<bool> GetCurrentBools() { throw new NotSupportedException(); }
            public virtual SpanPair<byte> GetCurrentBytes() { throw new NotSupportedException(); }
            public virtual SpanPair<int> GetCurrentInts() { throw new NotSupportedException(); }
            public virtual SpanPair<long> GetCurrentLongs() { throw new NotSupportedException(); }
            public virtual SpanPair<float> GetCurrentFloats() { throw new NotSupportedException(); }
            public virtual SpanPair<double> GetCurrentDoubles() { throw new NotSupportedException(); }
            public virtual SpanPair<string> GetCurrentStrings() { throw new NotSupportedException(); }
        }


    }
}
    

    
