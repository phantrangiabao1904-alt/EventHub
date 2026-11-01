using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web.Hosting;
using EventHub.Data;
using EventHub.Helpers;
using EventHub.Models;

namespace EventHub.Services
{
     



    public class ReminderService : IRegisteredObject
    {
        private static ReminderService _instance;
        private static readonly object StartLock = new object();

        private readonly Timer _timer;
        private readonly object _runLock = new object();
        private volatile bool _stopping;

        public static void Start()
        {
            lock (StartLock)
            {
                if (_instance != null) return;
                _instance = new ReminderService();
                HostingEnvironment.RegisterObject(_instance);
            }
        }

        private ReminderService()
        {
            _timer = new Timer(delegate { Tick(); }, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
        }

        private void Tick()
        {
            if (!Monitor.TryEnter(_runLock)) return;    
            try
            {
                if (_stopping) return;
                SendDueReminders();
            }
            catch (Exception ex) { Trace.TraceError("ReminderService lỗi: " + ex); }
            finally { Monitor.Exit(_runLock); }
        }

        private void SendDueReminders()
        {
            if (!Mailer.IsConfigured) return;

            var now = DateTime.Now;
            var limit = now.AddHours(AppConfig.ReminderHoursBefore);
            var today = now.Date;
            var lastDate = limit.Date;

            using (var db = new EventHubContext())
            {
                var candidates = db.Bookings.Include(b => b.User).Include(b => b.Venue)
                    .Where(b => b.Status == BookingStatus.Confirmed && b.ReminderSentAt == null
                                && b.EventDate >= today && b.EventDate <= lastDate)
                    .ToList();

                foreach (var b in candidates)
                {
                    if (_stopping) return;
                    var start = b.StartsAt();
                    if (start <= now || start > limit) continue;    

                    var mail = EmailTemplates.BookingReminder(b);
                    if (Mailer.TrySend(b.User.Email, mail.Subject, mail.Body))
                    {
                        b.ReminderSentAt = DateTime.Now;
                        db.SaveChanges();
                    }
                }
            }
        }

        public void Stop(bool immediate)
        {
            _stopping = true;
            _timer.Dispose();
            lock (_runLock) { }                            
            HostingEnvironment.UnregisterObject(this);
        }
    }
}
