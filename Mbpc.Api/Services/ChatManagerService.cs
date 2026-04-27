// Archivo: Mbpc.Api/Services/ChatManagerService.cs
#pragma warning disable SKEXP0070

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mbpc.Api.Services
{
    public class ChatManagerService : IChatService
    {
        private readonly IConfiguration              _configuration;
        private readonly IViajeService               _viajeService;
        private readonly ILogger<ChatManagerService> _logger;
        private readonly IMemoryCache                _memoryCache;

        private static readonly MemoryCacheEntryOptions _cacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        };

        public ChatManagerService(
            IConfiguration              configuration,
            IViajeService               viajeService,
            ILogger<ChatManagerService> logger,
            IMemoryCache                memoryCache)
        {
            _configuration = configuration;
            _viajeService  = viajeService;
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

            using var httpClient = new HttpClient(new GeminiLoggingHandler(_logger))
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            var kernel = Kernel.CreateBuilder()
                .AddGoogleAIGeminiChatCompletion(
                    modelId: modelId,
                    apiKey: apiKey,
                    apiVersion: GoogleAIVersion.V1_Beta,
                    httpClient: httpClient)
                .Build();

            var toolObtenerViajes = KernelFunctionFactory.CreateFromMethod(
                method: async () =>
                {
                    _logger.LogInformation("Gemini invocó la herramienta 'ObtenerViajesActivos'.");
                    var viajes = await _viajeService.GetViajesAsync();

                    if (viajes.Count == 0) return "No hay buques registrados.";

                    var lineas = viajes.Select(v =>
                        $"- Buque: {v.VesselName ?? "Sin nombre"} | " +
                        $"Estado: {v.NavegationStatusDesc ?? "Desconocido"} | " +
                        $"MMSI: {v.Mmsi ?? "N/A"} | " +
                        $"Última posición: {(v.MsgTime != default ? v.MsgTime.ToString("dd/MM/yyyy HH:mm") : "N/A")}");

                    return $"Hay {viajes.Count} buque(s):\n{string.Join("\n", lineas)}";
                },
                functionName: "ObtenerViajesActivos",
                description: "Obtiene la lista completa de buques activos y viajes en curso."
            );

            kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions("MbpcPlugin", [toolObtenerViajes]));

            // ── RESOLUCIÓN DE MEMORIA Y CACHÉ ──────────────────────────────
            ChatHistory historial;
            string currentId;

            // Intentamos recuperar la conversación si viene un ID válido
            if (!string.IsNullOrWhiteSpace(conversationId) && 
                _memoryCache.TryGetValue(conversationId, out ChatHistory? cachedHistory) && 
                cachedHistory != null)
            {
                historial = cachedHistory;
                currentId = conversationId;
            }
            else
            {
                // Conversación nueva: ID fresco y System Prompt inicial
                historial = new ChatHistory();
                historial.AddSystemMessage(
                    "Sos un asistente operativo estrictamente especializado del sistema MBPC " +
                    "(Monitor de Buques y Posiciones de Convoy) de la Prefectura Naval Argentina. " +
                    "Respondés siempre en español rioplatense, de manera clara, concisa y profesional. " +
                    "TUS REGLAS ESTRICTAS SON: " +
                    "1. SOLO podés responder preguntas sobre tráfico marítimo, buques, viajes, geolocalización o el sistema MBPC. " +
                    "2. Si el usuario pregunta CUALQUIER OTRA COSA (deportes, clima, historia general, etc.), DEBÉS negarte cortésmente diciendo que tu función se limita exclusivamente al sistema MBPC. " +
                    "3. Usá proactivamente tus herramientas de consulta cuando el usuario pregunte sobre buques activos o viajes en curso.");
                
                currentId = Guid.NewGuid().ToString();
            }

            // Agregamos el mensaje del usuario al historial
            historial.AddUserMessage(userMessage);

            var settings = new GeminiPromptExecutionSettings
            {
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
            };

            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            _logger.LogInformation("Enviando mensaje a Gemini ({ModelId})...", modelId);

            var respuesta = await chatService.GetChatMessageContentAsync(historial, settings, kernel, ct);

            _logger.LogInformation("Gemini respondió exitosamente.");

            var replyContent = respuesta.Content ?? "Sin contenido.";
            
            // Agregamos la respuesta del asistente para que la recuerde en el próximo turno
            historial.AddAssistantMessage(replyContent);

            // ── GUARDAR EN CACHÉ ──────────────────────────────────────────
            // Acá currentId nunca es nulo, así que evitamos el CS8604
            _memoryCache.Set(currentId, historial, _cacheOptions);

            return (replyContent, currentId);
        }
    }

    // ── INTERCEPTOR HTTP (SOLO DIAGNÓSTICO) ──────────────────────────────────
    public class GeminiLoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public GeminiLoggingHandler(ILogger logger)
        {
            _logger      = logger;
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogWarning(">>> [DEBUG SK] Llamando a la URL: {Method} {Url}", request.Method, request.RequestUri);
            
            var response = await base.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(">>> [DEBUG SK] Google devolvió un error: {StatusCode}", response.StatusCode);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(">>> [DEBUG SK] Detalle del body: {Body}", content);
            }
            
            return response;
        }
    }
}