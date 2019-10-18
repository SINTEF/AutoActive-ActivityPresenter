using System;

namespace SINTEF.AutoActive.UI.Helpers
{
    public static class TimeFormatter
    {
        public const long MicrosPerSecond = 1000000L;
        public static string FormatTime(long time, long offset = 0)
        {
            var remTime = time - offset;
            var sign = "";
            if (remTime < 0)
            {
                remTime = -remTime;
                sign = "-";
            }

            var hours = remTime / (3600L * MicrosPerSecond);

            var dateTime = DateTimeFromTime(remTime);

            return hours <= 24 ? 
                dateTime.ToString($"{sign}HH:mm:ss.fff") :
                dateTime.ToString($"{sign}yyyy-MM-dd\nHH:mm:ss.fff");
        }
        public static long TimeFromDateTime(DateTime dateTime)
        {
            return TimeFromTimeSpan(dateTime - new DateTime(1970, 1, 1, 0, 0, 0, 0));
        }
        public static long TimeFromTimeSpan(TimeSpan timeSpan)
        {
            return (long)(MicrosPerSecond * timeSpan.TotalSeconds);
        }
        public static long TimeFromSeconds(double time)
        {
            return (long) (MicrosPerSecond * time);
        }
        public static double SecondsFromTime(long time)
        {
            return (double) time / MicrosPerSecond;
        }

        public static DateTime DateTimeFromTime(long time)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddTicks(time * 10);
        }
    }
}
