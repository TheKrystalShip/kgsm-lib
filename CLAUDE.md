# KGSM-Lib Development Guide

## Project Overview

KGSM-Lib is a C# library (.NET 9.0) that provides interop capabilities with [KGSM](https://github.com/TheKrystalShip/KGSM), a Linux game server manager. The library communicates via shell process execution and Unix domain sockets for real-time events.

**Key Architecture**: SOLID-based with three service layers:
- **KgsmClient** (main facade) → **BlueprintService/InstanceService/EventService** → **ProcessRunner/UnixSocketClient** (infrastructure)

## Critical Patterns

### 1. Service Registration & DI

All services use Microsoft.Extensions.DependencyInjection. Register via `ServiceCollectionExtensions`:

```csharp
services.AddKgsmServices("/path/to/kgsm.sh", "/path/to/kgsm.sock");
```

**Lifetime rules**:
- `IProcessRunner`: Transient (stateless executor)
- `IUnixSocketClient`, `IEventService`, `IKgsmClient`: Singleton (maintain socket connection)
- `IBlueprintService`, `IInstanceService`: Transient (delegate to ProcessRunner)

### 2. Process Execution Pattern

All KGSM commands execute via `ProcessRunner.Execute()`:

```csharp
ProcessResult result = _processRunner.Execute(_kgsmPath, "--instances", "--json");
if (result.ExitCode != 0) {
    _logger.LogError("Command failed: {Error}", result.Stderr);
    throw new KgsmException($"Failed: {result.Stderr}");
}
```

**Return model**: `KgsmResult` wraps `ProcessResult` (Stdout, Stderr, ExitCode)

### 3. JSON Deserialization Conventions (source-generated — AOT/trim-safe)

The library is `IsAotCompatible` and **must stay reflection-free**: never call a
reflection-based `JsonSerializer.Deserialize<T>(json, options)` overload (it emits
IL2026/IL3050 and breaks under Native AOT). All deserialization flows through the
System.Text.Json **source generator** in `Json/KgsmJsonContext.cs`.

- **Registering a new type:** add `[JsonSerializable(typeof(YourType))]` to
  `KgsmJsonContext`. An unregistered type throws `NotSupportedException` at runtime
  (there is no reflection fallback). `KgsmCommandExecutor.ExecuteForJson<T>` resolves
  the contract via `KgsmJson.ExecutorOptions.GetTypeInfo(typeof(T))`.
- **KGSM's unconventional scalars** are handled by hand-written `JsonConverter<T>`s
  (all AOT-safe): `JsonStringToBoolConverter` ("0"/"1"/"active" → bool) and
  `JsonStringToIntConverter` ("123" → int) are registered globally on
  `KgsmJson.ExecutorOptions`; `JsonRecentLogsConverter` (string-or-`[]`) is applied
  per-property. KGSM emits some `Instance` bools/ints as strings, so the global
  converters are load-bearing, not optional.
- **Enums** are string-valued on the wire and decorated at the type with
  `[JsonConverter(typeof(JsonStringEnumConverter<TEnum>))]` (the generic, AOT-safe
  converter). Read-matching is case-insensitive, so KGSM's lowercase `"systemd"`
  binds to `LifecycleManager.Systemd`. This applies on both the executor and event
  paths — do not rely on options-level enum converters.

See `Json/KgsmJsonContext.cs`, `InstanceStatusDeserializationTests` (wire-shape
coverage), and `SystemService.GetInfo<T>()` (the one consumer-open generic — only
works for types registered in the context; pass a `JsonTypeInfo<T>` if you need
arbitrary `T` under AOT).

### 4. Event System Architecture

Events flow: **KGSM (Unix socket)** → **UnixSocketClient** → **EventService** → **User handlers**

```csharp
// Registration pattern
_eventHandlers[typeof(InstanceInstalledData)] = handler;

// Type mapping in EventService._eventTypeMapping
{ "instance_installed", typeof(InstanceInstalledData) }
```

**Event lifecycle**: `EventService.Initialize()` starts background listener, deserializes `EventWrapper`, matches type via `_eventTypeMapping`, invokes registered handlers.

### 5. Async Patterns (Critical)

**Always use `ConfigureAwait(false)` in library code** to avoid deadlocks:

```csharp
await socket.ConnectAsync(endpoint).ConfigureAwait(false);
await stream.ReadAsync(buffer).ConfigureAwait(false);
```

See `Services/EventService.cs` and `Services/InstanceService.cs` for examples.

### 6. Disposal Pattern

Services managing unmanaged resources (sockets, event handlers) implement dual disposal:

```csharp
public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
}

protected virtual void Dispose(bool disposing) {
    if (_disposed) return;
    if (disposing) { /* cleanup managed */ }
    _disposed = true;
}
```

**Guard checks**: Always check `_disposed` before operations in disposed classes.

## Build & Test Workflow

### Building
```bash
dotnet build kgsm-lib.sln                    # Debug build
dotnet build -c Release kgsm-lib.sln         # Release (generates NuGet package)
```

**Output**: `bin/$(Configuration)/net9.0/` contains `TheKrystalShip.KGSM.dll`

### Testing
No test project currently in repo, but `docs/testing.md` describes planned xUnit architecture with:
- Unit tests with Moq/NSubstitute
- Integration tests (require actual KGSM installation)
- Performance benchmarks with BenchmarkDotNet

### NuGet Packaging
`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` auto-generates package on Release builds.

**Package ID**: `TheKrystalShip.KGSM.Lib`  
**Namespace**: `TheKrystalShip.KGSM`

## File Organization

```
kgsm-lib/
├── Core/
│   ├── Interfaces/          # Service contracts (I*Service, I*Client)
│   └── Models/              # DTOs (Blueprint, Instance, KgsmResult, Event args)
├── Services/                # Implementations (*Service, *Client)
├── Events/                  # Event data types (EventTypes.cs)
├── Exceptions/              # KgsmException hierarchy
├── Extensions/              # ServiceCollectionExtensions
└── Utilities/               # LogParser (parses KGSM log formats)
```

## Common Gotchas

1. **KgsmInterop class**: Marked `[Obsolete]`, use `IKgsmClient` interface instead
2. **Socket path requirement**: EventService won't work without valid Unix socket path
3. **KGSM path validation**: No built-in validation - ensure `kgsm.sh` exists before instantiating services
4. **JSON parsing**: KGSM may return empty strings for missing fields - always null-coalesce: `?? new()`
5. **Log parsing timezones**: `LogParser` handles ISO8601 (Z suffix) and syslog formats differently

## Integration Points

- **External dependency**: KGSM shell script (not bundled, must be installed separately)
- **Communication**: Process execution (bash) + Unix domain socket (events)
- **Platform**: Linux-only (relies on Unix sockets and bash scripts)

## Documentation Standards

All public APIs require XML doc comments with:
- `<summary>` describing what it does
- `<param>` for each parameter
- `<returns>` for return values
- `<exception>` for thrown exceptions

**Generate docs**: `<DocumentationFile>` produces XML for IntelliSense/NuGet.

## Current Development Status

Tracked in `docs/production-readiness-plan.md`:
- Phase 1: Critical fixes (ConfigureAwait ✅, test failures 🔄)
- Target: v1.0.0 production release
- Blockers: Integration test failures, help text assertions
