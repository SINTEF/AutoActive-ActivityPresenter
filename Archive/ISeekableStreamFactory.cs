using System.IO;

namespace Archive
{
    public interface ISeekableStreamFactory
    {
        Stream GetStream();
    }
}
