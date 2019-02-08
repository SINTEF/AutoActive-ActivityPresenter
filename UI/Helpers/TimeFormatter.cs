namespace SINTEF.AutoActive.UI.Helpers
{
    public static class Utils
    {
        public const long MicrosPerSecond = 1000000L;
        public static string FormatTime(long time, long offset = 0)
        {
            
            var remTime = time - offset;

            var hours = remTime / (3600L * MicrosPerSecond);
            remTime -= hours * 3600L * MicrosPerSecond;

            var minutes = remTime / (60L * MicrosPerSecond);
            remTime -= minutes * 60L * MicrosPerSecond;

            var seconds = remTime / MicrosPerSecond;
            remTime -= seconds * MicrosPerSecond;

            var millis = remTime / 1000;

            return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{millis:D3}";
        }

        public static long TimeFromSeconds(double time)
        {
            return (long) (MicrosPerSecond * time);
        }
    }
}
