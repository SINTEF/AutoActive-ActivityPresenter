using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDataReader;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.Import.Excel
{
    [ImportPlugin(".xlsx",200)]
    public class ImportGaitUpReults : IImportPlugin
    {
        public async Task<bool> CanParse(IReadSeekStreamFactory readerFactory)
        {
            var stream = await readerFactory.GetReadStream();
            using (var reader = new StreamReader(stream))
            {
                var line = reader.ReadLine();
                return line != null && line.Contains("PK");
            }
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {

        }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory,
            Dictionary<string, object> parameters)
        {
            var importer = new GaitUpResultsImporter(parameters, readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    class GaitUpResultsImporter : GenericExcelImporter
    {

        public GaitUpResultsImporter(Dictionary<string, object> parameters, string filename) : base(parameters, filename)
        {

        }

        private long _startTime;
        protected override ExcelDataSetConfiguration PreProcessStream()
        {

            var i = 0;
            var conf = new ExcelDataSetConfiguration
            {
                UseColumnDataType = true,
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    FilterRow = rowReader => 12 <= ++i - 1,
                    FilterColumn = (rowReader, colIndex) => 3 <= colIndex,

                }
            };

            return conf;
        }

        protected override DataTable PostProcessData(DataTable dataTable)
        {
            dataTable = setHeaderName(dataTable);

            dataTable = deleteRowsWithNans(dataTable);

            dataTable = deleteStatistics(dataTable);

            List<String> columnNames = dataTable.Columns.Cast<DataColumn>()
                                             .Where(x => x.ColumnName.Contains("time"))
                                             .Select(x => x.ColumnName)
                                             .ToList();

            foreach (string name in columnNames)
            {
                DataRow[] rows = dataTable.Select();
                _startTime = 0L;
                string[] stringArray = rows.Select(row => row[name].ToString()).ToArray();
                long[] timeArray = StringArrayToTime(stringArray);
                dataTable.Columns.Remove(name);
                dataTable.Columns.Add(name, typeof(long));

                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    dataTable.Rows[i][name] = timeArray[i];
                }

            }

            return dataTable;

        }

        private DataTable setHeaderName(DataTable dataTable)
        {
            List<String> columnNames = dataTable.Rows[0].ItemArray.OfType<String>().ToList();

            for (int columnIndex = 0; columnIndex < dataTable.Columns.Count - 1; columnIndex++)
            {
                if (columnNames[columnIndex].ToString().Contains("HS_") == true)
                {
                    dataTable.Columns[columnIndex].ColumnName = "time" + columnNames[columnIndex].ToString().Replace("HS", "");
                }
                else
                {
                    dataTable.Columns[columnIndex].ColumnName = columnNames[columnIndex].ToString();
                }
            }
            return dataTable;
        }

        private DataTable deleteStatistics(DataTable dataTable)
        {
            List<int> rowsWithStatiscts = Enumerable.Range(0, 9).ToList();

            foreach (int rowNr in rowsWithStatiscts)
            {
                dataTable.Rows[rowNr].Delete();
            }

            dataTable.AcceptChanges();
            return dataTable;

        }

        private DataTable deleteRowsWithNans(DataTable dataTable)
        {

            foreach (DataColumn column in dataTable.Columns)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    if (row[column.ColumnName].ToString() == "NaN")
                    {
                        row.Delete();
                    }
                }
                dataTable.AcceptChanges(); //Delete rows now so we dont iterate through deleted rows multiple time
            }
            return dataTable;
        }

        public static long ConvSecToEpochUs(string timeString)
        {
            float timeFloat = float.Parse(timeString);
            float epochUs = timeFloat * 1000000;
            return Convert.ToInt64(epochUs);

        }

        private long[] StringArrayToTime(IEnumerable<string> stringTime)
        {
            return stringTime.Select(el => ConvSecToEpochUs(el) + _startTime).ToArray();
        }


        protected override void DoParseFile(Stream stream)
        {
            var (names, types, data) = processStream(stream);

            List<String> sides = new List<String>() { "_R", "_L" };

            foreach (string side in sides)
            {

                var booleanFilter = names.Select(name => !name.Contains(side)).ToList();

                List<Array> sideData = booleanFilter.Zip(data, (flag, name) => new { flag, name })
                                    .Where(x => x.flag)
                                    .Select(x => x.name).ToList();

                List<String> sideNames = booleanFilter.Zip(names, (flag, name) => new { flag, name })
                                    .Where(x => x.flag)
                                    .Select(x => x.name).ToList();

                List<Type> sideTypes = booleanFilter.Zip(types, (flag, name) => new { flag, name })
                                    .Where(x => x.flag)
                                    .Select(x => x.name).ToList();

                String sideTableName = TableName + side;

                int index = sideNames.FindIndex(name => name.Contains("time"));

                sideNames[index] = "time";

                createGenericExcelTable(sideNames, sideTypes, sideData, sideTableName);

            }

        }

    }

}
