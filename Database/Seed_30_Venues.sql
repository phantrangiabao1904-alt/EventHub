 
 
 
 
USE [EventHubMvc];
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

IF OBJECT_ID(N'dbo.Venues', N'U') IS NULL
    THROW 50001, 'Khong tim thay dbo.Venues. Hay khoi tao database EventHubMvc truoc.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Added int = 0, @Filled int = 0;
    DECLARE @Samples TABLE (
        [Name] nvarchar(200) NOT NULL PRIMARY KEY,
        [Description] nvarchar(max) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [Capacity] int NOT NULL,
        [Price] decimal(18,0) NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL
    );
    INSERT INTO @Samples ([Name], [Description], [Address], [Capacity], [Price], [ImageUrl]) VALUES
(N'Sảnh tiệc Grand Ballroom', N'Sảnh lớn với đèn chùm và không gian bàn tiệc rộng, phù hợp tiệc cưới, gala dinner và lễ kỷ niệm doanh nghiệp. Có khu vực sân khấu và đón khách. Giá thuê không gian cho một sự kiện, chưa gồm suất ăn, trang trí và dịch vụ thuê thêm.', N'Đường Nguyễn Huệ, TP. Hồ Chí Minh', 500, 25000000, N'/Content/images/venues/sample-01.jpg'),
(N'Sảnh tiệc White Palace', N'Sảnh sáng với cửa sổ lớn, tông màu trắng và lối đi thoáng. Phù hợp tiệc cưới vừa, hội nghị khách hàng và lễ ra mắt sản phẩm. Có thể bố trí bàn tròn hoặc bàn dài; giá chưa gồm thực đơn và dịch vụ đi kèm.', N'Đường Phan Đăng Lưu, TP. Hồ Chí Minh', 300, 18000000, N'/Content/images/venues/sample-02.jpg'),
(N'Sảnh Hoàng Gia', N'Không gian phong cách cổ điển, trần cao và đèn chùm, thích hợp tiệc cưới lớn, lễ vinh danh và gala doanh nghiệp. Khu vực sân khấu rộng để sắp xếp chương trình. Giá thuê địa điểm cho một sự kiện, chưa gồm ẩm thực và trang trí.', N'Đường Lê Duẩn, Hà Nội', 600, 32000000, N'/Content/images/venues/sample-03.jpg'),
(N'Sảnh Ánh Dương', N'Sảnh tiệc trong nhà với ánh sáng ấm và nhiều vị trí bố trí bàn tròn. Phù hợp lễ cưới, tiệc tất niên và gặp mặt đối tác. Có lối đi giữa sảnh thuận tiện cho chương trình sân khấu. Giá chưa gồm suất ăn hoặc gói âm thanh thuê thêm.', N'Đường Nguyễn Văn Linh, Đà Nẵng', 400, 22000000, N'/Content/images/venues/sample-04.jpg'),
(N'Vườn Hoàng Hôn', N'Khu vườn ngoài trời với đèn dây và không gian xanh, phù hợp tiệc cưới thân mật, tiệc tối và họp mặt công ty. Dễ bố trí khu đón khách và bàn tiệc theo chủ đề. Giá thuê không gian, chưa gồm trang trí, đồ ăn và phương án mái che.', N'Đường Trần Phú, Đà Nẵng', 200, 12000000, N'/Content/images/venues/sample-05.jpg'),
(N'Vườn tiệc Mộc Nhiên', N'Không gian tiệc ngoài trời phong cách mộc với bàn dài, phù hợp đính hôn, sinh nhật và họp mặt gia đình. Bố cục gần gũi, dễ xây dựng chủ đề hoa và ánh sáng. Giá thuê cho một sự kiện, chưa gồm thực đơn và trang trí theo yêu cầu.', N'Đường Nguyễn Duy Trinh, TP. Hồ Chí Minh', 120, 7500000, N'/Content/images/venues/sample-06.jpg'),
(N'Sảnh Hải Đăng', N'Sảnh tiệc có mái với ánh sáng tự nhiên, phù hợp tiệc trưa, họp mặt hội nhóm và lễ kỷ niệm. Có thể bố trí bàn tròn theo quy mô khách. Giá thuê chưa gồm suất ăn, đồ uống và thiết bị âm thanh bổ sung.', N'Đường Hùng Vương, Nha Trang', 250, 14000000, N'/Content/images/venues/sample-07.jpg'),
(N'Không gian tiệc Riverside', N'Không gian bàn tiệc thân mật với ánh sáng nhẹ, phù hợp tiệc tri ân, sinh nhật và lễ đính hôn. Bố cục bàn dài dễ tương tác giữa khách mời. Giá thuê riêng không gian cho một sự kiện, chưa gồm hoa tươi, suất ăn và đồ uống.', N'Đường Nguyễn Hữu Cảnh, TP. Hồ Chí Minh', 100, 8000000, N'/Content/images/venues/sample-08.jpg'),
(N'Hội trường Sông Hồng', N'Hội trường với khu vực trình chiếu lớn, bố trí phù hợp hội nghị, tọa đàm và chương trình đào tạo đông người. Có thể sắp ghế theo lớp học hoặc bàn hội thảo. Giá thuê không gian, chưa gồm thiết bị chuyên dụng, teabreak và nhân sự sự kiện.', N'Đường Giảng Võ, Hà Nội', 350, 15000000, N'/Content/images/venues/sample-09.jpg'),
(N'Không gian Kết Nối', N'Không gian giao lưu mở dành cho tọa đàm, networking và sinh hoạt cộng đồng. Sắp xếp chỗ ngồi linh hoạt để khách trao đổi và hoạt động theo nhóm. Giá thuê địa điểm cho một sự kiện, chưa gồm đồ uống, sân khấu và âm thanh bổ sung.', N'Đường Nguyễn Thị Minh Khai, TP. Hồ Chí Minh', 180, 8000000, N'/Content/images/venues/sample-10.jpg'),
(N'Phòng họp Lotus', N'Phòng họp kính yên tĩnh với bàn họp trung tâm, phù hợp họp nội bộ, phỏng vấn nhóm và trao đổi đối tác. Không gian đủ riêng tư để trình bày và thảo luận. Giá thuê cho một buổi sự kiện theo lịch đã chọn, chưa gồm teabreak và thiết bị gọi trực tuyến.', N'Đường Lê Lợi, Hà Nội', 20, 2000000, N'/Content/images/venues/sample-11.jpg'),
(N'Phòng họp Executive', N'Phòng họp doanh nghiệp với bàn dài và không gian thuyết trình, phù hợp họp điều hành, ký kết và đào tạo nhóm nhỏ. Giá thuê địa điểm cho một sự kiện; dịch vụ nước uống, teabreak và hỗ trợ kỹ thuật tính riêng khi chọn thêm.', N'Đường Điện Biên Phủ, TP. Hồ Chí Minh', 30, 3000000, N'/Content/images/venues/sample-12.jpg'),
(N'Phòng họp Panorama', N'Phòng họp có cửa kính rộng và tầm nhìn thoáng, phù hợp họp đối tác, trình bày dự án và hội thảo nhỏ. Ánh sáng tự nhiên tạo cảm giác dễ chịu khi làm việc. Giá thuê không gian, chưa gồm teabreak và các thiết bị trình chiếu thuê thêm.', N'Đường Trần Hưng Đạo, Đà Nẵng', 40, 3800000, N'/Content/images/venues/sample-13.jpg'),
(N'Không gian triển lãm Minimal', N'Không gian tối giản, tông sáng và sàn thoáng, phù hợp trưng bày sản phẩm, triển lãm nhỏ và họp báo. Có thể chủ động bố trí khu trải nghiệm và bàn đón khách. Giá thuê chưa gồm vách trưng bày, nội thất thuê thêm và thi công trang trí.', N'Đường Tràng Tiền, Hà Nội', 100, 5000000, N'/Content/images/venues/sample-14.jpg'),
(N'Workshop Studio Sáng Tạo', N'Studio có bàn làm việc nhóm và ánh sáng tự nhiên, phù hợp workshop thủ công, lớp kỹ năng và buổi chia sẻ sáng tạo. Không gian linh hoạt cho hoạt động thực hành. Giá thuê chưa gồm vật liệu workshop, đồ uống và thiết bị chuyên dụng.', N'Đường Võ Văn Tần, TP. Hồ Chí Minh', 40, 3000000, N'/Content/images/venues/sample-15.jpg'),
(N'Vườn Trà An Nhiên', N'Góc vườn xanh với bàn ghế gỗ, phù hợp tiệc trà, workshop nhẹ và họp mặt thân mật. Không gian gần gũi thiên nhiên dành cho sự kiện quy mô nhỏ. Giá thuê chưa gồm thực đơn trà bánh, trang trí và phương án che mưa.', N'Đường Hoàng Hoa Thám, Đà Lạt', 60, 3500000, N'/Content/images/venues/sample-16.jpg'),
(N'Phòng Workshop Gỗ Ấm', N'Không gian bàn nhóm với tông gỗ ấm, phù hợp workshop thiết kế, đào tạo nội bộ và họp dự án. Có thể linh hoạt sắp xếp vị trí người hướng dẫn. Giá thuê địa điểm cho một sự kiện, chưa gồm vật tư và dịch vụ ăn uống.', N'Đường Nguyễn Văn Cừ, Cần Thơ', 50, 3200000, N'/Content/images/venues/sample-17.jpg'),
(N'Coworking Hội Thảo', N'Không gian làm việc mở phù hợp ngày hội việc làm, đào tạo nhóm và sự kiện cộng đồng khởi nghiệp. Bố cục nhiều khu chức năng thuận tiện cho thảo luận. Giá thuê không gian, chưa gồm hệ thống âm thanh, teabreak và hỗ trợ tổ chức.', N'Đường Cách Mạng Tháng Tám, TP. Hồ Chí Minh', 120, 6000000, N'/Content/images/venues/sample-18.jpg'),
(N'Nhà hàng Mộc', N'Không gian nhà hàng phong cách gỗ hiện đại, phù hợp sinh nhật, tiệc công ty nhỏ và gặp mặt đối tác. Có thể bố trí khu dùng tiệc riêng theo nhóm khách. Giá chỉ là phí thuê không gian cho sự kiện, thực đơn và đồ uống tính riêng.', N'Đường Nguyễn Tri Phương, TP. Hồ Chí Minh', 80, 6000000, N'/Content/images/venues/sample-19.jpg'),
(N'Sân vườn Ngọc Lục Bảo', N'Sân vườn có lối đi xanh và khu bàn ngoài trời, phù hợp tiệc trưa, ra mắt sản phẩm nhỏ và lễ kỷ niệm gia đình. Không gian thuận tiện cho giao lưu và chụp ảnh. Giá thuê chưa gồm tiệc, hoa trang trí và phương án che thời tiết.', N'Đường Bạch Đằng, Đà Nẵng', 100, 6500000, N'/Content/images/venues/sample-20.jpg'),
(N'Nhà hàng Gạch Đỏ', N'Không gian nhà hàng tường gạch và ánh đèn ấm, phù hợp tiệc liên hoan, gặp mặt đối tác và kỷ niệm doanh nghiệp. Có thể bố trí bàn theo nhóm. Giá thuê riêng không gian, không bao gồm suất ăn, đồ uống và dịch vụ biểu diễn.', N'Đường Phan Đình Phùng, Hà Nội', 100, 7000000, N'/Content/images/venues/sample-21.jpg'),
(N'Cafe Warehouse', N'Cafe phong cách công nghiệp với không gian bàn ghế rộng, phù hợp talkshow, offline câu lạc bộ và giới thiệu sách. Dễ bố trí khu diễn giả và quầy tiếp đón. Giá thuê địa điểm cho một sự kiện, đồ uống và âm thanh tính riêng.', N'Đường Pasteur, TP. Hồ Chí Minh', 80, 4500000, N'/Content/images/venues/sample-22.jpg'),
(N'Sân hiên Biển Xanh', N'Không gian sân hiên thoáng với khu bàn tiệc mở, phù hợp tiệc chiều, sinh nhật và gặp mặt nhóm. Không gian nghỉ dưỡng nhẹ nhàng cho sự kiện thân mật. Giá thuê chưa gồm thực đơn, trang trí theo chủ đề và thiết bị sự kiện.', N'Đường Trần Phú, Nha Trang', 120, 9000000, N'/Content/images/venues/sample-23.jpg'),
(N'Lounge Thành Phố', N'Không gian lounge với quầy bar và ánh sáng ấm, phù hợp tiệc networking, sinh nhật người lớn và tiệc tri ân nhóm nhỏ. Giá chỉ bao gồm phí thuê không gian; đồ uống, nhân sự phục vụ và biểu diễn được chọn thêm theo nhu cầu.', N'Đường Hai Bà Trưng, TP. Hồ Chí Minh', 80, 6500000, N'/Content/images/venues/sample-24.jpg'),
(N'Rooftop Skyline', N'Sân thượng với tầm nhìn thành phố và khu bàn ngoài trời, phù hợp tiệc hoàng hôn, ra mắt thương hiệu và networking. Giá thuê cho một sự kiện, chưa gồm đồ ăn, đồ uống, âm thanh và phương án che thời tiết.', N'Đường Lý Tự Trọng, TP. Hồ Chí Minh', 150, 11000000, N'/Content/images/venues/sample-25.jpg'),
(N'Cafe Vườn Lá Xanh', N'Cafe vườn nhỏ với nhiều cây xanh, phù hợp workshop, sinh hoạt câu lạc bộ và họp mặt bạn bè. Không gian nhẹ nhàng dành cho sự kiện quy mô nhỏ. Giá thuê địa điểm chưa gồm đồ uống, bánh và vật tư workshop.', N'Đường Từ Hoa, Hà Nội', 40, 2800000, N'/Content/images/venues/sample-26.jpg'),
(N'Sân khấu ngoài trời Ánh Sao', N'Khu tổ chức chương trình ngoài trời quy mô lớn, phù hợp đêm nhạc, lễ hội cộng đồng và sự kiện doanh nghiệp. Sức chứa theo bố trí khán giả đứng hoặc ghế. Giá thuê mặt bằng, chưa gồm sân khấu kỹ thuật, âm thanh ánh sáng, an ninh và giấy phép chương trình.', N'Đường Nguyễn Tất Thành, Đà Nẵng', 800, 40000000, N'/Content/images/venues/sample-27.jpg'),
(N'Sảnh cưới Hoa Trắng', N'Sảnh cưới tông trắng với lối đi trung tâm và phong cách trang nhã, phù hợp lễ cưới, đính hôn và lễ kỷ niệm lớn. Giá thuê không gian cho một sự kiện, chưa gồm gói hoa, thực đơn và dịch vụ tổ chức trọn gói.', N'Đường Hùng Vương, Huế', 350, 20000000, N'/Content/images/venues/sample-28.jpg'),
(N'Sảnh sự kiện Neon', N'Không gian sự kiện tối phù hợp tiệc âm nhạc, gala trẻ và chương trình giải trí có sân khấu. Có thể bố trí khu khán giả đứng hoặc bàn tiệc theo chương trình. Giá thuê địa điểm, chưa gồm âm thanh ánh sáng chuyên nghiệp, nhân sự và các dịch vụ kỹ thuật.', N'Đường Nguyễn Văn Linh, TP. Hồ Chí Minh', 500, 28000000, N'/Content/images/venues/sample-29.jpg'),
(N'Rooftop Mây Trắng', N'Sân thượng thoáng với bàn ghế gỗ và không gian thư giãn, phù hợp tiệc chiều, sinh nhật và workshop ngoài trời. Giá thuê cho một sự kiện, chưa gồm đồ uống, trang trí, âm thanh và phương án che mưa.', N'Đường Hoàng Văn Thụ, Đà Lạt', 100, 7000000, N'/Content/images/venues/sample-30.jpg')
;

     
    UPDATE v
    SET [ImageUrl] = CASE WHEN NULLIF(LTRIM(RTRIM(v.[ImageUrl])), N'') IS NULL THEN s.[ImageUrl] ELSE v.[ImageUrl] END,
        [Description] = CASE WHEN NULLIF(LTRIM(RTRIM(v.[Description])), N'') IS NULL THEN s.[Description] ELSE v.[Description] END
    FROM dbo.Venues AS v
    INNER JOIN @Samples AS s ON LTRIM(RTRIM(v.[Name])) = s.[Name]
    WHERE NULLIF(LTRIM(RTRIM(v.[ImageUrl])), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(v.[Description])), N'') IS NULL;
    SET @Filled = @@ROWCOUNT;

    INSERT INTO dbo.Venues ([Name], [Description], [Address], [Capacity], [Price], [Status], [ImageUrl], [CreatedAt])
    SELECT s.[Name], s.[Description], s.[Address], s.[Capacity], s.[Price], 1, s.[ImageUrl], GETDATE()
    FROM @Samples AS s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Venues AS v WITH (UPDLOCK, HOLDLOCK) WHERE LTRIM(RTRIM(v.[Name])) = s.[Name]);
    SET @Added = @@ROWCOUNT;

    COMMIT TRANSACTION;
    SELECT @Added AS AddedVenues, @Filled AS FilledExistingVenues,
        (SELECT COUNT(*) FROM @Samples AS s WHERE EXISTS (SELECT 1 FROM dbo.Venues AS v WHERE LTRIM(RTRIM(v.[Name])) = s.[Name])) AS MatchedSampleVenues,
        (SELECT COUNT(*) FROM @Samples AS s WHERE EXISTS (SELECT 1 FROM dbo.Venues AS v WHERE LTRIM(RTRIM(v.[Name])) = s.[Name] AND NULLIF(LTRIM(RTRIM(v.[ImageUrl])), N'') IS NOT NULL)) AS SamplesWithImages;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;