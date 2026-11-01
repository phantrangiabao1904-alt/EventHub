using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using EventHub.Helpers;
using EventHub.ViewModels;

namespace EventHub.Services
{
     
    public static class ExcelReportWriter
    {
        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static byte[] Write(MonthlyReportViewModel report)
        {
            var summary = new List<object[]> {
                new object[] { "Chỉ tiêu", "Giá trị" },
                new object[] { "Kỳ báo cáo theo ngày tổ chức", report.Month.ToString("D2") + "/" + report.Year },
                new object[] { "Tổng số đơn", report.Bookings.Count },
                new object[] { "Chờ xác nhận", report.Pending },
                new object[] { "Đã duyệt - Chờ cọc", report.AwaitingDeposit },
                new object[] { "Đã xác nhận", report.Confirmed },
                new object[] { "Hoàn thành", report.Completed },
                new object[] { "Đã hủy", report.Cancelled },
                new object[] { "Tỷ lệ hủy (%)", report.CancellationRate },
                new object[] { "Giá trị đơn xác nhận/hoàn thành (VND; không phải tiền đã thu)", report.ContractValue },
                new object[] { "Tổng thu theo ngày ghi nhận phiếu (VND)", report.Receipts },
                new object[] { "Tổng hoàn theo ngày ghi nhận phiếu (VND)", report.Refunds },
                new object[] { "Thu ròng trong tháng (VND)", report.NetCollected }
            };
            var rows = new List<object[]> { new object[] { "Mã đơn", "Địa điểm", "Khách hàng", "Email", "Ngày tổ chức", "Bắt đầu", "Kết thúc", "Số khách", "Tổng giá trị (VND)", "Trạng thái", "Ngày tạo", "Ghi chú" } };
            rows.AddRange(report.Bookings.Select(b => new object[] { b.Id, b.Venue.Name, b.User.FullName, b.User.Email,
                b.EventDate.ToString("dd/MM/yyyy"), b.StartTime.ToString(@"hh\:mm"), b.EndTime.ToString(@"hh\:mm"), b.GuestCount,
                b.TotalPrice, Vi.BookingStatusName(b.Status), b.CreatedAt.ToString("dd/MM/yyyy HH:mm"), b.Notes ?? "" }));
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
                    Save(zip, "[Content_Types].xml", new XElement(ct + "Types",
                        new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                        new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                        Override(ct, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                        Override(ct, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
                        Override(ct, "/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                    Save(zip, "_rels/.rels", new XElement(P + "Relationships", Rel("rId1", "officeDocument", "xl/workbook.xml")));
                    Save(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R),
                        new XElement(S + "sheets", Sheet("Tổng quan", 1), Sheet("Đơn đặt chỗ", 2))));
                    Save(zip, "xl/_rels/workbook.xml.rels", new XElement(P + "Relationships",
                        Rel("rId1", "worksheet", "worksheets/sheet1.xml"), Rel("rId2", "worksheet", "worksheets/sheet2.xml")));
                    Save(zip, "xl/worksheets/sheet1.xml", Worksheet(summary));
                    Save(zip, "xl/worksheets/sheet2.xml", Worksheet(rows));
                }
                return stream.ToArray();
            }
        }

        private static XElement Worksheet(List<object[]> rows)
        {
            return new XElement(S + "worksheet",
                new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0),
                    new XElement(S + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", 1), new XAttribute("max", 12), new XAttribute("width", 24), new XAttribute("customWidth", 1))),
                new XElement(S + "sheetData", rows.Select((row, r) => new XElement(S + "row", new XAttribute("r", r + 1),
                    row.Select((value, c) => Cell(((char)('A' + c)).ToString() + (r + 1), value))))),
                new XElement(S + "autoFilter", new XAttribute("ref", "A1:" + (char)('A' + rows[0].Length - 1) + rows.Count)));
        }

        private static XElement Cell(string address, object value)
        {
            if (value is int || value is decimal)
                return new XElement(S + "c", new XAttribute("r", address), new XElement(S + "v", Convert.ToString(value, CultureInfo.InvariantCulture)));
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            var safe = new StringBuilder();
            for (var i = 0; i < text.Length && safe.Length < 32766; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                { safe.Append(text[i]); safe.Append(text[++i]); }
                else if (System.Xml.XmlConvert.IsXmlChar(text[i])) safe.Append(text[i]);
            }
            return new XElement(S + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"),
                new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), safe.ToString())));
        }

        private static XElement Sheet(string name, int id) { return new XElement(S + "sheet", new XAttribute("name", name), new XAttribute("sheetId", id), new XAttribute(R + "id", "rId" + id)); }
        private static XElement Rel(string id, string type, string target) { return new XElement(P + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target)); }
        private static XElement Override(XNamespace ns, string part, string type) { return new XElement(ns + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", type)); }
        private static void Save(ZipArchive zip, string name, XElement element)
        {
            using (var part = zip.CreateEntry(name).Open()) new XDocument(new XDeclaration("1.0", "utf-8", null), element).Save(part);
        }
    }
}
