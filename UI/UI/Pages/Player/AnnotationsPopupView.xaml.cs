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
        // The list is pre-populated with ID of existing annotations, plus 1 to [AnnotationPrepopulate]
        private static readonly int AnnotationPrepopulate = 0;

        private readonly AnnotationSet _annotationSet;
        private int _rowCount = 0;

        public AnnotationsPopupView()
        {
            InitializeComponent();

            var provider = AnnotationProvider.GetAnnotationProvider(false);
            _annotationSet = provider.AnnotationSet;
            var annotationInfos = _annotationSet.AnnotationInfo;

            foreach(var annotaion in _annotationSet.Annotations)
            {
                if (annotationInfos.ContainsKey(annotaion.Type)) continue;

                annotationInfos[annotaion.Type] = new AnnotationInfo();
            }

            for (var i = 1; i <= AnnotationPrepopulate; i++)
            {
                if (annotationInfos.ContainsKey(i)) continue;

                annotationInfos[i] = new AnnotationInfo();
            }

            foreach (var (index, info) in annotationInfos.Select(x => (x.Key, x.Value)))
            {
                AddLine(index, info);
            }

            UpdateLineID();
        }

        private int NextLineID()
        {
            var existingIds = _annotationSet.AnnotationInfo.Keys;
            return existingIds.Count == 0 ? 1 : existingIds.Max() + 1;
        }

        private void UpdateLineID()
        {
            LineID.Text = NextLineID().ToString();
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
            foreach (var prop in new[] { "Name", "Tag", "Comment" })
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

        private async void AddLineButton_Clicked(object sender, EventArgs e)
        {
            var annotationInfo = new AnnotationInfo();
            if (!int.TryParse(LineID.Text, out var index) || index < 0)
            {
                var indexSuggestion = NextLineID();
                var res = await DisplayActionSheet($"Could not parse {LineID.Text} to a valid Annotation ID. Would you like to use {indexSuggestion}?", "Yes", "No", new string[] {});
                switch(res)
                {
                    case "No":
                        return;
                    case "Yes":
                        index = indexSuggestion;
                        break;
                }
            }


            if (_annotationSet.AnnotationInfo.ContainsKey(index))
            {
                await XamarinHelpers.ShowOkMessage("Already exists", $"Annotation Info with ID {index} already exists. Can not add duplicate.", this);
                return;
            }

            _annotationSet.AnnotationInfo[index] = annotationInfo;
            AddLine(index, annotationInfo);

            UpdateLineID();
        }

        private async void CancelButton_Clicked(object sender, EventArgs e)
        {
            await PopupNavigation.Instance.PopAsync();
        }
    }
}