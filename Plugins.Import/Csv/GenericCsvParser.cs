using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Helpers;

namespace SINTEF.AutoActive.Plugins.Import.Csv
{
    [ImportPlugin(".csv")]
    public class ImportGenericCsv : IImportPlugin
    {
        public Task<bool> CanParse(IReadSeekStreamFactory readerFactory)
        {
            return Task.FromResult(true);
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {
            // Add option to specify that time coloumn is in milliseconds
            parameters["Time"] = (false, "The time column can be in milliseconds or microseconds.\nImport as milliseconds (Yes) or microseconds (No)");
        }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory,
            Dictionary<string, object> parameters)
        {
            var importer = new GenericCsvImporter(parameters, readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    public class GenericCsvImporter : BaseDataProvider
    {
        private readonly string _filename;
        private readonly Dictionary<string, object> _parameters;

        public GenericCsvImporter(Dictionary<string, object> parameters, string filename)
        {
            Name = parameters["Name"] as string;
            _filename = filename;
            _parameters = parameters;
        }

        protected virtual string TableName => "CSV";

        protected virtual void PreProcessStream(Stream stream)
        {
        }

        protected virtual void PostProcessData(List<string> names, List<Type> types, List<Array> data)
        {
        }

        protected override void DoParseFile(Stream stream)
        {
            PreProcessStream(stream);

            var (names, types, data) = GenericCsvParser.Parse(stream);

            names = names.Select(ReplaceIllegalNameCharacters).ToList();

            PostProcessData(names, types, data);

            var dict = new Dictionary<string, Array>();

            const string timeName = "time";

            var timeIndex = FindTimeDataIndex(names);
            dict[timeName] = EnsureTimeArray(data[timeIndex]);
            names.RemoveAt(timeIndex);
            types.RemoveAt(timeIndex);
            data.RemoveAt(timeIndex);

            for (var i = 0; i < names.Count; i++)
            {
                dict[names[i]] = EnsureValidType(data[i]);
            }

            AddChild(new GenericCsvTable(TableName, names, types, dict, _filename));
        }

        private static string ReplaceIllegalNameCharacters(string el)
        {
            return Regex.Replace(el, @"[-.() ]", "_");
        }

        private static int FindTimeDataIndex(List<string> names)
        {
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];

                if (name == "time")
                {
                    return i;
                }
            }

            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                if (name.ToLower().Contains("time"))
                {
                    return i;
                }

                if (name.ToLower().Contains("epoch"))
                {
                    return i;
                }
            }

            // If nothing else is found, use the first column as time
            return 0;
        }

        private static Array EnsureValidType(Array array)
        {
            if (!(array is DateTime[] arr)) return array;

            return arr.Select(TimeFormatter.TimeFromDateTime).ToArray();
        }

        private Array EnsureTimeArray(Array array)
        {
            switch (array)
            {
                case long[] longArray:
                    // Time as long array, check if specified to be in ms
                    if ((_parameters.ContainsKey("Time")) && 
                        ((bool)_parameters["Time"]))
                    {
                        // Time in ms, convert to us
                        return longArray.Select(TimeFormatter.TimeFromMilliSeconds).ToArray();
                    }
                    // Time already in us
                    return array;
                case double[] doubleArray:
                    // Convert from seconds to microseconds
                    return doubleArray.Select(TimeFormatter.TimeFromSeconds).ToArray();
                case DateTime[] dateTimeArray:
                    // Time is date time format, convert to us
                    return dateTimeArray.Select(TimeFormatter.TimeFromDateTime).ToArray();
                default:
                    // Unknown time format
                    throw new NotImplementedException();
            }
        }
    }

    public class GenericCsvTable : ImportTableBase, ISaveable
    {
        private readonly Dictionary<string, Array> _data;
        private readonly string _fileName;

        public GenericCsvTable(string tableName, IReadOnlyList<string> names, IReadOnlyList<Type> types,
            Dictionary<string, Array> data, string filename)
        {
            base.Name = tableName;
            IsSaved = false;
            _fileName = filename;
            _data = data;
            var timeColInfo = new ColInfo("time", "us");
            var startTime = ((long[]) data["time"])[0];

            var timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), startTime != 0L,
                base.Name + "/" + timeColInfo.Name, timeColInfo.Unit);

            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var type = types[i];

                var colInfo = new ColInfo(name, "-");

                var uri = base.Name + "/" + colInfo.Name;

