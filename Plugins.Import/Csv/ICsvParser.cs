using System;
using System.Collections.Generic;
using CsvHelper;

namespace SINTEF.AutoActive.Plugins.Import.Csv
{

    public interface ICsvParser<T>
    {
        void ConfigureCsvReader(CsvReader csvReader);

        void ParseRecord(int rowIdx, T record);

        Dictionary<string, Array> GetParsedData();
    }
}