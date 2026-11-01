# EventHub

Hệ thống đặt địa điểm tổ chức sự kiện bằng ASP.NET MVC 5.2.9, .NET Framework 4.8, Entity Framework 6.4.4 Code First và SQL Server.

## Chạy dự án

1. Cài Visual Studio với workload ASP.NET and web development và .NET Framework 4.8 Developer Pack.
2. Giải nén toàn bộ thư mục, mở `EventHub.sln`.
3. Build Solution, sau đó nhấn Ctrl + F5. Nếu thư viện chưa được nhận, chọn Restore NuGet Packages.

Thư mục `packages` được giữ để phục vụ build. Cấu hình SQL Server online đã có trong `EventHub/Web.config`; cần kết nối Internet. Server là `EventHubMVC.mssql.somee.com`, database là `EventHubMVC`.

## Tài khoản mẫu

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Quản trị viên | admin@eventhub.com | Admin@123 |
| Điều phối viên | coordinator@eventhub.com | Coord@123 |
| Khách hàng | client@eventhub.com | Client@123 |

## Database và migrations

Entity nằm trong `EventHub/Models`, DbContext nằm trong `EventHub/Data/EventHubContext.cs`, cấu hình EF6 Code First Migrations nằm trong `EventHub/Migrations/Configuration.cs`. Ứng dụng sử dụng migrations để tạo hoặc cập nhật cấu trúc bảng khi khởi động. EF6 tự quản lý bảng `__MigrationHistory`.

Database Somee đã có cấu trúc và 30 địa điểm mẫu. File `Database/Seed_30_Venues.sql` dùng để nạp lại danh mục bằng SQL khi cần; có thể chạy lại mà không tạo trùng địa điểm. Ảnh mẫu nằm trong `EventHub/Content/images/venues`.

## Cấu hình email

Để dùng đăng ký OTP, quên mật khẩu và gửi email QR, sao chép `EventHub/secrets.config.example` thành `EventHub/secrets.config`, rồi điền tài khoản SMTP và mật khẩu ứng dụng của người gửi. Không cần cấu hình email để đăng nhập bằng tài khoản mẫu và kiểm tra các trang quản lý. Khi chưa cấu hình SMTP, chức năng gửi email sẽ báo chưa được cấu hình hoặc gửi thất bại.

## Chức năng chính

Khách hàng có thể lọc địa điểm và lịch trống, gửi yêu cầu kèm dịch vụ, xem đơn, in phiếu và đánh giá đơn hoàn thành. Nhân viên duyệt yêu cầu và gửi QR; đơn chỉ đặt thành công khi nhân viên bấm xác nhận đã nhận đủ cọc. Admin quản lý địa điểm, ảnh, dịch vụ, người dùng, phần trăm cọc, QR và báo cáo Excel.

`EventHub/Uploads` lưu ảnh do người dùng tải lên và cần được giữ cùng ứng dụng. `Database/IMAGE_CREDITS.md` ghi nguồn ảnh minh họa. Bản nộp giữ cấu hình Somee theo lựa chọn của chủ dự án, không kèm tài khoản SMTP riêng.
