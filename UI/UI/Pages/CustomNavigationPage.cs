using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;
using Xamarin.Forms.Internals;

namespace SINTEF.AutoActive.UI.Pages
{
    public class CustomNavigationPage : NavigationPage
    {
        public CustomNavigationPage(Page root) : base(root)
        {
            // Auto-hide the navigation bar whenever a page is added
            SetHasNavigationBar(root, false);
            PushRequested += OnNewPage;
            InsertPageBeforeRequested += OnNewPage;
        }

        private void OnNewPage(object sender, NavigationRequestedEventArgs e)
        {
            SetHasNavigationBar(e.Page, false);
        }
    }
}
