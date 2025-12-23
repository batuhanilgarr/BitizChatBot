# Güvenlik Dokümantasyonu

## Uygulanan Güvenlik Önlemleri

### 1. Rate Limiting
- **Middleware**: `RateLimitMiddleware`
- **Limitler**:
  - Dakikada maksimum 30 istek
  - Saatte maksimum 200 istek
- **Kapsam**: Sadece `/api/chat` endpoint'leri
- **Bypass**: Admin endpoint'leri rate limit'ten muaf

### 2. Input Validation & Sanitization
- **Servis**: `SecurityService`
- **Özellikler**:
  - HTML tag temizleme
  - Script injection koruması
  - Maksimum uzunluk kontrolü (400 karakter)
  - Tekrarlayan karakter kontrolü
  - XSS koruması

### 3. Spam Protection
- URL/email pattern tespiti
- Tekrarlayan karakter kontrolü
- Çoklu link/mention kontrolü

### 4. Session Security
- **IP Address Tracking**: Her session IP adresi ile kaydedilir
- **User-Agent Tracking**: User-Agent değişiklikleri loglanır
- **Session Hijacking Detection**: IP/UA değişiklikleri uyarı olarak loglanır

### 5. Database Security
- **Entity Framework Core**: SQL injection koruması (parametreli sorgular)
- **PostgreSQL Support**: Production için PostgreSQL desteği
- **Connection String Security**: Hassas bilgiler environment variables'da saklanmalı

### 6. CSRF Protection
- **Antiforgery Token**: Blazor Server tarafından otomatik sağlanır
- **Middleware**: `app.UseAntiforgery()`

### 7. HTTPS Enforcement
- **HSTS**: Production'da aktif (`app.UseHsts()`)
- **HTTPS Redirection**: Aktif (`app.UseHttpsRedirection()`)

## PostgreSQL Kullanımı

### Connection String Formatı
```
Host=localhost;Port=5432;Database=bitizchatbot;Username=postgres;Password=yourpassword
```

### Aktifleştirme
`appsettings.json` dosyasında:
```json
{
  "Database": {
    "UsePostgreSQL": true
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=bitizchatbot;Username=postgres;Password=yourpassword"
  }
}
```

### Migration
```bash
dotnet ef migrations add AddConversationContexts
dotnet ef database update
```

## Conversation Context Storage

### Memory Storage (Varsayılan - Önerilen)
- **Performans**: Çok hızlı (RAM'de)
- **Basitlik**: Database query yok
- **Yeterli**: Tek server için ideal
- **Not**: Server restart'ta kaybolur ama son mesajlardan yeniden oluşturulabilir

### Database Storage (Sadece Multiple Server İçin)
- **Scalability**: Multiple server instance'ları destekler
- **Load Balancer**: Farklı server'lara giderse state korunur
- **Trade-off**: Biraz daha yavaş (her mesajda database query)

### Entity Structure
- `SessionId`: Primary key
- `CurrentIntent`: Intent type (JSON)
- `CollectedParametersJson`: Parameters (JSON)
- `Brand`, `Model`, `Year`, `Season`: Tire search fields
- `BrandModelInvalidAttempts`: Validation counter
- `AwaitingWhatsAppConsent`, `AwaitingWhatsAppPhone`: Flow state
- `LastDealerSummary`: Last dealer search summary
- `LastActivity`: Last activity timestamp
- `CreatedAt`: Creation timestamp

## Güvenlik Best Practices

### Production Checklist
- [ ] Connection string'leri environment variables'a taşı
- [ ] API key'leri secure storage'da sakla
- [ ] Rate limit değerlerini production'a göre ayarla
- [ ] Logging'i production seviyesine çıkar
- [ ] HTTPS certificate'i yapılandır
- [ ] Firewall kurallarını yapılandır
- [ ] Database backup stratejisi oluştur
- [ ] Monitoring ve alerting kur

### Environment Variables Örneği
```bash
export ConnectionStrings__DefaultConnection="Host=..."
export Database__UsePostgreSQL="true"
export LlmSettings__ApiKey="..."
```

## Rate Limit Ayarları

Mevcut limitler development için ayarlanmıştır. Production için:
- **Dakikalık limit**: 20-30 istek (spam koruması için)
- **Saatlik limit**: 100-200 istek (normal kullanım için)

Limitler `RateLimitMiddleware.cs` dosyasında ayarlanabilir.

## Logging

Güvenlik olayları şu şekilde loglanır:
- Rate limit aşımları: `Warning` seviyesinde
- Spam tespiti: `Warning` seviyesinde
- Session hijacking şüphesi: `Warning` seviyesinde
- Input validation hataları: `Information` seviyesinde

