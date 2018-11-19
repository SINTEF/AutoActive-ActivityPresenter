namespace SINTEF.AutoActive.UI.Helpers
{
    public static class Utils
    {
        public static string FormatTime(long time, long offset = 0)
        {
            const long micro = 1000000L;
            var remTime = time - offset;

            var hours = remTime / (3600L * micro);
            remTime -= hours * 3600L * micro;

            var minutes = remTime / (60L * micro);
            remTime -= minutes * 60L * micro;

            var seconds = remTime / micro;
            remTime -= seconds * micro;

            var millis = remTime / 1000;

            return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{millis:D3}";
        }
    }
}
