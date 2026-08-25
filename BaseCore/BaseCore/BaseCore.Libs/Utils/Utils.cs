using System;
using System.Text.RegularExpressions;

namespace BaseCore.Libs.Utils
{
    public static class Utils
    {
        public static DateTime GetDateFrom(DateTime dateFrom, int timeZone)
        {
            return new DateTime(dateFrom.Year, dateFrom.Month, dateFrom.Day, 0, 0, 0).AddHours(-timeZone);
        }

        public static DateTime GetDateTo(DateTime dateTo, int timeZone)
        {
            return new DateTime(dateTo.Year, dateTo.Month, dateTo.Day, 0, 0, 0).AddDays(1).AddHours(-timeZone);
        }

        public static DateTime GetUTCDateOnly(DateTime inputDateTime)
        {
            return inputDateTime.ToUniversalTime().Date;
        }

        public static DateTime GetUTCDateTime(DateTime inputDateTime)
        {
            return inputDateTime.ToUniversalTime();
        }

        public static string GetFormatDate(DateTime inputDateTime, string format)
        {
            return inputDateTime.ToString(format);
        }

        public static string GetRelativeTimeAgo(DateTime dateTime)
        {
            const int SECOND = 1;
            const int MINUTE = 60 * SECOND;
            const int HOUR = 60 * MINUTE;
            const int DAY = 24 * HOUR;
            const int MONTH = 30 * DAY;

            var ts = new TimeSpan(DateTime.UtcNow.Ticks - dateTime.Ticks);
            double delta = Math.Abs(ts.TotalSeconds);

            if (delta < 1 * MINUTE)
                return ts.Seconds == 1 ? "one second ago" : (ts.Seconds == 0 ? 1 : ts.Seconds) + " seconds ago";

            if (delta < 2 * MINUTE)
                return "a minute ago";

            if (delta < 45 * MINUTE)
                return ts.Minutes + " minutes ago";

            if (delta < 90 * MINUTE)
                return "an hour ago";

            if (delta < 24 * HOUR)
                return ts.Hours + " hours ago";

            if (delta < 48 * HOUR)
                return "yesterday";

            if (delta < 30 * DAY)
                return ts.Days + " days ago";

            if (delta < 12 * MONTH)
            {
                int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                return months <= 1 ? "one month ago" : months + " months ago";
            }

            return "";
        }

        public static string RemoveSpecial(string input, string[] removeCharacters)
        {
            foreach (var itemRemove in removeCharacters)
            {
                input = Regex.Replace(input, itemRemove, "");
            }
            return input;
        }
    }
}
