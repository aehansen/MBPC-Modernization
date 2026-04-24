// Archivo: Mbpc.Api/Services/Auth/HttpCosteraUserContext.cs
using System.Security.Claims;

namespace Mbpc.Api.Services.Auth
{
    public class HttpCosteraUserContext : ICosteraUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<HttpCosteraUserContext> _logger;

        public HttpCosteraUserContext(
            IHttpContextAccessor httpContextAccessor, 
            ILogger<HttpCosteraUserContext> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public int GetCurrentCosteraId()
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user is null)
            {
                _logger.LogWarning("GetCurrentCosteraId: HttpContext o User es null.");
                return -1;
            }

            var claimValue = user.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(claimValue))
            {
                _logger.LogWarning("GetCurrentCosteraId: Claim 'CosteraId' ausente en el token.");
                return -1;
            }

            if (!int.TryParse(claimValue, out var costeraId))
            {
                _logger.LogWarning(
                    "GetCurrentCosteraId: Claim 'CosteraId' con valor no numérico: '{Valor}'.", claimValue);
                return -1;
            }

            return costeraId;
        }
    }
}