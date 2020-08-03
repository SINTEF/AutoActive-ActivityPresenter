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
#if DEBUG
                return $"Version {version.Major}.{version.Minor}.{version.Build} - DEBUG";
#else
                return $"Version {version.Major}.{version.Minor}.{version.Build}";
#endif
            }
        }
    }
}
