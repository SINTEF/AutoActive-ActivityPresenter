using System.Threading.Tasks;

namespace SINTEF.AutoActive.FileSystem
{
    public interface IFileBrowser
    {
        Task<IReadWriteSeekStreamFactory> BrowseForArchive();

        // TODO: Should also have some helpers to find supported files from plugins
        Task<IReadSeekStreamFactory> BrowseForImportFile();

        Task<IReadWriteSeekStreamFactory> BrowseForSave();
    }
}
