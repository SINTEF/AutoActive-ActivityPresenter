using System.IO;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.FileSystem
{
    public interface IReadSeekStreamFactory
    {
        Task<Stream> GetReadStream();
    }

    public interface IReadWriteSeekStreamFactory : IReadSeekStreamFactory
    {
        Task<Stream> GetReadWriteStream();
    }
}
