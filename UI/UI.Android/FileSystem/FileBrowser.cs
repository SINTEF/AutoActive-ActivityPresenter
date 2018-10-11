using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.OS.Storage;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Droid.FileSystem;
using Xamarin.Forms;

using Debug = System.Diagnostics.Debug;

[assembly: Dependency(typeof(FileBrowser))]
namespace SINTEF.AutoActive.UI.Droid.FileSystem
{
    class ReadSeekStreamFactory : IReadSeekStreamFactory
    {
        protected string _path;

        public string Name { get; private set; }
        public string Extension { get; private set; }
        public string Mime { get; private set; }


        internal ReadSeekStreamFactory(string path)
        {
            _path = path;
            // TODO: Should we open a reader/writer to ensure no-one changes the file while we are potentially doing other stuff?

            Name = Path.GetFileNameWithoutExtension(path);
            Extension = Path.GetExtension(path);
        }

        public Task<Stream> GetReadStream()
        {
            try
            {
                var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (!stream.CanRead) throw new IOException("Stream must readable");
                if (!stream.CanSeek) throw new IOException("Stream must be seekable");
                return Task.FromResult((Stream)stream);
            }
            catch (Exception ex)
            {
                return Task.FromException<Stream>(ex);
            }
        }
    }

    class ReadWriteSeekStreamFactory : ReadSeekStreamFactory, IReadWriteSeekStreamFactory
    {
        internal ReadWriteSeekStreamFactory(string path) : base(path) { }

        public Task<Stream> GetReadWriteStream()
        {
            try
            {
                Debug.WriteLine("OPENING READ/WRITE STREAM");
                var stream = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (!stream.CanRead) throw new IOException("Stream must readable");
                if (!stream.CanWrite) throw new IOException("Stream must be writable");
                if (!stream.CanSeek) throw new IOException("Stream must be seekable");
                return Task.FromResult((Stream)stream);
            }
            catch (Exception ex)
            {
                return Task.FromException<Stream>(ex);
            }
        }
    }

    public class FileBrowser : IFileBrowser
    {
        // FIXME: Not thread-safe?
        internal static TaskCompletionSource<Android.Net.Uri> uriSource;

        void ListRecursive(Java.IO.File file, string prepend)
        {
            var subFiles = file.ListFiles();
            if (subFiles != null)
            {
                foreach (var sub in subFiles)
                {
                    Debug.WriteLine(prepend + sub.Name);
                    ListRecursive(sub, prepend + "-");
                }
            }
        }

        // FIXME: How can we specify what kind of files we can open?

        public async Task<IReadWriteSeekStreamFactory> BrowseForArchive()
        {
            uriSource = new TaskCompletionSource<Android.Net.Uri>();

            var context = Android.App.Application.Context;
            Intent pickerIntent = new Intent(context, typeof(FileBrowserActivity));
            //pickerIntent.SetFlags(ActivityFlags.NewTask);
            context.StartActivity(pickerIntent);

            // Wait for the URI to be picked
            var uri = await uriSource.Task;

            // Make sure we can open this file locally
            // FIXME: If not, we have to copy it somehwere
            var path = GetPath(context, uri);
            Debug.WriteLine($"PATH TO OPEN: {path}");

            if (path != null && path.Length > 0)
            {
                // TODO: Should we check that the file is actually accessible?
                return new ReadWriteSeekStreamFactory(path);
            }
            return null;
        }

        public async Task<IReadSeekStreamFactory> BrowseForImportFile()
        {
            return await BrowseForArchive();
        }

        /* --- Helpers --- */
        string GetPath(Context context, Android.Net.Uri uri)
        {
            if (DocumentsContract.IsDocumentUri(context, uri))
            {
                var documentId = DocumentsContract.GetDocumentId(uri);
                Debug.WriteLine($"DOCUMENT URI: {uri} !! {documentId}");
                var split = documentId.Split(new[] { ':' }, 2);
                switch (uri.Authority)
                {
                    case "com.android.externalstorage.documents":
                        // Is external storage
                        if (split[0] == "primary") {
                            return Android.OS.Environment.ExternalStorageDirectory + "/" + split[1];
                        }
                        break;
                    case "com.android.providers.downloads.documents":
                        // Downloads folder
                        if (split[0] == "raw")
                        {
                            return split[1];
                        }
                        else
                        {
                            var contentUri = ContentUris.WithAppendedId(Android.Net.Uri.Parse("content://downloads/public_downloads"), long.Parse(documentId));
                            return GetDataColumn(context, contentUri, null, null);
                        }
                    case "com.android.providers.media.documents":
                        // Media document
                        var selection = "_id=?";
                        var selectionArgs = new[] { split[1] };
                        switch (split[0])
                        {
                            case "image":
                                return GetDataColumn(context, MediaStore.Images.Media.ExternalContentUri, selection, selectionArgs);
                            case "video":
                                return GetDataColumn(context, MediaStore.Video.Media.ExternalContentUri, selection, selectionArgs);
                            case "audio":
                                return GetDataColumn(context, MediaStore.Audio.Media.ExternalContentUri, selection, selectionArgs);
                            // TODO: Files!?
                        }
                        break;
                }
            }
            return null;
        }

        string GetDataColumn(Context context, Android.Net.Uri uri, string selection, string[] selectionArgs)
        {
            string data = null;
            var projection = new[] { "_data" };
            var cursor = context.ContentResolver.Query(uri, projection, selection, selectionArgs, null);
            if (cursor != null)
            {
                if (cursor.MoveToFirst())
                {
                    var column = cursor.GetColumnIndex(projection[0]);
                    if (column >= 0)
                    {
                        data = cursor.GetString(column);
                    }
                }
                cursor.Close();
            }
            return data;
        }

    }

    [Activity]
    class FileBrowserActivity : Activity
    {
        static readonly int READ_INTENT_CODE = 8008;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Debug.WriteLine($"Opening Picker");

            Intent intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            StartActivityForResult(intent, READ_INTENT_CODE);
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            try
            {
                if (resultCode == Result.Canceled || data == null)
                {
                    // Cancelled
                    FileBrowser.uriSource.SetCanceled();
                }
                else
                {
                    // Successfull
                    FileBrowser.uriSource.SetResult(data.Data);

                }
            }
            catch (Exception ex)
            {
                // Exception
                FileBrowser.uriSource.SetException(ex);
            }
            finally
            {
                Finish();
            }
        }
    }
}