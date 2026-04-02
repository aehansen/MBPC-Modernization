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
        private readonly IConfiguration         _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _logger        = logger;
        }

        /// <summary>
        /// Payload de login enviado desde el frontend.
        /// CosteraId == 0  →  Super Admin (acceso total a todas las costeras).
        /// CosteraId  > 0  →  Operador    (acceso restringido a su jurisdicción).
        /// </summary>
        public class LoginRequest
        {
            public int    CosteraId { get; set; }
            public string Password  { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // TODO: Integrar con el Membership Provider legacy de Oracle.
            // Por ahora validamos solo que la contraseña no esté vacía.
            // CosteraId == 0 es un valor de negocio válido (Super Admin), por lo que
            // NO se rechaza: se verifica solo que el campo Password venga informado.
            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { mensaje = "El campo Password es requerido." });

            _logger.LogInformation(
                "Generando token JWT — CosteraId: {CosteraId} | Rol: {Rol}",
                request.CosteraId,
                request.CosteraId == 0 ? "Admin" : "Operador");

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey   = jwtSettings.GetValue<string>("SecretKey")
                              ?? throw new InvalidOperationException("JwtSettings:SecretKey no está configurada.");

            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // El rol se determina por el valor de CosteraId:
            //   0  → "Admin"    (ve todos los buques de todas las costeras)
            //  > 0  → "Operador" (ve solo los buques de su propia costera)
            var rol = request.CosteraId == 0 ? "Admin" : "Operador";

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, $"costera_{request.CosteraId}"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                // CosteraId almacenado como string en el Claim; se parsea a int
                // en el servicio mediante GetCurrentCosteraId().
                new Claim("CosteraId",       request.CosteraId.ToString()),
                new Claim(ClaimTypes.Role,   rol)
            };

            var token = new JwtSecurityToken(
                issuer:             jwtSettings.GetValue<string>("Issuer"),
                audience:           jwtSettings.GetValue<string>("Audience"),
                claims:             claims,
                expires:            DateTime.UtcNow.AddMinutes(
                                        jwtSettings.GetValue<int>("ExpirationMinutes")),
                signingCredentials: creds
            );

            return Ok(new
            {
                token      = new JwtSecurityTokenHandler().WriteToken(token),
                expiracion = token.ValidTo,
                rol,
                costeraId  = request.CosteraId
            });
        }
    }
}
