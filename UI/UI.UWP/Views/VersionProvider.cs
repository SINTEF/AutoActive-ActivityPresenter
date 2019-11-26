using Windows.ApplicationModel;
using SINTEF.AutoActive.UI.UWP.Views;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;

[assembly: Dependency(typeof(VersionProvider))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class VersionProvider : IVersionProvider
    {
        public string Version
        {
            get
            {
                var version = Package.Current.Id.Version;
                return string.Format("v{0}.{1}.{2}", version.Major, version.Minor, version.Build);
            }
        }
    }
}
