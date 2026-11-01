using System;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web.Hosting;
using EventHub.Helpers;

namespace EventHub.Services
{
     
    public static class Mailer
    {
        public static bool IsConfigured
        {
            get { return AppConfig.Get("Smtp.User") != "" && AppConfig.Get("Smtp.Password") != ""; }
        }

         
        public static void Send(string to, string subject, string htmlBody, string qrImageUrl = null)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Chưa cấu hình email gửi đi. Hãy tạo file secrets.config (xem secrets.config.example).");

            var user = AppConfig.Get("Smtp.User");
            var pass = AppConfig.Get("Smtp.Password").Replace(" ", "");  
            var host = AppConfig.Get("Smtp.Host", "smtp.gmail.com");
            var port = AppConfig.GetInt("Smtp.Port", 587);
            var ssl = AppConfig.GetBool("Smtp.EnableSsl", true);
            var fromName = AppConfig.Get("Smtp.FromName", "EventHub");

            string physicalPath = null;
            if (!string.IsNullOrEmpty(qrImageUrl))
            {
                if (!DepositService.ValidQrPath(qrImageUrl)) throw new ArgumentException("Invalid QR image path.");
                physicalPath = HostingEnvironment.MapPath("~" + qrImageUrl);
                if (string.IsNullOrEmpty(physicalPath) || !System.IO.File.Exists(physicalPath)) throw new System.IO.FileNotFoundException("QR image is missing.");
            }
            using (var client = new SmtpClient(host, port))
            using (var msg = BuildMessage(user, fromName, to, subject, htmlBody, physicalPath))
            {
                client.EnableSsl = ssl;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(user, pass);
                client.Timeout = 20000;

                client.Send(msg);
            }
        }

        public static MailMessage BuildMessage(string fromAddress, string fromName, string to, string subject, string htmlBody, string qrPhysicalPath = null)
        {
            var message = new MailMessage();
            try
            {
                message.From = new MailAddress(fromAddress, fromName, Encoding.UTF8);
                message.To.Add(new MailAddress(to));
                message.Subject = subject; message.SubjectEncoding = Encoding.UTF8; message.BodyEncoding = Encoding.UTF8;
                if (string.IsNullOrEmpty(qrPhysicalPath)) { message.Body = htmlBody; message.IsBodyHtml = true; }
                else
                {
                    message.Body = "Vui lòng xem nội dung HTML và ảnh QR đính kèm để chuyển khoản tiền cọc.";
                    var html = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
                    message.AlternateViews.Add(html);
                    html.LinkedResources.Add(new LinkedResource(qrPhysicalPath, "image/jpeg") {
                        ContentId = "deposit-qr", TransferEncoding = System.Net.Mime.TransferEncoding.Base64
                    });
                }
                return message;
            }
            catch { message.Dispose(); throw; }
        }

         
        public static void SendInBackground(string to, string subject, string htmlBody)
        {
            HostingEnvironment.QueueBackgroundWorkItem(ct =>
            {
                try { Send(to, subject, htmlBody); }
                catch (Exception ex) { Trace.TraceError("Gửi email tới " + to + " thất bại: " + ex.Message); }
            });
        }

        public static bool TrySend(string to, string subject, string htmlBody, string qrImageUrl = null)
        {
            try { Send(to, subject, htmlBody, qrImageUrl); return true; }
            catch (Exception ex) { Trace.TraceError("Gửi email tới " + to + " thất bại: " + ex.Message); return false; }
        }
    }
}
