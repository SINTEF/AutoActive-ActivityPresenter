using System;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public static class TableStructureExtensions
    {
#if false
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<bool[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new BoolColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<byte[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new ByteColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<int[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new IntColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<long[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new LongColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<float[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new FloatColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<double[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new GenericColumn<double>(name, loader, index));
        }
#else

        public static void AddColumn<T>(this BaseDataStructure datastructure, string name, Task<T[]> loader,
            TableTimeIndex index) where T : IConvertible
        {
            datastructure.AddDataPoint(new GenericColumn<T>(name, loader, index));
        }
#endif

        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<string[]> loader, TableTimeIndex index)
        {
            datastructure.AddDataPoint(new StringColumn(name, loader, index));
        }
    }
}
