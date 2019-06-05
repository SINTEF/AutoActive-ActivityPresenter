using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.Pages.Player;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.HeadToHead
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HeadToHead : ContentPage, ISerializableView
    {
        private const string SelectedText = "[X]";
        private const string UnselectedText = "[ ]";

        private Button _selectedButton;
        private Button SelectedButton
        {
            get => _selectedButton;
            set
            {
                var oldButton = _selectedButton;
                if (oldButton != null) XamarinHelpers.EnsureMainThread(() => oldButton.Text = UnselectedText);
                _selectedButton = value;
                if (value != null) XamarinHelpers.EnsureMainThread(() => value.Text = SelectedText);
            }
        }
        private readonly Dictionary<Button, (TimeSynchronizedContext, PlayerGridLayout)> _dictionary = new Dictionary<Button, (TimeSynchronizedContext, PlayerGridLayout)>();

        public HeadToHead()
        {
            InitializeComponent();
            TreeView.DataPointTapped += TreeViewOnDataPointTapped;

            var masterContext = new TimeSynchronizedContext();
            _dictionary[LeftButton] = (masterContext, LeftGrid);

            var slaveContext = new SynchronizationContext(masterContext);
            OffsetSlider.OffsetChanged += (sender, args) => slaveContext.Offset = TimeFormatter.TimeFromSeconds(args.NewValue);
            _dictionary[RightButton] = (slaveContext, RightGrid);

            SelectButton_Clicked(LeftButton, new EventArgs());

            Playbar.ViewerContext = masterContext;
        }

        private void TreeViewOnDataPointTapped(object sender, IDataPoint dataPoint)
        {
            var (context, grid) = _dictionary[SelectedButton];
            grid.TogglePlotFor(dataPoint, context);
        }

        private void SelectButton_Clicked(object sender, EventArgs e)
        {
            if (!(sender is Button senderButton)) return;
            SelectedButton = senderButton;
        }

        private async void SaveView_OnClicked(object sender, EventArgs e)
        {
            await SaveView();
        }

        private async void LoadView_OnClicked(object sender, EventArgs e)
        {
            await LoadView();
        }


        private async Task SaveView()
        {
            var browser = DependencyService.Get<IFileBrowser>();
            if (browser == null)
            {
                await XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("File save error", "Could get file browser.", "OK");
                return;
            }

            var file = await browser.BrowseForSave((".aav", "AutoActive View"));
            if (file == null) return;

            var root = new JObject
            {
                ["Head2Head"] = SerializeView()
            };

            var stream = await file.GetReadWriteStream();

            using(var streamWriter = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(streamWriter))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, root);
            }
        }
        private async Task LoadView()
        {
            var browser = DependencyService.Get<IFileBrowser>();
            if (browser == null)
            {
                await XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("File open error", "Could get file browser.", "OK");
                return;
            }

            var file = await browser.BrowseForLoad((".aav", "AutoActive View"));
            if (file == null)
            {
                return;
            }

            var stream = await file.GetReadStream();

            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                var serializer = new JsonSerializer();
                var json = (JObject)serializer.Deserialize(reader);
                if (!(json.TryGetValue("Head2Head", out var headToHeadRaw) && headToHeadRaw is JObject headToHead))
                {
                    await XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("View load error",
                        "Could not find info about Head2Head view.", "OK");
                    return;
                }

                DeserializeView(headToHead);
            }
        }

        public void DeserializeView(JObject root)
        {
            if (root.TryGetValue("Left", out var leftRaw) && leftRaw is JObject left)
            {
                LeftGrid.DeserializeView(left);
            }

            if (root.TryGetValue("Right", out var rightRaw) && rightRaw is JObject right)
            {
                RightGrid.DeserializeView(right);

                if (right.TryGetValue("Offset", out var offset))
                {
                    OffsetSlider.Offset = offset.Value<double>();
                }
            }

        }

        public JObject SerializeView(JObject root = null)
        {
            if (root == null)
            {
                root = new JObject();
            }

            root["Left"] = LeftGrid.SerializeView();
            root["Right"] = RightGrid.SerializeView();
            root["Right"]["Offset"] = OffsetSlider.Offset;


            return root;
        }
    }
}