# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
