using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using EventHub.Data;
using EventHub.Models;
using EventHub.Services;
using EventHub.ViewModels;

public sealed class TestDatabaseConfiguration : DbConfiguration
{
    public TestDatabaseConfiguration() { SetProviderServices("System.Data.SqlClient", SqlProviderServices.Instance); }
}

public static class FeatureChecks
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++; Console.WriteLine("PASS: " + name);
    }

    public static int Main()
    {
        DbConfiguration.SetConfiguration(new TestDatabaseConfiguration());
        Database.SetInitializer<EventHubContext>(null);
        var name = "EventHub_FeatureTests_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder { DataSource = @".\SQLEXPRESS", InitialCatalog = name,
            IntegratedSecurity = true, MultipleActiveResultSets = true }.ConnectionString;
        try
        {
            using (var db = new EventHubContext(connection))
            {
                db.Database.Create();
                var client = new User { FullName = "Test Client", Email = "client@example.invalid", PasswordHash = "test", Role = Role.Client };
                var stranger = new User { FullName = "Other Client", Email = "other@example.invalid", PasswordHash = "test", Role = Role.Client };
                var admin = new User { FullName = "Test Admin", Email = "admin@example.invalid", PasswordHash = "test", Role = Role.Admin };
                var venue = new Venue { Name = "Test Venue", Address = "Test", Capacity = 100, Price = 1000000m };
                var option = new ServiceOption { Name = "=SUM(1,2)", UnitPrice = 100000m, IsActive = true };
                db.Users.Add(client); db.Users.Add(stranger); db.Users.Add(admin); db.Venues.Add(venue); db.ServiceOptions.Add(option); db.SaveChanges();
                var service = new BookingService(db);
                var extras = new BookingExtrasService(db);
                var day = DateTime.Today.AddDays(20);
                var selections = new[] { new ServiceSelectionViewModel { Selected = true, ServiceOptionId = option.Id, Quantity = 2, UnitPrice = 1 } };
                var first = service.Create(client.Id, venue.Id, day, TimeSpan.FromHours(9), TimeSpan.FromHours(12), 10, "test", selections);
                Check(first.Success, "Booking with services created");
                var id = first.Booking.Id;
                Check(first.Booking.TotalPrice == 1200000m && first.Booking.RequiredDeposit == 360000m, "Server prices and 30 percent deposit ignore forged price");
                option.UnitPrice = 900000m; db.SaveChanges();
                Check(db.BookingServiceItems.Single(s => s.BookingId == id).UnitPrice == 100000m && first.Booking.TotalPrice == 1200000m, "Catalog changes preserve booked price snapshots");
                Check(!service.Create(client.Id, venue.Id, day, TimeSpan.FromHours(10), TimeSpan.FromHours(11), 10, null).Success, "Overlapping pending booking rejected");
                var adjacent = service.Create(client.Id, venue.Id, day, TimeSpan.FromHours(12), TimeSpan.FromHours(13), 10, null);
                Check(adjacent.Success, "Adjacent time slots allowed");
                Check(!service.Create(client.Id, venue.Id, day, TimeSpan.FromHours(14), TimeSpan.FromHours(25), 10, null).Success, "Time outside same day rejected");
                Check(!service.Create(client.Id, venue.Id, day, TimeSpan.FromHours(14), TimeSpan.FromHours(15), 10, null, new[] { selections[0], selections[0] }).Success, "Duplicate service selections rejected");
                option.IsActive = false; db.SaveChanges();
                Check(!service.Create(client.Id, venue.Id, day, TimeSpan.FromHours(14), TimeSpan.FromHours(15), 10, null, selections).Success, "Deactivated service rejected");
                Check(!service.CancelByClient(id, stranger.Id).Success, "Other customer cannot cancel booking");
                Check(!service.ChangeStatus(id, BookingStatus.Confirmed, client.Id).Success, "Customer cannot perform staff transition");
                Check(!service.ChangeStatus(id, BookingStatus.Confirmed, admin.Id).Success, "Staff cannot bypass approval and deposit");
                var deposits = new DepositService(db);
                var qr = "/Uploads/PaymentQr/" + new string('a', 32) + ".jpg";
                Check(deposits.SaveSettings(client.Id, new DepositSettingsViewModel { Percent = 40, QrImageUrl = qr }) != null, "Customer cannot modify deposit settings");
                Check(deposits.SaveSettings(admin.Id, new DepositSettingsViewModel { Percent = 40, QrImageUrl = qr, BankName = "Test Bank" }) == null, "Admin can configure default percentage and QR");
                Check(deposits.SaveSettings(admin.Id, new DepositSettingsViewModel { Percent = 0 }) != null, "Zero percent setting rejected");
                Check(deposits.Request(admin.Id, new DepositRequestViewModel { BookingId = id, Percent = 30 }) != null, "Approval without QR rejected");
                Check(deposits.Request(admin.Id, new DepositRequestViewModel { BookingId = id, Percent = 30, QrImageUrl = qr, BankName = "Test Bank" }) == null,
                    "Approval stores QR snapshot and waits for deposit");
                Check(first.Booking.Status == BookingStatus.AwaitingDeposit && first.Booking.RequiredDeposit == 360000m, "Approval does not confirm booking");
                Check(!service.ChangeStatus(id, BookingStatus.Confirmed, admin.Id).Success, "Approved booking still cannot confirm without sufficient money");
                var review = new ReviewViewModel { BookingId = id, Rating = 5, Comment = "Great venue <script>literal text</script>" };
                Check(extras.Review(client.Id, review) != null, "Review before completion rejected");
                var receipt = new RecordPaymentViewModel { BookingId = id, Type = PaymentEntryType.Receipt, Amount = 100000m, Reference = "TEST-RECEIPT", RequestId = Guid.NewGuid() };
                Check(extras.RecordPayment(client.Id, receipt) != null, "Customer cannot record own payment");
                Check(extras.RecordPayment(admin.Id, receipt) == null, "Deposit receipt accepted");
                Check(first.Booking.Status == BookingStatus.AwaitingDeposit, "Partial deposit remains awaiting deposit");
                Check(deposits.Request(admin.Id, new DepositRequestViewModel { BookingId = id, Percent = 20, QrImageUrl = qr }) != null, "Percentage cannot change after partial payment");
                Check(extras.RecordPayment(admin.Id, receipt) == null && db.PaymentEntries.Count(p => p.BookingId == id) == 1, "Repeated request does not duplicate money");
                receipt.RequestId = Guid.NewGuid();
                Check(extras.RecordPayment(admin.Id, receipt) != null, "Same receipt reference cannot be entered twice with a new request ID");
                var topup = new RecordPaymentViewModel { BookingId = id, Type = PaymentEntryType.Receipt, Amount = 260000m, Reference = "TEST-TOPUP", RequestId = Guid.NewGuid() };
                bool newlyConfirmed;
                Check(extras.RecordPayment(admin.Id, topup, out newlyConfirmed) == null && newlyConfirmed && first.Booking.Status == BookingStatus.Confirmed,
                    "Staff receipt reaching deposit atomically confirms booking");
                Check(extras.RecordPayment(admin.Id, topup, out newlyConfirmed) == null && !newlyConfirmed, "Repeated top-up does not reconfirm or renotify");
                Check(deposits.SaveSettings(admin.Id, new DepositSettingsViewModel { Percent = 50, QrImageUrl = "/Uploads/PaymentQr/" + new string('b', 32) + ".jpg" }) == null
                    && first.Booking.RequiredDeposit == 360000m && first.Booking.DepositQrImageUrl == qr, "New defaults preserve already sent per-booking QR and amount");
                var invalid = new RecordPaymentViewModel { BookingId = id, Type = PaymentEntryType.Receipt, Amount = 1000000m, Reference = "OVER", RequestId = Guid.NewGuid() };
                Check(extras.RecordPayment(admin.Id, invalid) != null, "Overpayment rejected");
                invalid.Type = PaymentEntryType.Refund; invalid.Amount = 360001m;
                Check(extras.RecordPayment(admin.Id, invalid) != null, "Refund above net paid rejected");
                invalid.Amount = 60000m;
                Check(extras.RecordPayment(admin.Id, invalid) != null, "Refund cannot leave a confirmed reservation under-deposited");
                first.Booking.EventDate = DateTime.Today.AddDays(-1); db.SaveChanges();
                Check(service.ChangeStatus(id, BookingStatus.Completed, admin.Id).Success, "Completed transition recorded after event starts");
                Check(extras.RecordPayment(admin.Id, invalid) == null, "Partial post-event refund accepted");
                var detail = new BookingDetailsViewModel { Booking = first.Booking, Payments = db.PaymentEntries.Where(p => p.BookingId == id).ToList() };
                Check(detail.NetPaid == 300000m && detail.Balance == 900000m, "Payment balance subtracts refunds correctly");
                Check(db.BookingStatusChanges.Count(h => h.BookingId == id) == 4, "Create, approve, deposit-confirm and completion audit rows persisted");
                Check(extras.Review(stranger.Id, review) != null, "Other customer cannot review booking");
                Check(extras.Review(client.Id, review) == null, "Owner can review completed booking");
                Check(extras.Review(client.Id, review) != null && db.VenueReviews.Count() == 1, "Duplicate review rejected");
                Check(service.CancelByClient(adjacent.Booking.Id, client.Id).Success && !service.HasConflict(venue.Id, day, TimeSpan.FromHours(12), TimeSpan.FromHours(13)), "Cancelled booking frees its slot");
                invalid.BookingId = adjacent.Booking.Id; invalid.Type = PaymentEntryType.Receipt; invalid.RequestId = Guid.NewGuid();
                Check(extras.RecordPayment(admin.Id, invalid) != null, "Receipt for cancelled booking rejected");
                var quick = service.Create(client.Id, venue.Id, day, TimeSpan.FromHours(14), TimeSpan.FromHours(15), 10, null);
                Check(quick.Success, "One-click payment test booking created");
                Check(extras.ConfirmDeposit(admin.Id, quick.Booking.Id, Guid.NewGuid(), out newlyConfirmed) != null, "One-click deposit still requires approval first");
                Check(deposits.Request(admin.Id, new DepositRequestViewModel { BookingId = quick.Booking.Id, Percent = 50, QrImageUrl = qr }) == null, "Quick booking approval succeeds with only percentage and QR");
                Check(extras.ConfirmDeposit(client.Id, quick.Booking.Id, Guid.NewGuid(), out newlyConfirmed) != null, "Customer cannot use one-click deposit confirmation");
                Check(extras.ConfirmDeposit(admin.Id, quick.Booking.Id, Guid.Empty, out newlyConfirmed) != null, "Empty confirmation token rejected");
                var quickRequest = Guid.NewGuid();
                Check(extras.ConfirmDeposit(admin.Id, quick.Booking.Id, quickRequest, out newlyConfirmed) == null && newlyConfirmed
                    && quick.Booking.Status == BookingStatus.Confirmed && db.PaymentEntries.Single(p => p.BookingId == quick.Booking.Id).Amount == 500000m,
                    "One click calculates required deposit and confirms booking without payment fields");
                Check(extras.ConfirmDeposit(admin.Id, quick.Booking.Id, quickRequest, out newlyConfirmed) == null && !newlyConfirmed
                    && extras.ConfirmDeposit(admin.Id, quick.Booking.Id, Guid.NewGuid(), out newlyConfirmed) == null
                    && db.PaymentEntries.Count(p => p.BookingId == quick.Booking.Id) == 1, "Double click and refreshed confirmation never duplicate deposit");
                Check(extras.ConfirmBalance(admin.Id, quick.Booking.Id, Guid.NewGuid()) == null
                    && db.PaymentEntries.Count(p => p.BookingId == quick.Booking.Id) == 2, "One click records remaining balance");
                Check(service.CancelByClient(quick.Booking.Id, client.Id).Success && extras.ConfirmRefund(admin.Id, quick.Booking.Id, Guid.NewGuid()) == null
                    && db.PaymentEntries.Single(p => p.BookingId == quick.Booking.Id && p.Type == PaymentEntryType.Refund).Amount == 1000000m,
                    "One click records full refund after cancellation");
                var now = new DateTime(2026, 10, 26, 10, 0, 0);
                Check(AvailabilityService.Validate(now.Date, TimeSpan.FromHours(9), TimeSpan.FromHours(12), now) != null, "Search rejects elapsed time today");
                Check(AvailabilityService.Validate(now.Date.AddDays(1), null, TimeSpan.FromHours(12), now) != null, "Search rejects incomplete time filter");
                Check(AvailabilityService.Validate(now.Date.AddDays(1), TimeSpan.FromHours(9), TimeSpan.FromHours(12), now) == null, "Search accepts valid future time filter");
                first.Booking.Venue = venue; first.Booking.User = client; first.Booking.Notes = "=HYPERLINK(\"evil\")\u0001";
                var email = EmailTemplates.DepositRequest(first.Booking);
                Check(email.Body.Contains("cid:deposit-qr") && email.Body.Contains("EVENTHUB " + id), "Deposit email contains embedded QR reference and booking transfer reference");
                var mailFolder = Path.Combine(Path.GetTempPath(), "eventhub-mail-checks-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(mailFolder);
                var qrFile = Path.Combine(mailFolder, "qr.jpg");
                try
                {
                    File.WriteAllBytes(qrFile, new byte[] { 255, 216, 255, 217 });
                    using (var message = Mailer.BuildMessage("sender@example.invalid", "Test", "customer@example.invalid", email.Subject, email.Body, qrFile))
                    using (var smtp = new System.Net.Mail.SmtpClient())
                    {
                        Check(message.AlternateViews.Count == 1 && message.AlternateViews[0].LinkedResources[0].ContentId == "deposit-qr", "QR image is included as an inline email resource");
                        smtp.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.SpecifiedPickupDirectory;
                        smtp.PickupDirectoryLocation = mailFolder;
                        smtp.Send(message);
                    }
                    var mime = File.ReadAllText(Directory.GetFiles(mailFolder, "*.eml").Single());
                    Check(mime.IndexOf("Content-ID: <deposit-qr>", StringComparison.OrdinalIgnoreCase) >= 0 && mime.Contains("image/jpeg"), "Serialized email embeds the QR image without requiring public localhost URLs");
                }
                finally { foreach (var file in Directory.GetFiles(mailFolder)) File.Delete(file); Directory.Delete(mailFolder); }
                var report = new MonthlyReportViewModel { Month = 10, Year = 2026, Bookings = new List<Booking> { first.Booking, adjacent.Booking }, Receipts = 360000m, Refunds = 60000m };
                Check(report.ContractValue == 1200000m && report.CancellationRate == 50m && report.NetCollected == 300000m, "Report separates contract value, cancellation rate and net receipts");
                var workbook = ExcelReportWriter.Write(report);
                using (var stream = new MemoryStream(workbook))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    Check(zip.GetEntry("[Content_Types].xml") != null && zip.GetEntry("xl/workbook.xml") != null, "Export is an Open XML workbook package");
                    foreach (var entry in zip.Entries) using (var part = entry.Open()) XDocument.Load(part);
                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    using (var part = zip.GetEntry("xl/worksheets/sheet2.xml").Open())
                    {
                        var sheet = XDocument.Load(part);
                        Check(!sheet.Descendants(ns + "f").Any() && sheet.Descendants(ns + "t").Any(t => t.Value.StartsWith("=HYPERLINK")), "Formula-like text remains literal and invalid XML chars removed");
                    }
                }
                Console.WriteLine("All " + passed + " feature checks passed. No email sent.");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // Only the uniquely named database created by this test can be removed.
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^EventHub_FeatureTests_[a-f0-9]{32}$"))
                using (var cleanup = new EventHubContext(connection)) cleanup.Database.Delete();
        }
    }
}
