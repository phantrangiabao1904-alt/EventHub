using System.Configuration;

namespace EventHub.Helpers
{
     
    public static class AppConfig
    {
        public static string Get(string key, string fallback = "")
        {
            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }

        public static int GetInt(string key, int fallback)
        {
            int n;
            return int.TryParse(Get(key), out n) ? n : fallback;
        }

        public static bool GetBool(string key, bool fallback)
        {
            bool b;
            return bool.TryParse(Get(key), out b) ? b : fallback;
        }

         
        public static int OtpExpireMinutes { get { return GetInt("Otp.ExpireMinutes", 10); } }

         
        public static int ReminderHoursBefore { get { return GetInt("Reminder.HoursBefore", 24); } }

         
        public static string BaseUrl { get { return Get("App.BaseUrl").TrimEnd('/'); } }
    }
}
