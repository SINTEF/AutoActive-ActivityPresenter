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
        ObservableCollection<DummyArchive> arList = new ObservableCollection<DummyArchive>();

        public WelcomePage ()
		{
			InitializeComponent ();
            EmployeeView.ItemsSource = arList;

            arList.Add(new DummyArchive("Garmin", "C:/Garmin", new[] { "Pulse", "Cadence", "Speed", "Temperature" }));
            arList.Add(new DummyArchive("GaitUp", "C:/GaitUp", new[] { "LeftArm", "RightArm" }));
            arList.Add(new DummyArchive("Video", "C:/Video", new[] { "Front", "Back", "Drone" }));
        }

        async void OnButtonClicked(object sender, EventArgs args) {
            var nextPage = new ArchivePage(new DummyArchive("New Archive", "C:/New Archive", new[] { "Empty File" }));
            await Navigation.PushAsync(nextPage);
        }

        async void OnItemSelected(object sender, EventArgs args)
        {
            DummyArchive selected = (DummyArchive)EmployeeView.SelectedItem;
            var nextPage = new ArchivePage(selected);
            await Navigation.PushAsync(nextPage);
        }
	}
}