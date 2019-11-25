using SINTEF.AutoActive.Databus.AllocCheck;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class StringColumn : TableColumn
    {
        internal string[] data;
        private Task<string[]> loader;

#if DEBUG_MEM
        private readonly AllocTrack mt;
#endif
        public StringColumn(string name, Task<string[]> loader, TableTimeIndex index, string uri, string unit) : base(typeof(string), name, loader, index, uri)
        {
#if DEBUG_MEM
            mt = new AllocTrack(this, Name);
#endif
            this.loader = loader;
            Unit = unit;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateStringViewer(TableTimeIndex index)
        {
            return new StringColumnViewer(index, this);
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            return (null, null);
        }
    }

    public class StringColumnViewer : TableColumnViewer
    {
        private StringColumn column;

        internal StringColumnViewer(TableTimeIndex index, StringColumn column) : base(index, column)
        {
            this.column = column;
        }

        public override SpanPair<string> GetCurrentStrings()
        {
            return new SpanPair<string>(StartIndex, Index.Data.AsSpan(StartIndex, Length), column.data.AsSpan(StartIndex, Length));
        }
    }
}
