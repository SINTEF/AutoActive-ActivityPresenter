using SINTEF.AutoActive.Databus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public class PlayerTreeView : ListView
    {
        public static readonly GridLength DefaultWidth = 200;

        static readonly ObservableCollection<IDataPoint> dataPoints = new ObservableCollection<IDataPoint>();

        static PlayerTreeView()
        {
            DataRegistry.DataPointAdded += (datapoint) =>
            {
                dataPoints.Add(datapoint);
            };

            DataRegistry.DataPointRemoved += (datapoint) =>
            {
                dataPoints.Remove(datapoint);
            };
        }

        public PlayerTreeView ()
		{
            BackgroundColor = Color.White;

            ItemsSource = dataPoints;

            var template = new DataTemplate(() =>
            {
                var cell = new TextCell();
                cell.SetBinding(TextCell.TextProperty, "Name");
                return cell;
            });

            ItemTemplate = template;

            SelectionMode = ListViewSelectionMode.None;

            ItemTapped += PlayerTreeView_ItemTapped;
        }

        public event EventHandler<DataStructure> DataStructureTapped;
        public event EventHandler<IDataPoint> DataPointTapped;

        private void PlayerTreeView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item is IDataPoint datapoint)
            {
                DataPointTapped?.Invoke(this, datapoint);
            }
            else if (e.Item is DataStructure datastructure)
            {
                DataStructureTapped?.Invoke(this, datastructure);
            }
        }
    }
}