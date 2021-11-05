using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public abstract partial class MovableObject : ContentView, IDropCollector, IDraggable
    {
        private const double MarginPerLevel = 15.0d;
        protected VisualizedStructure _element;

        public MovableObject()
        {
            InitializeComponent();
            ChildElements.Margin = new Thickness(MarginPerLevel, 0, 0, 0);
        }

        public string Name => Element.Name;
        public double Height => OuterFrame.Height;
        public double Width => OuterFrame.Width;

        protected Color ButtonColor
        {
            set
            {
                BranchButton.BackgroundColor = value;
            }
        }

        public void setButtonSettings()
        {
            OuterFrame.BorderColor = Color.FromHex("#1D2637");
            OuterFrame.CornerRadius = 0;
            OuterFrame.Padding = 0;
            SpaceBetweenFrameAndButton.Margin = 0;
        }


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
                if (_element.DataStructure != null)
                {
                    _element.DataStructure.Children.CollectionChanged += DataStructureChildrenChanged;
                    _element.DataStructure.DataPoints.CollectionChanged += DataStructureChildrenChanged;
                }
                if (BranchButton == null) return;
                BranchButton.Text = _element.Name;


                _element.OnExpandChanged += BranchOnOnExpandChanged;
                UpdateExpandedName();

            }
        }


        private void DataStructureChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateExpandedName();
            EnsureCorrectChildren();
        }

        private void EnsureCorrectChildren()
        {
            var dataStructure = _element;
            var i = -1;
            foreach (var child in dataStructure.Children)
            {
                i++;

                if (i >= ChildElements.Children.Count)
                {
                    var newChild = CreateChildElement(child);
                    ChildElements.Children.Add(newChild);
                    continue;
                }

                var childElement = ChildElements.Children[i];
                if (!(childElement is MovableObject branchView))
                {
                    throw new ArgumentException("Invalid child argument");
                }

                if (child == branchView.Element)
                {
                    continue;
                }

                // Look for later matching items:
                var existingChildIx = ChildElements.Children.Skip(i)
                    .IndexOf(el => (el as MovableObject)?.Element == child);
                if (existingChildIx != -1)
                {
                    var tmpChildElement = ChildElements.Children[existingChildIx];
                    ChildElements.Children.RemoveAt(existingChildIx);
                    ChildElements.Children.Insert(i, tmpChildElement);
                    continue;
                }
                ChildElements.Children.Insert(i, CreateChildElement(child));
            }

            i++;

            if (i == 0)
                ChildElements.Children.Clear();

            while (i < ChildElements.Children.Count)
            {
                ChildElements.Children.RemoveAt(ChildElements.Children.Count - 1);
            }

        }

        private bool IsClickable()
        {
            return Element.DataPoint != null;
        }

        public DataTreeView ParentTree { get; set; }

        private void UpdateExpandedName()
        {
            if (_element == null) return;

            ExpandButton.IsVisible = _element.Children.Any();
            if (!ExpandButton.IsVisible && _element.IsExpanded)
            {
                // TODO(sigurdal): The following line would be the logical behaviour, but there seems to be a bug with Xamarin not showing the element when it is made visible again
                // _element.IsExpanded = false;
            }


            ExpandButton.Text = _element.IsExpanded ? "-" : "+";
        }

        private void BranchOnOnExpandChanged(object sender, bool isExpanded)
        {
            UpdateExpandedName();
            if (!isExpanded)
            {
                ChildElements.IsVisible = false;
                return;
            }

            ChildElements.IsVisible = true;

            EnsureCorrectChildren();
        }

        public abstract MovableObject CreateChildElement(VisualizedStructure element);


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

        public abstract void ObjectDroppedOn(IDraggable item);
    }
}