using SINTEF.AutoActive.Databus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Player
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class PlaybarView : ContentView
	{
        public static readonly GridLength DefaultHeight = 40;

        public DataViewerContext ViewerContext { get; set; }

        public PlaybarView ()
		{
			InitializeComponent ();
		}

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            ViewerContext.UpdateRange(e.NewValue, e.NewValue + 100);
        }
    }
}