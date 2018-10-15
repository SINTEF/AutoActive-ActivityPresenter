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


            AddChild(new MqttTable("MqttDummy"));
        }
    }

    internal class MqttValue
    {
        public string name { get; set; }
        public long time { get; set; }
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
            internal TableTimeIndexDyn timeCol;
            internal DoubleColumnDyn dataCol;
            internal double minTime = 0;
            internal double maxTime = 0;
            public MqttColumn(string name, MqttTable mt)
            {
                this.name = name;
                Debug.WriteLine($"MqttColumn name: {name} ");
                timeCol = new TableTimeIndexDyn(name + "Time", false);
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
                var timeCol1 = new TableTimeIndexDyn("Time1", false);
                var dataCol1 = new DoubleColumnDyn("Data1", timeCol1);
                this.AddDataPoint(dataCol1);

                var timeCol2 = new TableTimeIndexDyn("Time2", false);
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
                    timeCol1.AddData(count);
                    dataCol1.AddData((double)Math.Sin((double)count / 10));
                    dataCol1.UpdateDataRange(0, count);

                    timeCol2.AddData(count / 2);
                    dataCol2.AddData((double)Math.Sin((double)count / 10));
                    dataCol2.UpdateDataRange(0, count / 2);
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
    

    
