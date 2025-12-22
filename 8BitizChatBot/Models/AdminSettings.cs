using System.Text.Json.Serialization;

namespace BitizChatBot.Models;

public class AdminSettings
{
    public int Id { get; set; }
    public string LlmProvider { get; set; } = "Ollama";
    public string ModelName { get; set; } = "hermes3:8b";
    public string ApiKey { get; set; } = string.Empty;
    public string OllamaBaseUrl { get; set; } = "http://3.14.208.235:11434";
    public string SystemPrompt { get; set; } =
        "Sen Bridgestone lastikleri ve bayi konumları için yardımcı bir asistansın. Kullanıcılara lastik ve bayi bilgisi sağla. Asla <think> etiketi veya herhangi bir içsel düşünce gösterme. Sadece son cevabı temiz ve Türkçe ver. Sadece Bridgestone lastikleri ve bayi konumları hakkında soruları cevapla. Başka bir konuda soru gelirse, sadece şu cevabı ver: \"Üzgünüm, sadece Bridgestone lastikleri ve bayi konumları hakkında sorulara cevap verebilirim. Size lastik önerileri konusunda yardımcı olabilirim.\"";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Chatbot Appearance & Behavior Settings
    public string ChatbotName { get; set; } = "Bridgestone Chatbot";
    public string ChatbotLogoUrl { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#000000";
    public string SecondaryColor { get; set; } = "#dc143c";
    public string WelcomeMessage { get; set; } = "Merhaba! Bridgestone chatbot'una hoş geldiniz. Size nasıl yardımcı olabilirim?\n\nÖrnek sorular:\n• En yakın bayi nerede?\n• İstanbul Kadıköy'de bayi var mı?\n• 2021 Corolla için yaz lastiği öner";
    public bool ChatbotOnline { get; set; } = true;
    public bool OpenChatOnLoad { get; set; } = false;
    public List<string> QuickReplies { get; set; } = new() { "Konumuma yakın bayileri arıyorum" };
    
    // Quick Responses
    public string GreetingResponse { get; set; } = "Merhaba! Bridgestone chatbot'una hoş geldiniz. Size nasıl yardımcı olabilirim?";
    public string HowAreYouResponse { get; set; } = "Teşekkür ederim, iyiyim! Size nasıl yardımcı olabilirim? Bridgestone bayileri, lastik önerileri veya başka bir konuda bilgi verebilirim.";
    public string WhoAreYouResponse { get; set; } = "Ben Bridgestone'un dijital asistanıyım. Size yakın bayileri bulma, araçınıza uygun lastik önerileri sunma ve Bridgestone hakkında bilgi verme konularında yardımcı olabilirim. Nasıl yardımcı olabilirim?";
    public string WhatCanYouDoResponse { get; set; } = "Size şu konularda yardımcı olabilirim:\n\n📍 Yakınınızdaki Bridgestone bayilerini bulma\n🏙️ Şehir/ilçe bazında bayi arama\n🚗 Araçınıza uygun lastik önerileri\nℹ️ Bridgestone hakkında genel bilgiler\n\nNasıl yardımcı olabilirim?";
    public string ThanksResponse { get; set; } = "Rica ederim! Başka bir konuda yardımcı olabilir miyim?";
    public string GoodbyeResponse { get; set; } = "Hoşça kalın! İyi günler dilerim. İhtiyacınız olduğunda buradayım!";
}

