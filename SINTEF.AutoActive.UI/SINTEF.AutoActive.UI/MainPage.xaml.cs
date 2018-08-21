using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            MockData.MockData.Context.UpdateRange(e.NewValue / 100, e.NewValue / 100 + 10);
        }
    }
}
