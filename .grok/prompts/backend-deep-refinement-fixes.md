# Backend Deep Refinement — FIX pass (Grok CLI)

Prior session reported **VERDICT: FAIL** in `.grok/logs/backend-deep-refinement-run.log`. Most slices landed; fix the blockers below without regressing completed work.

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`, `.grok/logs/backend-deep-refinement-run.log`

**Mandatory:** Fix all items. Run the exact final gate after each fix cluster. Do not ask for confirmation. Use `read_file` before every `search_replace`.

## Context — already done (verify only, do not redo unless broken)

- Slice 1a: `GlobalExceptionHandler` sanitization, 23505→409, `DuplicateImportException` typed mapping
- Slice 1c: `RequireWorkspaceReadAuth` wired on read endpoints; ops handbook updated
- Slice 2b/2c: `QuickBooksImportService` projection refactor + structural validation
- Slice 3b: `WorkspaceSnapshotComposerTests` parity with `EnterpriseRateService`
- Slice 4a: FluentValidation justification comment in `Program.cs`
- Slice 5b: sync export note in `docs/wiley-widget-functional-breakdown.md`
- Slice 5a partial: `AICacheWarmingService` converted to `BackgroundService`; conditional registration attempted in `Program.cs`

## Fix 1 — AICache DI regression (P0 blocker)

**Problem:** Integration HighRisk fails (35+) because API host fails startup in `ApiApplicationFactory` — `AICacheWarmingService` requires `IAIService` which is not registered in test/integration hosts.

**Required:**

- Ensure `AICacheWarmingService` is only registered when `IAIService` (and full AI stack) is actually wired, OR register a safe `NullAIService`/no-op fallback for hosts that need the type
- Mirror `TelemetryStartupService` pattern: config-gated registration that defaults **off** in IntegrationTest / test factories
- Verify `AI:CacheWarming:Enabled` default does not force registration when AI services are absent
- Re-run: `dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"`

## Fix 2 — JWT Authority null safety (P1)

**File:** `WileyCoWeb.Api/Configuration/JwtAuthenticationExtensions.cs`

**Problem:** Possible `NullReferenceException` when `Authentication:Jwt:Enabled=true` but `Authority` is null/whitespace (broken logic inside the Authority check block).

**Required:** Safely handle null/whitespace Authority; preserve test-dummy authority intent for integration tests; throw clear `InvalidOperationException` on misconfig.

## Fix 3 — Read-auth 401 integration test (P1)

**File:** `tests/WileyCoWeb.IntegrationTests/GlobalExceptionHandlerTests.cs` (or companion)

**Problem:** Slice 1c requires proving 401 without Bearer token when JWT enabled. Current coverage incomplete.

**Required:** At least one HighRisk test: snapshot GET or exports list returns 401 when `Authentication:Jwt:Enabled=true` and no token (use test factory pattern already started in this file).

## Fix 4 — Track new source files

**Required:** Ensure these are part of the working tree (stage if committing later):

- `src/WileyWidget.Data/DuplicateImportException.cs`
- `tests/WileyCoWeb.IntegrationTests/GlobalExceptionHandlerTests.cs`

Do not commit under `temp/`.

## Fix 5 — Final verification gate (all must pass)

```powershell
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
```

Summarize: fixes applied, test counts, PR split recommendation (auth vs error-handling vs hosted-services).
