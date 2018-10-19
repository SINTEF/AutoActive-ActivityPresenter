using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    //public delegate void DataViewHasDataRangeChangedHandler(double from, double to);
    public delegate void TimeViewerWasChangedHandler(ITimeViewer sender, long start, long end);

    public interface ITimeViewer
    {
        ITimePoint TimePoint { get; }

        long Start { get; }
        long End { get; }

        event TimeViewerWasChangedHandler TimeChanged;

        void UpdatedTimeIndex();
    }
}
