﻿using System;
using System.Diagnostics;
using System.Linq;
using SINTEF.AutoActive.UI.Helpers;
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
    }

    public enum TimeStepLength {
        Step, Short, Large, None
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