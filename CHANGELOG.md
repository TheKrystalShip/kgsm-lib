# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **`IBlueprintService.GetScaffold()`** — reads the engine's blueprint skeleton
  (`<KGSM_TEMPLATES_DIR>/blueprint.tp`) verbatim, so a surface seeding a new blueprint's buffer starts
  from the same authoritative template the assistant's authoring lane works from. The directory is
  engine-reported (`kgsm --paths --json`), never re-derived in C#; a missing directory or unreadable
  file returns `null` rather than a substitute skeleton. The template's instructional header is part of
  the returned text — it is the authoring help a manual writer reads while filling the file in.

### Added
- **RCON support**: `IRconClient` interface, `RconClient` (minimal Source RCON protocol
  implementation), `RconException`. AOT-safe, no external dependencies. Connects via TCP,
  authenticates with a password, executes commands, and returns raw text responses. Intended
  for periodic polling (connect → execute → disconnect) rather than persistent connections.
- **RCON properties on `Instance` model**: `RconPort` (nullable int), `RconPassword` (string),
  `RconPollIntervalSeconds` (nullable int), `RconPlayersCommand` (string, default "players").
  Materialized from the blueprint into the instance config; the watchdog reads these to poll
  game servers for connected players.

### Added
- **`FileOpResult<T>.Errors`** — the individual reasons behind a multi-reason failure, kept as a list so a
  surface can render one line per reason instead of splitting `Message` back apart. `WriteRaw`'s
  `InvalidDraft` is the one producer today, carrying the engine validator's own error strings verbatim;
  every other outcome leaves it empty and says everything in `Message`.
- **`IBlueprintFiles.ReadRaw(name, maxBytes)` + `WriteRaw(name, content, opts)` — byte-level blueprint
  file I/O.** The typed path (`Create`/`Render`/`TryParse`) handles native blueprints only and strips
  every comment, so a container blueprint or a commented one cannot survive a round-trip through it.
  These read and write the exact bytes instead, which is what lets a surface edit any blueprint as text.
  `ReadRaw` is the one method whose jail spans BOTH engine-reported blueprints directories — a shipped
  blueprint has to be readable to be edited into an override — while every write stays user-dir-only:
  saving an edit to a shipped blueprint creates an override that shadows it, and the system directory is
  structurally unreachable. `WriteRaw` validates through the ENGINE on a temp file the engine's
  `*.bp.yaml` glob cannot see, so an invalid draft never occupies the real filename; a rejection returns
  `InvalidDraft` carrying the engine's full error list. `BlueprintWriteOptions` carries `ExpectedEtag`
  (guarding the file that was READ, which for a first override is the system file), `MaxBytes`, and the
  `Actor`/`Origin` stamped on the emitted event.
- **`IBlueprintService.FindAll(name)` + `Validate(nameOrPath)`** — the engine's path-resolution and
  schema-check surfaces, typed. `FindAll` reports every candidate path with whether it exists, which is
  how a consumer tells a purely custom blueprint apart from a user copy shadowing a shipped one; unlike
  `FindPath` it reports on existence alone, so a MALFORMED blueprint stays locatable — precisely the file
  an editor is opened to repair. `Validate` probes rather than executes, because the engine reports an
  invalid blueprint as a non-zero exit carrying the verdict on stdout: executing would discard exactly
  the answer that matters. No verdict at all returns null — unknown, never an assumed pass.
- **Blueprint lifecycle events — `blueprint_created` / `blueprint_updated` / `blueprint_removed`.**
  `WriteRaw`, `Create` and `Remove` emit them with the caller's provenance threaded through. A failed
  emit never fails the file operation: the bytes are already committed and valid, so reporting an error
  would claim a save that did happen did not. Payloads carry name, tier, override state and runtime —
  never the file body or a diff.
- `RecordingEventManagementService` (test-only) — an `IEventManagementService` that records emissions so
  a test can assert the event type, provenance, and parameters an authority actually threaded through.

### Changed
- **The event data hierarchy grew a subject-neutral root, `KgsmEventDataBase`.** `EventDataBase` was
  instance-scoped by contract ("all events have an InstanceName"), which held only because every event so
  far happened to concern an instance. Blueprints are the first subject that is not one, and forcing them
  through `InstanceName` would fabricate an instance relationship that does not exist. `EventDataBase`
  (instance-scoped) and the new `BlueprintEventDataBase` (blueprint-scoped) are now siblings beneath the
  root, which carries the emission metadata every event has regardless of subject. Adding the next
  non-instance subject means adding one sibling rather than revisiting the root again.
  - **Interface change:** `IEventService.RegisterHandler<T>`'s constraint moves from `EventDataBase` to
    `KgsmEventDataBase`. Call sites are typed per-event and are unaffected; an IMPLEMENTOR of
    `IEventService` (including a test fake) must restate the new constraint to compile.
  - **Interface change:** implementors of `IBlueprintFiles` must add `ReadRaw`/`WriteRaw`, and `Remove`
    gains optional `actor`/`origin` parameters. `BlueprintFiles`'s constructor now also takes
    `IBlueprintService` and `IEventManagementService`.

### Fixed
- `EventServiceTests.Initialize_SubscribesToSocketAndStartsListening` asserted a call made on a
  fire-and-forget background task the moment `Initialize` returned, so it failed intermittently under a
  loaded scheduler. It now waits for the listener to actually start.
- **Lifecycle verbs get their own timeout tier — `KgsmTimeoutOptions.Lifecycle` (default 5 minutes).**
  `start`/`stop`/`restart` ran on the 30s `Default` tier, but a stop writes the instance's stop command
  and drains for up to its `stop_command_timeout_seconds` before the supervisor hard-kills. With the
  shipped default of 30s the two deadlines coincided exactly, so `ProcessRunner` killed the KGSM process
  tree at the very moment the stop was completing: the caller was told the stop FAILED, and — because
  killing KGSM tore down the control-socket connection — the watchdog's own stop was aborted mid-drain
  too. The new tier sits above KGSM's internal ceilings (60s for a start, 120s for a stop) so the inner
  timeout is always the one that fires.

### Added
- `IBlueprintFiles.Render(draft)` + `IBlueprintFiles.TryParse(yaml)` — a matched render↔parse pair for
  the native blueprint YAML template, so an authoring surface can show a user the editable draft text and
  read their edits back. `Render` is the exact string `Create` writes, but pure (no filesystem, no engine);
  `TryParse` is its inverse — a deterministic, AOT-safe line parser (no reflection-based YAML library),
  tolerant of light hand-edits (unquoted/double-quoted scalars, whole-line comments) and preserving
  `$instance_*` placeholders and colons inside quoted scalars. Structural checks only (safe name, native
  runtime, required `executable_file`); semantic validity stays the engine's authority via readback.
- `IWatchdogClient.ForgetAsync(instanceName)` — deregisters an instance from the watchdog entirely
  (supervision table entry, cgroup, boot-autostart intent, persisted restart counters), mapping to the
  daemon's new `DELETE /instance/{name}` (kgsm-watchdog 1.9.0). This is the typed path for the uninstall
  counterpart: without it there was no way to un-supervise an instance, so the daemon held a
  `desired=running` record for every uninstalled native server forever. Idempotent — an unknown name is
  a successful no-op (the instance's kgsm spec is normally already deleted by the time it is called);
  `Ok = false` (409) means the instance is still running and was deliberately NOT deregistered, rather
  than orphaning the process.
  - **Interface change:** implementors of `IWatchdogClient` (test fakes included) must add the member.

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
