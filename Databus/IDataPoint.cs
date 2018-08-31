using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus
{
    public interface IDataPoint
    {
        // TODO: Define

        Type Type { get; }

        string Name { get; set; }

        // Type
        // Name
        // Path

        // Other metadata?

        Task<IDataViewer> CreateViewerIn(DataViewerContext context);
    }
}
