using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public class TableIndex : FloatColumn
    {
        public TableIndex(string name, Task<float[]> loader) : base(name, loader, null) { }

        internal int FindIndex(int current, double value)
        {
            // FIXME: This is far from perfect
            if (current >= 0 && data[current] == value) return current;

            // Do a binary search starting at the previous index
            int first = 0;
            int last = data.Length - 1;

            if (current < 0) current = (first + last) / 2;

            while (first < last)
            {
                if (value < data[first]) return first;
                if (value > data[last]) return last;

                if (value > data[current]) first = current + 1;
                else last = current - 1;
                current = (last + first) / 2;

            }
            return current;
        }
    }
}
