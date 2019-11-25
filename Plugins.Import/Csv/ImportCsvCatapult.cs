using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.AllocCheck;

namespace SINTEF.AutoActive.Plugins.Import.Csv
{
    [ImportPlugin(".csv", 200)]
    public class ImportCsvCatapult : IImportPlugin
    {
#if DEBUG_MEM
        private AllocTrack mt;

        public ImportCsvCatapult()
        {
            mt = new AllocTrack(this);
        }
#endif
        public async Task<bool> CanParse(IReadSeekStreamFactory readerFactory)
        {
            var stream = await readerFactory.GetReadStream();
            using (var reader = new StreamReader(stream))
            {
                var line = reader.ReadLine();
                return line != null && line.Contains("Logan");
            }
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {

        }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory,
            Dictionary<string, object> parameters)
        {
            var importer = new CatapultCsvImporter(parameters, readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    public class CatapultCsvImporter : GenericCsvImporter
    {
#if DEBUG_MEM
        private AllocTrack mt;
#endif
        public CatapultCsvImporter(Dictionary<string, object> parameters, string filename) : base(parameters, filename)
        {
#if DEBUG_MEM
            mt = new AllocTrack(this, filename);
#endif
        }

        private static long ParseDateTime(string date, string time)
        {
            var dateTime = DateTime.Parse(date + " " + time, CultureInfo.InvariantCulture);
            return TimeFormatter.TimeFromDateTime(dateTime);
        }

        protected override string TableName => "Catapult";
        private long _startTime;

        protected override void PreProcessStream(Stream stream)
        {
            var lines = new List<string>(10);
            var streamPos = stream.Position;
            using (var sr = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
            {
                for (var i = 0; i < lines.Capacity; i++)
                {
                    if (sr.EndOfStream) break;
                    lines.Add(sr.ReadLine());
                }
            }

            var parameters = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                if (!line.Contains("=")) continue;
                var lineSplit = line.Split(new[] {'='}, 2);
                parameters[lineSplit[0]] = lineSplit[1];
            }

            _startTime = 0L;
            if (parameters.TryGetValue("Date", out var date) && parameters.TryGetValue("Time", out var time))
            {
                try
                {
                    _startTime = ParseDateTime(date, time);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not parse start time \"{date} - {time}\": {ex}");
                }
            }
            else
            {
                Debug.WriteLine("Start time not found.");
            }

            stream.Seek(streamPos, SeekOrigin.Begin);
        }

        public static long ConvHmssToEpochUs(string timeString)
        {
            long hours = 0;
            long minutes;
            long seconds;
            long centiSeconds;
            // Expected character format 'M:S.SS' or 'H:M:S.SS'
            var timeSplit = timeString.Split(':');

            var secondsSplit = timeSplit.Last().Split('.');

            if (timeSplit.Length == 2)
            {
                minutes = long.Parse(timeSplit[0]);
                seconds = long.Parse(secondsSplit[0]);
                centiSeconds = long.Parse(secondsSplit[1]);
            }
            else
            {
                hours = long.Parse(timeSplit[0]);
                minutes = long.Parse(timeSplit[1]);
                seconds = long.Parse(secondsSplit[0]);
                centiSeconds = long.Parse(secondsSplit[1]);
            }

            var epochUs = ((hours * 3600L + minutes * 60L + seconds) * 100L + centiSeconds) * 10000L;
            return epochUs;
        }

        private long[] StringArrayToTime(IEnumerable<string> stringTime)
        {
            return stringTime.Select(el => ConvHmssToEpochUs(el) + _startTime).ToArray();
        }

        protected override void PostProcessData(List<string> names, List<Type> types, List<Array> data)
        {
            names[0] = "time";
            types[0] = typeof(long);
            data[0] = StringArrayToTime((string[]) data[0]);


            // Catapult data might have a final column which is empty
            var lastIndex = names.Count - 1;
            if (!string.IsNullOrEmpty(names[lastIndex])) return;
            names.RemoveAt(lastIndex);
            types.RemoveAt(lastIndex);
            data.RemoveAt(lastIndex);
        }
    }
}
