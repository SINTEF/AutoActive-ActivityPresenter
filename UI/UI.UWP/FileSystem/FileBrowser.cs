using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.Import;
using SINTEF.AutoActive.UI.UWP.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Xamarin.Forms;

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

        public string Name { get; }
        public string Extension { get; }
        public string Mime { get; }

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
            catch (Exception)
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

    internal class ReadWriteSeekStreamFactory : ReadSeekStreamFactory, IReadWriteSeekStreamFactory
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
        private const string DefaultExtension = ".aaz";

        public async Task<IReadWriteSeekStreamFactory> BrowseForLoad((string, string) extensionDescription = default((string, string)))
        {
            var picker = new FileOpenPicker();
            if (extensionDescription.Item1 == default(string) && extensionDescription.Item2 == default(string))
                picker.FileTypeFilter.Add(DefaultExtension);
            else
            {
                var (extension, _) = extensionDescription;
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();
            return file == null ? null : new ReadWriteSeekStreamFactory(file);
        }

        public async Task<IReadOnlyList<IReadSeekStreamFactory>> BrowseForImportFiles()
        {
            var picker = new FileOpenPicker();
            // Find all extensions we support
            foreach (var (extension, type)  in ImportPlugins.ExtensionTypes)
            {
                // TODO: Perhaps do some checking here, it's a bit picky about the format
                // FIXME: Also, I don't know how well this handles duplicates
                picker.FileTypeFilter.Add(extension);
            }
            var files = await picker.PickMultipleFilesAsync();
            return files.Count == 0 ? null : new List<ReadWriteSeekStreamFactory>(files.Select(file => new ReadWriteSeekStreamFactory(file)));
        }

        public async Task<IReadWriteSeekStreamFactory> BrowseForSave((string, string) extensionDescription = default((string, string)),
            string filename = null)
        {
            var picker = new FileSavePicker();
            if (extensionDescription.Item1 == default(string) && extensionDescription.Item2 == default(string))
                picker.FileTypeChoices.Add("AutoActive archive", new List<string> { DefaultExtension });
            else
            {
                var (extension, description) = extensionDescription;
                picker.FileTypeChoices.Add(description, new List<string> { extension });
            }

            picker.SuggestedFileName = filename ?? DateTime.Now.ToString("yyyy-MM-dd--HH-mm");

            var file = await picker.PickSaveFileAsync();
            return file == null ? null : new ReadWriteSeekStreamFactory(file);
        }

        public async Task<IReadSeekStreamFactory> LoadFromUri(string uri)
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(uri);
            return new ReadWriteSeekStreamFactory(storageFile);
        }
    }
}
