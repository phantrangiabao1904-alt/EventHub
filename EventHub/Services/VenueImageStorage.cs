using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web;

namespace EventHub.Services
{
    public static class VenueImageStorage
    {
        public static string Save(HttpPostedFileBase file, string directory, string virtualDirectory = "/Uploads/Venues")
        {
            if (file.ContentLength <= 0 || file.ContentLength > 3 * 1024 * 1024)
                throw new ArgumentException("Ảnh phải có dung lượng từ 1 byte đến 3 MB.");
            using (var image = Image.FromStream(file.InputStream, true, true))
            {
                if (!image.RawFormat.Equals(ImageFormat.Jpeg) && !image.RawFormat.Equals(ImageFormat.Png))
                    throw new ArgumentException("Chỉ hỗ trợ ảnh JPEG hoặc PNG.");
                if (image.Width > 8000 || image.Height > 8000 || (long)image.Width * image.Height > 24000000)
                    throw new ArgumentException("Ảnh tối đa 8000 pixel mỗi chiều và 24 megapixel.");
                Directory.CreateDirectory(directory);
                var name = Guid.NewGuid().ToString("N") + ".jpg";
                using (var bitmap = new Bitmap(image)) bitmap.Save(Path.Combine(directory, name), ImageFormat.Jpeg);
                return virtualDirectory.TrimEnd('/') + "/" + name;
            }
        }
    }
}
