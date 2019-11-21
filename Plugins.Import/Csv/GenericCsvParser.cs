using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CsvHelper;
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

        }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory, Dictionary<string, object> parameters)
        {
            var importer = new GenericCsvImporter(parameters, readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    public class GenericCsvImporter : BaseDataProvider
    {
        string _filename;
        public GenericCsvImporter(Dictionary<string, object> parameters, string filename)
        {
            Name = parameters["Name"] as string;
            _filename = filename;
        }

        protected virtual string TableName { get => "CSV-data"; }

        protected virtual void PreProcessStream(Stream stream) { }
        protected virtual void PostProcessData(List<string> names, List<Type> types, List<Array> data) { }

        protected override void DoParseFile(Stream stream)
        {
            PreProcessStream(stream);

            var (names, types, data) = GenericCsvParser.Parse(stream);

            names = names.Select(el => ReplaceIllegalNameCharacters(el)).ToList();

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
            return el.Replace(".", "");
        }

        private int FindTimeDataIndex(List<string> names)
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

        private Array EnsureValidType(Array array)
        {
            if (!(array is DateTime[]arr)) return array;

            return arr.Select(dt => TimeFormatter.TimeFromDateTime(dt)).ToArray();
        }

        private Array EnsureTimeArray(Array array)
        {
            if (array is long[])
                return array;

            if (array is double[] doubleArray)
            {
                return doubleArray.Select(val => TimeFormatter.TimeFromSeconds(val)).ToArray();
            }

            if (array is DateTime[] dateTimeArray)
            {
                return dateTimeArray.Select(dt => TimeFormatter.TimeFromDateTime(dt)).ToArray();
            }

            throw new NotImplementedException();
        }
    }

    public class GenericCsvTable : ImportTableBase, ISaveable
    {
        private readonly Dictionary<string, Array> _data;
        private string _fileName;

        public GenericCsvTable(string tableName, List<string> names, List<Type> types, Dictionary<string, Array> data, string filename)
        {
            Name = tableName;
            _fileName = filename;
            _data = data;
            var timeColInfo = new ColInfo("time", "us");
            var startTime = ((long[])data["time"])[0];

            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), startTime != 0L, Name + "/" + timeColInfo.Name, timeColInfo.Unit);

            for (int i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var type = types[i];

                var colInfo = new ColInfo(name, null);

                var uri = Name + "/" + colInfo.Name;

                if (type == typeof(double))
                {
                    this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), _timeIndex, uri, colInfo.Unit);
                }
                else if (type == typeof(long))
                {
                    this.AddColumn(colInfo.Name, GenerateLoader<long>(colInfo), _timeIndex, uri, colInfo.Unit);
                }
                else if (type == typeof(string))
                {
                    this.AddColumn(colInfo.Name, GenerateLoader<string>(colInfo), _timeIndex, uri, colInfo.Unit);
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
            var metaTable = new JObject { ["type"] = "no.sintef.table" };
            metaTable["attachments"] = new JArray(new object[] { fileId });
            metaTable["units"] = new JArray(GetUnitArr());
            metaTable["is_world_clock"] = _timeIndex.IsSynchronizedToWorldClock;
            metaTable["version"] = 1;

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

        private static readonly List<Type> typeHierarchy = new List<Type>
        {
            typeof(long),
            typeof(double),
            typeof(DateTime),
            typeof(string)
        };

        internal static Type TryGuessTypeSingle(string field)
        {

            if (long.TryParse(field, out long _))
            {
                return typeof(long);
            }
            if (double.TryParse(field, out double _))
            {
                return typeof(double);
            }
            if (DateTime.TryParse(field, out DateTime _))
            {
                return typeof(DateTime);
            }

            return typeof(string);
        }

        private static Type ReduceType(Type oldType, Type newType)
        {
            if (oldType == newType) return oldType;

            return (typeHierarchy.IndexOf(newType) > typeHierarchy.IndexOf(oldType)) ? newType : oldType;
        }

        private static int CountLines(Stream stream)
        {
            var streamPos = stream.Position;
            var lineCount = 0;
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    lineCount++;
                }
            }
            stream.Seek(streamPos, SeekOrigin.Begin);
            return lineCount;
        }

        internal static List<Type> TryGuessType(Stream stream, CsvHelper.Configuration.Configuration config = null)
        {
            var streamStartPosition = stream.Position;
            try
            {
                var types = new List<Type>();

                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true))
                using (var csv = config == null ? new CsvReader(reader, true) : new CsvReader(reader, config, true))
                {
                    if (!csv.Read()) throw new ArgumentException("Could not find valid fields");
                    if (!csv.ReadHeader()) throw new ArgumentException("Could not find valid header");

                    var header = ((IDictionary<string, object>)csv.GetRecord<dynamic>()).Keys;

                    if (!csv.Read()) throw new ArgumentException("Could not find valid fields");
                    var record = (IDictionary<string, object>)csv.GetRecord<dynamic>();

                    foreach (var field in record)
                    {
                        types.Add(TryGuessTypeSingle((string)field.Value));
                    }

                    for (int readCount = 0; readCount < NumLinesToRead; readCount++)
                    {
                        if (!csv.Read()) return types;

                        record = (IDictionary<string, object>)csv.GetRecord<dynamic>();

                        var ix = 0;
                        foreach (var field in record)
                        {
                            types[ix] = ReduceType(types[ix], TryGuessTypeSingle((string)field.Value));
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
                        record = (IDictionary<string, object>)csv.GetRecord<dynamic>();

                        var ix = 0;
                        foreach (var field in record)
                        {
                            var strVal = field.Value as string;
                            if (strVal != null && strVal != "")
                            {
                                types[ix] = ReduceType(types[ix], TryGuessTypeSingle((string)field.Value));
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

        private static CsvHelper.Configuration.Configuration DetectConfiguration(Stream stream)
        {
            var streamPos = stream.Position;
            var configuration = new CsvHelper.Configuration.Configuration();

            // Try to detect delimiter typically (Norwegian Excel format)
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true))
            {
                try
                {
                    using (var csv = configuration == null ? new CsvReader(reader, true) : new CsvReader(reader, configuration, true))
                    {
                        csv.Read();
                        csv.ReadHeader();

                        csv.Read();
                        if (((IDictionary<string, object>)csv.GetRecord<dynamic>()).Count == 1)
                        {
                            // Less than half of the lines contain the delimiter, guess ';' and norwegian commas instead
                            configuration.Delimiter = ";";
                            configuration.CultureInfo = new CultureInfo("no-NB");
                        }
                    }
                } catch(BadDataException) { }
                stream.Seek(streamPos, SeekOrigin.Begin);

                try
                {
                    using (var csv = configuration == null ? new CsvReader(reader, true) : new CsvReader(reader, configuration, true))
                    {
                        csv.Read();
                        csv.ReadHeader();

                        csv.Read();
                        if (((IDictionary<string, object>)csv.GetRecord<dynamic>()).Count != 1)
                        {
                            stream.Seek(streamPos, SeekOrigin.Begin);
                            return configuration;
                        }
                    }
                }
                catch (BadDataException) { }
                stream.Seek(streamPos, SeekOrigin.Begin);


                // Check if we have \n line endings or \r\n
                var buf = new byte[512];
                var dataRead = stream.Read(buf, 0, 512);
                var lineEndSize = buf.Contains((byte)'\r') ? 2 : 1;

                var nextStreamPos = FindLikelyHeaderStart(buf, ',');

                stream.Seek(nextStreamPos, SeekOrigin.Begin);

                // Reset configuration and try to skip lines until we find multiple elements
                configuration = new CsvHelper.Configuration.Configuration();
                try
                {
                    using (var csv = configuration == null ? new CsvReader(reader, true) : new CsvReader(reader, configuration, true))
                    {
                        csv.Read();
                        csv.ReadHeader();

                        csv.Read();
                        if (((IDictionary<string, object>)csv.GetRecord<dynamic>()).Count > 1)
                        {
                            stream.Seek(nextStreamPos, SeekOrigin.Begin);
                            return configuration;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error when handling csv: {ex.Message}");
                }

                stream.Seek(streamPos, SeekOrigin.Begin);
            }
            return configuration;

        }

        private static int FindLikelyHeaderStart(byte[] buf, char delimiter)
        {
            var newline = (byte)'\n';
            var illegalHeaderCharacters = new byte[] { (byte)'=' };
            var delim = (byte)delimiter;

            for (var i=0; i<buf.Length; i++)
            {
                if(buf[i] == delim)
                {
                    var endIx = Array.IndexOf(buf, newline, i) + 1;
                    var startIx = Array.LastIndexOf(buf, newline, i) + 1;

                    var isLegal = true;
                    for(var j= startIx; j<endIx; j++)
                    {
                        if (illegalHeaderCharacters.Contains(buf[j]))
                        {
                            isLegal = false;
                            break;
                        }
                    }
                    if (isLegal)
                    {
                        return startIx;
                    }
                    i = endIx;
                }
            }

            return 0;
        }

        public static Array IListToArray(IList list, Type elementType)
        {
            MethodInfo castMethod = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(new Type[] { elementType });
            MethodInfo toArrayMethod = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(new Type[] { elementType });

            var castedObjectEnum = castMethod.Invoke(null, new object[] { list });
            return (Array)toArrayMethod.Invoke(null, new object[] { castedObjectEnum });
        }

        public static (List<string>, List<Type>, List<Array>) Parse(Stream stream)
        {
            // I'm assuming that it is quicker to count the number of lines to initialize the lists for the output
            int lineCount = CountLines(stream);

            var configuration = DetectConfiguration(stream);

            var types = TryGuessType(stream, configuration);

            var data = new List<IList>();
            var listGenericType = typeof(List<>);
            foreach (var type in types)
            {
                var genericListType = listGenericType.MakeGenericType(type);
                var list = (IList)Activator.CreateInstance(genericListType, new object[] { lineCount });
                data.Add(list);

            }

            List<string> headers = null;
            using (var reader = new StreamReader(stream))
            using (var csv = configuration == null ? new CsvReader(reader) : new CsvReader(reader, configuration))
            {
                csv.Read();
                csv.ReadHeader();

                headers = new List<string>(((IDictionary<string, object>)csv.GetRecord<dynamic>()).Keys.Select(s => s.Trim()));

                while (csv.Read())
                {
                    for (int i = 0; i < data.Count; i++)
                    {
                        data[i].Add(csv.GetField(types[i], i));
                    }
                }
            }

            var dataArray = new List<Array>(data.Count);
            for (int i = 0; i < data.Count; i++)
            {
                var type = types[i];
                var list = data[i];

                dataArray.Add(IListToArray(list, type));
            }

            return (headers, types, dataArray);
        }
    }
}
