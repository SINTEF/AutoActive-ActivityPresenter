using Rg.Plugins.Popup.Services;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class GroupPopup : Rg.Plugins.Popup.Pages.PopupPage, INotifyPropertyChanged
    {

        private List<IDataPoint> _dataPoints = null;

        public GroupPopup(List<IDataPoint> dataPoints)
        {
            InitializeComponent();
            _dataPoints = dataPoints;
        }

        private async void DeleteButton_Clicked(object sender, EventArgs e)
        {
            DataGroup group = (DataGroup) GroupPicker.SelectedItem;
            DataGroups.DeleteGroup(group);
            await PopupNavigation.Instance.PopAsync();
        }

        private async void SelectButton_Clicked(object sender, EventArgs e)
        {
            DataGroup group = (DataGroup)GroupPicker.SelectedItem;
            DataGroups.AddDataPointsToGroup(_dataPoints, group);
            await PopupNavigation.Instance.PopAsync();
        }

        private async void AddNameButton_Clicked(object sender, EventArgs e)
        {
            string groupName = EntryName.Text;
            var exist = DataGroups.Groups.Find(x => x.GroupName == groupName);
            if (exist != null)
            {
                await DisplayAlert("Warning", "Please find a unique name for the group", "OK");
                return;
            }
            DataGroup group = new DataGroup(groupName);
            DataGroups.AddGroup(group);
            DataGroups.AddDataPointsToGroup(_dataPoints, group);
            await PopupNavigation.Instance.PopAsync();
        }
    }
}