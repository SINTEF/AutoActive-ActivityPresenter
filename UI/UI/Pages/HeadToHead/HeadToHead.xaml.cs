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
using SINTEF.AutoActive.UI.Views.DynamicLayout;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.HeadToHead
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HeadToHead : ContentPage, ISerializableView
    {
        public string ViewType => "no.sintef.ui.head2head";
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
        private readonly Dictionary<Button, (TimeSynchronizedContext, PlaceableContainer)> _dictionary = new Dictionary<Button, (TimeSynchronizedContext, PlaceableContainer)>();

        public HeadToHead()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            TreeView.DataPointTapped += TreeViewOnDataPointTapped;

            var masterContext = new TimeSynchronizedContext();
            _dictionary[LeftButton] = (masterContext, LeftGrid);

            var slaveContext = new SynchronizationContext(masterContext);
            OffsetSlider.OffsetChanged += (sender, args) =>
            {
                slaveContext.Offset = TimeFormatter.TimeFromSeconds(args.NewValue);
                Playbar.DataTrackline.InvalidateSurface();
            };
            _dictionary[RightButton] = (slaveContext, RightGrid);

            SelectButton_Clicked(LeftButton, new EventArgs());

            Playbar.ViewerContext = masterContext;
            Playbar.DataTrackline.RegisterFigureContainer(LeftGrid);
            Playbar.DataTrackline.RegisterFigureContainer(RightGrid);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            TreeView.DataPointTapped -= TreeViewOnDataPointTapped;

            Playbar.DataTrackline.DeregisterFigureContainer(LeftGrid);
            Playbar.DataTrackline.DeregisterFigureContainer(RightGrid);
        }

        private void TreeViewOnDataPointTapped(object sender, IDataPoint dataPoint)
        {
            var (context, container) = _dictionary[SelectedButton];

            container.SelectItem(dataPoint, context);
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

                await DeserializeView(headToHead);
            }
        }

        public async Task DeserializeView(JObject root, IDataStructure archive=null)
        {
            await DeserializeView(root, archive, archive);
        }

        public async Task DeserializeView(JObject root, IDataStructure archiveLeft, IDataStructure archiveRight)
        {
            if (root.TryGetValue("Left", out var leftRaw) && leftRaw is JObject left)
            {
                await LeftGrid.DeserializeView(left, archiveLeft);
            }

            if (root.TryGetValue("Right", out var rightRaw) && rightRaw is JObject right)
            {
                await RightGrid.DeserializeView(right, archiveRight);

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