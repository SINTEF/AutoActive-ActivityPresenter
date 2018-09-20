using System.IO;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.FileSystem
{
    public interface IReadSeekStreamFactory
    {
        string Name { get; }
        string Extension { get; }
        string Mime { get; }
        Task<Stream> GetReadStream();
    }

    public interface IReadWriteSeekStreamFactory : IReadSeekStreamFactory
    {
        Task<Stream> GetReadWriteStream();
    }
}
