import React, { useState, useRef, useEffect } from "react";

// ─────────────────────────────────────────────────────────────────────────────
// CONFIGURACIÓN DE API
// Usamos import.meta.env para Vite.
// Forzamos 127.0.0.1 en vez de localhost para evitar problemas de resolución IPv6 con Kestrel.
// ─────────────────────────────────────────────────────────────────────────────
const API_BASE_URL = import.meta.env.VITE_API_URL ?? "http://127.0.0.1:5009";
const CHAT_ENDPOINT = `${API_BASE_URL}/api/chat`;

// ─────────────────────────────────────────────────────────────────────────────
// TIPOS
// ─────────────────────────────────────────────────────────────────────────────
interface Message {
  id: string;
  sender: "bot" | "user" | "error";
  text: string;
  timestamp: Date;
}

interface ChatFloatingWindowProps {
  botName?: string;
  welcomeMessage?: string;
}

interface ChatRequestBody {
  message: string;
  conversationId?: string;
}

interface ChatApiResponse {
  reply: string;
  isSuccess: boolean;
  conversationId?: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// HELPERS
// ─────────────────────────────────────────────────────────────────────────────
const generateId = (): string =>
  `msg-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;

const formatTime = (date: Date): string =>
  date.toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit" });

// ─────────────────────────────────────────────────────────────────────────────
// ÍCONOS SVG INLINE
// ─────────────────────────────────────────────────────────────────────────────
const IconChat = () => (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-6 h-6">
    <path d="M4.913 2.658c2.075-.27 4.19-.408 6.337-.408 2.147 0 4.262.139 6.337.408 1.922.25 3.291 1.861 3.291 3.793V13.5c0 1.932-1.369 3.543-3.291 3.793a40.147 40.147 0 0 1-6.337.408c-2.147 0-4.262-.139-6.337-.408-1.922-.25-3.291-1.861-3.291-3.793V6.45c0-1.932 1.369-3.543 3.291-3.793Z" />
    <path d="M3.623 19.267c.023.59.6 1.014 1.15.807l1.79-.675a1.5 1.5 0 0 1 1.066.012l.142.04c.143.04.285.078.427.113a.75.75 0 0 0 .344-1.46c-.156-.037-.312-.078-.466-.123l-.142-.041a3 3 0 0 0-2.133-.024l-1.789.674a.25.25 0 0 1-.318-.23c0-.022.001-.043.003-.065l.133-1.332a.75.75 0 1 0-1.493-.15l-.133 1.332a1.75 1.75 0 0 0 .04 1.121Z" />
  </svg>
);

const IconClose = () => (
  <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={2.5} stroke="currentColor" className="w-5 h-5">
    <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
  </svg>
);

const IconSend = () => (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
    <path d="M3.478 2.404a.75.75 0 0 0-.926.941l2.432 7.905H13.5a.75.75 0 0 1 0 1.5H4.984l-2.432 7.905a.75.75 0 0 0 .926.94 60.519 60.519 0 0 0 18.445-8.986.75.75 0 0 0 0-1.218A60.517 60.517 0 0 0 3.478 2.404Z" />
  </svg>
);

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENTE PRINCIPAL
// ─────────────────────────────────────────────────────────────────────────────
export default function ChatFloatingWindow({
  botName = "Asistente MBPC",
  welcomeMessage = "Hola, soy el asistente IA. ¿En qué puedo ayudarte hoy?"
}: ChatFloatingWindowProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [inputValue, setInputValue] = useState("");
  const [isTyping, setIsTyping] = useState(false);

  // ── NUEVO: Estado para mantener el ID de conversación entre turnos ──────────
  const [conversationId, setConversationId] = useState<string | undefined>(undefined);

  const [messages, setMessages] = useState<Message[]>([
    {
      id: "welcome",
      sender: "bot",
      text: welcomeMessage,
      timestamp: new Date(),
    },
  ]);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    if (isOpen) {
      scrollToBottom();
      setTimeout(() => inputRef.current?.focus(), 100);
    }
  }, [messages, isOpen]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") setIsOpen(false);
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  const handleToggle = () => setIsOpen(!isOpen);

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputValue.trim() || isTyping) return;

    const userText = inputValue.trim();
    const userMsg: Message = {
      id: generateId(),
      sender: "user",
      text: userText,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInputValue("");
    setIsTyping(true);

    const requestBody: ChatRequestBody = {
      message: userText,
      ...(conversationId !== undefined && { conversationId }),
    };

    try {
      // ── SOLUCIÓN: Obtenemos el token del localStorage (como hace apiClient)
      const token = localStorage.getItem('mbpc_token'); // <-- Asegurate de que esta sea la key correcta que usás en tu app

      const response = await fetch(CHAT_ENDPOINT, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          // ── SOLUCIÓN: Inyectamos el token en la cabecera Authorization
          ...(token && { "Authorization": `Bearer ${token}` })
        },
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        throw new Error(`Error de servidor: ${response.status}`);
      }

      const data: ChatApiResponse = await response.json();

      if (!data.isSuccess) {
        throw new Error(data.reply || "Error desconocido en la IA");
      }

      if (data.conversationId) {
        setConversationId(data.conversationId);
      }

      const botMsg: Message = {
        id: generateId(),
        sender: "bot",
        text: data.reply,
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, botMsg]);

    } catch (error: unknown) {
      const errorMessage =
        error instanceof Error ? error.message : "Error inesperado";

      const errorMsg: Message = {
        id: generateId(),
        sender: "error",
        text: `Falla técnica: ${errorMessage}. Asegurate que la API esté corriendo.`,
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, errorMsg]);
    } finally {
      setIsTyping(false);
    }
  };

  return (
    <div className="chat-bot-container font-sans">
      {isOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed bottom-24 right-6 z-50 w-[350px] md:w-[400px] h-[550px] max-h-[80vh] flex flex-col bg-white rounded-2xl shadow-2xl overflow-hidden border border-slate-200 animate-chat-in"
        >
          {/* Header */}
          <div className="bg-slate-800 p-4 text-white flex items-center justify-between shadow-md">
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center text-xs font-bold border border-blue-400">
                IA
              </div>
              <div>
                <h3 className="font-semibold text-sm leading-none">{botName}</h3>
                <span className="text-[10px] text-emerald-400 flex items-center gap-1 mt-1">
                  <span className="w-1.5 h-1.5 bg-emerald-400 rounded-full animate-pulse" />
                  En línea
                </span>
              </div>
            </div>
            <button onClick={handleToggle} className="p-1 hover:bg-slate-700 rounded-lg transition-colors">
              <IconClose />
            </button>
          </div>

          {/* Body */}
          <div className="flex-grow overflow-y-auto p-4 space-y-4 bg-slate-50">
            {messages.map((msg) => (
              <div key={msg.id} className={`flex ${msg.sender === "user" ? "justify-end" : "justify-start"}`}>
                <div className={`max-w-[85%] rounded-2xl p-3 text-sm shadow-sm ${
                  msg.sender === "user"
                    ? "bg-blue-600 text-white rounded-tr-none"
                    : msg.sender === "error"
                    ? "bg-red-50 text-red-700 border border-red-200"
                    : "bg-white text-slate-700 border border-slate-200 rounded-tl-none"
                }`}>
                  <p className="leading-relaxed">{msg.text}</p>
                  <span className={`text-[9px] block mt-1 opacity-70 ${msg.sender === "user" ? "text-right" : ""}`}>
                    {formatTime(msg.timestamp)}
                  </span>
                </div>
              </div>
            ))}
            {isTyping && (
              <div className="flex justify-start">
                <div className="bg-white border border-slate-200 rounded-2xl rounded-tl-none p-3 shadow-sm">
                  <div className="flex gap-1">
                    <span className="w-1.5 h-1.5 bg-slate-400 rounded-full animate-bounce" />
                    <span className="w-1.5 h-1.5 bg-slate-400 rounded-full animate-bounce [animation-delay:0.2s]" />
                    <span className="w-1.5 h-1.5 bg-slate-400 rounded-full animate-bounce [animation-delay:0.4s]" />
                  </div>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          {/* Footer */}
          <div className="p-4 bg-white border-t border-slate-100">
            <form onSubmit={handleSend} className="relative flex items-center gap-2">
              <input
                ref={inputRef}
                type="text"
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                placeholder="Escribí un mensaje..."
                className="w-full bg-slate-100 border-none rounded-xl py-3 px-4 pr-12 text-sm focus:ring-2 focus:ring-blue-600 transition-all outline-none text-slate-800"
              />
              <button
                type="submit"
                disabled={!inputValue.trim() || isTyping}
                className="absolute right-2 p-2 text-blue-600 hover:text-blue-800 disabled:text-slate-300 transition-colors"
              >
                <IconSend />
              </button>
            </form>
          </div>
        </div>
      )}

      {/* Botón Flotante */}
      <button
        onClick={handleToggle}
        className={`
          fixed bottom-6 right-6 z-50
          w-14 h-14 rounded-full
          flex items-center justify-center
          shadow-lg hover:shadow-xl hover:scale-105
          transition-all duration-300
          focus:outline-none focus:ring-4 focus:ring-blue-900/30
          ${isOpen ? "bg-slate-700 text-white" : "bg-blue-900 text-white"}
        `}
      >
        <span className={`transition-transform duration-300 ${isOpen ? "rotate-90" : "rotate-0"}`}>
          {isOpen ? <IconClose /> : <IconChat />}
        </span>
        {!isOpen && (
          <span className="absolute -top-0.5 -right-0.5 w-3.5 h-3.5 bg-emerald-500 rounded-full border-2 border-white" />
        )}
      </button>

      <style>{`
        @keyframes chat-in {
          from { opacity: 0; transform: translateY(20px) scale(0.95); }
          to { opacity: 1; transform: translateY(0) scale(1); }
        }
        .animate-chat-in {
          animation: chat-in 0.25s ease-out forwards;
        }
      `}</style>
    </div>
  );
}
