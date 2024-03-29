﻿using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Plugins.Import.Mqtt.Columns;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Plugins.Import.Mqtt
{
    public abstract class TableColumnDyn : IDataPoint
    {
        protected TableTimeIndexDyn index;
        internal readonly List<IDynDataViewer> dynDataViewers = new List<IDynDataViewer>();

        public event EventHandler DataChanged;

        public string URI => "LIVE";
        public Type DataType { get; private set; }
        public string Name { get; set; }

        internal virtual double HasDataFrom => index.HasDataFrom;
        internal virtual double HasDataTo => index.HasDataTo;

        internal double? MinValueHint { get; set; }
        internal double? MaxValueHint { get; set; }

        public ITimePoint Time => index;
        public string Unit { get; set; }

        internal TableColumnDyn(Type type, string name, TableTimeIndexDyn index)
        {
            DataType = type;
            Name = name;
            this.index = index;
        }

        public void UpdatedData()
        {
            index.UpdatedTimeIndex();
            foreach (var viewer in dynDataViewers)
            {
                viewer.UpdatedData();
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task<IDataViewer> CreateViewer()
        {
            TableColumnDynViewer newViewer;
            switch (this)
            {
                // case BoolColumnDyn c:
                //     newViewer = CreateBoolDynViewer(index);
                //     break;
                // case ByteColumnDyn c:
                //     newViewer = CreateByteDynViewer(index);
                //     break;
                // case IntColumnDyn c:
                //     newViewer = CreateIntDynViewer(index);
                //     break;
                case LongColumnDyn c:
                    newViewer = CreateLongDynViewer(index);
                    break;
                // case FloatColumnDyn c:
                //     newViewer = CreateFloatDynViewer(index);
                //     break;
                case DoubleColumnDyn c:
                    newViewer = CreateDoubleDynViewer(index);
                    break;
                // case StringColumnDyn c:
                //     newViewer = CreateStringDynViewer(index);
                // break;
                default:
                    throw new NotSupportedException();
            }
            dynDataViewers.Add(newViewer);
            return Task.FromResult((IDataViewer)newViewer);
        }

        //protected abstract (double? min, double? max) GetDataMinMax();

        protected virtual TableColumnDynViewer CreateBoolDynViewer(TableTimeIndexDyn index) { throw new NotSupportedException(); }
        protected virtual TableColumnDynViewer CreateByteDynViewer(TableTimeIndexDyn index) { throw new NotSupportedException(); }
        protected virtual TableColumnDynViewer CreateIntDynViewer(TableTimeIndexDyn index) { throw new NotSupportedException(); }
        protected virtual TableColumnDynViewer CreateLongDynViewer(TableTimeIndexDyn index) { throw new NotSupportedException(); }
        protected virtual TableColumnDynViewer CreateFloatDynViewer(TableTimeIndexDyn index) { throw new NotSupportedException(); }
        protected virtual TableColumnDynViewer CreateDoubleDynViewer(TableTimeIndexDyn index) { throw new NotSupportedException(); }
        protected virtual TableColumnDynViewer CreateStringDynViewer(TableTimeIndexDyn index) { throw new NotSupportedException(); }
    }

    public abstract class TableColumnDynViewer : ITimeSeriesViewer, IDynDataViewer
    {
        protected TableTimeIndexDyn index;
        protected int startIndex = -1;
        protected int endIndex = -1;
        //protected double lastFrom = 0;
        //protected double lastTo = 0;
        protected int length = -1;

        //public delegate void DataChangedHandler();
        //event DataChangedHandler DataChanged;

        protected TableColumnDynViewer(TableTimeIndexDyn index, TableColumnDyn column)
        {
            this.index = index;
            Column = column;
        }

        public long PreviewPercentage { get; set; }
        public void SetTimeRange(long from, long to)
        {
            var diff = to - from;
            var startTime = from - diff * PreviewPercentage / 100;
            var endTime = startTime + diff;
            var start = index.FindIndex(startIndex, startTime);
            var end = index.FindIndex(endIndex, endTime);
            CurrentTimeRangeFrom = from;
            CurrentTimeRangeTo = to;
            //lastTo = to;
            //lastFrom = from;
            //Debug.WriteLine("TableColumnDynViewer::RangeUpdated " + this.Column.Name + " " + from + " " + to + " " + startIndex + " " + endIndex);
            if (start != startIndex || end != endIndex)
            {
                startIndex = start;
                endIndex = end;
                length = endIndex - startIndex + 1;
                Debug.WriteLine("TableColumnDynViewer::SetTimeRange   Changed " + this.Column.Name);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UpdatedData()
        {
            // Inform context
            //TODO Fixme HasDataRangeChanged?.Invoke(from, to);

            // Update visible data range if changed
            var start = index.FindIndex(startIndex, CurrentTimeRangeFrom);
            var end = index.FindIndex(endIndex, CurrentTimeRangeTo);
            if (start != startIndex || end != endIndex)
            {
                startIndex = start;
                endIndex = end;
                length = endIndex - startIndex + 1;
                //Debug.WriteLine("TableColumnDynViewer::UpdatedData   Changed " + this.Column.Name);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public TableColumnDyn Column { get; private set; }
        public IDataPoint DataPoint => Column;

        public event EventHandler Changed;


        public double? MinValueHint => Column.MinValueHint;
        public double? MaxValueHint => Column.MaxValueHint;

        public long CurrentTimeRangeFrom { get; private set; }
        public long CurrentTimeRangeTo { get; private set; }

        public virtual SpanPair<bool> GetCurrentBools() { throw new NotSupportedException(); }
        public virtual SpanPair<string> GetCurrentStrings() { throw new NotSupportedException(); }
        public virtual SpanPair<T> GetCurrentData<T>() where T : IConvertible { throw new NotImplementedException(); }
    }


}
