using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Helpers;
using System.Text;

namespace SINTEF.AutoActive.Plugins.Import.Excel
{
    [ImportPlugin(".xlsx")]
    public class ImportGenericExcel : IImportPlugin
    {

        public Task<bool> CanParse(IReadSeekStreamFactory readerFactory)
        {
            return Task.FromResult(true);
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {
            
        }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory,
            Dictionary<string, object> parameters)
        {
            var importer = new GenericExcelImporter(parameters, readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }

    }


    public class GenericExcelImporter : BaseDataProvider
    {
        private readonly string _filename;

        public GenericExcelImporter(Dictionary<string, object> parameters, string filename)
        {
            Name = parameters["Name"] as string;
            _filename = filename;
        }

        protected virtual string TableName => "EXCEL";


        protected virtual ExcelDataSetConfiguration PreProcessStream()
        {
            ExcelDataSetConfiguration conf = new ExcelDataSetConfiguration();
            return conf;
        }


        protected virtual DataTable PostProcessData(DataTable dataTable)
        {
            return dataTable;
        }



        protected override void DoParseFile(Stream stream)
        {
            ExcelDataSetConfiguration conf = PreProcessStream();

            DataTable dataTable = GenericExcelParser.Parse(stream, conf);

            dataTable = PostProcessData(dataTable);

            List<String> columnNames = dataTable.Columns.Cast<DataColumn>()
                                             .Select(x => x.ColumnName)
                                             .ToList();

            var (names, types, data) = GenericExcelParser.convertDatatable(dataTable);

            names = names.Select(ReplaceIllegalNameCharacters).ToList();



            if (names.Select(name => !name.Contains("_L") || !name.Contains("_R")).ToList().Count == names.Count)
            {
                List<String> sides = new List<String>() { "_R", "_L" };

                foreach (string side in sides)
                {


                    var BooleanFilter = names.Select(name => !name.Contains(side)).ToList();

                    List<Array> sideData = BooleanFilter.Zip(data, (flag, name) => new { flag, name })
                                        .Where(x => x.flag)
                                        .Select(x => x.name).ToList();

                    List<String> sideNames = BooleanFilter.Zip(names, (flag, name) => new { flag, name })
                                        .Where(x => x.flag)
                                        .Select(x => x.name).ToList();

                    List<Type> sideTypes = BooleanFilter.Zip(types, (flag, name) => new { flag, name })
                                        .Where(x => x.flag)
                                        .Select(x => x.name).ToList();

                    String sideTableName = TableName + side;

                    int index = sideNames.FindIndex(name => name.Contains("time"));

                    sideNames[index] = "time";



                    createGenericExcelTable(sideNames, sideTypes, sideData, sideTableName);


                }

            }
            else
            {
                createGenericExcelTable(names, types, data, TableName);
            }

        }

        private void createGenericExcelTable(List<String> names, List<Type> types, List<Array> data, String tableName)
        {

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

            AddChild(new GenericExcelTable(tableName, names, types, dict, _filename));
        }


        private static string ReplaceIllegalNameCharacters(string el)
        {
            return el.Replace(".", "");
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

        private static Array EnsureTimeArray(Array array)
        {
            switch (array)
            {
                case long[] _:
                    return array;
                case double[] doubleArray:
                    return doubleArray.Select(TimeFormatter.TimeFromSeconds).ToArray();
                case DateTime[] dateTimeArray:
                    return dateTimeArray.Select(TimeFormatter.TimeFromDateTime).ToArray();
                default:
                    throw new NotImplementedException();
            }
        }


        private static Array EnsureValidType(Array array)
        {
            if (!(array is DateTime[] arr)) return array;

            return arr.Select(TimeFormatter.TimeFromDateTime).ToArray();
        }




    }




    public class GenericExcelTable : ImportTableBase, ISaveable
    {
        private readonly Dictionary<string, Array> _data;
        private readonly string _fileName;

        public GenericExcelTable(string tableName, IReadOnlyList<string> names, IReadOnlyList<Type> types,
                               Dictionary<string, Array> data, string filename)
        {
            base.Name = tableName;
            IsSaved = false;
            _fileName = filename;
            _data = data;
            var timeColInfo = new ColInfo("time", "us");
            var startTime = ((long[])data["time"])[0];

            var timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), startTime != 0L,
                base.Name + "/" + timeColInfo.Name, timeColInfo.Unit);

            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var type = types[i];

                var colInfo = new ColInfo(name, null);

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
                ["attachments"] = new JArray(new object[] { fileId }),
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







    public class GenericExcelParser
    {
        private static Type TryGuessTypeSingle(string field)
        {

            if (long.TryParse(field, out _))
            {
                return typeof(long);
            }

            if (double.TryParse(field, out _))
            {
                return typeof(double);
            }

            if (DateTime.TryParse(field, out _))
            {
                return typeof(DateTime);
            }

            return typeof(string);
        }


        public static (List<string> names, List<Type> types, List<Array> data) convertDatatable(DataTable dataTable)
        {
            List<Array> data = new List<Array>();
            List<String> names = new List<String>();
            List<Type> types = new List<Type>();

            DataRow[] rows = dataTable.Select();

            foreach (DataColumn column in dataTable.Columns)
            {
                try
                {


                    string[] stringArray = rows.Select(row => row[column.ColumnName].ToString()).ToArray();
                    var typesInColumn = new List<Type>();
                    typesInColumn.AddRange(stringArray.Select(field => TryGuessTypeSingle((string)field)));


                    if (typesInColumn.Select(field => field.FullName).Contains("System.Double"))
                    {
                        data.Add(Array.ConvertAll(stringArray, Double.Parse));
                        types.Add(typeof(double));
                        names.Add(column.ColumnName);
                    }

                    else if (typesInColumn.Select(field => field.FullName).Contains("System.Int64"))
                    {
                        data.Add(Array.ConvertAll(stringArray, long.Parse));
                        types.Add(typeof(long));
                        names.Add(column.ColumnName);
                    }

                    else
                    {
                        data.Add(Array.ConvertAll(stringArray, DateTime.Parse));
                        types.Add(typeof(DateTime));
                        names.Add(column.ColumnName);
                    }

                }
                catch (FormatException)
                {
                    //Throws away columns which does not contain any data, as these columns cause a FormatException 
                }


            }

            return (names, types, data);

        }



        public static DataTable Parse(Stream stream, ExcelDataSetConfiguration conf)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); //Necessary as encoding 1252 is not supported by default
            IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet(conf);
            DataTable dataTable = result.Tables[0];

            return dataTable;

        }





    }

}
