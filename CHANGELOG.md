# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **`KgsmEventCatalog`** — the one registry of what the engine emits, so every consumer stops
  working it out separately. Per type: the class its payload deserializes into (`PayloadType`), its
  subject, whether it reports a fact or a step inside one (`EventWeight`), whether it reports
  something completing or failing (`EventOutcome`), and per payload field what kind of data that
  field holds (`FieldSensitivity`, `FieldShape`). All 55 typed events are classified.

  **`EventService` dispatches off it**, so an event that can be deserialized is necessarily one that
  has been classified — there is no second table to fall out of step with, and an event is added by
  adding a descriptor because there is nowhere else to register it. The `Instance<TData>` /
  `BlueprintEvent<TData>` helpers are constrained to the matching payload base, so an event's subject
  and its payload's own base cannot disagree either.

  **It states facts and never policy.** `Phase` does not mean "hide this" and `Personal` does not
  mean "refuse this" — a consumer decides what to do with a fact, and two consumers may decide
  differently. The moment the catalog carries a permission, every surface inherits whichever one
  wrote the rule.

  `FieldSensitivity` is why: `PlayerAddr` is `Personal` (it identifies a person rather than a
  player, and the game shows it to nobody), `Command` is `Privileged` (a console command can create
  an operator or carry a token), and a moderation `Target` is `Conditional` — it may be an address,
  a name or an id, the blueprint declares which, and the event does not carry that. This library
  already refuses to classify a `Target` on a consumer's behalf; the catalog refuses for the same
  reason rather than guessing.

  `Describe` never returns null. An unrecognised type comes back with `Known` false, its subject
  read off the engine's naming convention, and **no fields** — which means "render nothing from the
  payload", not "the payload is empty". An event nobody has classified may carry anything.

  Three tests hold it to the engine: every declared payload property is classified, no descriptor
  names a field its payload does not have, and every event data class in the assembly is named by
  some descriptor — that last one catching a payload class added and never wired, which the runtime
  would drop as an unknown type. A new event or a new field fails this build until somebody
  classifies it. The library stays reflection-free at runtime; the reflection is in the test project.

- **`IInstanceFiles.Find` and `IInstanceFiles.Search`** — a recursive name walk (glob) and a content
  search (regex) inside an instance's jail, so a consumer can locate a game's own config without
  descending a directory at a time. A game's config layout is a fact about the game, not about KGSM's
  paths, so it is found rather than known: reaching Palworld's `PalWorldSettings.ini` cost five
  sequential single-level listings before this.
- **Symlinked directories are recorded and never descended into.** Containment for the walk is that
  absence of a path out, not a check applied afterwards — the weaker of two containment rules is the
  one an attacker picks, so there is only the one.
- Both bound the work (`FindOptions` / `FileSearchOptions`) and report **truncation separately from an
  incomplete walk**: "more matched than were returned" and "the walk stopped early" are different
  facts, and collapsing them lets *I stopped looking* read as *that is all there is*. Directories
  named `backups` are skipped unless asked for — an archived copy is not the file a question about the
  live server is about. Content search skips binaries and oversized files, and refuses an expression
  that does not compile (`FileOpOutcome.InvalidArgument`); patterns run `NonBacktracking`, since a
  caller-supplied regex over thousands of files is otherwise a denial of service.

### Changed

- **`IWatchdogClient.GetAllPlayersAsync` → `GetPlayerPresenceAsync`, and it now answers whether a
  roster is knowable at all.** It returns
  `IReadOnlyDictionary<string, WatchdogInstancePresence>` — every instance the supervisor knows,
  each carrying `Detection` (`log` / `rcon` / `none` / `unknown`) beside its sessions.

  The old shape listed only instances with tracked sessions, which made an absent instance ambiguous
  between "nobody is online" and "this game cannot report players" — and every consumer that read the
  first meaning of the second stated something the host does not know. Detection travels with the
  roster so a caller cannot take one without the other; `WatchdogInstancePresence.IsDetected` is the
  guard, true only for `log` and `rcon`.

  **The supervisor decides it.** The predicate includes whether a pattern *compiles*, which is not
  something a consumer can re-derive from an instance's config — and three surfaces each deriving it
  is how they come to disagree. Renamed rather than overloaded so the shape change is a compile error
  at every call site.

