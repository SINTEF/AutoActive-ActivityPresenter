using System;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public static class TableStructureExtensions
    {
        public static void AddColumn<T>(this BaseDataStructure datastructure, string name, Task<T[]> loader,
            TableTimeIndex index, string uri) where T : IConvertible
        {
            datastructure.AddDataPoint(new GenericColumn<T>(name, loader, index, uri));
        }

        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<string[]> loader, TableTimeIndex index, string uri)
        {
            datastructure.AddDataPoint(new StringColumn(name, loader, index, uri));
        }
    }
}
