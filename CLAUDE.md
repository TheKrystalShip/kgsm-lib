# KGSM-Lib Development Guide

## Project Overview

KGSM-Lib is a C# library (.NET 10.0) that provides interop capabilities with [KGSM](https://github.com/TheKrystalShip/KGSM), a Linux game server manager. The library communicates via shell process execution, and receives events either from the engine's on-disk event journal or over a Unix domain socket.

**Key Architecture**: SOLID-based with three service layers:
- **KgsmClient** (main facade) → **BlueprintService/InstanceService/EventService** → **ProcessRunner/IEventSource** (infrastructure)

## Critical Patterns

### 1. Service Registration & DI

All services use Microsoft.Extensions.DependencyInjection. Register via `ServiceCollectionExtensions`:

```csharp
services.AddKgsmServices("/path/to/kgsm.sh");                        // journal transport, default location
services.AddKgsmServices("/path/to/kgsm.sh", "/path/to/kgsm.sock");  // socket transport
services.AddKgsmServices(new KgsmOptions { ... });                   // full control (see §4)
```

**Lifetime rules**:
- `IProcessRunner`: Transient (stateless executor)
- `IEventSource`, `IEventCursorStore`, `IEventService`, `IKgsmClient`: Singleton (one transport per process)
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

Events flow: **an `IEventSource`** → **EventService** → **User handlers**

```csharp
// Registration pattern
_eventHandlers[typeof(InstanceInstalledData)] = handler;

// Type mapping in EventService._eventTypeMapping
{ "instance_installed", typeof(InstanceInstalledData) }
```

**Event lifecycle**: `EventService.Initialize()` starts the background transport, deserializes `EventWrapper`, matches type via `_eventTypeMapping`, invokes registered handlers.

**Two transports, one interface.** `EventService` consumes raw envelopes from `IEventSource`
and never learns which transport produced them, so **a consumer changes transport without
touching a handler**. Pick with `KgsmOptions.EventTransport`:

| | `Journal` (`EventJournalReader`) | `Socket` (`UnixSocketClient`) — the default |
|---|---|---|
| Source | `/var/lib/kgsm/events/YYYY-MM-DD.ndjson` | a socket the consumer binds |
| Readers per host | any number, no coordination | one — **binding is exclusive** |
| Engine-side config | none | the engine must list every consumer's socket path |
| Consumer was down | catches up from its cursor | the events are gone |
| Missed events | reported as an `EventJournalGap` | indistinguishable from no event |

`Socket` is the default so taking a new version of the library never moves a consumer's
transport on its own.

**Journal specifics.** Position is an `EventCursor` — a segment plus a byte offset — kept by an
`IEventCursorStore` (`FileEventCursorStore`, `NullEventCursorStore`, or the consumer's own; a
consumer that owns a database should store the cursor there, beside what it derives from the
events). Delivery is **at-least-once**: the cursor is stored only past events already
dispatched, so a crash costs re-delivery, never loss — a consumer that persists what it reads
must be idempotent.

`EventStartPosition` is a real per-consumer decision, not a default to accept: a consumer that
indexes events needs `CursorOrOldest` so it can rebuild, while one that announces them needs
`CursorOrTail` so it never replays a backlog into a chat channel. When retention has deleted
the segment a cursor names, the reader raises `EventJournalGap` through
`IEventService.RegisterGapHandler` and falls back to its cold-start position — surfacing the
discontinuity is what lets a consumer report its history as incomplete rather than implying
coverage it does not have.

The byte offset is exact only because **each event is one whole line** — the engine writes
payloads compact for that reason, and only complete lines are dispatched. Anything that
rewrites a segment in place (a log rotator's `copytruncate`) invalidates every cursor into it,
which is why retention deletes whole segments and never truncates one.

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

**Output**: `bin/$(Configuration)/net10.0/` contains `TheKrystalShip.KGSM.dll`

### Testing
xUnit (v2) suite in `kgsm-lib.Tests/` — run with `dotnet test kgsm-lib.sln`. All green,
no skips. Unit tests mock the collaborator the class under test actually depends on:
service tests mock `IKgsmCommandExecutor` (and `ILifecycleService` for the operational
verbs InstanceService forwards), `EventService` tests mock `IUnixSocketClient` and raise
its `EventReceived` event to drive the full wire→dispatch route.

`EventJournalReaderTests` runs the **real** reader against a temporary directory — the journal
is ordinary files, so its whole contract is unit-testable: start position, whole-line framing,
segment rolling, cursor resume, and gap reporting. It writes segments the way the engine does,
one complete line per append.

**Process/socket-bound classes are intentionally not in the unit suite** —
`LogSubscriptionService` (spawns a real `kgsm --follow` `Process`) and `UnixSocketClient`
(raw socket I/O) need a live KGSM and belong in an integration category, not here. Their
one unit-testable dependency, `LogParser`, is covered (`Utilities/LogParserTests.cs`).

### NuGet Packaging
`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` auto-generates package on Release builds.

**Package ID**: `TheKrystalShip.KGSM.Lib`  
**Namespace**: `TheKrystalShip.KGSM`

### Shipping it (there is no `deploy/` here)

This is a library, not a service: it has no install prefix, no systemd unit, and **no
`deploy/setup.sh` + `deploy/deploy.sh` pair** — the two-script deploy pattern the runnable
`kgsm-*` repos use does not apply. Shipping a change means bumping `<Version>`, packing, and
dropping the `.nupkg` into the local feed the consumers restore from. **NuGet caches by
`id+version`**, so a same-version repack is served stale — every change consumers must see needs a
version bump, then a matching `<PackageReference>` bump in each consuming repo.

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
2. **Event transport paths**: the socket transport needs a valid socket path (and one no other process has bound); the journal transport needs a readable journal directory, but tolerates one that does not exist yet — a host that has never emitted an event has no journal directory until it does
3. **KGSM path validation**: No built-in validation - ensure `kgsm.sh` exists before instantiating services
4. **JSON parsing**: KGSM may return empty strings for missing fields - always null-coalesce: `?? new()`
5. **Log parsing timezones**: `LogParser` handles ISO8601 (Z suffix) and syslog formats differently

## Integration Points

- **External dependency**: KGSM shell script (not bundled, must be installed separately)
- **Communication**: Process execution (bash) + events over the on-disk journal or a Unix domain socket
- **Platform**: Linux-only (relies on Unix sockets and bash scripts)

## Documentation Standards

All public APIs require XML doc comments with:
- `<summary>` describing what it does
- `<param>` for each parameter
- `<returns>` for return values
- `<exception>` for thrown exceptions

**Generate docs**: `<DocumentationFile>` produces XML for IntelliSense/NuGet.

## Current Development Status

Tracked in `docs/production-readiness-plan.md`. Test suite is green with no skips
(the prior ~20-failure / 11-skip degraded baseline — stale `IProcessRunner` mocks,
inverted assertions, dead tests for removed APIs — was cleaned up). Remaining work toward
publish is operational (CI / publish-on-tag), not product: see the ecosystem-level
`../architecture-review-findings.md` (findings #1 stranded-lib-distribution, #3 no-CI).

## Version tracking

- **Version source:** `<Version>` in `kgsm-lib/kgsm-lib.csproj`
- Bump the version whenever you make a user-facing change (new feature, bug fix, behaviour change). Patch for fixes, minor for new features, major for breaking changes.
- Update `CHANGELOG.md` under `## [Unreleased]` with a brief entry for every meaningful change.
- A git tag matching the new version should be created on release: `git tag v<version>`.
