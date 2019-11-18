using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views.DynamicLayout
{
    public class ResizableStackLayout : Layout<View>
    {
        public StackOrientation Orientation { get; set; }
        public const double MinimumWeight = 1;

        public ResizableStackLayout()
        {
            Orientation = StackOrientation.Vertical;
        }

        private bool _childAdded;
        private bool _childRemoved;

        protected override void OnChildAdded(Element child)
        {
            base.OnChildAdded(child);
            if (!(child is DraggableSeparator splitterView))
            {
                _childAdded = true;
                return;
            }
            splitterView.Orientation = Orientation;

            //TODO: handle removing these elements when the page is closed
            splitterView.DragStart += Splitter_DragStart;
            splitterView.Dragged += Splitter_Dragged;
        }

        protected override void OnChildRemoved(Element child)
        {
            base.OnChildRemoved(child);

            if (!(child is DraggableSeparator splitterView))
            {
                _childRemoved = true;
                return;
            }

            splitterView.DragStart -= Splitter_DragStart;
            splitterView.Dragged -= Splitter_Dragged;
        }

        public const double SeparatorSize = 2d;

        public const double DefaultPercentageValue = 100d;
        public static readonly BindableProperty SizeWeightProperty = BindableProperty.CreateAttached(
            "SizeWeight",
            typeof(double),
            typeof(ResizableStackLayout),
            DefaultPercentageValue,
            BindingMode.OneWay,
            (bindable, value) => (double)value >= MinimumWeight,
            SizeWeightPropertyChanged
            );

        private static void SizeWeightPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (!(bindable is View view) || oldValue == newValue)
                return;

            if (!(view.Parent is ResizableStackLayout parent)) return;

            parent.InvalidateLayout();
        }

        public static double GetSizeWeight(BindableObject bindable)
        {
            return (double)bindable.GetValue(SizeWeightProperty);
        }

        public static void SetSizeWeight(BindableObject bindable, double value)
        {
            bindable.SetValue(SizeWeightProperty, value);
        }

        protected override void LayoutChildren(double x, double y, double width, double height)
        {
            if (!Children.Any(child => child.IsVisible)) return;

            try
            {
                if (_childAdded)
                {
                    InsertMissingSeparators();
                    _childAdded = false;
                }
            }
            catch (InvalidOperationException)
            { }

            try
            {
                if (_childRemoved)
                {
                    RemoveAdditionalSeparators();
                    _childRemoved = false;
                }
            }
            catch (InvalidOperationException)
            { }

            var totalWeight = Children.Where(child => child.IsVisible && !(child is DraggableSeparator)).Sum(child => GetSizeWeight(child));

            var numSeparators = Children.Count(child => child.IsVisible && child is DraggableSeparator);


            // Reset all weights if they are very small
            if (Math.Abs(totalWeight) < 1d)
            {
                totalWeight = 0d;
                foreach (var child in Children)
                {
                    SetSizeWeight(child, DefaultPercentageValue);
                    totalWeight += DefaultPercentageValue;
                }
            }

            if (Orientation == StackOrientation.Horizontal)
            {
                var childWidths = width - numSeparators * SeparatorSize;
                var xPos = x;
                foreach (var child in Children)
                {
                    if (!child.IsVisible) continue;
                    double childWidth;
                    if (child is DraggableSeparator)
                    {
                        childWidth = SeparatorSize;
                    }
                    else
                    {
                        childWidth = childWidths * GetSizeWeight(child) / totalWeight;
                    }

                    LayoutChildIntoBoundingRegion(child, new Rectangle(xPos, y, childWidth, height));
                    xPos += childWidth;
                }
            }
            else
            {
                var yPos = y;
                foreach (var child in Children)
                {
                    if (!child.IsVisible) continue;
                    
                    double childHeight;
                    if (child is DraggableSeparator)
                    {
                        childHeight = SeparatorSize;
                    }
                    else
                    {
                        childHeight = height * GetSizeWeight(child) / totalWeight;
                    }

                    LayoutChildIntoBoundingRegion(child, new Rectangle(x, yPos, width, childHeight));
                    yPos += childHeight;
                }
            }
        }

        private void InsertMissingSeparators()
        {
            var missingSeparators = new List<int>();
            for (var i = 1; i < Children.Count; i++)
            {
                if (Children[i - 1] is DraggableSeparator)
                {
                    continue;
                }

                if (!(Children[i] is DraggableSeparator))
                {
                    missingSeparators.Add(i);
                }
            }

            IEnumerable<int> en = missingSeparators;
            foreach (var index in en.Reverse())
            {
                Children.Insert(index, new DraggableSeparator());
            }
        }

        private void RemoveAdditionalSeparators()
        {
            var extraSeparators = new List<int>();
            var i = 0;
            for (; i < Children.Count; i++)
            {
                if (Children[i] is DraggableSeparator) extraSeparators.Add(i);
                else break;
            }
        
            for (; i < Children.Count - 1; i++)
            {
                if (Children[i] is DraggableSeparator && Children[i + 1] is DraggableSeparator)
                {
                    extraSeparators.Add(i);
                }
            }

            if (Children.Last() is DraggableSeparator)
            {
                if (!extraSeparators.Any() || extraSeparators.Last() != Children.Count - 1)
                {
                    extraSeparators.Add(Children.Count - 1);
                }
            }

            IEnumerable<int> en = extraSeparators;
            foreach (var index in en.Reverse())
            {
                var el = Children[index];
                Children.RemoveAt(index);
                if (el is DraggableSeparator separator)
                {
                    separator.InvokeRemoved();
                }
            }
            
        }

        #region Drag Handling
        private const double MinDragLength = 2d;
        private double _lastPos;
        private void Splitter_DragStart(DraggableSeparator sender, double x, double y)
        {
            _lastPos = Orientation == StackOrientation.Vertical ? y : x;
        }

        private void Splitter_Dragged(DraggableSeparator sender, double x, double y, double dx, double dy)
        {
            var curPos = (Orientation == StackOrientation.Vertical ? y : x);
            var newPos = _lastPos - curPos;

            if (Math.Abs(newPos) < MinDragLength) return;

            _lastPos = curPos;

            if (!(sender.Parent is ResizableStackLayout layout)) return;

            var diff = Math.Sign(newPos);

            var index = layout.Children.IndexOf(sender);
            if (index <= 0 || index + 1 >= layout.Children.Count )
            {
                Debug.WriteLine("ResizableStackLayout: Illegal index - should never be here.");
                return;
            }
            var leftChild = layout.Children[index - 1];
            var rightChild = layout.Children[index + 1];

            DiffWeight(leftChild, rightChild, diff);
        }

        private static void DiffWeight(BindableObject left, BindableObject right, double diff)
        {
            var leftWeight = GetSizeWeight(left);
            var rightWeight = GetSizeWeight(right);

            var newLeftWeight = leftWeight - diff;
            var newRightWeight = rightWeight + diff;

            if (newLeftWeight < MinimumWeight || newRightWeight < MinimumWeight)
            {
                return;
            }

            left.SetValue(SizeWeightProperty, newLeftWeight);
            right.SetValue(SizeWeightProperty, newRightWeight);
        }
        #endregion
    }
}
