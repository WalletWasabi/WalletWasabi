using System;

namespace TwitterBot
{
    public static class DateTimeProvider
    {
        public static Func<DateTime> Current = () => DateTime.UtcNow;

        public static DateTime UtcNow
        {
            get { return Current(); }
        }
    }
}
