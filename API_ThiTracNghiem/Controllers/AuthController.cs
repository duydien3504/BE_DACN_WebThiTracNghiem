using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Utils;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;
        private readonly ITokenService _tokenService;

        public AuthController(ApplicationDbContext db, IEmailService email, ITokenService tokenService)
        {
            _db = db;
            _email = email;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Kiểm tra tồn tại user theo Email/Phone
            var existed = await _db.Users.AnyAsync(u => u.Email == request.Email || u.PhoneNumber == request.PhoneNumber);
            if (existed) return Conflict(new { message = "Email hoặc SĐT đã tồn tại" });

            // Parse ngày sinh dd/MM/yyyy
            DateTime? dob = null;
            if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
            {
                if (DateTime.TryParseExact(request.DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    dob = d;
                }
                else
                {
                    return BadRequest(new { message = "Ngày sinh không đúng định dạng dd/MM/yyyy" });
                }
            }

            // Tạo OTP và lưu DB
            var otpCode = new Random().Next(100000, 999999).ToString();

            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Gender = request.Gender,
                DateOfBirth = dob,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = await _db.Roles.Where(r => r.RoleName == "Student").Select(r => r.RoleId).FirstOrDefaultAsync(),
                IsEmailVerified = false,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();

            var otp = new OTP
            {
                UserId = user.UserId,
                OtpCode = otpCode,
                Purpose = "register",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            await _db.OTPs.AddAsync(otp);
            await _db.SaveChangesAsync();

            // Gửi email OTP
            var subject = "Mã xác thực đăng ký";
            var body = EmailTemplates.BuildOtpCard("Xin chào " + request.FullName + ",", "Mã OTP đăng ký của bạn", otpCode, 5);
            await _email.SendAsync(request.Email, subject, body);

            return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để nhập OTP xác thực." });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var now = DateTime.UtcNow;
            var otp = await _db.OTPs
                .Where(o => o.UserId == user.UserId && o.OtpCode == request.Otp && !o.IsUsed && o.ExpiresAt > now)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn" });
            }

            otp.IsUsed = true;
            user.IsEmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Xác thực thành công" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });

            var ok = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!ok) return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });

            // Phát sinh OTP login và gửi email
            var otpCode = new Random().Next(100000, 999999).ToString();
            var otp = new OTP
            {
                UserId = user.UserId,
                OtpCode = otpCode,
                Purpose = "login",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            await _db.OTPs.AddAsync(otp);
            await _db.SaveChangesAsync();

            var subject = "Mã OTP đăng nhập";
            var body = EmailTemplates.BuildOtpCard(null, "Mã OTP đăng nhập của bạn", otpCode, 5);
            await _email.SendAsync(user.Email!, subject, body);

            return Ok(new { message = "Đã gửi OTP tới email. Vui lòng xác minh." });
        }

        [HttpPost("verify-login-otp")]
        public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var now = DateTime.UtcNow;
            var otp = await _db.OTPs
                .Where(o => o.UserId == user.UserId && o.OtpCode == request.Otp && !o.IsUsed && o.ExpiresAt > now && o.Purpose == "login")
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn" });
            }

            otp.IsUsed = true;
            await _db.SaveChangesAsync();

            var roleName = (await _db.Roles.Where(r => r.RoleId == user.RoleId).Select(r => r.RoleName).FirstOrDefaultAsync()) ?? "Student";
            var (token, expiresAt) = _tokenService.Generate(user, roleName);

            // Tạo phiên đăng nhập
            var session = new AuthSession
            {
                UserId = user.UserId,
                DeviceInfo = Request.Headers["User-Agent"].ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                LoginAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.AuthSessions.Add(session);
            await _db.SaveChangesAsync();

            return Ok(new { token, expiresAt });
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Tự đọc token từ header và validate để tránh 401 trước khi vào action
            var auth = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(auth)) return Unauthorized();

            var token = auth.Trim();
            var prefix = "Bearer ";
            while (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring(prefix.Length).TrimStart();
            }
            token = token.Trim('"');

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config["Jwt:Key"]))
            };

            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, parameters, out var _);
                var sub = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                          ?? principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(sub, out var userId)) return Unauthorized();

                var session = await _db.AuthSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .OrderByDescending(s => s.LoginAt)
                    .FirstOrDefaultAsync();

                if (session != null)
                {
                    session.IsActive = false;
                    session.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }

                return Ok(new { message = "logged out" });
            }
            catch
            {
                return Unauthorized();
            }
        }
    }
}