                if (type == typeof(double))
                {
                    this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), timeIndex, uri, colInfo.Unit);
                }
                else if (type == typeof(long))
                {
                    this.AddColumn(colInfo.Name, GenerateLoader<long>(colInfo), timeIndex, uri, colInfo.Unit);
                }
                else if (type == typeof(string))
                {
                    this.AddColumn(colInfo.Name, GenerateLoader<string>(colInfo), timeIndex, uri, colInfo.Unit);
                }
            }
        }

        public bool IsSaved { get; }

        public override Dictionary<string, Array> ReadData()
        {
            return _data;
        }

        public Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            // TODO: give a better name?
            var fileId = "/Import" + "/" + Name + "." + Guid.NewGuid();

            // Make table object
            var metaTable = new JObject
            {
                ["type"] = "no.sintef.table",
                ["attachments"] = new JArray(new object[] {fileId}),
                ["units"] = new JArray(GetUnitArray()),
                ["is_world_clock"] = DataPoints.First().Time.IsSynchronizedToWorldClock,
                ["version"] = 1
            };

            var userTable = new JObject { };
            userTable["filename"] = _fileName;

            // Place objects into root
            root["meta"] = metaTable;
            root["user"] = userTable;

            return WriteTable(fileId, writer);
        }
    }

    public class GenericCsvParser
    {
        private const int NumLinesToRead = 100;
        private const int FileEndOffset = 2000;

        private static readonly List<Type> TypeHierarchy = new List<Type>
        {
            typeof(long),
            typeof(double),
            typeof(DateTime),
            typeof(string)
        };


        public static Configuration DefaultConfig => AlternativeConfigurations[0];

        private static readonly Configuration[] AlternativeConfigurations = new Configuration[] {
            new Configuration
            {
                Delimiter = ",",
                CultureInfo = CultureInfo.InvariantCulture
            },
            new Configuration {
                Delimiter = ";",
                CultureInfo = new CultureInfo("no-NB")
            }, // Norwegian format (Excel)
            new Configuration()
        };

        public static Type TryGuessTypeSingle(string field, CultureInfo culture)
        {

            if (long.TryParse(field, out _))
            {
                return typeof(long);
            }
            if (double.TryParse(field, NumberStyles.Float | NumberStyles.AllowThousands, culture.NumberFormat,out _))
            {
                return typeof(double);
            }

            if (DateTime.TryParse(field, culture.DateTimeFormat, DateTimeStyles.None, out _))
            {
                return typeof(DateTime);
            }

            return typeof(string);
        }

        private static Type ReduceType(Type oldType, Type newType)
        {
            if (oldType == newType) return oldType;

            if ((TypeHierarchy.IndexOf(newType) > TypeHierarchy.IndexOf(oldType)))
                return newType;
            return oldType;
        }

        private static int CountLines(Stream stream)
        {
            var streamPos = stream.Position;
            var lineCount = 0;
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true))
            {
                while (!reader.EndOfStream)
                {
                    reader.ReadLine();
                    lineCount++;
                }
            }

            stream.Seek(streamPos, SeekOrigin.Begin);
            return lineCount;
        }

        public static List<Type> TryGuessType(Stream stream, Configuration config = null)
        {
            var streamStartPosition = stream.Position;
            config = config ?? DefaultConfig;
            var culture = config.CultureInfo;
            try
            {
                var types = new List<Type>();

                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true))
                using (var csv = new CsvReader(reader, config, true))
                {
                    if (!csv.Read()) throw new ArgumentException("Could not find valid fields");
                    if (!csv.ReadHeader()) throw new ArgumentException("Could not find valid header");


                    if (!csv.Read()) throw new ArgumentException("Could not find valid fields");
                    var record = (IDictionary<string, object>) csv.GetRecord<dynamic>();

                    types.AddRange(record.Select(field => TryGuessTypeSingle((string) field.Value, culture)));

                    for (var readCount = 0; readCount < NumLinesToRead; readCount++)
                    {
                        if (!csv.Read()) return types;

                        record = (IDictionary<string, object>) csv.GetRecord<dynamic>();

                        var ix = 0;
                        foreach (var field in record)
                        {
                            types[ix] = ReduceType(types[ix], TryGuessTypeSingle((string) field.Value, culture));
                            ix++;
                        }
                    }

                    var pos = reader.BaseStream.Position;

                    // Seek to end of stream to check if there are any changes there
                    reader.BaseStream.Seek(FileEndOffset, SeekOrigin.End);
                    if (reader.BaseStream.Position < pos)
                    {
                        reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                    }
                    else
                    {
                        // Skip to the end of a line
                        csv.Read();
                    }

                    while (csv.Read())
                    {
                        record = (IDictionary<string, object>) csv.GetRecord<dynamic>();

                        var ix = 0;
                        foreach (var field in record)
                        {
                            var strVal = (field.Value as string)?.Trim();
                            if (!string.IsNullOrEmpty(strVal))
                            {
                                types[ix] = ReduceType(types[ix], TryGuessTypeSingle(strVal, culture));
                            }

                            ix++;
                        }
                    }

                }

                return types;
            }
            finally
            {
                stream.Seek(streamStartPosition, SeekOrigin.Begin);
            }
        }

        private static bool TestConfiguration(Stream stream, Configuration configuration)
        {
            var streamPos = stream.Position;
            try
            {
                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true))
                {
                    try
                    {
                        using (var csv = new CsvReader(reader, configuration, true))
                        {
                            csv.Read();
                            csv.ReadHeader();

                            csv.Read();
                            stream.Seek(streamPos, SeekOrigin.Begin);
                            var row = (IDictionary<string, object>) csv.GetRecord<dynamic>();
                            return row.Count > 1;
                        }
                    }
                    catch (BadDataException)
                    {
                    }
                    catch (ReaderException)
                    {
                    }
                }
            }
            finally
            {
                stream.Seek(streamPos, SeekOrigin.Begin);
            }

            return false;
        }

        private static Configuration DetectConfiguration(Stream stream)
        {
            foreach (var config in AlternativeConfigurations)
            {
                if (TestConfiguration(stream, config))
                {
                    return config;
                }
            }

            var buf = new byte[512];
            stream.Read(buf, 0, buf.Length);
            var nextStreamPos = FindLikelyHeaderStart(buf, ',');
            stream.Seek(nextStreamPos, SeekOrigin.Begin);

            foreach (var config in AlternativeConfigurations)
            {
                if (TestConfiguration(stream, config))
                {
                    return config;
                }
            }

            return DefaultConfig;

        }

        private static int FindLikelyHeaderStart(byte[] buf, char delimiter)
        {
            const byte newline = (byte) '\n';
            var illegalHeaderCharacters = new[] {(byte) '='};
            var byteDelimiter = (byte) delimiter;

            for (var i = 0; i < buf.Length; i++)
            {
                if (buf[i] != byteDelimiter) continue;


                var endIx = Array.IndexOf(buf, newline, i) + 1;
                var startIx = Array.LastIndexOf(buf, newline, i) + 1;

                var isLegal = true;
                for (var j = startIx; j < endIx; j++)
                {
                    if (!illegalHeaderCharacters.Contains(buf[j])) continue;

                    isLegal = false;
                    break;
                }

                if (isLegal)
                {
                    return startIx;
                }

                i = endIx;
            }

            return 0;
        }

        public static Array ListInterfaceToArray(IList list, Type elementType)
        {
            var castMethod = typeof(Enumerable).GetMethod("Cast")?.MakeGenericMethod(elementType);
            if (castMethod == null) return null;

            var toArrayMethod = typeof(Enumerable).GetMethod("ToArray")?.MakeGenericMethod(elementType);
            if (toArrayMethod == null) return null;

            var castedObjectEnum = castMethod.Invoke(null, new object[] {list});
            return (Array) toArrayMethod.Invoke(null, new[] {castedObjectEnum});
        }

        private static IList CreateListOfType(Type type, int capacity)
        {
            var listGenericType = typeof(List<>);
            var genericListType = listGenericType.MakeGenericType(type);
            var list = (IList)Activator.CreateInstance(genericListType, new object[] { capacity });
            return list;
        }

        // This method fails if there is a line that includes a , (comma) before the 'header' (e.g. Hyper IMU files)
        public static (List<string>, List<Type>, List<Array>) Parse(Stream stream)
        {
            // I'm assuming that it is quicker to count the number of lines to initialize the lists for the output
            var lineCount = CountLines(stream);

            var configuration = DetectConfiguration(stream);

            var types = TryGuessType(stream, configuration);
            var data = types.Select(type => CreateListOfType(type, lineCount)).ToList();

            List<string> headers;
            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, configuration))
            {
                csv.Read();
                csv.ReadHeader();

                headers = new List<string>(
                    ((IDictionary<string, object>) csv.GetRecord<dynamic>()).Keys.Select(s => s.Trim()));

                while (csv.Read())
                {
                    for (var i = 0; i < data.Count; i++)
                    {
                        data[i].Add(csv.GetField(types[i], i));
                    }
                }
            }

            var dataArray = new List<Array>(data.Count);
            for (var i = 0; i < data.Count; i++)
            {
                var type = types[i];
                var list = data[i];

                dataArray.Add(ListInterfaceToArray(list, type));
            }

            return (headers, types, dataArray);
        }
    }
}
