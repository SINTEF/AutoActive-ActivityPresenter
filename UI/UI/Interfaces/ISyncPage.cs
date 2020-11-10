using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface ISyncPage
    {
        void RemoveCorrelationPreview(IDataPoint datapoint);

        void AdjustOffset(object sender, ValueChangedEventArgs args);
    }
}
