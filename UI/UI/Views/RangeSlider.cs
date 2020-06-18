using System;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
    public class RangeSlider : Slider, IDisposable
    {
        private Page _page;
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool nativeOnly)
        {
            if (nativeOnly) return;

            _page = XamarinHelpers.GetCurrentPage();
            if (_page != null)
                _page.Disappearing += OnDisappearing;
        }

        private void OnDisappearing(object sender, EventArgs e)
        {
            if (_page != null)
                _page.Disappearing -= OnDisappearing;
            Dispose();
        }

        public void OnSelectedValueChanged(MinMaxValueChanged minMaxValueChanged)
        {
            SelectedValueChanged?.Invoke(this, minMaxValueChanged);
        }

        public void SetAvailableTime(long min, long max)
        {
            AvailableTimeChanged?.Invoke(this, new MinMaxValueChanged(min, max));
        }

        public event EventHandler<MinMaxValueChanged> AvailableTimeChanged;
        public event EventHandler<MinMaxValueChanged> SelectedValueChanged;
    }

    public class MinMaxValueChanged
    {
        public MinMaxValueChanged(long min, long max)
        {
            Min = min;
            Max = max;
        }
        public long Min;
        public bool MinChanged;
        public long Max;
        public bool MaxChanged;
    }
}
