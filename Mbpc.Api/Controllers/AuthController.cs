using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public class LoginRequest
        {
            public string Usuario { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string CosteraId { get; set; } = string.Empty; // Para forzar el mock en dev
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // TODO: Integrar con el Membership Provider legacy de Oracle
            // Por ahora, aceptamos cualquier intento si viene con datos
            if (string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrWhiteSpace(request.CosteraId))
            {
                return BadRequest(new { mensaje = "Usuario y CosteraId son requeridos." });
            }

            _logger.LogInformation("Generando token JWT para usuario {Usuario} en Costera {CosteraId}", request.Usuario, request.CosteraId);

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings.GetValue<string>("SecretKey");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, request.Usuario),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("CosteraId", request.CosteraId), // <-- CLAIM CUSTOM CLAVE
                new Claim(ClaimTypes.Role, "OperadorCostera")
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings.GetValue<string>("Issuer"),
                audience: jwtSettings.GetValue<string>("Audience"),
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(jwtSettings.GetValue<int>("ExpirationMinutes")),
                signingCredentials: creds
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiracion = token.ValidTo
            });
        }
    }
}