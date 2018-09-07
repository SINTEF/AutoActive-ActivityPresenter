using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using SINTEF.AutoActive.Archive;

namespace SINTEF.AutoActive.UI
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class ArchivePage : ContentPage
	{
        private DummyArchive _archive;
        public DummyArchive Archive { get => _archive; set { _archive = value;  } }

		public ArchivePage (DummyArchive Ar)
		{
            Archive = Ar;
			InitializeComponent ();

            TitleLabel.BindingContext = Archive;
            TitleLabel.SetBinding(Label.TextProperty, "Title");
            PathLabel.BindingContext = Archive;
            PathLabel.SetBinding(Label.TextProperty, "Path");

            fileList.ItemsSource = Ar.Files;
		}

        async void OnShowButtonClicked(Object sender, EventArgs args)
        {
            await Navigation.PushAsync(new MainPage());
        }
	}
}