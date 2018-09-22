using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public static class TableStructureExtensions
    {
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<bool[]> loader, TableIndex index)
        {
            datastructure.AddDataPoint(new BoolColumn(name, loader, index));
        }

        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<byte[]> loader, TableIndex index)
        {
            datastructure.AddDataPoint(new ByteColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<int[]> loader, TableIndex index)
        {
            datastructure.AddDataPoint(new IntColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<long[]> loader, TableIndex index)
        {
            datastructure.AddDataPoint(new LongColumn(name, loader, index));
        }

        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<float[]> loader, TableIndex index)
        {
            datastructure.AddDataPoint(new FloatColumn(name, loader, index));
        }
        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<double[]> loader, TableIndex index)
        {
            datastructure.AddDataPoint(new DoubleColumn(name, loader, index));
        }

        public static void AddColumn(this BaseDataStructure datastructure, string name, Task<string[]> loader, TableIndex index)
        {
            datastructure.AddDataPoint(new StringColumn(name, loader, index));
        }
    }
}
