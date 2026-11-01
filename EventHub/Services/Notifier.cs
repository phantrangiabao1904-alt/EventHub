using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using EventHub.Data;
using EventHub.Models;

namespace EventHub.Services
{
     
    public static class Notifier
    {
        public static void Received(EventHubContext db, int bookingId) { Queue(db, bookingId, EmailTemplates.BookingReceived); }

        public static void Confirmed(EventHubContext db, int bookingId) { Queue(db, bookingId, EmailTemplates.BookingConfirmed); }

        public static bool DepositRequested(EventHubContext db, int bookingId)
        {
            try
            {
                var booking = db.Bookings.Include(b => b.User).Include(b => b.Venue).Single(b => b.Id == bookingId);
                var mail = EmailTemplates.DepositRequest(booking);
                return Mailer.TrySend(booking.User.Email, mail.Subject, mail.Body, booking.DepositQrImageUrl);
            }
            catch (Exception ex) { Trace.TraceError("Không gửi được QR cọc: " + ex.Message); return false; }
        }

        public static void Cancelled(EventHubContext db, int bookingId, bool byClient)
        {
            Queue(db, bookingId, b => EmailTemplates.BookingCancelled(b, byClient));
        }

        private static void Queue(EventHubContext db, int bookingId, Func<Booking, EmailContent> build)
        {
            try
            {
                var b = db.Bookings.Include(x => x.User).Include(x => x.Venue).FirstOrDefault(x => x.Id == bookingId);
                if (b == null || b.User == null || b.Venue == null) return;
                var mail = build(b);           
                Mailer.SendInBackground(b.User.Email, mail.Subject, mail.Body);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Không tạo được email thông báo cho đơn #" + bookingId + ": " + ex.Message);
            }
        }
    }
}
