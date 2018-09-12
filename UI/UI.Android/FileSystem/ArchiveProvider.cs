using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Provider;
using Android.Database;
using Java.IO;
using SINTEF.AutoActive.UI.FileSystem;

namespace SINTEF.AutoActive.UI.Droid.FileSystem
{
    //[ContentProvider(new[] { "no.sintef.autoactive.archives" }, Exported = true, GrantUriPermissions = true, Enabled = true, Permission = "android.permission.MANAGE_DOCUMENTS")]
    //[IntentFilter(new [] { "android.content.action.DOCUMENTS_PROVIDER" })]
    [Register("no.sintef.autoactive.ArchivesProvider")]
    public class ArchivesProvider : DocumentsProvider
    {

        static readonly string[] DEFAULT_ROOT_PROJECTION = new string[]
        {
            DocumentsContract.Root.ColumnRootId,
            //DocumentsContract.Root.ColumnMimeTypes,
            DocumentsContract.Root.ColumnFlags,
            DocumentsContract.Root.ColumnIcon,
            DocumentsContract.Root.ColumnTitle,
            //DocumentsContract.Root.ColumnSummary,
            DocumentsContract.Root.ColumnDocumentId,
            //DocumentsContract.Root.ColumnAvailableBytes,
            //DocumentsContract.Root.ColumnCapacityBytes,
        };

        static readonly string[] DEFAULT_DOCUMENT_PROJECTION = new string[]
        {
            DocumentsContract.Document.ColumnDocumentId,
            DocumentsContract.Document.ColumnMimeType,
            DocumentsContract.Document.ColumnDisplayName,
            DocumentsContract.Document.ColumnLastModified,
            DocumentsContract.Document.ColumnFlags,
            DocumentsContract.Document.ColumnSize,
            //DocumentsContract.Document.ColumnIcon,
            //DocumentsContract.Document.ColumnSummary,
        };

        File archivesRoot;

        public override bool OnCreate()
        {
            archivesRoot = Context.FilesDir;

            var allsessions = SessionDatabase.All;

            System.Diagnostics.Debug.WriteLine($"CREATING DOCUMENT PROVIDER - {archivesRoot.Path}");
            return true;
        }

        public override ICursor QueryRoots(string[] projection)
        {
            System.Diagnostics.Debug.WriteLine($"QUERY ROOTS {projection}");

            var result = new MatrixCursor(projection ?? DEFAULT_ROOT_PROJECTION);

            var row = result.NewRow();
            row.Add(DocumentsContract.Root.ColumnRootId, "root");
            //row.Add(DocumentsContract.Root.ColumnSummary, "AutoActive archives");
            row.Add(DocumentsContract.Root.ColumnFlags, (int)DocumentRootFlags.LocalOnly);
            row.Add(DocumentsContract.Root.ColumnIcon, Resource.Drawable.Icon);
            row.Add(DocumentsContract.Root.ColumnTitle, "AutoActive");
            row.Add(DocumentsContract.Root.ColumnDocumentId, GetDocIDForFile(archivesRoot));
            //row.Add(DocumentsContract.Root.ColumnMimeTypes, "application/octet-stream\n*/*");
            //row.Add(DocumentsContract.Root.ColumnAvailableBytes, archivesRoot.FreeSpace);

            return result;
        }

        public override ICursor QueryChildDocuments(string parentDocumentId, string[] projection, string sortOrder)
        {
            System.Diagnostics.Debug.WriteLine($"QUERY CHILD DOCUMENTS {parentDocumentId} - {projection} - {sortOrder}");

            var result = new MatrixCursor(projection ?? DEFAULT_DOCUMENT_PROJECTION);

            var row = result.NewRow();
            row.Add(DocumentsContract.Document.ColumnDocumentId, "root/testfile");
            row.Add(DocumentsContract.Document.ColumnMimeType, "application/octet-stream");
            row.Add(DocumentsContract.Document.ColumnDisplayName, "Test file");
            row.Add(DocumentsContract.Document.ColumnLastModified, null);
            row.Add(DocumentsContract.Document.ColumnFlags, (int)DocumentContractFlags.SupportsMove);
            row.Add(DocumentsContract.Document.ColumnSize, null);

            return result;
        }

        public override ICursor QueryDocument(string documentId, string[] projection)
        {
            System.Diagnostics.Debug.WriteLine($"QUERY DOCUMENT {documentId} - {projection}");

            var result = new MatrixCursor(projection ?? DEFAULT_DOCUMENT_PROJECTION);

            return result;
        }

        public override ParcelFileDescriptor OpenDocument(string documentId, string mode, CancellationSignal signal)
        {
            System.Diagnostics.Debug.WriteLine($"OPEN DOCUMENT {documentId} - {mode} - {signal}");

            throw new NotImplementedException();
        }

        /* -- Helpers -- */
        string GetDocIDForFile(File file)
        {
            var path = file.AbsolutePath;
            var rootPath = archivesRoot.AbsolutePath;
            if (path == rootPath)
            {
                path = "";
            }
            else if (rootPath.EndsWith("/"))
            {
                path = path.Substring(rootPath.Length);
            }
            else
            {
                path = path.Substring(rootPath.Length + 1);
            }
            return "root:" + path;
        }
    }
}