- **`GetPlayerPresenceAsync` returns null on an unreachable daemon** instead of throwing
  `HttpRequestException`, matching what the contract already documented and what every other read on
  this client does. Null is the daemon being unavailable, never a host with nobody playing.

### Fixed

- **`RconClient` authenticates with `SERVERDATA_AUTH`.** The auth packet is type 3; type 0 is
  `SERVERDATA_RESPONSE_VALUE`, a server→client type. A server answers it with id -1 — the same
  rejection a wrong password earns — so the failure reads as a credentials problem and survives any
  amount of checking the password.

- **The auth verdict is matched by packet type, not by arrival order.** Servers may precede
  `SERVERDATA_AUTH_RESPONSE` with an empty `SERVERDATA_RESPONSE_VALUE` carrying the same id. Taking
  the first packet as the verdict leaves the real one queued and every later read returns the
  previous request's packet.

- **A command response ends at the protocol's sentinel.** `ExecuteCommandAsync` follows the command
  with an empty `SERVERDATA_RESPONSE_VALUE` and reads until the server echoes it, which orders
  correctly behind however many packets the response occupies. Waiting instead for an empty body
  never returns for a server whose reply is a single packet with content and no trailer.

- **Split responses are concatenated verbatim.** The parts are byte continuations; the newline
  previously inserted between them landed inside whatever token straddled the split.

- **Reads have a deadline.** `NetworkStream.ReadTimeout` governs only synchronous reads, so an async
  read carried no timeout of its own and a server that stopped answering hung the call for as long as
  the caller's token allowed. Exhausting it now raises `RconException`.

- **A 1–2 character response body is no longer dropped.** The body was read only when the packet
  exceeded 12 bytes, two more than the 10 an empty body occupies.

### Added

- **`Instance.RconPlayersRegex`** — the blueprint's pattern for reading one player out of
  `RconPlayersCommand`'s output, with optional named groups `id` and `name`. Rosters are worded per
  game — a header and one `-Name` line per player here, an id and a name in columns there — so the
  shape travels as data and a consumer applies it without knowing which game it is polling. Empty
  means the output cannot be read, which is not the same as a server reporting nobody connected.

- **`InstanceUpdateAvailableData`** — the engine's `instance_update_available` event, carrying
  `CurrentVersion` and `LatestVersion`. Update availability is a fact kgsm establishes and announces
  through the journal, so every consumer reads it the same way it reads the rest of `server.*`;
  nothing has to run its own probe or keep the answer in memory.

- **`IInstanceService.CheckUpdate(instanceName, emit, actor, origin)`** — `emit` runs the engine's
  recording check, which writes what it found beside the instance and announces a version it has not
  announced before. Recording is what makes a repeated sweep silent, so exactly one caller per host
  owns it; left `false`, the check answers the caller and changes nothing. `actor`/`origin` stamp the
  announcement the same way every other mutating verb stamps its events — an unattended sweep that
  omits them is attributed to whatever OS user it runs as, which reads as a person having asked.

- **`VersionInfo.CheckedAt`** — when `Latest` was fetched from upstream. A fast status read answers
  from the record the engine keeps, so how old the answer is comes with it. `null` means no check has
  ever run, never a substituted moment.

- **`KgsmTimeoutOptions.UpdateCheck`** (3 minutes) for the two commands that reach a game's upstream —
  `check-update` and `version --latest`. Both sat on the 30-second default meant for local reads,
  which is the same ceiling a container's registry probe already uses internally: the outer timeout
  would kill the process tree and report a timeout for a check the engine was about to answer
  honestly.

