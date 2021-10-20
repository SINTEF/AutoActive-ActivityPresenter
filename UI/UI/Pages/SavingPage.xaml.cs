using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.Views.TreeView;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Plugins.Import;
using Parquet.Data;
using System.IO;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using ICSharpCode.SharpZipLib.Zip;

namespace SINTEF.AutoActive.UI.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SavingPage : ContentPage
    {
        private readonly IFileBrowser _browser;
        private bool _isSaving;
        private bool _treeMightHaveChanged;

        public SavingPage()
        {
            InitializeComponent();
            NavigationBar.SaveArchiveButton.BackgroundColor = Color.FromHex("23A2B1");
            DataRegistry.ProviderAdded += el => DataTree.Tree.Children.Add(el);
            DataRegistry.ProviderRemoved += el => DataTree.Tree.Children.Remove(el);

            // Add current items
            foreach (var dataProvider in DataRegistry.Providers)
                DataTree.Tree.Children.Add(dataProvider);

            SavingTree.ItemDroppedOn += SavingTreeOnItemDroppedOn;
            RemovalTree.ItemDroppedOn += RemovalTreeOnItemDroppedOn;

            _browser = DependencyService.Get<IFileBrowser>();
            if (_browser == null)
            {
                XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("Critical error", "Could get file browser. Will not be able to open and save files.", "OK");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            SaveComplete += OnSaveComplete;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            SaveComplete -= OnSaveComplete;
        }

        protected override bool OnBackButtonPressed()
        {
            var ret = base.OnBackButtonPressed();

            return CheckBeforeExit(ret);
        }

        public bool ExitShouldBeInterrupted(bool ret, Action exitCommand)
        {
            if (_isSaving)
            {
                var displayTask = DisplayAlert("Saving in progress",
                    "Saving is in progress.\nQuitting might corrupt the archive being saved.\nDo you want to quit anyways?",
                    "Quit", "Wait");
                displayTask.ContinueWith(task =>
                {
                    if (displayTask.Result)
                    {
                        // Switch back to player page (main page)
                        XamarinHelpers.EnsureMainThread(exitCommand);
                    }
                });
                return true;
            }

            if (_treeMightHaveChanged)
            {
                var displayAlert = DisplayAlert("Unsaved data", "There might be unsaved data.\n\nAre sure you want to quit?",
                    "Quit", "Cancel");
                displayAlert.ContinueWith(task =>
                {
                    if (displayAlert.Result)
                    {
                        // Switch back to player page (main page)
                        XamarinHelpers.EnsureMainThread(exitCommand);
                    }
                });
                return true;
            }
            return false;
        }

        public bool CheckBeforeExit(bool ret)
        {
            return ExitShouldBeInterrupted(ret, async () => await Navigation.PopAsync());
        }

        private static bool IsOwnParent(MovableObject target, MovableObject item)
        {
            var parent = XamarinHelpers.GetTypedElementFromParents<MovableObject>(target.Parent);
            while (parent != null)
            {
                if (parent == item)
                    return true;
                parent = XamarinHelpers.GetTypedElementFromParents<MovableObject>(parent.Parent);
            }

            return false;
        }

        private static void MoveElementInsideTree(MovableObject target, MovableObject item)
        {
            if (target.Element.DataPoint != null || item.Element.DataPoint != null)
            {
                Debug.WriteLine("Reordering of data points not implemented yet");
                return;
            }

            if (IsOwnParent(target, item))
            {
                Debug.WriteLine("You shouldn't become your own grandmother.");
                return;
            }

            // If immediate element of parent, move to top
            if (target.Element.DataStructure.Children.Contains(item.Element.DataStructure))
            {
                var ix = target.Element.DataStructure.Children.IndexOf(item.Element.DataStructure);
                if (ix == 0) return;
                target.Element.DataStructure.Children.Move(ix, 0);
                return;
            }

            var parent = XamarinHelpers.GetTypedElementFromParents<MovableObject>(item.Parent);
            if (parent != null)
            {
                parent.Element.DataStructure.Children.Remove(item.Element.DataStructure);
            }
            else {
                var treeParent = XamarinHelpers.GetTypedElementFromParents<DataTreeView>(item.Parent);
                if (treeParent == null)
                {
                    Debug.WriteLine("Unknown parent");
                    return;
                }
                treeParent.Tree.Children.Remove(item.Element.DataStructure);
            }
            target.Element.DataStructure.Children.Add(item.Element.DataStructure);
        }

        private static async void MoveElementInsideTreeT(DataTreeView target, MovableObject branchItem)
        {
            if (target.Tree.Children.Count == 0)
            {
                return;
            }

            if (!(branchItem is FolderView))
            {
                await XamarinHelpers.ShowOkMessage("Error", $"First element in tree must be a Folder");
                return;
            }

            var ix = target.Tree.Children.IndexOf(branchItem.Element.DataStructure);
            if (ix != -1)
            {
                if (ix == target.Tree.Children.Count - 1)
                {
                    return;
                }

                target.Tree.Children.Move(ix, target.Tree.Children.Count - 1);
                return;
            }

            var branchParent = XamarinHelpers.GetTypedElementFromParents<MovableObject>(branchItem.Parent);
            branchParent.Element.DataStructure.Children.Remove(branchItem.Element.DataStructure);
            target.Tree.Children.Add(branchItem.Element.DataStructure);
        }

        private async void SavingTreeOnItemDroppedOn(object sender, (DataTreeView parent, IDropCollector target, IDraggable item) args)
        {
            var (parent, target, item) = args;

            if (target == item)
            {
                return;
            }

            if (!(item is MovableObject branchItem))
            {
                Debug.WriteLine($"Unknown dragged item: {item}");
                return;
            }

            _treeMightHaveChanged = true;

            if (parent == SavingTree)
            {
                if (target == SavingTree)
                {
                    MoveElementInsideTreeT(SavingTree, branchItem);
                    return;
                }

                if (!(target is MovableObject branchTarget))
                {
                    Debug.WriteLine($"Unknown target {target}");
                    return;
                }
                MoveElementInsideTree(branchTarget, branchItem);
                return;
            }

            if (target != SavingTree)
            {
                AddChild(target, branchItem);

                return;
            }

            if (branchItem is FolderView)
            {
                SavingTree.Tree.Children.Add(branchItem.Element.DataStructure);
            }
            else
            {
                await XamarinHelpers.ShowOkMessage("Error", $"First element in tree must be a Folder");
                return;
            }
        }

        private void RemovalTreeOnItemDroppedOn(object sender, (DataTreeView parent, IDropCollector container, IDraggable item) args)
        {
            var (parent, target, item) = args;

            if (target == item)
            {
                return;
            }

            if (!(item is MovableObject branchItem))
            {
                Debug.WriteLine($"Unknown dragged item: {item}");
                return;
            }


            if (parent != SavingTree)
            {
                return;
            }

            _treeMightHaveChanged = true;

            var branchParent = XamarinHelpers.GetTypedElementFromParents<MovableObject>(branchItem.Parent);
            if (item is DataPointView)
            {
                branchParent.Element.DataStructure.RemoveDataPoint(branchItem.Element.DataPoint);
                return;
            }

            if (branchParent == null)
            {
                var dataTreeView = XamarinHelpers.GetTypedElementFromParents<DataTreeView>(branchItem.Parent);

                if (dataTreeView == null)
                {
                    Debug.WriteLine("Unkown parent");
                    return;
                }

                dataTreeView.Tree.Children.Remove(branchItem.Element.DataStructure);
            }
            else
            {
                branchParent.Element.DataStructure.RemoveChild(branchItem.Element.DataStructure);
            }
        }

        private void AddChild(IDropCollector target, MovableObject branchItem)
        {
            if (!(target is MovableObject branchTarget))
            {
                Debug.WriteLine("Illegal target");
                return;
            }

            if (!(branchTarget.Element.DataStructure is IDataStructure structure))
            {
                Debug.WriteLine("Illegal target (must be DataStructure)");
                return;
            }

            if (branchItem.Element.DataPoint != null)
            {
                structure.DataPoints.Add(branchItem.Element.DataPoint);
            }
            else
            {
                structure.Children.Add(branchItem.Element.DataStructure);
            }
        }

        private void AddFolderClicked(object sender, EventArgs e)
        {
            _treeMightHaveChanged = true;
            var folder = new TemporaryFolder("New Folder");
            SavingTree.Tree.Children.Add(folder);
        }

        private async void SaveButtonClicked(object sender, EventArgs e)
        {
            _isSaving = true;
            SavingLabel.Text = "Saving";
            SavingProgress.Progress = 0;
            if (!await VerifyArchive(SavingTree.Tree))
            {
                _isSaving = false;
                return;
            }
            SaveButton.IsEnabled = false;
            await SaveArchive(SavingTree.Tree);
        }

        private async Task<bool> VerifyArchive(DataTree sessions)
        {
            foreach (var session in sessions)
            {
                if (string.IsNullOrWhiteSpace(session.Name))
                {
                    await XamarinHelpers.ShowOkMessage("Session name is empty", "Session and data structure name can't be empty or only whitespace.", this);
                    return false;
                }

                if (session.Children.Count == 0)
                {
                    await XamarinHelpers.ShowOkMessage("Empty session", "Can't save sessions without any child elements", this);
                    return false;
                }

                if (!await VerifySessionStructure(session))
                {
                    return false;
                }

                var (verRet, verMsg) = VerifyChildren(session.Children);
                if (verRet) continue;

                await XamarinHelpers.ShowOkMessage("Invalid structure names", $"Sessions and data structures can't contain sibling elements with the same name ({session.Name} - {verMsg})", this);
                return false;
            }
            return true;
        }


        private (bool, string) VerifyChildren(ObservableCollection<IDataStructure> sessionChildren)
        {
            var names = new HashSet<string>();
            var duplicated = new HashSet<string>();
            foreach(var child in sessionChildren)
            {

                if (string.IsNullOrWhiteSpace(child.Name)) return (false, "empty name");
                if (!names.Add(child.Name))
                {
                    duplicated.Add(child.Name);
                }
            }

            return (names.Count == sessionChildren.Count, string.Join(",", duplicated));
        }

        private async Task<bool> VerifySessionStructure(IDataStructure dataStructure)
        {
            bool saveSession = true;
            foreach (IDataStructure childStructure in dataStructure.Children)
            {
                if (!saveSession)
                {
                    return saveSession;
                }

                if (childStructure.Children.Count > 0)
                {
                    saveSession = await VerifySessionStructure(childStructure);
                }
                else
                {
                    saveSession = await childStructure.VerifyStructure();

                }
            }
            return saveSession;
        }


        private void AddAllClicked(object sender, EventArgs e)
        {
            _treeMightHaveChanged = true;
            foreach (var el in DataTree.Tree)
            {
                SavingTree.Tree.Children.Add(el);
            }
        }

        private void OnSaveComplete(object sender, SaveCompleteArgs args)
        {
            XamarinHelpers.EnsureMainThread(async () =>
            {
                _isSaving = false;
                SavingLabel.Text = "";
                SaveButton.IsEnabled = true;
                SavingProgress.Progress = 1;

                switch (args.Status)
                {
                    case SaveStatus.Cancel:
                        return;
                    case SaveStatus.Failure:
                        await DisplayAlert("Save failed", args.Message, "OK");
                        return;
                    case SaveStatus.Success:
                        _treeMightHaveChanged = false;
                        await DisplayAlert("Saving done", "Save completed successfully", "OK");
                        return;
                }
            });
        }

        public event EventHandler<SaveCompleteArgs> SaveComplete;

        private async Task SaveArchive(DataTree dataTree)
        {
            // TODO: add option for saving existing sessions
            foreach (var el in SavingTree.Tree)
            {
                Debug.WriteLine(el.Name);
                foreach (var child in el.Children)
                {
                    Debug.WriteLine($" -> s: {child.Name}");
                }
                foreach (var child in el.DataPoints)
                {
                    Debug.WriteLine($" -> p: {child.Name}");
                }
            }

            if (!dataTree.Any())
            {
                SaveComplete?.Invoke(this, new SaveCompleteArgs(SaveStatus.Failure, "No data selected for save."));
                return;
            }

            var file = await _browser.BrowseForSave();

            if (file == null)
            {
                SaveComplete?.Invoke(this, new SaveCompleteArgs(SaveStatus.Cancel, "Save cancelled"));
                return;
            }


            new Thread(async () =>
                {
                    await WriteArchive(dataTree, file);
                }
            ).Start();
        }

        private async Task WriteArchive(DataTree dataTree, IReadWriteSeekStreamFactory file)
        {
            var stream = await file.GetReadWriteStream();

            var archive = Archive.Archive.Create(stream);

            foreach (var dataStructure in dataTree)
            {
                var session = ArchiveSession.Create(archive, dataStructure.Name);
                foreach (var child in dataStructure.Children)
                {
                    session.AddChild(child);
                }

                foreach (var dataPoint in dataStructure.DataPoints)
                {
                    session.AddDataPoint(dataPoint);
                }

                archive.AddSession(session);
            }

            archive.SavingProgressChanged += ArchiveOnSavingProgress;
            await archive.WriteFile();
            archive.SavingProgressChanged -= ArchiveOnSavingProgress;
            archive.Close();
            file.Close();
            if (archive.ErrorList.Any())
            {
                var msg = "";

                foreach (var (name, ex) in archive.ErrorList)
                {
                    msg += $"  {name} - {ex.Message}";
                }

                SaveComplete?.Invoke(this, new SaveCompleteArgs(SaveStatus.Failure, $"Error when saving:\n\n{msg}"));
                return;
            }

            SaveComplete?.Invoke(this, new SaveCompleteArgs(SaveStatus.Success, "success"));
        }

        private void ArchiveOnSavingProgress(object sender, double progress)
        {
            XamarinHelpers.EnsureMainThread(() => SavingProgress.Progress = progress);
        }

        public class SaveCompleteArgs
        {
            public SaveCompleteArgs(SaveStatus status, string message)
            {
                Status = status;
                Message = message;
            }

            public string Message;
            public SaveStatus Status;
        }

        public enum SaveStatus
        {
            Success, Failure, Cancel
        }

        private void ClearClicked(object sender, EventArgs e)
        {
            _treeMightHaveChanged = false;
            SavingTree.Tree = new DataTree();
        }
    }




}