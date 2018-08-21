﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus
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