- **`IInstanceBackups.OpenArchive(instance, backupId)`** — read access to a compressed backup's archive
  bytes, plus the sha256 its manifest recorded. Its own service rather than a second root on
  `IInstanceFiles`, because backups deliberately live OUTSIDE the working directory (uninstalling an
  instance removes that directory wholesale and leaves the backups intact) — two roots with different
  lifetimes, and folding them together would make "the instance's files" mean two things.

  Only a **compressed** backup can be opened. An uncompressed one is a `data/` tree, not a single
  artifact: there is nothing to hand over as one stream and no digest to verify it against, so it is
  refused rather than tarred on the fly into something the manifest never described. The digest is the
  manifest's, carried verbatim — recomputing it from the bytes being served would read the archive twice
  and prove nothing about their provenance.

  The **manifest is the gate**: a directory with no readable manifest is not a backup, which is what
  keeps a half-built one (still staging) and a foreign directory invisible — the same rule the engine's
  own listing applies, so the two cannot disagree about what exists.

### Changed

- **The path-containment jail is one implementation** (`InstanceJail`), shared by `InstanceFiles` and
  `InstanceBackups` rather than written twice. Two jails that are "the same rules, written twice" drift,
  and a drifted containment check is not a weaker guarantee but no guarantee — the weaker of the two is
  the one an attacker picks. The roots differ per service; the rule does not.

- **`IInstanceService.DeleteBackup(instance, backupId, actor, origin)`** — remove one backup by id.
  The engine accepts only an id it itself lists as a backup, so a directory in the backups store
  carrying no manifest is refused rather than removed.

- **`InstanceBackupDeletedData` and `InstanceBackupsPrunedData`** — the two backup-removal events,
  registered in the type map and `KgsmJsonContext`. Separate types because they answer different
  questions: a delete is an operator naming one snapshot, a prune is retention policy sweeping
  whatever fell outside the keep window. The delete carries the backup id in `Source`; the prune
  carries `Deleted`/`Kept` as ints, since one event covers the whole sweep and the ids it removed are
  exactly the ones the instance no longer lists.

### Removed

- **`IWatchdogClient.OpenUpnpAsync` / `CloseUpnpAsync`**, and the `WatchdogUpnpActionResult` /
  `WatchdogUpnpOpenRequest` models they carried. An instance's router forwards are opened by the
  supervisor when it starts and released when it stops, so there is no on-demand open for a client to
  drive; the watchdog no longer serves the routes these called. `GetUpnpAsync` stays — reading what the
  router actually holds is a question a diagnosing operator has. **Breaking**, hence the major bump: an
  implementer of the interface loses two members.

### Added

- **`InstanceUpnpReassertedData`** — the typed half of kgsm's `instance_upnp_reasserted`
  (kgsm 3.11.0-rc1), registered in the event-type map and in `KgsmJsonContext`. A router forward that
  went missing while its instance kept running, put back by the watchdog's sweep. Separate from
  `InstanceUpnpOpenedData` because an open accompanies a bring-up while this one says the mapping
  disappeared with nothing on this host asking for it. `Ports` holds the subset that was missing, not
  the instance's whole configured set.

- **`InstanceRestartStartedData` / `InstanceRestartFinishedData`** — the typed halves of kgsm's restart
  bracket (`instance_restart_started` / `instance_restart_finished`, kgsm 3.7.4-rc1), registered in the
  event-type map and in `KgsmJsonContext`. A restart runs its stop and its start internally, so no
  other event fires between them: this pair is the only thing that spans the run, and it completes the
  set — update, stop and restart are all bracketed the same way now. Finished says the run ended;
  `InstanceRestartedData` remains the separate fact that the instance came back.

### Added — `IEventJournalHistory`, reading the journal back

Querying what the engine did, as the companion to `IEventJournalReader`'s tailing of it. Both read
the same segments, and neither needs anything running besides the engine that wrote them — so a
host answers for its own history with no daemon, no index, and no leaf installed.

```csharp
EventHistoryPage page = await history.QueryAsync(new EventHistoryQuery
{
    Instance = "factorio", SinceMs = since, Limit = 50
});
```

