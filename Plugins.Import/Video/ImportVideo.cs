using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MetadataExtractor;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.UI.Helpers;

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
            var importer = new VideoImporter(readerFactory);
            var stream = await readerFactory.GetReadStream();
            importer.ParseFile(stream);
            return importer;
        }
    }

    public class VideoImporter : BaseDataProvider
    {
        public VideoImporter(IReadSeekStreamFactory readerFactory)
        {
            _readerFactory = readerFactory;

            Name = "Imported Video";
        }

        private IReadSeekStreamFactory _readerFactory;
        public string GetCreatedProperty(Stream stream)
        {
            var metaData = ImageMetadataReader.ReadMetadata(stream);

            return (from data in metaData
                    from el in data.Tags
                    where el.Name.Contains("Created")
                    select el.Description).FirstOrDefault();
        }
        public static bool TryParseDateTime(string dateTimeStr, out DateTime date)
        {
            var culture = CultureInfo.GetCultureInfo("en-US");

            return DateTime.TryParseExact(dateTimeStr, "ddd MMM dd HH:mm:ss yyyy", culture,
                DateTimeStyles.None, out date);
        }

        public long GetCreatedTime(Stream stream)
        {
            var property = GetCreatedProperty(stream);
            return property != null && TryParseDateTime(property, out var date) ? TimeFormatter.TimeFromDateTime(date) : 0L;
        }


        public void Close()
        {
            _readerFactory?.Close();
        }

        protected override void DoParseFile(Stream stream)
        {
            var startTime = GetCreatedTime(stream);

            var jsonRoot = new JObject
            {
                ["meta"] = new JObject
                {
                    ["start_time"] = startTime
                },
                ["user"] = new JObject()
            };

            var video = new ArchiveVideo(jsonRoot, _readerFactory);
            AddChild(video);
        }
    }
}
