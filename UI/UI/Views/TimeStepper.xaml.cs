using System;
using System.Diagnostics;
using System.Linq;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Pages;
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

        public Button GetPlayButton
        {
            get => PlayButton;
        }

        public bool AreButtonsVisible
        {
            set
            {
                FarBack.IsVisible = value;
                Back.IsVisible = value;
                SligthlyBack.IsVisible = value;
                PlayButton.IsVisible = value;
                SligthlyForward.IsVisible = value;
                Forward.IsVisible = value;
                FarForward.IsVisible = value;
            }
        }

        public bool AreButtonsEnabled
        {
            set
            {
                FarBack.IsEnabled = value;
                Back.IsEnabled = value;
                SligthlyBack.IsEnabled = value;
                PlayButton.IsEnabled = value;
                SligthlyForward.IsEnabled = value;
                Forward.IsEnabled = value;
                FarForward.IsEnabled = value;
            }
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

        public void PlayButton_Clicked(object sender, EventArgs e)
        {
            TimeStepEvent eventArgs;

            if (PlayButton.Text == "PLAY")
            {
                eventArgs = new TimeStepEvent
                {

                    Play = StartPlay.Start

                };
            }
            else
            {
                eventArgs = new TimeStepEvent
                {
                    Play = StartPlay.Stop
                };
            }

            OnStep?.Invoke(this, eventArgs);

            if (PlayButton.Text == "PLAY")
            {
                PlayButton.Text = "STOP";
            }
            else
            {
                PlayButton.Text = "PLAY";
            }

        }

        public event EventHandler<TimeStepEvent> OnStep;

        public void KeyUp(KeyEventArgs args)
        {
            switch (args.Key)
            {
                case "Space":
                    args.Handled = true;
                    return;
                case "Left":
                    args.Handled = true;
                    break;
                case "Right":
                    args.Handled = true;
                    break;
                default:
                    return;
            }
        }

        public void KeyDown(KeyEventArgs args)
        {
            var timeStepEvent = new TimeStepEvent();
            switch (args.Key)
            {
                case "Space":
                    PlayButton_Clicked(this, new EventArgs());
                    args.Handled = true;
                    return;
                case "Left":
                    args.Handled = true;
                    timeStepEvent.Direction = TimeStepDirection.Backward;
                    break;
                case "Right":
                    args.Handled = true;
                    timeStepEvent.Direction = TimeStepDirection.Forward;
                    break;
                default:
                    return;
            }

            switch (args.Modifiers)
            {
                case KeyModifiers.None:
                    timeStepEvent.Length = TimeStepLength.Step;
                    break;
                case KeyModifiers.Shift:
                    timeStepEvent.Length = TimeStepLength.Short;
                    break;
                case KeyModifiers.Ctrl:
                    timeStepEvent.Length = TimeStepLength.Large;
                    break;
                case KeyModifiers.Ctrl | KeyModifiers.Shift:
                    timeStepEvent.Length = TimeStepLength.VeryLarge;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            OnStep?.Invoke(this, timeStepEvent);
        }
    }

    public enum TimeStepLength {
        None, Step, Short, Large, VeryLarge
    }

    public enum TimeStepDirection
    {
        Backward = -1,
        Forward = 1,
        None = 0
    }

    public enum StartPlay
    {
        None, Start, Stop
    }

    public struct TimeStepEvent
    {
        public TimeStepLength Length;
        public TimeStepDirection Direction;
        public StartPlay Play;

        public long AsOffset()
        {
            long offset;
            switch (Length)
            {
                case TimeStepLength.Step:
                    offset = TimeFormatter.TimeFromSeconds(1d / 30);
                    break;
                case TimeStepLength.Short:
                    offset = TimeFormatter.TimeFromSeconds(1);
                    break;
                case TimeStepLength.Large:
                    offset = TimeFormatter.TimeFromSeconds(10);
                    break;
                case TimeStepLength.VeryLarge:
                    offset = TimeFormatter.TimeFromSeconds(60);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (Direction == TimeStepDirection.Backward)
            {
                offset = -offset;
            }

            return offset;
        }
    }
}