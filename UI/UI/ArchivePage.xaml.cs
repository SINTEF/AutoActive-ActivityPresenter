using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class ArchivePage : ContentPage
	{
		public ArchivePage ()
		{
			InitializeComponent ();
		}

        async void OnShowButtonClicked(Object sender, EventArgs args)
        {
            await Navigation.PushAsync(new MainPage());
        }
	}
}