using System;
using System.Linq;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI
{
    public static class XamarinHelpers
    {
        public static Page GetCurrentPage(INavigation navigation)
        {
            return navigation.NavigationStack.LastOrDefault();
        }

        public static Page GetCurrentPage(View view)
        {
            return GetCurrentPage(view.Navigation);;
        }

        public static Page GetCurrentPage()
        {
            return GetCurrentPage(Application.Current.MainPage.Navigation);
        }

        public static void EnsureMainThread(Action action)
        {
            if (Device.IsInvokeRequired)
                Device.BeginInvokeOnMainThread(action);
            else
                action();
        }

        public static IFigureContainer GetFigureContainerFromParents(Element element)
        {
            while (element != null)
            {
                if (element is IFigureContainer container)
                {
                    return container;
                }
                element = element.Parent;
            }

            throw new ArgumentException("Layout not recognized");
        }
    }
}
