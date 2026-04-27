// Archivo: Mbpc.Api/Services/ChatManagerService.cs
#pragma warning disable SKEXP0070

using Mbpc.Api.Plugins;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mbpc.Api.Services
{
    public class ChatManagerService : IChatService
    {
        private readonly IConfiguration              _configuration;
        private readonly IViajeService               _viajeService;
        private readonly IConvoyManagerService       _convoyService;
        private readonly ILogger<ChatManagerService> _logger;
        private readonly IMemoryCache                _memoryCache;

        private static readonly MemoryCacheEntryOptions _cacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        };

        // ── SYSTEM PROMPT ────────────────────────────────────────────────────
        // Separado en una constante para facilitar su mantenimiento.
        private const string SystemPrompt =
            "Sos un asistente operativo estrictamente especializado del sistema MBPC " +
            "(Monitor de Buques y Posiciones de Convoy) de la Prefectura Naval Argentina. " +
            "Respondés siempre en español rioplatense, de manera clara, concisa y profesional.\n\n" +

            "CAPACIDADES DISPONIBLES:\n" +
            "Tenés acceso a tres herramientas de consulta en tiempo real:\n" +
            "1. ObtenerViajesActivos: lista general de toda la flota activa con estado de navegación.\n" +
            "2. ConsultarPosicionBuque: telemetría AIS en tiempo real (Lat/Lon, velocidad, rumbo, ID interno). " +
               "Aceptá nombre parcial o MMSI.\n" +
            "3. ObtenerDetalleOperativo: detalle completo de un viaje (etapas, barcazas, tipo de carga, " +
               "convoy). Requiere el 'ID interno' que devuelve ConsultarPosicionBuque.\n\n" +

            "REGLAS ESTRICTAS DE OPERACIÓN:\n" +
            "1. SOLO podés responder preguntas sobre tráfico marítimo, buques, viajes, " +
               "geolocalización, convoyes o el sistema MBPC. " +
               "Si el usuario consulta CUALQUIER OTRA COSA, negáte cortésmente indicando " +
               "que tu función se limita exclusivamente al sistema MBPC.\n" +
            "2. FLUJO DE CONSULTA OBLIGATORIO: Si el usuario pregunta por un buque sin " +
               "proporcionar su ID interno, primero invocá 'ConsultarPosicionBuque' para " +
               "obtener el ID interno. Solo entonces, si el usuario pide detalles de convoy " +
               "o carga, invocá 'ObtenerDetalleOperativo' con ese ID.\n" +
            "3. Nunca inventés datos de posición, carga, estado ni identificadores. " +
               "Si la herramienta no devuelve información, informalo con claridad.\n" +
            "4. Usá las herramientas de forma proactiva: ante cualquier pregunta sobre " +
               "un buque concreto, consultá primero la telemetría antes de responder.";

        public ChatManagerService(
            IConfiguration              configuration,
            IViajeService               viajeService,
            IConvoyManagerService       convoyService,
            ILogger<ChatManagerService> logger,
            IMemoryCache                memoryCache)
        {
            _configuration = configuration;
            _viajeService  = viajeService;
            _convoyService = convoyService;
            _logger        = logger;
            _memoryCache   = memoryCache;
        }

        public async Task<(string Reply, string ConversationId)> GetChatResponseAsync(
            string userMessage,
            string? conversationId = null,
            CancellationToken ct = default)
        {
            var apiKey  = _configuration["Gemini:ApiKey"];
            var modelId = _configuration["Gemini:ModelId"] ?? "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "TU_GEMINI_API_KEY_AQUI")
                throw new InvalidOperationException("API Key de Gemini no configurada.");

            // ── CONSTRUCCIÓN DEL KERNEL ──────────────────────────────────────
            using var httpClient = new HttpClient(new GeminiLoggingHandler(_logger))
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            var kernel = Kernel.CreateBuilder()
                .AddGoogleAIGeminiChatCompletion(
                    modelId:    modelId,
                    apiKey:     apiKey,
                    apiVersion: GoogleAIVersion.V1_Beta,
                    httpClient: httpClient)
                .Build();

            kernel.Plugins.AddFromObject(
                new MbpcOperationalPlugin(_viajeService, _convoyService),
                "MbpcOperativo");

            // ── RESOLUCIÓN DE HISTORIAL ──────────────────────────────────────
            ChatHistory historial;
            string currentId;

            if (!string.IsNullOrWhiteSpace(conversationId) &&
                _memoryCache.TryGetValue(conversationId, out ChatHistory? cachedHistory) &&
                cachedHistory is not null)
            {
                historial = cachedHistory;
                currentId = conversationId;
            }
            else
            {
                historial = new ChatHistory();
                historial.AddSystemMessage(SystemPrompt);
                currentId = Guid.NewGuid().ToString();
            }

            // ── ENVÍO Y RESPUESTA ────────────────────────────────────────────
            historial.AddUserMessage(userMessage);

            var settings = new GeminiPromptExecutionSettings
            {
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
            };

            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            _logger.LogInformation("Enviando mensaje a Gemini ({ModelId})...", modelId);

            try
            {
                // Intentamos comunicarnos con Gemini
                var respuesta = await chatService.GetChatMessageContentAsync(historial, settings, kernel, ct);

                _logger.LogInformation("Gemini respondió exitosamente.");

                var replyContent = respuesta.Content ?? "Sin contenido.";
                
                historial.AddAssistantMessage(replyContent);

                _memoryCache.Set(currentId, historial, _cacheOptions);

                return (replyContent, currentId);
            }
            catch (HttpOperationException ex)
            {
                // ¡Atrapamos el límite de cuota o saturación de Google!
                _logger.LogWarning("Límite de API de Gemini alcanzado (429/503): {Message}", ex.Message);
                
                // Mensaje amigable para el frontend sin guardar el error en el historial
                var fallbackMessage = "⚠️ El servicio de inteligencia artificial está experimentando alta demanda temporal o límite de cuota. Por favor, espere un minuto y vuelva a intentar.";
                
                return (fallbackMessage, currentId);
            }
            catch (Exception ex)
            {
                // Cualquier otro error grave
                _logger.LogError(ex, "Error inesperado al consultar a Gemini.");
                throw;
            }
        }
    }

    // ── INTERCEPTOR HTTP (DIAGNÓSTICO) ───────────────────────────────────────
    public class GeminiLoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public GeminiLoggingHandler(ILogger logger)
        {
            _logger      = logger;
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken  cancellationToken)
        {
            _logger.LogWarning(
                ">>> [DEBUG SK] Llamando a la URL: {Method} {Url}",
                request.Method, request.RequestUri);

            var response = await base.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    ">>> [DEBUG SK] Google devolvió un error: {StatusCode}",
                    response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(">>> [DEBUG SK] Detalle del body: {Body}", body);
            }

            return response;
        }
    }
}
