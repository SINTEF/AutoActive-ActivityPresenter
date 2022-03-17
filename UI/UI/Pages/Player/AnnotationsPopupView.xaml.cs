using Rg.Plugins.Popup.Services;
using SINTEF.AutoActive.Plugins.Import.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AnnotationsPopupView : Rg.Plugins.Popup.Pages.PopupPage
    {
        private readonly SortedSet<int> _elements = new SortedSet<int>();

        public AnnotationsPopupView()
        {
            InitializeComponent();

            var provider = AnnotationProvider.GetAnnotationProvider(false);
            var annotationSet = provider.AnnotationSet;

            var names = annotationSet.AnnotationNames;
            var tags = annotationSet.AnnotationTags;
            var comments = annotationSet.AnnotationTypeComments;

            foreach (var tag in tags)
            {
                _elements.Add(tag.Key);
            }

            foreach (var name in names)
            {
                _elements.Add(name.Key);
            }

            foreach (var comment in comments)
            {
                _elements.Add(comment.Key);
            }

            for (var i = 0; i < 10; i++)
            {
                _elements.Add(i);
            }

            var count = 1;
            foreach (var index in _elements)
            {
                var el = new Entry()
                {
                    Text = index.ToString(),
                    Style = (Style)Application.Current.Resources["entrySettings"],
                    IsReadOnly = true,
                };

                Grid.SetRow(el, count);
                Grid.SetColumn(el, 0);



                count++;
                LayoutGrid.Children.Add(el);
            }
        }

        private async void CancelButton_Clicked(object sender, EventArgs e)
        {
            await PopupNavigation.Instance.PopAsync();
        }
    }
}