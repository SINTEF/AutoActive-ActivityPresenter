using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface ITimePoint
    {
        bool IsSynchronizedToWorldClock { get; }

        // TODO: If it is constant or changing?
        Task<ITimeViewer> CreateViewer();
    }
}
