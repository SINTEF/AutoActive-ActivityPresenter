using Newtonsoft.Json;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.Import.Mqtt.Columns;
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


            AddChild(new MqttStr("MqttDummy"));
        }
    }

    internal class MqttJsonTable
    {
        public string name { get; set; }
        public long[] time { get; set; }
        public MqttJsonColumn[] columns { get; set; }
    }

    public class MqttJsonColumn
    {
        public string name { get; set; }
        public double[] val { get; set; }
    }

    public class MqttStr : BaseDataStructure
    {
        internal MqttStr(string id)
        {
            Name = id;

            // Start MQTT listener thread
            MakeMqttListener();
        }

        ~MqttStr()
        {
            if (mqttListener != null) mqttListener.Abort();
        }

        private Thread mqttListener = null;
        private bool runListener = true;

        private Dictionary<string, MqttTable> mqttDict = new Dictionary<string, MqttTable>();

        internal class MqttTable : BaseDataStructure
        {
            internal bool isValid {get;}
            internal TableTimeIndexDyn timeCol;
            internal DoubleColumnDyn[] dataColArr;
            public MqttTable(Dictionary<string, object> rx)
            {
                isValid = false;
                if (rx.ContainsKey("metadata"))
                {
                    var metadata_o = rx["metadata"];
                    var metadata = rx["metadata"] as Dictionary<string, object>;
                    if (metadata != null)
                    {
                        Name = metadata["name"] as string;
                        var colNames = metadata["col_names"];
                        var colTypes = metadata["col_types"];
                        //Debug.WriteLine($"MqttColumn name: {name} ");
                        //timeCol = new TableTimeIndexDyn(name + "Time", false);
                        //dataColArr = new DoubleColumnDyn[jsonTab.columns.Length];
                        //for (int col = 0; col < jsonTab.columns.Length; col++)
                        //{
                        //dataColArr[col] = new DoubleColumnDyn(jsonTab.columns[col].name, timeCol);
                        //mt.AddDataPoint(dataColArr[col]);
                        //}
                    }
                }
            }

            public void RxValue(MqttJsonTable jsonTab)
            {
                //Debug.WriteLine($"RxValue name: {rx.name} time: {rx.time} val: {rx.val}");
                for (int i = 0; i < jsonTab.time.Length; i++)
                {
                    timeCol.AddData(jsonTab.time[i]);
                    for (int col = 0; col < dataColArr.Length; col++)
                    {
                        dataColArr[col] = new DoubleColumnDyn(jsonTab.columns[col].name, timeCol);
                        dataColArr[col].AddData(jsonTab.columns[col].val[i]);
                    }
                }
                for (int col = 0; col < dataColArr.Length; col++)
                {
                    dataColArr[col].UpdatedData();
                }
            }
        }

        private void rxMqtt(MqttApplicationMessage msg)
        {
            string payloadStr = Encoding.UTF8.GetString(msg.Payload);
            Debug.WriteLine($"Message received in topic: {msg.Topic} msg: {payloadStr}");
            //var rx = JsonConvert.DeserializeObject<MqttJsonTable>(payloadStr);
            // Debug.WriteLine($"Json Message received name: {rx.name} time: {rx.time.Length} val: {rx.columns.Length}");
            var rxDecode = JsonConvert.DeserializeObject<Dictionary<string, object>>(payloadStr);
            if (rxDecode.ContainsKey("uuid"))
            {
                string uuid = rxDecode["uuid"] as string;
                if (uuid != null)
                {

                    Debug.WriteLine($"Json Message received uuid: {uuid}");
                    if (!mqttDict.ContainsKey(uuid))
                    {
                        var newTable = new MqttTable(rxDecode);
                        if (newTable.isValid)
                        {
                            mqttDict.Add(uuid, newTable);
                            //newTable.RxValue(rxDecode);
                        }
                    }
                    else
                    {
                        //mqttDict[uuid].RxValue(rxDecode);
                    }
                }
            }
        }

        /* Start the listener */
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
                var timeCol1 = new TableTimeIndexDyn("Time1", false);
                var dataCol1 = new DoubleColumnDyn("Data1", timeCol1);
                this.AddDataPoint(dataCol1);

                var timeCol2 = new TableTimeIndexDyn("Time2", false);
                var dataCol2 = new DoubleColumnDyn("Data2", timeCol2);
                this.AddDataPoint(dataCol2);


                long count = 0;
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
                    timeCol1.AddData(count*1000000);
                    dataCol1.AddData((double)Math.Sin((double)count / 10));
                    dataCol1.UpdatedData();

                    timeCol2.AddData(count * 1000000 / 2);
                    dataCol2.AddData((double)Math.Sin((double)count / 10));
                    dataCol2.UpdatedData();
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

    }

}
    

    
