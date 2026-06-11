# Using kgsm-lib as the host-monitor's instance inventory

**Status**: Reference · **Created**: 2026-06-11
**Audience**: the spin-off host-monitoring project (processes / CPU / memory / disk)

This note records what kgsm-lib already exposes so the monitor can locate every
KGSM instance's footprint **without re-shelling `kgsm.sh` or parsing instance
`.ini` files itself**. The short version: two AOT-safe, name-keyed calls cover
the entire per-instance inventory. No new kgsm-lib accessor was needed (one
convenience, `Instance.SystemdUnit`, was added — see below).

> **Spine: never fabricate a metric.** This monitor exists because the prior
> attempt (`kgsm-api`) invented CPU/memory from `Random.Shared` and the GC heap.
> kgsm-lib gives you the *inventory* (what to measure, where); the monitor reads
> *real* sources (`/proc`, cgroups, `statvfs`/`DriveInfo`) or reports "unknown".

## The two-call recipe (and why it's two calls, not one)

| Call | Returns | Cadence |
|---|---|---|
| `IInstanceService.GetAll()` → `Dictionary<string, Instance>` | **Static footprint** for the whole fleet (45 fields/instance): dirs, pid file, ports, runtime, lifecycle manager, systemd unit file, compose file. Changes rarely. | **Fetch once**, refresh on instance create/remove (or an occasional re-poll). |
| `IInstanceService.GetAllStatuses(bool fast = true)` → `Dictionary<string, InstanceRuntimeStatus>` | **Runtime state**: `Process.Pid`, `Status` (running?), `Resources.DiskUsage`. Changes every tick. | **Sample on the loop.** Use `fast: true` — it skips the per-instance network update-check (~20× cheaper) and you don't want update polling on a metrics cadence. |

Both dictionaries are keyed by instance name; join them by key. **Don't ask for a
merged "monitoring inventory" helper** — the static/runtime split *is* the right
seam for a sampler. Pull the static footprint once; re-pulling 45 static fields
every sample just to get a PID is wasted work.

`GetAllStatuses` is the bulk read: one KGSM bootstrap for the whole fleet instead
of one process per instance (it exists specifically to avoid that fan-out).

## Field-by-field: what the monitor needs → where it comes from

| Monitor need | kgsm-lib source | Notes |
|---|---|---|
| List of instances | `GetAll()` keys (or `GetAllStatuses()` keys) | name-keyed |
| Is it running? | `GetAllStatuses()[name].Status` (bool) | management-script status (see liveness caveat) |
| Main PID | `GetAllStatuses()[name].Process.Pid` (`int?`) | null when stopped |
| PID file path | `GetAll()[name].PidFile` | standalone-native fallback source for the PID |
| Runtime kind | `GetAll()[name].Runtime` / `…Configuration.Runtime` | `Native` \| `Container` |
| Lifecycle manager | `GetAll()[name].LifecycleManager` / `…Configuration.LifecycleManager` | `Standalone` \| `Systemd` |
| systemd unit (→ cgroup) | `GetAll()[name].SystemdUnit` | e.g. `7dtd.service`; empty if not systemd |
| Container compose file | `GetAll()[name].ComposeFile` | e.g. `<wd>/<name>.docker-compose.yml`; empty if native |
| Working / install / logs / saves dirs | `GetAll()[name].WorkingDir` / `InstallDir` / `LogsDir` / `SavesDir` | disk-usage targets |
| Ports | `GetAll()[name].Ports` / `…Configuration.Ports` | pipe-separated `26900:26903/tcp\|…` |
| Instance disk usage (snapshot) | `GetAllStatuses()[name].Resources.DiskUsage` | human string (`"16G"`) from `du -sh`; for byte time-series, measure the dirs yourself |

## Per-instance metric anchor, by type

How you actually measure an instance differs by type — kgsm-lib tells you which:

- **Native + standalone** → start from `Process.Pid`, walk the process tree
  yourself for child-inclusive CPU/memory (`/proc/<pid>/stat`, `/proc/<pid>/status`).
- **Systemd** → use `Instance.SystemdUnit` to find the cgroup
  (`/sys/fs/cgroup/system.slice/<unit>/…`). cgroup accounting already includes
  child processes — prefer it over the bare PID.
- **Container** → use `Instance.ComposeFile`; resolve the running container via
  the Docker API/CLI and read *its* cgroup. kgsm-lib deliberately does **not**
  expose a "container name" — Docker derives it from the compose project + service,
  and any string we synthesized would be a guess. Resolving it is the monitor's job.

**Verification status (be honest about it):** the live probes here were run
against a native-standalone instance (`7dtd`), where `SystemdServiceFile` and
`ComposeFile` are empty. The systemd and container anchors are confirmed from
KGSM source (`commands/handlers/files.systemd.sh` populates
`systemd_service_file=<dir>/<name>.service`; `commands/handlers/instances.sh`
populates `compose_file=<wd>/<name>.docker-compose.yml`), **not** exercised live
on this box. Validate against a real systemd and a real container instance before
relying on those two paths.

## What kgsm-lib does NOT give you (the monitor's own job)

Keep these out of kgsm-lib — they are sampling/OS concerns, not KGSM inventory:

- **Host-level time-series** (CPU%, memory, swap, disk, load over time). KGSM's
  `SystemService` returns a one-shot snapshot as text (`GetInfo`/`GetMemory`/…);
  the monitor samples `/proc` natively for series.
- **Per-process CPU%** — requires two `/proc/<pid>/stat` reads delta'd over an
  interval. kgsm-lib gives the PID; the rate is yours.
- **Child-process walking** for standalone instances.
- **Container-name / container-id resolution** from the compose file.

## AOT note

kgsm-lib is `IsAotCompatible` and reflection-free; all CLI deserialization runs
through the `KgsmJsonContext` source generator. `Instance`,
`Dictionary<string, Instance>`, `InstanceRuntimeStatus`, and
`Dictionary<string, InstanceRuntimeStatus>` are already registered, so the two
inventory calls work under Native AOT as-is. If you add a method that
deserializes a new type, register it in `Json/KgsmJsonContext.cs` or it throws
`NotSupportedException` at runtime (there is no reflection fallback). See
`CLAUDE.md` §3.
