using System;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Views;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface IFigureContainer
    {
        FigureView Selected { get; set; }

        void RemoveChild(FigureView figureView);

        event EventHandler<(IDataPoint, DataViewerContext)> DatapointAdded;
        event EventHandler<(IDataPoint, DataViewerContext)> DatapointRemoved;
    }
}
