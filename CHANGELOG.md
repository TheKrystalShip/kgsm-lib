# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `IBlueprintFiles` (+ impl `BlueprintFiles`) — the write-side authority for native-runtime
  blueprint files: `Create(NativeBlueprintDraft, overwrite)` templates a draft into a valid
  `<name>.bp.yaml` string and atomically writes it into the user blueprints directory; `Remove`
  deletes one from the user dir ONLY (structurally incapable of touching the read-only system
  blueprints dir); `Exists` checks without reading. The SECOND kgsm-lib service that does direct
  `System.IO` (the first is `IInstanceFiles`, whose shape this mirrors: atomic temp→fsync→rename
  write, a jail, `FileOpResult`/`FileOpOutcome` outcome-not-exception). No semantic validation —
  only structural checks (a required `native.executable_file`, a safe lowercase-slug `name`); the
  engine stays the schema authority (validated by reading back through `IBlueprintService.GetInfo`
  in a later phase). The jail root — the user blueprints directory — is learned from the engine
  fresh on every call from `kgsm --paths --json`'s `user.KGSM_USER_BLUEPRINTS_DIR` (deserialized to
  the new `KgsmPaths` model via `KgsmJsonContext`), never re-derived from XDG rules in C#. The YAML
  is a deterministic string template (no YamlDotNet/reflection-based serializer — Native-AOT-safe),
  field order matching `templates/blueprint.tp`; every string scalar is single-quoted (YAML's one
  escape, doubling an embedded `'`), and every nullable field renders the literal `null` — never a
  fabricated placeholder. New `NativeBlueprintDraft`/`NativeBlueprintMetadataDraft`/
  `NativeBlueprintNativeDraft` DTOs (`Core/Models/NativeBlueprintDraft.cs`) and two new
  `FileOpOutcome` members (`BlueprintsDirUnavailable`, `InvalidDraft`) reusing the existing
  `FileOpResult`/`FileOpOutcome` channel from `IInstanceFiles`. Wired onto
  `IKgsmClient.BlueprintFiles` and `AddKgsmServices`. Phase 1 of the assistant-blueprint-authoring
  plan (`assistant-blueprint-authoring-plan.md`) — kgsm engine untouched.
- `IInstanceFiles` (+ impl `InstanceFiles`) — the single jailed filesystem authority for an
  instance's working directory: `List`, `Read`, `Write` (create/overwrite), `Delete`, `Rename`.
  The first kgsm-lib service that does direct `System.IO` (every other service shells the
  `kgsm` engine) and the first P/Invoke in the repo (`Native/LibC.cs`, a Native-AOT-safe
  `LibraryImport` `lstat`/`stat` wrapper with a blittable exact-size struct). The project now
  sets `<AllowUnsafeBlocks>` — required unconditionally by the `LibraryImport` source
  generator regardless of parameter shape (confirmed empirically), orthogonal to AOT/trim
  safety; no hand-written pointer code. The jail is ported from kgsm-api's `InstanceFileService` (the
  stronger of the ecosystem's two prior jails, `instance-filesystem-authority-plan.md`): a
  full POSIX-realpath canonicaliser resolving symlinks at EVERY path component (not just the
  leaf), a 64-hop loop guard, NUL-path rejection, and exact-root-or-descendant ordinal
  containment. Only regular files are opened for read/write; directories are only listed (or,
  with `DeleteOptions.AllowDir`, deleted when empty — never recursive); FIFO/socket/device is
  never opened. Binary detection is an 8 KB NUL scan plus a full strict-UTF-8 decode; writes
  are atomic (temp file → fsync → mode-preserve → rename, never truncate-in-place); reads and
  writes carry an sha256 etag for optimistic concurrency (`WriteOptions.ExpectedEtag` →
  `EtagMismatch` on drift); `WriteOptions.Backup` writes a sibling `.kgsmbak` before
  overwriting. Size caps are always a caller-supplied parameter, never a lib-wide default.
  New `FileOpResult`/`FileOpResult<T>` result type and `DirListing`/`FileEntry`/`FileKind`/
  `FileContent`/`FileStat`/`WriteOptions`/`DeleteOptions`/`RenameOptions` DTOs
  (`Core/Models/InstanceFileModels.cs`) — deliberately NOT registered in `KgsmJsonContext`,
  in-process only (the firewall-result precedent). Wired onto `IKgsmClient.InstanceFiles` and
  `AddKgsmServices`. Phase 1 of the cross-repo instance-filesystem-authority plan.
