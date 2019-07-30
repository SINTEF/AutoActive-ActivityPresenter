using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.FileSystem
{
    public interface IFileBrowser
    {
        Task<IReadWriteSeekStreamFactory> BrowseForLoad((string, string) extensionDescription = default);

        // TODO: Should also have some helpers to find supported files from plugins
        Task<IReadOnlyList<IReadSeekStreamFactory>> BrowseForImportFiles();

        Task<IReadWriteSeekStreamFactory> BrowseForSave((string, string) extensionDescription = default, string filename = null);

    }
}
