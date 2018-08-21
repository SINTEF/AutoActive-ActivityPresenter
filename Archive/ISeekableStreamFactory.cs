using System.IO;

namespace SINTEF.AutoActive.Archive
{
    public interface ISeekableStreamFactory
    {
        Stream GetStream();
    }
}
