using System.IO;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace API_ThiTracNghiem.Services
{
    public interface ICloudStorage
    {
        Task<string> UploadImageAsync(IFormFile file, string folder);
        Task<string> UploadFileAsync(IFormFile file, string folder);
    }

    public class CloudinaryService : ICloudStorage
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration configuration)
        {
            var cloud = configuration["Cloudinary:CloudName"];
            var key = configuration["Cloudinary:ApiKey"];
            var secret = configuration["Cloudinary:ApiSecret"];
            _cloudinary = new Cloudinary(new Account(cloud, key, secret));
            _cloudinary.Api.Secure = true;
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0) return string.Empty;

            await using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };
            var result = await _cloudinary.UploadAsync(uploadParams);
            return result.SecureUrl?.ToString() ?? string.Empty;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0) return string.Empty;

            var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

            await using var stream = file.OpenReadStream();

            if (contentType.StartsWith("video/"))
            {
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false,
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                return result.SecureUrl?.ToString() ?? string.Empty;
            }

            if (contentType == "application/pdf" ||
                contentType.Contains("msword") ||
                contentType.Contains("officedocument"))
            {
                // Upload như image để Cloudinary cho phép render/xem (tránh chặn raw delivery với tài khoản untrusted)
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false,
                    // Đảm bảo giữ đuôi .pdf hoặc docx trên public_id để delivery đúng
                    Format = null
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                return result.SecureUrl?.ToString() ?? string.Empty;
            }

            // Mặc định: upload như hình ảnh
            var imageParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };
            var imageResult = await _cloudinary.UploadAsync(imageParams);
            return imageResult.SecureUrl?.ToString() ?? string.Empty;
        }
    }
}


