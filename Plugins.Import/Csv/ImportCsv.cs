using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;

using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.Import.Csv
{

    public abstract class CsvTableBase : ImportTableBase
    {

        public override abstract Dictionary<string, Array> ReadData();

        public Dictionary<string, Array> GenericReadData<CsvRecord>(ICsvParser<CsvRecord> parser, IReadSeekStreamFactory readerFactory)
        {
            // Read data from file 
            try
            {
                // We must be able to read the csv file multiple times
                //   CrvReader close the stream at end of file... create new stream each time reading file.
                //var csvStream = readerFactory.GetReadStream().GetAwaiter().GetResult();
                var csvStream = Task.Run(() => readerFactory.GetReadStream()).GetAwaiter().GetResult();
                csvStream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(csvStream))
                {
                    using (var csv = new CsvReader(reader))
                    {
                        parser.ConfigureCsvReader(csv);


                        // Prepare while loop
                        var records = csv.GetRecords<CsvRecord>();
                        var myEnum = records.GetEnumerator();
                        var hasRec = myEnum.MoveNext();
                        int rowCount = 0;   // Number of data rows read
                        while (hasRec)
                        {
                            // Fetch data from record
                            var rec = myEnum.Current;

                            parser.ParseRecord(rowCount, rec);

                            // Prepare for next iteration
                            rowCount++;
                            hasRec = myEnum.MoveNext();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var txt = ex.Message;

            }

            return parser.GetParsedData();
        }
    }
}