Filters are `Instance`, `Blueprint`, `Type`, `SinceMs`, `UntilMs`, ANDed, each optional. `Instance`
and `Blueprint` are orthogonal: a server and the blueprint it was built from routinely share a name,
and a query for one never returns the other. Results are newest-first, keyset-paged through
`BeforeTsMs`/`BeforeId` → `NextCursorTsMs`/`NextCursorId`, and `Limit` is clamped to `[1, 1000]`.

The cursor is a `(timestamp, id)` pair, not an id alone, so a caller merging this history with
another source can page both feeds from one cursor. The id it passes may belong to that other source
and name no event here — it is only ever compared as a tie-break, never resolved against the
journal. Resolving it would mean an id from the other feed read as "no cursor", and the caller would
page the newest rows forever.

There is no index and no cache, which is what stops a second copy from disagreeing with the record.
Segments are named by date, so a bounded window is narrowed by file *name* before one is opened;
segments are read newest-first and the scan stops when the page fills; and each is streamed forward
once with matches held in a ring buffer the size of the page, so memory is bounded by the page and
no file is read backwards or loaded whole. `KgsmOptions.EventHistoryScanBudgetBytes` (64 MiB) bounds
the read regardless, and hitting it sets `Truncated` rather than passing off a prefix as the whole
answer.

Three signals keep a partial answer from reading as a complete one. `CoverageFrom` is the oldest
moment the journal can still answer for — retention deletes whole segments oldest-first, so it is
exact. `JournalReadable` distinguishes an unreadable journal from one that matched nothing.
`Truncated` reports a budgeted scan. A query never throws for a missing or unreadable journal; an
event carrying no timestamp is reported and dropped rather than given a fabricated one.

### Changed — events carry their journal position (**breaking**)

`IEventService.RegisterRawHandler` takes `Func<EventWrapper, EventPosition, Task>`, and
`IEventSource.EventReceived` takes `Func<string, EventPosition, Task>`.

`EventPosition` is the segment and the byte offset an event's line begins at. Because each event is
one whole line and retention deletes whole segments rather than rewriting them, no two events share
a position and an event's position never changes — so `AuditId.ForPosition` turns it into a stable
id. That is what lets a consumer watching events arrive and a consumer reading history back name the
same event identically with no coordination: one takes the position from the transport, the other
from the file, and both compute the same id.

Only a raw handler receives it; typed dispatch does not, so a consumer needing the id inside a typed
handler captures it from a raw handler first.

### Added — `AuditId.ForPosition` / `TryParsePosition`

`evt_<segment>_<offset>`, e.g. `evt_2026-08-07_000000001234`. Unique by construction, and ordered
like the file — segment names are equal-width dates and the offset is fixed-width, so comparing two
ids as plain strings compares their positions in the journal. One value therefore serves as both
identity and pagination cursor.

`AuditId.ForEvent` remains, for a caller holding an envelope with no position to hand. It hashes a
timestamp of one-second granularity and so cannot distinguish two identical events emitted within
the same second; `ForPosition` can, and is what the audit trail is keyed on.

### Added

- **`InstanceStopStartedData` / `InstanceStopFinishedData`** — the typed halves of kgsm's shutdown
  bracket (`instance_stop_started` / `instance_stop_finished`, kgsm 3.7.3-rc1), registered in the
  event-type map and in `KgsmJsonContext` so an AOT consumer can deserialize them. They give `stop`
  the shape `update` already has: a consumer learns that an instance is shutting down for as long as
  the supervisor waits for the game to drain, from the journal alone, whichever entrypoint drove it.
  Finished is emitted on every outcome — it says the run ended, while `InstanceStoppedData` remains
  the separate fact that the instance is down.

### Added — player moderation

- **`IInstanceService.Kick` / `Ban` / `Unban`**, each taking the instance, the target, and the
  optional `actor` / `origin` provenance pair the rest of the operational verbs take. They run
  `kgsm instances kick|ban|unban <instance> <target>` and **pass the target through untouched** —
  the engine substitutes it into the blueprint's template, so the lib never builds the console
  command itself. A second implementation of that substitution here would be a second answer that
  could disagree with the one that actually runs. A target containing a line break is rejected
  before a process is spawned (a console reads one command per line, so it would deliver a second
  command nobody issued).

