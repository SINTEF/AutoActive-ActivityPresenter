using System;
using System.Threading;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
    public class DeltaSlider : Slider
    {
        public static readonly BindableProperty OffsetProperty =
            BindableProperty.Create("Offset", typeof(double), typeof(DeltaSlider), 0.0);

        private bool _manipulating;
        private double _updateRate = 25;
        private int _updateDelay = 1000 / 25;
        public double UpdateRate
        {
            get => _updateRate;
            set
            {
                if (!(Math.Abs(_updateRate) >= 0.5)) return;

                _updateRate = value;
                _updateDelay = (int)(1000 / UpdateRate);
            }
        }

        public DeltaSlider()
        {
            Unfocused += (a, b) => ManipulationCompleted();
            ValueChanged += (a, b) => ValueChangedHandler();

            (new Thread(UpdateOffset)).Start();
        }

        public double Offset
        {
            get => (double)GetValue(OffsetProperty);
            set
            {
                var offset = Offset;
                SetValue(OffsetProperty, value);
                OffsetChanged?.Invoke(this, new ValueChangedEventArgs(offset, value));
            }
        }

        private void UpdateOffset()
        {
            while (IsVisible)
            {
                Thread.Sleep(_updateDelay);
                if (!_manipulating) continue;
                var s = Math.Sign(Value);
                var v = Math.Abs(Value) / 60;
                Offset += s * Math.Exp(v) / UpdateRate;
            }
        }

        private void ValueChangedHandler()
        {
            if (!_manipulating && Value != 0) Value = 0;
        }

        public void ManipulationStarted()
        {
            _manipulating = true;
        }
        public void ManipulationCompleted()
        {
            Value = 0;
            _manipulating = false;
        }

        public event EventHandler<ValueChangedEventArgs> OffsetChanged;
    }
}
