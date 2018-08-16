using System;
using System.Collections.Generic;
using System.Text;

namespace Databus
{
    public interface IDataPoint
    {
        // TODO: Define

        Type Type { get; }

        // Type
        // Name
        // Path

        // Other metadata?

        IDataViewer CreateViewerIn(DataViewerContext context);
    }
}
