using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using MetadataExtractor;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.Import.Video
{
    [ImportPlugin(".mov")]
    [ImportPlugin(".avi")]
    [ImportPlugin(".mkv")]
    [ImportPlugin(".mp4")]
    public class ImportVideoPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory)
        {
            var stream = await readerFactory.GetReadStream();
            var metaData = ImageMetadataReader.ReadMetadata(stream);
            foreach (var data in metaData)
            {
                foreach (var el in data.Tags)
                {
                    if (el.Name.Contains("Created"))
                    {
                        Debug.WriteLine(el.Name + ": " + el.Description);
                    }
                }
                Debug.WriteLine(data.Name);
            }

            return null;
        }
    }
}
