using System;
using System.Diagnostics;
using System.Linq;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class BranchView : ContentView, IDropCollector, IDraggable
    {
        private const double MarginPerLevel = 15.0d;
        private VisualizedStructure _element;

        public BranchView()
        {
            InitializeComponent();

            ChildElements.Margin = new Thickness(MarginPerLevel, 0, 0, 0);
        }

        public string Name => Element.Name;

        public VisualizedStructure Element
        {
            get => _element;
            set
            {
                if (_element != null)
                {
                    _element.OnExpandChanged -= BranchOnOnExpandChanged;
                    if (_element.Children.Any())
                    {
                        foreach (var child in _element.Children)
                        {
                            child.OnExpandChanged -= BranchOnOnExpandChanged;
                        }
                    }
                }

                _element = value;
                if (BranchButton == null) return;
                BranchButton.Text = _element.Name;


                _element.OnExpandChanged += BranchOnOnExpandChanged;
                UpdateExpandedName();
                if (!IsClickable())
                {
                    BranchButton.BackgroundColor = Color.White;
                }
            }
        }

        private bool IsClickable()
        {
            return Element.DataPoint != null;
        }

        public PlayerTreeView ParentTree { get; set; }

        private void UpdateExpandedName()
        {
            if (_element == null) return;

            ExpandButton.IsVisible = _element.Children.Any();
            ExpandButton.Text = _element.IsExpanded ? "-" : "+";
        }

        private void BranchOnOnExpandChanged(object sender, bool isExpanded)
        {
            UpdateExpandedName();
            if (isExpanded)
            {
                ChildElements.IsVisible = true;
                var elements = ChildElements.Children.Select(el => ((BranchView)el).Element);

                if (elements.SequenceEqual(_element.Children))
                {
                    return;
                }

                ChildElements.Children.Clear();

                foreach (var element in _element.Children)
                {
                    ChildElements.Children.Add(new BranchView { ParentTree = ParentTree, Element = element });
                }
            }
            else
            {
                ChildElements.IsVisible = false;
            }
        }

        private void BranchButton_OnClicked(object sender, EventArgs e)
        {
            if (Element.DataPoint != null)
            {
                ParentTree.DataPointClicked(Element.DataPoint);
                return;
            }
            ExpandButton_OnClicked(sender, e);
        }

        private void ExpandButton_OnClicked(object sender, EventArgs e)
        {
            _element.IsExpanded ^= true;
        }

        private void BranchButton_OnAlternateClicked(object sender, EventArgs e)
        {
            NameChangeEntry.Text = Element.Name;
            NameChangeEntry.Focus();

            BranchButton.IsVisible = false;
            NameChangeLayout.IsVisible = true;
        }

        private void ChangeNameButton(object sender, EventArgs e)
        {
            SaveNameChange();
        }

        private void NameChangeEntry_OnCompleted(object sender, EventArgs e)
        {
            SaveNameChange();
        }

        private void NameChangeComplete()
        {
            NameChangeLayout.IsVisible = false;
            BranchButton.IsVisible = true;
        }

        private void SaveNameChange()
        {
            var name = NameChangeEntry.Text;
            Element.Name = name;
            BranchButton.Text = name;

            NameChangeComplete();
        }

        public void ObjectDroppedOn(IDraggable item)
        {
            Debug.WriteLine($"{item} dropped on {this} ({Name})");
        }
    }
}