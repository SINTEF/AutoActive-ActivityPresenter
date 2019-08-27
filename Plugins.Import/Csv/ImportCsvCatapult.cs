﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;

namespace SINTEF.AutoActive.Plugins.Import.Csv.Catapult
{


    [ImportPlugin(".csv")]
    public class CatapultImportPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory)
        {
            var importer = new CatapultImporter(readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    public class CatapultImporter : CsvImporterBase
    {
        private Stream _csvStream;

        internal CatapultImporter(string name)
        {
            Name = name;
            _csvStream = null;
        }


        public override Dictionary<string, Array> ReadData()
        {
            return GenericReadData<CatapultRecord>(new CatapultParser(), _csvStream);
        }


        protected override void DoParseFile(Stream s)
        {
            _csvStream = s;

            bool isWorldSynchronized = false;
            string columnName = "Time";
            string uri = Name + "/" + columnName;

            var time = new TableTimeIndex(columnName, GenerateLoader<long>(columnName), isWorldSynchronized, uri);

            columnName = "Forward";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Sideways";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Up";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Dpr";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Gyr1";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Gyr2";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Gyr3";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Altitude";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Vel";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "HDOP";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "VDOP";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Longitude";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Latitude";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Heartrate";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Acc";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Rawvel";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);
    }
}

    public class CatapultParser : ICsvParser<CatapultRecord>
    {
        // Make all the arrays needed
        private long[] timeData = null;
        private float[] forwardData = null;
        private float[] sidewaysData = null;
        private float[] upData = null;
        private float[] dprData = null;
        private float[] gyr1Data = null;
        private float[] gyr2Data = null;
        private float[] gyr3Data = null;
        private float[] altitudeData = null;
        private float[] velData = null;
        private float[] hdopData = null;
        private float[] vdopData = null;
        private float[] logitudeData = null;
        private float[] latitudeData = null;
        private float[] heartrateData = null;
        private float[] accData = null;
        private float[] rawvelData = null;

        private int lastIdx = 0;

        public CatapultParser()
        {

        }

        public void ConfigureCsvReader(CsvReader csvReader)
        {
            // Configure csv reader
            csvReader.Configuration.ShouldSkipRecord = CheckLine;
            csvReader.Configuration.BadDataFound = null;
            csvReader.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
            csvReader.Configuration.CountBytes = true;
        }

        public void ParseRecord(int rowIdx, CatapultRecord rec)
        {
            var currLength = timeData?.Length ?? 0;
            if (rowIdx >= currLength)
            {
                var newLength = currLength + 1000;
                Array.Resize(ref timeData, newLength);
                Array.Resize(ref forwardData, newLength);
                Array.Resize(ref sidewaysData, newLength);
                Array.Resize(ref upData, newLength);
                Array.Resize(ref dprData, newLength);
                Array.Resize(ref gyr1Data, newLength);
                Array.Resize(ref gyr2Data, newLength);
                Array.Resize(ref gyr3Data, newLength);
                Array.Resize(ref altitudeData, newLength);
                Array.Resize(ref velData, newLength);
                Array.Resize(ref hdopData, newLength);
                Array.Resize(ref vdopData, newLength);
                Array.Resize(ref logitudeData, newLength);
                Array.Resize(ref latitudeData, newLength);
                Array.Resize(ref heartrateData, newLength);
                Array.Resize(ref accData, newLength);
                Array.Resize(ref rawvelData, newLength);
            }

            timeData[rowIdx] = rowIdx;
            //timeData[rowCount] = rec.Inputtime;  TODO convert time

            forwardData[rowIdx] = rec.Forward;
            sidewaysData[rowIdx] = rec.Sideways;
            upData[rowIdx] = rec.Up;
            dprData[rowIdx] = rec.Dpr;
            gyr1Data[rowIdx] = rec.Gyr1;
            gyr2Data[rowIdx] = rec.Gyr2;
            gyr3Data[rowIdx] = rec.Gyr3;
            altitudeData[rowIdx] = rec.Altitude;
            velData[rowIdx] = rec.Vel;
            hdopData[rowIdx] = rec.HDOP;
            vdopData[rowIdx] = rec.VDOP;
            logitudeData[rowIdx] = rec.Longitude;
            latitudeData[rowIdx] = rec.Latitude;
            heartrateData[rowIdx] = rec.Heartrate;
            accData[rowIdx] = rec.Acc;
            rawvelData[rowIdx] = rec.Rawvel;

            lastIdx = rowIdx;
        }

        public Dictionary<string, Array> GetParsedData()
        {
            Dictionary<string, Array> locData = new Dictionary<string, Array>();

            // Wrap up and store result
            var finalLength = lastIdx + 1;
            Array.Resize(ref timeData, finalLength);
            Array.Resize(ref forwardData, finalLength);
            Array.Resize(ref sidewaysData, finalLength);
            Array.Resize(ref upData, finalLength);
            Array.Resize(ref dprData, finalLength);
            Array.Resize(ref gyr1Data, finalLength);
            Array.Resize(ref gyr2Data, finalLength);
            Array.Resize(ref gyr3Data, finalLength);
            Array.Resize(ref altitudeData, finalLength);
            Array.Resize(ref velData, finalLength);
            Array.Resize(ref hdopData, finalLength);
            Array.Resize(ref vdopData, finalLength);
            Array.Resize(ref logitudeData, finalLength);
            Array.Resize(ref latitudeData, finalLength);
            Array.Resize(ref heartrateData, finalLength);
            Array.Resize(ref accData, finalLength);
            Array.Resize(ref rawvelData, finalLength);
            locData.Add("Time", timeData);
            locData.Add("Forward", forwardData);
            locData.Add("Sideways", sidewaysData);
            locData.Add("Up", upData);
            locData.Add("Dpr", dprData);
            locData.Add("Gyr1", gyr1Data);
            locData.Add("Gyr2", gyr2Data);
            locData.Add("Gyr3", gyr3Data);
            locData.Add("Altitude", altitudeData);
            locData.Add("Vel", velData);
            locData.Add("HDOP", hdopData);
            locData.Add("VDOP", vdopData);
            locData.Add("Longitude", logitudeData);
            locData.Add("Latitude", latitudeData);
            locData.Add("Heartrate", heartrateData);
            locData.Add("Acc", accData);
            locData.Add("Rawvel", rawvelData);

            return locData;
        }

        private List<string> _preHeaderItems = new List<string>();
        private readonly string[] _preHeaderSignatures = { "Logan", "rawFileName=", "From=", "Date=", "Time=", "Athlete=", "EventDescription=" };
        internal bool CheckLine(string[] l)
        {
            foreach (string signature in _preHeaderSignatures)
            {
                if (l[0].StartsWith(signature))
                {
                    _preHeaderItems.Add(l[0]);
                    return true;
                }
            }
            return false;
        }

    }

    public class CatapultRecord
    {

        [Name("Time")]
        public string Inputtime { get; set; }

        [Name("Forward")]
        public float Forward { get; set; }

        [Name("Sideways")]
        public float Sideways { get; set; }

        [Name("Up")]
        public float Up { get; set; }

        [Name("Vel(Dpr)")]
        public float Dpr { get; set; }

        [Name("Gyr1(d/s)")]
        public float Gyr1 { get; set; }

        [Name("Gyr2(d/s)")]
        public float Gyr2 { get; set; }

        [Name("Gyr3(d/s)")]
        public float Gyr3 { get; set; }

        [Name("Altitude")]
        public float Altitude { get; set; }

        [Name("Vel(av)")]
        public float Vel { get; set; }

        [Name("HDOP")]
        public float HDOP { get; set; }

        [Name("VDOP")]
        public float VDOP { get; set; }

        [Name("Longitude")]
        public float Longitude { get; set; }

        [Name("Latitude")]
        public float Latitude { get; set; }

        [Name("Heart Rate")]
        public float Heartrate { get; set; }

        [Name("Acc(dpr)")]
        public float Acc { get; set; }

        [Name("Raw Vel.")]
        public float Rawvel { get; set; }

        [Name("GPS Time")]
        public string GPStime { get; set; }

        [Name("GPS Latitude")]
        public string GPSlatitude { get; set; }

        [Name("GPS Longitude")]
        public string GPSlongitude { get; set; }

    }


}