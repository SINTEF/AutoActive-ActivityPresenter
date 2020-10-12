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
using SINTEF.AutoActive.UI.Views;
using SINTEF.AutoActive.UI.Views.DynamicLayout;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.HeadToHead
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HeadToHead : ISerializableView
    {
        public string ViewType => "no.sintef.ui.head2head";
        private const string SelectedText = "x";
        private const string UnselectedText = " ";

        private TimeSynchronizedContext _leftContext;
        private SynchronizationContext _rightContext;

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

        private long? _selectedLeftTime;
        private long? SelectedLeftTime
        {
            get => _selectedLeftTime;
            set
            {
                _selectedLeftTime = value;
                LeftTimeButton.Text = _selectedLeftTime.HasValue
                    ? TimeFormatter.FormatTime(_selectedLeftTime.Value, dateSeparator: ' ')
                    : "SET SYNC POINT";
            }
        }

        private long? _selectedRightTime;
        private long? SelectedRightTime
        {
            get => _selectedRightTime;
            set
            {
                _selectedRightTime = value;
                RightTimeButton.Text = _selectedRightTime.HasValue
                    ? TimeFormatter.FormatTime(_selectedRightTime.Value, dateSeparator: ' ')
                    : "SET SYNC POINT";
            }
        }

        public HeadToHead()
        {
            InitializeComponent();
            NavigationBar.Head2HeadPageButton.BackgroundColor = Color.FromHex("23A2B1");
            LeftTimeStepper.GetPlayButton.IsVisible = false;
            RightTimeStepper.GetPlayButton.IsVisible = false;
            LeftTimeStepper.AreButtonsEnabled = false;
            RightTimeStepper.AreButtonsEnabled = false;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            TreeView.DataPointTapped += TreeViewOnDataPointTapped;

            if (_leftContext == null)
            {
                _leftContext = new TimeSynchronizedContext();
                _rightContext = new SynchronizationContext(_leftContext);
            }

            SelectButton_Clicked(LeftButton, new EventArgs());

            Playbar.ViewerContext = _leftContext;
            Playbar.DataTrackline.RegisterFigureContainer(LeftGrid);
            Playbar.DataTrackline.RegisterFigureContainer(RightGrid);

            LeftGrid.DatapointAdded += OnDatapointAdded;
            RightGrid.DatapointAdded += OnDatapointAdded;
            KeyDown += Playbar.KeyDown;
            KeyUp += Playbar.KeyUp;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            TreeView.DataPointTapped -= TreeViewOnDataPointTapped;

            LeftGrid.DatapointAdded -= OnDatapointAdded;
            RightGrid.DatapointAdded -= OnDatapointAdded;

            Playbar.DataTrackline.DeregisterFigureContainer(LeftGrid);
            Playbar.DataTrackline.DeregisterFigureContainer(RightGrid);

            KeyDown -= Playbar.KeyDown;
            KeyUp -= Playbar.KeyUp;
        }

        private void OnDatapointAdded(object sender, (IDataPoint point, DataViewerContext context) args)
        {
            if (args.context == _leftContext)
                LeftTimeButton.IsEnabled = LeftTimeStepper.AreButtonsEnabled = true;
            else
                RightTimeButton.IsEnabled = RightTimeStepper.AreButtonsEnabled = true;

            CommonStart.IsEnabled = LeftTimeButton.IsEnabled && RightTimeButton.IsEnabled;
        }

        private void TreeViewOnDataPointTapped(object sender, IDataPoint dataPoint)
        {
            if (SelectedButton == LeftButton)
            {
                LeftGrid.SelectItem(dataPoint, _leftContext);
                ClearLeft.IsEnabled = true;
            }
            else if (SelectedButton == RightButton)
            {
                RightGrid.SelectItem(dataPoint, _rightContext);
                ClearRight.IsEnabled = true;
            }
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
            if (!await SerializableViewHelper.EnsureViewType(root, this, false, true))
            {
                return;
            }
            if (root.TryGetValue("Left", out var leftRaw) && leftRaw is JObject left)
            {
                LeftGrid.ViewerContext = _leftContext;
                await LeftGrid.DeserializeView(left, archiveLeft);
            }

            if (root.TryGetValue("Right", out var rightRaw) && rightRaw is JObject right)
            {
                RightGrid.ViewerContext = _rightContext;
                await RightGrid.DeserializeView(right, archiveRight);
            }

            if (root.ContainsKey("Offset"))
            {
                _rightContext.Offset = root.Value<long>("Offset");
            }

        }

        public JObject SerializeView(JObject root = null)
        {
            root = SerializableViewHelper.SerializeDefaults(root, this);

            root["Left"] = LeftGrid.SerializeView();
            root["Right"] = RightGrid.SerializeView();
            root["Offset"] = _rightContext.Offset;

            return root;
        }

        private void LeftTimeStepper_OnOnStep(object sender, TimeStepEvent e)
        {
            var context = _leftContext;
            var diff = context.SelectedTimeTo - context.SelectedTimeFrom;
            var offset = e.AsOffset();
            var from = context.SelectedTimeFrom + offset;
            var to = from + diff;
            var (minTime, _) = context.GetAvailableTimeMinMax(true);

            if (from < minTime)
            {
                from = minTime;
                to = from + diff;
            }

            context.SetSelectedTimeRange(from, to);
        }

        private void UpdateSyncButtonVisibility()
        {
            ApplySyncButton.IsEnabled = SelectedLeftTime.HasValue && SelectedRightTime.HasValue;
        }

        private void LeftTimeButton_OnClicked(object sender, EventArgs e)
        {
            SelectedLeftTime = _leftContext.SelectedTimeFrom;

            UpdateSyncButtonVisibility();
        }
        private void RightTimeButton_OnClicked(object sender, EventArgs e)
        {
            SelectedRightTime = _rightContext.SelectedTimeFrom;

            UpdateSyncButtonVisibility();
        }

        private void RightTimeStepper_OnOnStep(object sender, TimeStepEvent e)
        {
            _rightContext.Offset += e.AsOffset();
        }


        private long _totalOffset = 0L;
        private void OnApplySync(object sender, EventArgs e)
        {
            var offset = _selectedRightTime - _selectedLeftTime;
            if (!offset.HasValue) return;
            var offsetDiff = offset.Value - _rightContext.Offset;
            _totalOffset += offsetDiff;
            _rightContext.Offset = _totalOffset;
        }

        private void SetCommonStart_OnClicked(object sender, EventArgs e)
        {
            var (masterMin, _) = _leftContext.GetAvailableTimeMinMax(true);
            var (slaveMin, _) = _rightContext.GetAvailableTimeMinMax(true);

            _rightContext.Offset = slaveMin - masterMin;
            _totalOffset = _rightContext.Offset;
        }

        private void ClearLeft_OnClicked(object sender, EventArgs e)
        {
            var children = XamarinHelpers.GetAllChildElements<FigureView>(LeftGrid);
            foreach (var child in children)
            {
                child.RemoveThisView();
            }

        }

        private void ClearRight_OnClicked(object sender, EventArgs e)
        {
            var children = XamarinHelpers.GetAllChildElements<FigureView>(RightGrid);
            foreach (var child in children)
            {
                child.RemoveThisView();
            }
        }
    }
}