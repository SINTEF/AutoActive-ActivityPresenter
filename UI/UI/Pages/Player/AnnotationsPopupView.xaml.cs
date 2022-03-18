using Rg.Plugins.Popup.Services;
using SINTEF.AutoActive.Plugins.Import.Json;
using System;
using System.Linq;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AnnotationsPopupView : Rg.Plugins.Popup.Pages.PopupPage
    {
        private readonly AnnotationSet _annotationSet;
        private int _rowCount = 0;

        public AnnotationsPopupView()
        {
            InitializeComponent();

            var provider = AnnotationProvider.GetAnnotationProvider(false);
            _annotationSet = provider.AnnotationSet;
            var annotationInfos = _annotationSet.AnnotationInfo;

            for (var i = 1; i <= 20; i++)
            {
                if (annotationInfos.ContainsKey(i)) continue;

                annotationInfos[i] = new AnnotationInfo();
            }

            foreach (var (index, info) in annotationInfos.Select(x => (x.Key, x.Value)))
            {
                AddLine(index, info);
            }
        }

        private void AddLine(int index, AnnotationInfo info)
        {
            _rowCount++;

            var entryId = new Entry()
            {
                Style = (Style)Application.Current.Resources["entrySettings"],
                IsReadOnly = true,
                HorizontalTextAlignment = TextAlignment.Center,
                Text = index.ToString(),
            };
            Grid.SetRow(entryId, _rowCount);
            Grid.SetColumn(entryId, 0);
            LayoutGrid.Children.Add(entryId);

            var propCount = 1;
            foreach(var prop in new[] { "Name", "Tag", "Comment" })
            {
                var entry = new Entry()
                {
                    BindingContext = info,
                    Style = (Style)Application.Current.Resources["entrySettings"],
                };
                entry.SetBinding(Entry.TextProperty, prop);
                Grid.SetRow(entry, _rowCount);
                Grid.SetColumn(entry, propCount++);
                LayoutGrid.Children.Add(entry);
            }
        }

        private void AddLineButton_Clicked(object sender, EventArgs e)
        {
            var index = _annotationSet.AnnotationInfo.Keys.Max() + 1;
            var annotationInfo = new AnnotationInfo();
            _annotationSet.AnnotationInfo[index] = annotationInfo;
            AddLine(index, annotationInfo);
        }

        private async void CancelButton_Clicked(object sender, EventArgs e)
        {
            await PopupNavigation.Instance.PopAsync();
        }
    }
}