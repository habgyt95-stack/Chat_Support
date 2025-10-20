# Chat_Support AI Coding Agent Instructions

## Architecture Overview

This is a **Clean Architecture** solution built on .NET 9 with React + Vite frontend, implementing a real-time chat and customer support system with SignalR. Originally generated from [Clean.Architecture.Solution.Template](https://github.com/jasontaylordev/CleanArchitecture) v9.0.10.

### Project Structure
- **Domain**: Entities, value objects, enums, events (pure domain logic, no dependencies)
- **Application**: Business logic via CQRS (Commands/Queries), DTOs, interfaces, MediatR pipeline behaviors
- **Infrastructure**: Data persistence (EF Core), SignalR hubs (`ChatHub`, `GuestChatHub`), external services (SMS, FCM notifications, JWT auth)
- **Web**: ASP.NET Core Minimal API endpoints, React SPA (ClientApp), SignalR hub routing
- **AppHost**: .NET Aspire orchestration for local development
- **Tests**: Functional, unit, integration, and acceptance tests

### Key Architectural Patterns

**CQRS + MediatR Pipeline**:
- All business logic in `Application/*/Commands` and `Application/*/Queries`
- Commands return DTOs (e.g., `ChatRoomDto`, `ChatMessageDto`)
- Pipeline behaviors: `ValidationBehaviour`, `AuthorizationBehaviour`, `PerformanceBehaviour`, `UnhandledExceptionBehaviour`
- Validators use FluentValidation; failures throw `Application.Common.Exceptions.ValidationException`

**SignalR Real-Time Communication**:
- Two hubs: `ChatHub` (authenticated users) and `GuestChatHub` (anonymous support)
- JWT tokens passed via query string: `?access_token={token}` (see `Infrastructure/DependencyInjection.cs` JWT events)
- User presence tracking via `IPresenceTracker` (singleton, in-memory)
- Hub methods invoke commands/queries through MediatR, then broadcast updates to clients
- Custom user ID provider: `SubClaimUserIdProvider` maps SignalR connections to JWT `sub` claim

**Authentication & Identity**:
- Custom OTP-based login via SMS (`RequestOtpCommand`, `VerifyOtpCommand`) using Kavenegar service
- JWT auth configured in `Infrastructure/DependencyInjection.cs` with custom claim mapping prevention:
  ```csharp
  JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
  JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
  ```
- `IUser` service provides current user context (ID, RegionId) injected into handlers

**Domain Entities**:
- Key entities: `ChatRoom`, `ChatMessage`, `ChatRoomMember`, `MessageStatus`, `MessageReaction`, `KciUser`, `SupportAgent`
- `ChatRoomType` enum: `UserToUser`, `Group`, `Support`
- `MessageType` enum: `Text`, `Image`, `File`, `Voice`, `Location`, `System`
- All auditable entities inherit `BaseAuditableEntity` (auto-tracked via `AuditableEntityInterceptor`)

## Development Workflows

### Build & Run

**Local Development**:
```powershell
# Build entire solution
dotnet build -tl

# Run with Aspire (orchestrates SQL Server + Web app)
cd .\src\AppHost
dotnet run

# Run Web directly (requires manual DB setup)
cd .\src\Web
dotnet watch run
# Frontend: https://localhost:5173 (Vite dev server proxies to backend)
# Backend API: https://localhost:5001/api
```

**Frontend Development**:
```powershell
cd .\src\Web\ClientApp
npm install
npm run dev  # Vite dev server on port 5173
```

### Testing

```powershell
# Unit + Integration + Functional tests (exclude acceptance)
dotnet test --filter "FullyQualifiedName!~AcceptanceTests"

# Acceptance tests (requires app running on https://localhost:5001)
cd .\src\Web
dotnet run  # Terminal 1
cd .\tests\Web.AcceptanceTests
dotnet test  # Terminal 2
```

**Test Infrastructure**:
- Functional tests use `CustomWebApplicationFactory` with Testcontainers for SQL Server
- `Testing.cs` provides `SendAsync<T>()` helper to send MediatR requests in tests
- Test users: `RunAsDefaultUserAsync()`, `RunAsAdministratorAsync()`

### Scaffolding New Features

Use Clean Architecture templates to generate CQRS boilerplate:

```powershell
cd .\src\Application

# Generate command
dotnet new ca-usecase --name CreateTodoList --feature-name TodoLists --usecase-type command --return-type int

# Generate query
dotnet new ca-usecase -n GetTodos -fn TodoLists -ut query -rt TodosVm

# If template not installed:
dotnet new install Clean.Architecture.Solution.Template::9.0.10
```

## Project-Specific Conventions

### Chat Module Features (`Application/Chats`)

**Commands**: `CreateChatRoom`, `SendMessage`, `EditMessage`, `DeleteMessage`, `MarkMessagesAsRead`, `ReactToMessage`, `ForwardMessage`, `UpdateChatRoom`, `AddGroupMember`, `RemoveGroupMember`, `ToggleChatRoomMute`, `SoftDeletePersonalChat`, `UploadChatFile`

**Queries**: `GetChatRooms`, `GetChatMessages`, `GetMessageReadReceipts`

**Key Patterns**:
- Message read receipts tracked per user via `MessageStatus` entity
- Group management: only owners/admins can edit (checked in `UpdateChatRoomCommandHandler`)
- Soft delete for personal chats; hard delete for groups (owner only)
- File uploads stored in `wwwroot/uploads` via `IFileStorageService`

### Support Module Features (`Application/Support`)

**Customer Support Flow**:
1. Guest/user creates support chat via `StartSupportChatCommand`
2. `IAgentAssignmentService.AssignAgentAsync()` selects available agent based on:
   - Region match
   - Active status (`IsActive = true`)
   - Current load vs. `MaxConcurrentChats`
3. If agent goes offline/inactive, their chats auto-transferred via `TransferChatCommand`
4. Agent management: `CreateSupportAgent`, `UpdateSupportAgent`, `DeleteSupportAgent`

**Agent Management**:
- Admins manage agents via `GET/POST/PUT/DELETE /api/support/agents`
- When deleting agent, active chats transferred to next available agent
- Agent availability tracked in real-time via SignalR presence

### Frontend Architecture (`src/Web/ClientApp`)

**React + Context API**:
- `ChatContext.jsx`: Global chat state via `useReducer`, handles SignalR event listeners
- `signalRService.js`: Singleton service for SignalR connection management
- JWT token stored in localStorage, passed to SignalR via query string

**State Management Pattern**:
```javascript
// ChatContext uses reducer pattern with actions:
dispatch({ type: 'ADD_MESSAGE', payload: message });
dispatch({ type: 'MARK_ALL_MESSAGES_AS_READ_IN_ROOM', payload: { roomId } });
dispatch({ type: 'UPDATE_ROOM', payload: updatedRoom });
```

**SignalR Event Handling**:
- Frontend listens: `ReceiveMessage`, `MessageEdited`, `MessageDeleted`, `UserOnline`, `UserOffline`, `RoomUpdated`
- Frontend invokes: `SendMessage`, `JoinRoom`, `LeaveRoom`, `MarkAsRead`, `EditMessage`, `DeleteMessage`

### Persian (Farsi) Language Support

**Important**: Many comments, error messages, and documentation are in Persian. Key files:
- `CHANGES.md`: Persian changelog documenting all features/fixes
- Frontend UI strings: Persian labels and messages
- When adding features, follow existing bilingual pattern

## Configuration & Dependencies

### Central Package Management

Uses `Directory.Packages.props` for version management (MSBuild CPM):
```xml
<PackageVersion Include="Ardalis.GuardClauses" Version="4.6.0" />
<PackageVersion Include="AutoMapper" Version="13.0.1" />
<PackageVersion Include="FluentValidation" Version="11.9.0" />
```

### Key External Services

- **SMS**: Kavenegar (Iranian SMS provider) via `ISmsService` → `KavenegarSmsService`
- **Push Notifications**: FCM (Firebase Cloud Messaging) via `IMessageNotificationService` → `FcmNotificationService`
- **Database**: SQL Server (local dev via Aspire, see `src/AppHost/Program.cs`)
- **Auth**: Custom JWT with BCrypt password hashing for OTP

### Connection Strings

Configured via Aspire service discovery:
```csharp
// AppHost/Program.cs
var database = builder.AddSqlServer("sql").AddDatabase("Chat_SupportDb");
builder.AddProject<Projects.Web>("web")
    .WithReference(database)
    .WaitFor(database);
```

## Common Tasks & Troubleshooting

### Adding a New Chat Feature

1. **Define command/query** in `Application/Chats/Commands` or `Queries`
2. **Add handler** implementing `IRequestHandler<TRequest, TResponse>`
3. **Map endpoint** in `Web/Endpoints/Chat.cs`:
   ```csharp
   chatApi.MapPost("/rooms/{roomId}/feature", YourMethod).RequireAuthorization();
   ```
4. **SignalR broadcast** (if real-time): Call hub clients in handler or via `IChatHubService`
5. **Frontend integration**: Add API call in `ClientApp/src/services/chatApi.js`, listen for SignalR events in `ChatContext.jsx`

### Database Migrations

```powershell
cd .\src\Infrastructure
dotnet ef migrations add MigrationName --startup-project ..\Web
dotnet ef database update --startup-project ..\Web
```

### SignalR Connection Issues

- Verify JWT token not expired (2-hour lifetime by default)
- Check `Context.User.Identity.Name` in hub methods for auth
- Enable detailed errors: `builder.Services.AddSignalR(options => { options.EnableDetailedErrors = true; });`
- Browser console should show `[SignalR]` prefixed logs from `signalRService.js`

### File Upload & Download

**Upload:**
- Uploads saved to `wwwroot/uploads/{userId}/{filename}`
- Static file middleware configured in `Program.cs`: `RequestPath = "/uploads"`
- Max file size controlled by `IFormFile` limits and Kestrel settings

**Download (WebView Compatible):**
- Dedicated endpoint: `GET /api/chat/download?filePath={filePath}`
- Sends proper `Content-Disposition: attachment; filename="..."` header
- Supports 50+ file types with correct MIME types
- Range request support for resumable downloads
- Frontend uses direct `<a href>` links (not fetch/blob) for WebView compatibility
- Middleware adds `Content-Disposition` to all `/uploads` static files

## Recent Changes (from CHANGES.md)

Major features added:
- ✅ Group chat editing (name, description) by owner/admin
- ✅ Unread message counter fix for offline users
- ✅ Mandatory captions for file uploads
- ✅ Support agent management system with auto-assignment
- ✅ Chat transfer when agent goes offline
- ✅ Message reactions and forwarding
- ✅ Read receipts with per-user tracking

## References

- Base template: https://github.com/jasontaylordev/CleanArchitecture
- Minimal API endpoints: `Web/Endpoints/*.cs` (no controllers)
- Entity configurations: `Infrastructure/Data/Configurations/*.cs`
- Domain events: `Domain/Events/*.cs` (dispatched via `DispatchDomainEventsInterceptor`)
