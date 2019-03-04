using System;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public static class TableStructureExtensions
    {
        public static void AddColumn<T>(this BaseDataStructure datastructure, string name, Task<T[]> loader,
            TableTimeIndex index) where T : IConvertible
        {
            datastructure.AddDataPoint(new GenericColumn<T>(name, loader, index));
        }

        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<string[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new StringColumn(name, loader, index));
        }
    }
}