- **`ModerationCommand.TryGetTargetKind` and the `ModerationTargetKind` enum (`Ip` / `Name` /
  `Id`)** — the identity contract, read out of the template's placeholder. A blueprint writing
  `kick {ip}` says both "the verb is kick" and "hand it an IP address", so a caller reads the
  placeholder to know which field of a player record to send. A template with no recognised
  placeholder, or with more than one, is reported as **unsupported** rather than resolved to a
  guess — an ambiguous template names no single identity, and a bare verb would send the command
  with no target at all.

- **`Blueprint.KickCommand` / `BanCommand` / `UnbanCommand`** (nullable) and the matching
  `Instance` properties bound from the `kick_command` / `ban_command` / `unban_command` wire
  fields. Empty/null means the game declares no such command, in which case the engine refuses the
  action rather than approximating it with a different one.

- **Three moderation event types** — `instance_player_kicked`, `instance_player_banned`,
  `instance_player_unbanned` (`InstancePlayerKickedData` / `BannedData` / `UnbannedData`, sharing
  `InstanceModerationDataBase`), carrying `Target` and the resolved `Command`. They are their own
  types rather than `instance_input_sent` records because the subject is a player, not a command:
  a consumer asking "who was banned on this server" filters on the type instead of pattern-matching
  text a hand-typed `SendInput` could also produce. Unlike the join/leave pair (autonomous
  observations stamped `system`), these carry operator provenance.

  `Target` is carried **verbatim and never classified** — the blueprint is where that meaning is
  declared, and re-deriving it here would be a second answer that could disagree. A consumer that
  needs the kind reads it from the instance's template with `ModerationCommand.TryGetTargetKind`.

  Note for a consumer offering "lift a ban": an unban's subject is by definition not connected, so
  it cannot be resolved from a live player roster — `instance_player_banned` is the record to
  select from.

  Requires kgsm ≥ 3.7.0-rc1.

### Removed — BREAKING: the Unix socket event transport

- **`UnixSocketClient` and `IUnixSocketClient` are gone**, along with `KgsmEventTransport`,
  `KgsmOptions.SocketPath`, `KgsmOptions.EventTransport`, the
  `AddKgsmServices(services, kgsmPath, socketPath)` overload, and
  `IEventManagementService`'s `EnableSocket` / `DisableSocket` / `TestSocket` / `GetSocketStatus`.
  The engine no longer has a socket transport to drive. **This is the major version bump.**

  Socket binding is exclusive — one socket, one reader — which is the whole reason a consumer
  ever needed its own path and the engine ever needed to be configured with the list of them.
  The journal is a plain file: every consumer on a host reads the same directory, concurrently,
  with no registration and nothing to reserve. There is no longer a transport to select, so
  `AddKgsmServices(kgsmPath)` and the options overload both wire the journal reader.

  **Migrating:** drop the `socketPath` argument (or `SocketPath` / `EventTransport` from your
  options) and set `EventJournalDirectory` if the engine's journal is not at
  `/var/lib/kgsm/events`. Handler code is unaffected — it was already written against
  `IEventService`, which is what made this swap possible without touching a consumer.

  `SocketException` stays: it belongs to `IFirewallService`, which talks to the kgsm-firewall
  authority over its own socket and is unrelated to events.

### Fixed
- **`EventService.Initialize()` is idempotent.** Two callers legitimately reach it — `KgsmClient`'s
  constructor and whatever the consumer wires — and a second pass both re-subscribed the transport's
  `EventReceived` and started a second read loop, so one event was delivered **four** times
  (twice-subscribed × two loops). Every consumer then did its thing four times over: four Discord
  announcements, four notifications, four cache busts for one server starting. On the journal
  transport the two loops additionally raced the reader's single cursor. Repeat calls now log at
  debug and return; a caller must not have to know who else initializes.

