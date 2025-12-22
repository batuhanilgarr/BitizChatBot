# Architecture Documentation

## Overview

This Blazor Server application implements an LLM-powered chatbot with intent-based API routing. The system intelligently detects user intent and routes requests to appropriate external APIs for dealer and tire searches.

## Architecture Layers

### 1. Presentation Layer (Blazor Components)
- **AdminDashboard.razor**: Admin interface for LLM configuration
- **Chat.razor**: Main chat interface with message bubbles
- **MainLayout.razor**: Application layout wrapper
- **NavMenu.razor**: Navigation menu

### 2. API Layer (Controllers)
- **ChatController**: REST API endpoint for chat messages (`POST /api/chat/message`)
- **AdminController**: REST API endpoints for admin settings (`GET/POST /api/admin/settings`)

### 3. Service Layer
- **ChatOrchestrationService**: Main orchestration service that coordinates intent detection and API calls
- **LlmService**: Handles LLM API communication (OpenAI/Anthropic)
- **ExternalApiService**: Handles Bridgestone API calls
- **AdminSettingsService**: Manages admin settings persistence

### 4. Data Layer
- **ApplicationDbContext**: Entity Framework Core context
- **AdminSettings**: Entity model for storing LLM configuration

### 5. Models/DTOs
- **ChatMessage**: Represents chat messages in the UI
- **AdminSettings**: LLM configuration settings
- **DealerDto**: Dealer information from external API
- **TireDto**: Tire information from external API
- **IntentDetectionResult**: Result of intent detection with parameters

## Request Flow

### Chat Message Flow

```
User Input (Chat.razor)
    ↓
ChatOrchestrationService.ProcessMessageAsync()
    ↓
LlmService.DetectIntentAsync()
    ↓
[Intent Detection]
    ├─→ DealerSearchByLocation → ExternalApiService.SearchDealersByLocationAsync()
    ├─→ DealerSearchByCityDistrict → ExternalApiService.SearchDealersByCityDistrictAsync()
    ├─→ TireSearch → ExternalApiService.SearchTiresAsync()
    └─→ GeneralQuestion → LlmService.GenerateResponseAsync()
    ↓
Format Response
    ↓
Return to User
```

### Intent Detection Process

1. **LLM-Based Detection**: User message is sent to LLM with structured prompt requesting JSON response
2. **JSON Parsing**: Response is parsed to extract intent and parameters
3. **Fallback Detection**: If LLM fails, keyword-based detection is used
4. **Parameter Extraction**: Required parameters (lat/long, city/district, brand/model/year/season) are extracted
5. **Clarification**: If parameters are missing, user is asked for clarification

## External API Integration

### Bridgestone APIs

1. **Dealer Search by Location**
   - Endpoint: `GET /api/ai/SearchDealers?lat={lat}&longitude={long}`
   - Returns: List of dealers sorted by distance

2. **Dealer Search by City/District**
   - Endpoint: `GET /api/ai/SearchByLocation?city={city}&district={district}`
   - Returns: List of dealers in specified location

3. **Tire Search**
   - Endpoint: `GET /api/ai/Search?brand={brand}&model={model}&year={year}&season={season}`
   - Returns: List of matching tires

## LLM Integration

### Supported Providers

1. **OpenAI**
   - Endpoint: `https://api.openai.com/v1/chat/completions`
   - Authentication: Bearer token in Authorization header
   - Request Format: Standard OpenAI chat completion format

2. **Anthropic (Claude)**
   - Endpoint: `https://api.anthropic.com/v1/messages`
   - Authentication: x-api-key header + anthropic-version header
   - Request Format: Anthropic messages API format

### Intent Detection Prompt

The system uses a structured prompt that requests JSON output:

```json
{
  "intent": "DealerSearchByLocation|DealerSearchByCityDistrict|TireSearch|GeneralQuestion",
  "parameters": {
    "latitude": "<number if mentioned>",
    "longitude": "<number if mentioned>",
    "city": "<city name if mentioned>",
    "district": "<district name if mentioned>",
    "brand": "<vehicle brand if mentioned>",
    "model": "<vehicle model if mentioned>",
    "year": "<year if mentioned>",
    "season": "<summer|winter|all season if mentioned>"
  },
  "requiresClarification": <true|false>,
  "clarificationMessage": "<message if clarification needed>"
}
```

## Database Schema

### AdminSettings Table

```sql
CREATE TABLE AdminSettings (
    Id INT PRIMARY KEY,
    LlmProvider NVARCHAR(100) NOT NULL,
    ModelName NVARCHAR(100) NOT NULL,
    ApiKey NVARCHAR(MAX) NOT NULL,
    SystemPrompt NVARCHAR(MAX),
    Temperature DECIMAL(3,2),
    MaxTokens INT,
    UpdatedAt DATETIME2
)
```

## Security Considerations

1. **API Key Storage**: API keys are stored in the database, not in code
2. **Input Validation**: All user inputs are validated
3. **SQL Injection**: Protected via Entity Framework Core parameterized queries
4. **HTTPS**: Recommended for production deployments
5. **API Key Exposure**: Admin API endpoint masks API key in responses (shows "***")

## Error Handling

- **LLM API Errors**: Caught and returned as user-friendly error messages
- **External API Errors**: Logged and returned with appropriate error messages
- **Missing Parameters**: User is prompted for clarification
- **Database Errors**: Logged and user is notified

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=8BitizChatBot;..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Admin Settings (Stored in Database)

- LLM Provider: OpenAI or Anthropic
- Model Name: e.g., gpt-4o, gpt-4, claude-3-opus
- API Key: Provider-specific API key
- System Prompt: Custom system prompt for the chatbot
- Temperature: 0.0-2.0 (controls randomness)
- Max Tokens: Maximum response length

## Extension Points

### Adding New Intents

1. Add new `IntentType` enum value
2. Update `BuildIntentDetectionPrompt()` with examples
3. Add handler in `ChatOrchestrationService.ProcessMessageAsync()`
4. Create corresponding API service method if needed

### Adding New External APIs

1. Add method to `IExternalApiService`
2. Implement in `ExternalApiService`
3. Create DTOs for response
4. Add formatting logic in `ChatOrchestrationService`

### Adding New LLM Providers

1. Update `GetApiUrl()` method
2. Update `CreateChatRequest()` for provider-specific format
3. Update `ExtractResponseText()` for provider-specific response parsing
4. Update header logic in request creation

## Performance Considerations

- **Database Queries**: Admin settings are cached per request (scoped service)
- **HTTP Clients**: Configured with appropriate timeouts (30s external APIs, 60s LLM)
- **Intent Detection**: Uses lower temperature (0.3) for more deterministic results
- **Response Formatting**: Limits displayed results to 10 items

## Testing Recommendations

1. **Unit Tests**: Service layer methods
2. **Integration Tests**: API endpoints
3. **E2E Tests**: Chat flows with mock external APIs
4. **Load Tests**: Concurrent chat sessions

