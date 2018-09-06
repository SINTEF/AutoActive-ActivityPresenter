using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using SINTEF.AutoActive.Archive;

namespace SINTEF.AutoActive.UI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WelcomePage : ContentPage
	{
        DummyArchive ar = new DummyArchive("title", "path");
        ObservableCollection<DummyArchive> arList = new ObservableCollection<DummyArchive>();

        public WelcomePage ()
		{
			InitializeComponent ();
            EmployeeView.ItemsSource = arList;

            arList.Add(new DummyArchive("Title1", "Path1"));
            arList.Add(new DummyArchive("Title2", "Path2"));
            arList.Add(new DummyArchive("Title3", "Path3"));
        }

        async void OnButtonClicked(object sender, EventArgs args) {
            var nextPage = new ArchivePage(new DummyArchive("Title1", "Path1"));
            await Navigation.PushAsync(nextPage);
        }

        async void OnItemSelected(object sender, EventArgs args)
        {
            var nextPage = new ArchivePage(new DummyArchive("Title1", "Path1"));
            await Navigation.PushAsync(nextPage);
        }
	}
}