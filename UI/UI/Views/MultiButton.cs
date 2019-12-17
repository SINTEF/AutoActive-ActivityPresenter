using System;
using Xamarin.Forms;

namespace TreeVisualizer
{
    public class MultiButton : Button
    {
        public void OnAlternateClicked()
        {
            AlternateClicked?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler AlternateClicked;
    }
}