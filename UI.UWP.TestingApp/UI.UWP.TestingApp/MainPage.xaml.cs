using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SINTEF.AutoActive.UI.Pages.Player;
using Xamarin.Forms;

namespace UI.UWP.TestingApp
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
            Splitter.Orientation = ResizableStackLayout.Orientation;
        }

        private double _splitterStartDragWidth;

        private void Splitter_DragStart(PlayerSplitterView sender)
        {
            _splitterStartDragWidth = sender.Width;
        }

        private void Splitter_Dragged(PlayerSplitterView sender, double x, double y)
        {
            var newWidth = _splitterStartDragWidth - x;
            if (newWidth >= 0 && newWidth + sender.Width <= Width)
            {
                //sender.Width = _treeViewWidth = new GridLength(newWidth);
                Debug.WriteLine(newWidth);
            }
        }

        private void PlayerSplitterView_OnDragStart()
        {
            Debug.WriteLine("Drag start");
        }

        private void PlayerSplitterView_OnDragged(double x, double y)
        {
            Debug.WriteLine($"Dragged {x} x {y}");
        }
    }
}
