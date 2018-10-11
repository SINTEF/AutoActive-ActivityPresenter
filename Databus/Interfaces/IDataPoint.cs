using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface IDataPoint
    {
        Type DataType { get; }

        string Name { get; set; }

        ITimePoint Time { get; }

        // Other metadata?

        Task<IDataViewer> CreateViewer();
    }
}
