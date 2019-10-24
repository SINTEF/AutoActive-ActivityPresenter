using System;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages
{
    public class KeypressPage : ContentPage
    {
        public event EventHandler<KeyEventArgs> KeyDown;
        public event EventHandler<KeyEventArgs> KeyUp;

        public void InvokeKeyDown(KeyEventArgs e)
        {
            KeyDown?.Invoke(this, e);
        }

        public void InvokeKeyUp(KeyEventArgs ea)
        {
            KeyUp?.Invoke(this, ea);
        }
    }

    public class KeyEventArgs : EventArgs
    {
        public string Key { get; set; }
    }
}