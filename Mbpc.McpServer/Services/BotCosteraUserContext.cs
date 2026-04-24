using Mbpc.Api.Services.Auth;

namespace Mbpc.McpServer.Services;

/// <summary>
/// Contexto de usuario inyectado específicamente para las operaciones del Bot MCP.
/// Actúa como un Super Admin de solo lectura.
/// </summary>
public class BotCosteraUserContext : ICosteraUserContext
{
    public int GetCurrentCosteraId()
    {
        // Retornamos 0 de manera estática para identificar las acciones del bot (Super Admin)
        return 0;
    }
}