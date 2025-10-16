using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_ThiTracNghiem.Contracts;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ICloudStorage _cloud;

        public UsersController(ApplicationDbContext db, ICloudStorage cloud)
        {
            _db = db;
            _cloud = cloud;
        }

        [Authorize]
        [HttpPost("upload-avatar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> UploadAvatar([FromForm] UploadAvatarRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var file = request.File;
            if (file == null || file.Length == 0) return BadRequest(new { message = "File rỗng" });
            if (!file.ContentType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Chỉ chấp nhận định dạng ảnh" });

            var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var userId)) return Unauthorized();

            var url = await _cloud.UploadImageAsync(file, "avatars");
            if (string.IsNullOrWhiteSpace(url)) return StatusCode(500, new { message = "Upload thất bại" });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound();

            user.AvatarUrl = url;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { avatarUrl = url });
        }
    }
}