### Added
- **The event journal transport.** `EventJournalReader` tails the engine's append-only NDJSON
  journal (`/var/lib/kgsm/events/YYYY-MM-DD.ndjson`) instead of binding a socket, selected with
  `KgsmOptions.EventTransport = KgsmEventTransport.Journal` or the new
  `AddKgsmServices(kgsmPath)` overload. Both transports now sit behind `IEventSource`, which
  `EventService` consumes, so **no handler code changes** when a consumer switches — and each
  consumer switches on its own schedule. The socket stays the default: taking this version does
  not move a consumer's transport on its own.

  What the journal buys is what a socket cannot do. Binding is exclusive, so each consumer
  needed its own path and the engine had to be configured with the list of them; a file has no
  such constraint, so any number of consumers read the same journal and the engine holds no
  knowledge of who reads. Delivery stops being live-only: a consumer that was down catches up
  from its stored position (`IEventCursorStore`, with `FileEventCursorStore` and
  `NullEventCursorStore` supplied, and a consumer that owns a database expected to store the
  cursor there). Position is a segment plus a byte offset, which is exact because each event is
  one whole line — only complete lines are dispatched, so a partially-flushed append is picked
  up whole on the next pass rather than delivered truncated.

  Where a consumer starts is an explicit per-consumer decision (`EventStartPosition`): a
  consumer that materializes the journal into an index must be able to replay it, while one
  that announces events must never replay a backlog. When a stored position can no longer be
  satisfied — retention deletes segments on age alone and never consults a consumer — the
  reader reports an `EventJournalGap` through `IEventService.RegisterGapHandler` and then falls
  back to its cold-start position, so a consumer can record that its history before that point
  is incomplete instead of presenting a partial record as a whole one. The socket transport
  could not express this at all: a missed event was indistinguishable from one that never
  happened.

  Delivery is at-least-once — the cursor is stored only past events already dispatched, so a
  crash costs re-delivery rather than loss, and a consumer that persists what it reads must be
  idempotent (a deterministic `AuditId` is what makes that free). The saved position is the
  oldest unfinished one, which also covers an emit that starts just before midnight and lands
  in yesterday's segment after the reader has moved into today's.

### Changed
- **The scheduled backup cadence replaces the on-restart backup toggle.** `Instance` carries
  `BackupSchedule` / `BackupTime` / `BackupDay` (kgsm's `backup_schedule`, `backup_time`,
  `backup_day`), and `AutoBackupOnRestart` is gone. A backup is taken against the instance as it
  is, running or not, so it no longer needs a restart window to happen in and the two schedules
  are independent. `Timezone` now serves both — one instance has one answer for what time it is.

### Added
- **Server notes** — `IInstanceService.SetInstanceNote()` plus `Instance.NoteBody` /
  `NoteUpdatedBy` / `NoteUpdatedAt`: the operator-authored free-text note a surface renders on a
  game server. Stored in the instance's `.config.ini` under `note` (base64) + `note_updated_by` +
  `note_updated_at`, so kgsm itself needs no note-specific command — the write is three ordinary
  `config-set` calls and the read comes off the roster. `InstanceNote` owns the codec: the body is
  encoded because that file is sourced as `key="value"` and re-emitted through a tab-delimited jq
  pipeline, where a raw quote, `$`, backtick, tab or newline would brick the instance. Attribution
  is written first and the body last, and `InstanceNoteResult` reports which keys landed, so a
  partial write can never credit a new body to the wrong person. A value that does not decode is
  returned verbatim, so a hand-edited note still renders. Bodies are capped at 600 characters and
  an over-long one throws before any write rather than being truncated.

### Added
- **`IInstanceService.GetBackupsDetailed()`** and the `InstanceBackup` model — an instance's
  backups with everything each one records (id, creation time, captured version, size, file
  count, sources, sha256), newest first, from `kgsm instances backups <instance> --json`.
  `GetBackups()` still returns the id-only listing for callers that need to distinguish an
  engine failure from an empty store. `InstanceBackup` is registered in `KgsmJsonContext`
  (required — there is no reflection fallback under AOT). `Sha256` is null for an
  uncompressed backup, which is a tree rather than a single artifact; null means "not
  applicable", never a placeholder digest.

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
