using SINTEF.AutoActive.UI.Views;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface IFigureContainer
    {
        FigureView Selected { get; set; }

        void RemoveChild(FigureView figureView);
    }
}
