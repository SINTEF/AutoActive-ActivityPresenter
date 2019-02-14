using System;
using System.Collections.Generic;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Interfaces;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages
{
	public partial class StorageSelector : ContentPage
	{
	    public IList<ArchiveSession> Sessions { get; }
	    public IList<IDataStructure> DataPoints { get; }

        public List<ArchiveSession> SelectedSession { get; set; }
        public List<IDataStructure> SelectedDataPoints { get; set; }
        public string SessionName = DateTime.Now.ToString("yyyy-MM-dd--HH-mm");

	    public bool Cancelled = true;

        public StorageSelector()
	    {
	        Sessions = new List<ArchiveSession>();
	        DataPoints = new List<IDataStructure>();

            InitializeComponent();
	    }

	    public StorageSelector(IList<ArchiveSession> sessions, IList<IDataStructure> dataPoints)
	    {
	        Sessions = sessions;
	        DataPoints = dataPoints;

            InitializeComponent();
        }

	    private void Cancel_OnClicked(object sender, EventArgs e)
	    {
	        Cancelled = true;
	        Navigation.PopAsync();
	    }

	    private void Save_OnClicked(object sender, EventArgs e)
	    {
	        Cancelled = false;
	        Navigation.PopAsync();
	    }
	}
}