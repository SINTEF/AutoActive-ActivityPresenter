using System;
using System.IO;
using SINTEF.AutoActive.FileSystem;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI
{
    public class VideoPlayer : View
    {
        // Source property
        public static readonly BindableProperty SourceProperty =
            BindableProperty.Create(nameof(Source), typeof(IReadSeekStreamFactory), typeof(VideoPlayer), null);

        [TypeConverter(typeof(IReadSeekStreamFactory))]
        public IReadSeekStreamFactory Source
        {
            set { SetValue(SourceProperty, value); }
            get { return (IReadSeekStreamFactory)GetValue(SourceProperty); }
        }

        public Label Label { get; internal set; }

        // Mime property
        public static readonly BindableProperty MimeTypeProperty =
            BindableProperty.Create(nameof(MimeType), typeof(string), typeof(VideoPlayer), null);

        [TypeConverter(typeof(string))]
        public string MimeType
        {
            set { SetValue(MimeTypeProperty, value); }
            get { return (string)GetValue(MimeTypeProperty); }
        }

        // Allowed Offset property
        public static readonly BindableProperty AllowedOffsetProperty =
            BindableProperty.Create(nameof(AllowedOffset), typeof(double), typeof(VideoPlayer), 2d);

        [TypeConverter(typeof(double))]
        public double AllowedOffset
        {
            set { SetValue(AllowedOffsetProperty, value); }
            get { return (double)GetValue(AllowedOffsetProperty); }
        }

        // Position changed event TODO: replace this with binding?
        public event PositionChangedEvent PositionChanged;

        // Position property
        public static readonly BindableProperty PositionProperty =
            BindableProperty.Create(nameof(Position), typeof(string), typeof(TimeSpan), null);

        private bool _isPlaying;

        [TypeConverter(typeof(TimeSpan))]
        public TimeSpan Position
        {
            set
            {
                SetValue(PositionProperty, value);
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(value));
            }
            get
            {
                var ret = GetValue(PositionProperty);
                if (ret == null) return TimeSpan.Zero;

                return (TimeSpan)ret;
            }
        }

        public event EventHandler<bool> PlayingChanged;

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                PlayingChanged?.Invoke(this, _isPlaying);
            }
        }

        private double _playbackRate = 1d;
        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                _playbackRate = value;
                PlaybackRateChanged?.Invoke(this, _playbackRate);
            }
        }

        private double _offset;

        public event EventHandler<double> OffsetChanged;
        public double CurrentOffset
        {
            get => _offset;
            set
            {
                _offset = value;
                OffsetChanged?.Invoke(this, _offset);
                XamarinHelpers.EnsureMainThread(() => Label.Text = $"{_offset:F3}\n{Compensator:F3}");
            }
        }

        public double Compensator { get; set; }

        public event EventHandler<double> PlaybackRateChanged;

        public event EventHandler<string> PlaybackError;

        public void TriggerPlaybackError(string msg)
        {
            PlaybackError?.Invoke(this, msg);
        }

    }

    public delegate void PositionChangedEvent(object sender, PositionChangedEventArgs args);

    public class PositionChangedEventArgs : EventArgs
    {
        public TimeSpan Time;
        public PositionChangedEventArgs(TimeSpan time)
        {
            Time = time;
        }
    }
}