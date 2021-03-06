﻿using System;
using System.ComponentModel;
using System.Threading;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
    public class DeltaSlider : Slider, IDisposable
    {
        public new event PropertyChangedEventHandler PropertyChanged;

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

        private Page _page;
        private readonly Thread _thread;
        public DeltaSlider()
        {
            Unfocused += (a, b) => ManipulationCompleted();
            ValueChanged += (a, b) => ValueChangedHandler();

            _page = XamarinHelpers.GetCurrentPage();
            if(_page != null)
                _page.Disappearing += OnDisappearing;
            _thread = new Thread(UpdateOffset);
            _thread.Start();
        }


        private void OnDisappearing(object sender, EventArgs e)
        {
            if(_page != null)
                _page.Disappearing -= OnDisappearing;
            Dispose();
        }


        private double _offset;
        public double Offset
        {
            get => _offset;
            set
            {
                if (_offset == value) return;
                var oldValue = _offset;
                _offset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Offset"));
                OffsetChanged?.Invoke(this, new ValueChangedEventArgs(oldValue, value));
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

        protected virtual void Dispose(bool val)
        {
            IsVisible = false;
            _thread.Join();
        }
        public void Dispose()
        {
            Dispose(true);
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
