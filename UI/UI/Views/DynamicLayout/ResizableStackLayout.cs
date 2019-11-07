using System;
using System.Linq;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views.DynamicLayout
{
    public class ResizableStackLayout : Layout<View>
    {
        public StackOrientation Orientation { get; set; }
        public ResizableStackLayout()
        {
            Orientation = StackOrientation.Vertical;
        }

        public const double DefaultPercentageValue = 100d;
        public static readonly BindableProperty SizeWeightProperty = BindableProperty.CreateAttached(
            "SizeWeight",
            typeof(double),
            typeof(ResizableStackLayout),
            DefaultPercentageValue,
            BindingMode.OneWay,
            (bindable, value) => (double)value >= 0);

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

            var totalPercentage = Children.Where(child => child.IsVisible).Sum(child => GetSizeWeight(child));

            if (Math.Abs(totalPercentage) < 0.1d)
            {
                totalPercentage = 0d;
                foreach (var child in Children)
                {
                    SetSizeWeight(child, DefaultPercentageValue);
                    totalPercentage += DefaultPercentageValue;
                }
            }

            if (Orientation == StackOrientation.Horizontal)
            {
                var xPos = x;
                foreach (var child in Children)
                {
                    if (!child.IsVisible) continue;
                    var childWidth = width * GetSizeWeight(child) / totalPercentage;
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
                    var childHeight = height * GetSizeWeight(child) / totalPercentage;
                    LayoutChildIntoBoundingRegion(child, new Rectangle(x, yPos, width, childHeight));
                    yPos += childHeight;
                }
            }
        }
    }
}
