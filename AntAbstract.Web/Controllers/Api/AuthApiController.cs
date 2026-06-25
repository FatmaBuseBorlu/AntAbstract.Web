using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AntAbstract.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _config;

        public AuthApiController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
        }

        public class LoginRequest
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;
            [Required]
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        [EnableRateLimiting("api-auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Unauthorized(new { error = "Geçersiz e-posta veya şifre." });

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                    return Unauthorized(new { error = "Hesabınız geçici olarak kilitlendi." });
                return Unauthorized(new { error = "Geçersiz e-posta veya şifre." });
            }

            if (await _userManager.GetTwoFactorEnabledAsync(user))
                return TwoFactorRequired();

            var token = await GenerateJwtAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                token,
                expiresIn = 86400,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    roles
                }
            });
        }

        [HttpPost("refresh")]
        [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> Refresh()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            if (await _userManager.GetTwoFactorEnabledAsync(user))
                return TwoFactorRequired();

            var token = await GenerateJwtAsync(user);
            return Ok(new { token, expiresIn = 86400 });
        }

        private ObjectResult TwoFactorRequired()
        {
            return StatusCode(403, new
            {
                requiresTwoFactor = true,
                error = "İki faktörlü doğrulama gerekli. Lütfen web giriş ekranından devam edin."
            });
        }

        private async Task<string> GenerateJwtAsync(AppUser user)
        {
            var key = _config["Jwt:Key"] ?? "AntAbstract-Default-Key-Change-In-Production-2026!";
            var issuer = _config["Jwt:Issuer"] ?? "AntAbstract";

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Email, user.Email ?? ""),
                new(ClaimTypes.GivenName, user.FirstName ?? ""),
                new(ClaimTypes.Surname, user.LastName ?? ""),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: issuer,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
