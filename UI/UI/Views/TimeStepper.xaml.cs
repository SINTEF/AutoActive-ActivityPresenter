using System;
using System.Diagnostics;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class TimeStepper : ContentView
    {
        public TimeStepper()
        {
            InitializeComponent();
        }

        private void StepClicked(object sender, EventArgs e)
        {
            if (!(sender is Button button))
            {
                Debug.WriteLine("Sender is not a button");
                return;
            }

            TimeStepEvent eventArgs;
            switch (button.Text)
            {
                case "<<<":
                    eventArgs = new TimeStepEvent
                    {
                        Length = TimeStepLength.Large,
                        Direction = TimeStepDirection.Backward
                    };
                    break;
                case "<<":
                    eventArgs = new TimeStepEvent
                    {
                        Length = TimeStepLength.Short,
                        Direction = TimeStepDirection.Backward
                    };
                    break;
                case "<":
                    eventArgs = new TimeStepEvent
                    {
                        Length = TimeStepLength.Step,
                        Direction = TimeStepDirection.Backward
                    };
                    break;
                case ">>>":
                    eventArgs = new TimeStepEvent
                    {
                        Length = TimeStepLength.Large,
                        Direction = TimeStepDirection.Forward
                    };
                    break;
                case ">>":
                    eventArgs = new TimeStepEvent
                    {
                        Length = TimeStepLength.Short,
                        Direction = TimeStepDirection.Forward
                    };
                    break;
                case ">":
                    eventArgs = new TimeStepEvent
                    {
                        Length = TimeStepLength.Step,
                        Direction = TimeStepDirection.Forward
                    };
                    break;

                default:
                    Debug.WriteLine("Unknown step sender");
                    return;
            }

            OnStep?.Invoke(this, eventArgs);
        }

        public event EventHandler<TimeStepEvent> OnStep;
    }

    public enum TimeStepLength {
        Step, Short, Large
    }

    public enum TimeStepDirection
    {
        Backward = -1,
        Forward = 1
    }

    public struct TimeStepEvent
    {
        public TimeStepLength Length;
        public TimeStepDirection Direction;
    }
}