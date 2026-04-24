// Archivo: Mbpc.Api/Services/Auth/ICosteraUserContext.cs
namespace Mbpc.Api.Services.Auth
{
    public interface ICosteraUserContext
    {
        /// <summary>
        /// Obtiene el CosteraId del contexto de ejecución actual.
        /// Retorna 0 para Super Admin, > 0 para Operador, y -1 en caso de error.
        /// </summary>
        int GetCurrentCosteraId();
    }
}