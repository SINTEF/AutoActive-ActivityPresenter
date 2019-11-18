using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface ITimePoint
    {
        bool IsSynchronizedToWorldClock { get; }

        // TODO: If it is constant or changing?
        Task<ITimeViewer> CreateViewer();

        void TransformTime(long offset, double scale);

    }
}
