using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface ITimePoint
    {
        bool IsSynchronizedToWorldClock { get; }

        // TODO: If it is constant or changing?
        Task<ITimeViewer> CreateViewer();

        long Start { get; }
        long End { get; }

        void TransformTime(long offset, double scale);
    }
}
