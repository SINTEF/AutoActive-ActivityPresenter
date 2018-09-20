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

        public PlaybarView ()
		{
			InitializeComponent ();
		}
	}
}