namespace BitizChatBot.Models;

public class DomainAppearance
{
    public string Domain { get; set; } = string.Empty;
    public string ChatbotName { get; set; } = "Bridgestone Chatbot";
    public string ChatbotLogoUrl { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#000000";
    public string SecondaryColor { get; set; } = "#dc143c";
    public string WelcomeMessage { get; set; } = "Merhaba! Bridgestone chatbot'una hoş geldiniz. Size nasıl yardımcı olabilirim?\n\nÖrnek sorular:\n• En yakın bayi nerede?\n• İstanbul Kadıköy'de bayi var mı?\n• 2021 Corolla için yaz lastiği öner";
    public bool ChatbotOnline { get; set; } = true;
    public bool OpenChatOnLoad { get; set; } = false;
    public List<string> QuickReplies { get; set; } = new() { "Konumuma yakın bayileri arıyorum" };
    
    // Hazır Cevaplar
    public string GreetingResponse { get; set; } = "Merhaba! Bridgestone chatbot'una hoş geldiniz. Size nasıl yardımcı olabilirim?";
    public string HowAreYouResponse { get; set; } = "Teşekkür ederim, iyiyim! Size nasıl yardımcı olabilirim? Bridgestone bayileri, lastik önerileri veya başka bir konuda bilgi verebilirim.";
    public string WhoAreYouResponse { get; set; } = "Ben Bridgestone'un dijital asistanıyım. Size yakın bayileri bulma, araçınıza uygun lastik önerileri sunma ve Bridgestone hakkında bilgi verme konularında yardımcı olabilirim.";
    public string WhatCanYouDoResponse { get; set; } = "Size şu konularda yardımcı olabilirim:\n• Yakınınızdaki Bridgestone bayilerini bulma\n• Araçınıza uygun lastik önerileri sunma\n• Bridgestone ürünleri hakkında bilgi verme";
    public string ThanksResponse { get; set; } = "Rica ederim! Başka bir konuda yardımcı olabilir miyim?";
    public string GoodbyeResponse { get; set; } = "Güle güle! İyi günler dilerim.";
}