- `IWatchdogClient` gains the watchdog's on-demand UPnP control surface: `GetUpnpAsync`,
  `OpenUpnpAsync`, and `CloseUpnpAsync` (the typed client for the daemon's `GET /upnp/{name}` and
  `POST /upnp/{name}/open|close`). `GetUpnpAsync` returns `null` only when the daemon is unreachable and
  otherwise a `WatchdogUpnpList` whose `State` distinguishes a real query (`"queried"`, mappings possibly
  empty) from an inability to reach the router (`"unavailable"`) — the latter is never presented as "no
  forwards". `OpenUpnpAsync` accepts an optional explicit `PortMapping` set (else the instance's own
  ports) and `CloseUpnpAsync` removes them; both return a `WatchdogUpnpActionResult` whose `Outcome` is
  the honest three-way `applied` / `skipped` / `failed`, and both stamp the emitted audit event with an
  `origin` (default `control`). New `WatchdogUpnpMapping` / `WatchdogUpnpList` / `WatchdogUpnpActionResult`
  / `WatchdogUpnpOpenRequest` models, registered reflection-free in `KgsmJsonContext` (Native-AOT-safe).
- `IEventService.RegisterRawHandler(Func<EventWrapper, Task>)`: a catch-all hook that fires
  with the full envelope for every event the socket delivers, including event types with no
  `RegisterHandler<T>` mapping — the foundation for a neutral, raw event-history persister
  (kgsm-monitor) that never silently drops a new engine event type. Fires before typed
  dispatch and never suppresses it; each raw handler invocation is isolated in its own
  try/catch so a throwing handler can't stop the socket read loop or block other handlers.
- `TheKrystalShip.KGSM.Events.AuditId.ForEvent(EventWrapper)`: a pure, deterministic
  content-derived event id (`"evt_" + 16-hex-char SHA-256 digest`, no `Guid`/randomness) —
  hashes hostname + timestamp (unix ms) + event type + the `Data` payload's `InstanceName` +
  a digest of `Data`'s raw JSON text. Lets two independent computations of the same event
  (e.g. kgsm-monitor persisting it and kgsm-api's live handler correlating it) agree on the
  same id with zero coordination.
- `Instance.CpuPriority` (string?) and `Instance.MemoryCapMb` (int?) — expose the new per-instance
  resource-cap config keys (`cpu_priority`/`memory_cap_mb`) for watchdog cgroup enforcement.
- `IWatchdogClient.SetCpuPriorityAsync` — live-applies a CPU priority to a running instance's cgroup
  (low/normal/high → cpu.weight 50/100/400); returns `Ok=false` (not an exception) when the cgroup
  is absent (not running).
- `Instance.ScheduledRestart`, `RestartTime`, `RestartDay`, `Timezone` — expose the new schedule
  config keys for kgsm-scheduler to read.
- `IWatchdogClient.RestartAsync` — atomic intentional restart via `POST /restart/{name}?origin=`.
- `Instance.AutoBackupOnRestart` (bool?), `BackupRetention` (int?) — expose the Phase 4
  auto-backup config keys for kgsm-scheduler to read.
- Added `CrashRestart` (bool?) and `CrashMaxRestarts` (int?) to `Instance` model for per-instance crash-restart policy (Phase 6)
- `IInstanceService.PruneBackups(instanceName, keepN, actor, origin)` — prune old backups
  via the new `instances prune-backups --keep=N` kgsm primitive. Called by the scheduler
  after each auto-backup to enforce the retention policy.

## [1.31.0] - 2026-07-03

### Added
- `IWatchdogClient.EnableAsync(name)` / `DisableAsync(name)`: add/remove an instance in the
  watchdog's persisted boot-autostart set (POST `/enable`·`/disable`; idempotent — an
  already-enabled/disabled name returns `Ok == false` (409), never throws).
- `IWatchdogClient.GetEnabledNamesAsync()`: query the current boot-autostart set (GET `/enabled`);
  empty list when none, never null. Phase 1 of the KGSM Settings milestone (per-instance autostart).

## [1.30.0] - 2026-07-01

### Added
- `WatchdogPlayer` DTO: mirrors the watchdog's `PlayerSession` record for the `GET /players`
  endpoint response.
- `IWatchdogClient.GetAllPlayersAsync()`: fetches the live player session map from the watchdog.
  Returns `Dictionary<string, WatchdogPlayer[]>?` keyed by instance name, or `null` when the
  daemon is unreachable.

## [1.28.0] - 2026-06-30

### Added
- Initial versioned release.
