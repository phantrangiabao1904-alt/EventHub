using System;

namespace EventHub.Services
{
    public static class AvailabilityService
    {
        public static string Validate(DateTime? date, TimeSpan? start, TimeSpan? end, DateTime now)
        {
            if (!date.HasValue && !start.HasValue && !end.HasValue) return null;
            if (!date.HasValue || !start.HasValue || !end.HasValue)
                return "Vui lòng chọn đủ ngày, giờ bắt đầu và giờ kết thúc.";
            if (date.Value.Date < now.Date) return "Ngày tổ chức không được ở trong quá khứ.";
            if (start.Value < TimeSpan.Zero || end.Value >= TimeSpan.FromDays(1) || end.Value <= start.Value)
                return "Giờ kết thúc phải sau giờ bắt đầu và trong cùng ngày (00:00–23:59).";
            if (date.Value.Date == now.Date && start.Value <= now.TimeOfDay)
                return "Giờ bắt đầu phải sau thời điểm hiện tại.";
            return null;
        }
    }
}
