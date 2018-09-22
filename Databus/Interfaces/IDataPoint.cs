using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface IDataPoint
    {
        // TODO: Define

        Type DataType { get; }

        string Name { get; set; }

        // Type
        // Name
        // Path

        // Other metadata?

        Task<IDataViewer> CreateViewerIn(DataViewerContext context);
    }
}
