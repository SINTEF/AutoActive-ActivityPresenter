using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Pages.Player;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.HeadToHead
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HeadToHead : ContentPage
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
    }
}