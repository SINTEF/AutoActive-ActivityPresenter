using System;
using System.Collections.Generic;
using Xamarin.Forms;
using Windows.Storage.Pickers;

using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.UWP.FileSystem;
using System.Threading.Tasks;
using System.IO;
using Windows.Storage;
using SINTEF.AutoActive.Plugins.Import;

[assembly: Dependency(typeof(FileBrowser))]
namespace SINTEF.AutoActive.UI.UWP.FileSystem
{
    class ReadSeekStreamFactory : IReadSeekStreamFactory
    {
        protected StorageFile File;
        protected List<Stream> Streams = new List<Stream>();

        internal ReadSeekStreamFactory(StorageFile file)
        {
            File = file;
            Name = file.DisplayName;
            Extension = file.FileType;
            Mime = file.ContentType;
            // TODO: Should we open a reader/writer to ensure no-one changes the file while we are potentially doing other stuff?
        }

        public string Name { get; private set; }
        public string Extension { get; private set; }
        public string Mime { get; private set; }

        public async Task<Stream> GetReadStream()
        {
            Stream stream = null;
            try
            {
                stream = await File.OpenStreamForReadAsync();
                if (!stream.CanRead) throw new IOException("Stream must readable");
                if (!stream.CanSeek) throw new IOException("Stream must be seekable");
                Streams.Add(stream);
                return stream;
            }
            catch (Exception ex)
            {
                stream?.Close();
                throw;
            }
        }

        public void Close()
        {
            foreach (var stream in Streams)
            {
                stream.Close();
            }
        }
    }

    class ReadWriteSeekStreamFactory : ReadSeekStreamFactory, IReadWriteSeekStreamFactory
    {
        internal ReadWriteSeekStreamFactory(StorageFile file) : base(file) { }

        public async Task<Stream> GetReadWriteStream()
        {
            Stream stream = null;
            try
            {
                stream = await File.OpenStreamForWriteAsync();
                if (!stream.CanRead) throw new IOException("Stream must readable");
                if (!stream.CanWrite) throw new IOException("Stream must be writable");
                if (!stream.CanSeek) throw new IOException("Stream must be seekable");
                Streams.Add(stream);
                return stream;
            }
            catch (Exception)
            {
                stream?.Close();
                throw;
            }
        }
    }

    public class FileBrowser : IFileBrowser
    {
        private const string Extension = ".aaz";

        public async Task<IReadWriteSeekStreamFactory> BrowseForArchive()
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(Extension);
            var file = await picker.PickSingleFileAsync();
            return file == null ? null : new ReadWriteSeekStreamFactory(file);
        }

        public async Task<IReadSeekStreamFactory> BrowseForImportFile()
        {
            var picker = new FileOpenPicker();
            // Find all extensions we support
            foreach (var extension in ImportPlugins.SupportedExtensions)
            {
                // TODO: Perhaps do some checking here, it's a bit picky about the format
                // FIXME: Also, I don't know how well this handles duplicates
                picker.FileTypeFilter.Add(extension);
            }
            var file = await picker.PickSingleFileAsync();
            return file == null ? null : new ReadWriteSeekStreamFactory(file);
        }

        public async Task<IReadWriteSeekStreamFactory> BrowseForSave()
        {
            var picker = new FileSavePicker();

            picker.FileTypeChoices.Add("AutoActive archive", new List<string> { Extension });
            picker.SuggestedFileName = DateTime.Now.ToString("yyyy-MM-dd--HH-mm");

            var file = await picker.PickSaveFileAsync();
            return file == null ? null : new ReadWriteSeekStreamFactory(file);
        }
    }
}
