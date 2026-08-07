# KGSM-Lib Development Guide

## Project Overview

KGSM-Lib is a C# library (.NET 10.0) that provides interop capabilities with [KGSM](https://github.com/TheKrystalShip/KGSM), a Linux game server manager. The library communicates via shell process execution, and receives events by reading the engine's on-disk event journal.

**Key Architecture**: SOLID-based with three service layers:
- **KgsmClient** (main facade) → **BlueprintService/InstanceService/EventService** → **ProcessRunner/IEventSource** (infrastructure)

## Critical Patterns

### 1. Service Registration & DI

All services use Microsoft.Extensions.DependencyInjection. Register via `ServiceCollectionExtensions`:

```csharp
services.AddKgsmServices("/path/to/kgsm.sh");        // journal at its default location
services.AddKgsmServices(new KgsmOptions { ... });   // full control (see §4)
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

**One source, behind an interface.** `EventService` consumes raw envelopes from `IEventSource`
and never learns what produced them. That indirection is why the engine's transport could be
replaced without touching a single consumer's handler code, and it is worth keeping for the
same reason.

The source is `EventJournalReader`, reading `/var/lib/kgsm/events/YYYY-MM-DD.ndjson`. Any number
of consumers read the same segments concurrently — a file has no exclusive binding, so there is
nothing to reserve and nothing to tell the engine about. A consumer that was down catches up from
its cursor rather than losing what it slept through, and events it genuinely cannot recover are
reported as an `EventJournalGap` instead of being indistinguishable from no event.

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

**Every event carries its position.** `IEventSource.EventReceived` and
`IEventService.RegisterRawHandler` both take an `EventPosition` (segment + byte offset)
alongside the envelope. That position is the event's *identity*: one line per event and
whole-segment retention together mean no two events share one and an event's never changes.
`AuditId.ForPosition` turns it into `evt_<segment>_<offset>` — unique by construction, and
ordered like the file, so the same value works as an id and as a pagination cursor. Only raw
handlers see it; a consumer that needs the id inside a *typed* handler captures it from a raw
handler first (raw handlers run before typed dispatch, for every envelope).

### 4·a. Reading history back

`IEventJournalHistory.QueryAsync` is the other half of the journal: `IEventJournalReader`
tails it for what happens next, this reads back over what it already holds. **There is no index
and no cache** — the point is that nothing can disagree with the record, go stale, or need
rebuilding. A per-query scan is affordable because segments are date-named (a window is narrowed
by file *name* before one is opened), they're read newest-first with an early exit, and each is
streamed forward once into a ring buffer the size of the page.

The three fields that keep an answer honest are not optional decoration. `CoverageFrom` is the
oldest moment the journal can still answer for — a window reaching earlier is answered only from
there. `JournalReadable` separates "cannot see" from "nothing happened". `Truncated` says the
scan hit `KgsmOptions.EventHistoryScanBudgetBytes` and the page is a prefix. A consumer that
drops these turns a partial history into one that reads as complete, which is the exact failure
the journal exists to prevent.

An event with no timestamp is **dropped and logged**, never given a substitute — it cannot be
placed in a time-ordered history, and inventing a moment for it puts something that never
happened into the audit trail.

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
verbs InstanceService forwards), `EventService` tests mock `IEventSource` and raise its
`EventReceived` event to drive the full wire→dispatch route.

`EventJournalReaderTests` runs the **real** reader against a temporary directory — the journal
is ordinary files, so its whole contract is unit-testable: start position, whole-line framing,
segment rolling, cursor resume, and gap reporting. It writes segments the way the engine does,
one complete line per append.

**Process-bound classes are intentionally not in the unit suite** — `LogSubscriptionService`
spawns a real `kgsm --follow` `Process`, needs a live KGSM, and belongs in an integration
category rather than here. Its one unit-testable dependency, `LogParser`, is covered
(`Utilities/LogParserTests.cs`).

### NuGet Packaging
`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` auto-generates the package on Release builds.

⚠ **Because of that flag, `dotnet pack` does not reliably build first** — it packs whatever is
already in `bin/Release/`, so straight after an edit it will happily produce a package containing
the *previous* build, and consumers restore code you did not write. Build, then copy:

```bash
dotnet build kgsm-lib/kgsm-lib.csproj -c Release        # this is what makes the .nupkg
cp kgsm-lib/bin/Release/TheKrystalShip.KGSM.Lib.<v>.nupkg /home/heisen/local-nuget/
```

Verify before trusting it — a stale package fails as a baffling "my change isn't there":
`unzip -p <nupkg> lib/net10.0/TheKrystalShip.KGSM.dll | strings -el | grep '<a new string literal>'`
(`-el` matters: .NET string literals are UTF-16, so plain `strings` finds type names but never
message text).

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
2. **Journal directory**: needs to be readable, but is tolerated when absent — a host that has never emitted an event has no journal directory until it does, and the reader picks up the first segment when it appears
3. **KGSM path validation**: No built-in validation - ensure `kgsm.sh` exists before instantiating services
4. **JSON parsing**: KGSM may return empty strings for missing fields - always null-coalesce: `?? new()`
5. **Log parsing timezones**: `LogParser` handles ISO8601 (Z suffix) and syslog formats differently

## Integration Points

- **External dependency**: KGSM shell script (not bundled, must be installed separately)
- **Communication**: Process execution (bash) + events read from the on-disk journal
- **Platform**: Linux-only (relies on bash scripts, and on Unix sockets for the watchdog/firewall clients)

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
