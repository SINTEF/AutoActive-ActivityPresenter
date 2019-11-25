using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Directory = MetadataExtractor.Directory;

namespace SINTEF.AutoActive.Plugins.Import.Video
{
    [ImportPlugin(".mov")]
    [ImportPlugin(".avi")]
    [ImportPlugin(".mkv")]
    [ImportPlugin(".mp4")]
    [ImportPlugin(".mts")]
    public class ImportVideoPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory,
            Dictionary<string, object> parameters)
        {
            var importer = new VideoImporter(readerFactory, parameters);
            var stream = await readerFactory.GetReadStream();
            importer.ParseFile(stream);
            return importer;
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {
            parameters["CreatedAtStart"] = (true, "Created time is at the start of the video file");
        }

        public Task<bool> CanParse(IReadSeekStreamFactory readerFactory)
        {
            //TODO(sigurdal): verify format?
            return Task.FromResult(true);
        }
    }

    public class VideoImporter : BaseDataProvider
    {
        private readonly Dictionary<string, object> _parameters;
        private readonly IReadSeekStreamFactory _readerFactory;
        private IReadOnlyList<Directory> _metaData;

        private IReadOnlyList<Directory> GetMetaData(Stream stream)
        {
            return _metaData ?? (_metaData = ImageMetadataReader.ReadMetadata(stream));
        }

        public VideoImporter(IReadSeekStreamFactory readerFactory, Dictionary<string, object> parameters)
        {
            _readerFactory = readerFactory;
            _parameters = parameters;
        }

        public string GetProperty(Stream stream, string name)
        {
            return (from data in GetMetaData(stream)
                from el in data.Tags
                where el.Name.Contains(name)
                select el.Description).FirstOrDefault();
        }

        public static bool TryParseDateTime(string dateTimeStr, out DateTime date)
        {
            return DateTime.TryParseExact(dateTimeStr, "ddd MMM dd HH:mm:ss yyyy", CultureInfo.CurrentCulture,
                DateTimeStyles.None, out date);
        }


        public long GetCreatedTime(Stream stream)
        {
            var property = GetProperty(stream, "Created");
            if (!TryParseDateTime(property, out var date))
            {
                return 0L;
            }

            return date.Year <= 1970 ? 0L : TimeFormatter.TimeFromDateTime(date);
        }

        public long GetVideoLength(Stream stream)
        {
            var property = GetProperty(stream, "Duration");
            return property != null && TimeSpan.TryParse(property, out var date) ? TimeFormatter.TimeFromTimeSpan(date) : 0L;
        }

        protected override void DoParseFile(Stream stream)
        {
            Name = _parameters["Name"] as string;

            if (string.IsNullOrEmpty(Name))
            {
                Name = "Imported Video";
            }

            var startTime = 0L;
            try
            {
                startTime = GetCreatedTime(stream);

                if (!(bool) _parameters["CreatedAtStart"])
                {
                    var length = GetVideoLength(stream);
                    startTime -= length;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not extract created time from {Name}: {ex}");
            }

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
