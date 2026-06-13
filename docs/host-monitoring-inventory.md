# Using kgsm-lib as the host-monitor's instance inventory

**Status**: Reference · **Created**: 2026-06-11
**Audience**: the spin-off host-monitoring project (processes / CPU / memory / disk)

This note records what kgsm-lib already exposes so the monitor can locate every
KGSM instance's footprint **without re-shelling `kgsm.sh` or parsing instance
`.ini` files itself**. The short version: two AOT-safe, name-keyed calls cover
the entire per-instance inventory.

> **Spine: never fabricate a metric.** This monitor exists because the prior
> attempt (`kgsm-api`) invented CPU/memory from `Random.Shared` and the GC heap.
> kgsm-lib gives you the *inventory* (what to measure, where); the monitor reads
> *real* sources (`/proc`, cgroups, `statvfs`/`DriveInfo`) or reports "unknown".

## The two-call recipe (and why it's two calls, not one)

| Call | Returns | Cadence |
|---|---|---|
| `IInstanceService.GetAll()` → `Dictionary<string, Instance>` | **Static footprint** for the whole fleet: dirs, pid file, ports, runtime, compose file. Changes rarely. | **Fetch once**, refresh on instance create/remove (or an occasional re-poll). |
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
| Main PID | `GetAllStatuses()[name].Process.Pid` (`int?`) | null when stopped; a real PID for **native** instances (containers key off a container id — see anchors) |
| PID file path | `GetAll()[name].PidFile` | **overloaded** — a real **host PID** for native, a **Docker container id** for containers; disambiguate via `isContainer` below, don't assume it's a PID |
| Runtime kind | `GetAll()[name].Runtime` / `…Configuration.Runtime` | `Native` \| `Container` — the sole supervision discriminator |
| Container compose file | `GetAll()[name].ComposeFile` | e.g. `<wd>/<name>.docker-compose.yml`; empty if native |
| Working / install / logs / saves dirs | `GetAll()[name].WorkingDir` / `InstallDir` / `LogsDir` / `SavesDir` | disk-usage targets |
| Ports | `GetAll()[name].Ports` / `…Configuration.Ports` | pipe-separated `26900:26903/tcp\|…` |
| Instance disk usage (snapshot) | `GetAllStatuses()[name].Resources.DiskUsage` | human string (`"16G"`) from `du -sh`; for byte time-series, measure the dirs yourself |

## Per-instance metric anchor, by type

The discriminator is **`isContainer`** = the instance has a non-empty `ComposeFile`
(equivalently `Runtime == Container`). systemd is no longer a lifecycle manager —
every native instance is supervised by kgsm-watchdog under `kgsm.slice`, so there
is no systemd-unit anchor. The `.pid` file is **overloaded** by type (verified from
KGSM source: `templates/manage.container.d/03-lifecycle.sh` checks container
liveness with `docker ps --filter id=$(cat <pid_file>)`):

| Kind | Discriminator | Metric anchor |
|---|---|---|
| **Native** | no `ComposeFile` | `.pid` / `Process.Pid` = a **real host PID** → walk the `/proc` process tree (`stat` / `status` / `io`) for child-inclusive totals. (Watchdog-supervised natives also live in a per-instance cgroup under `/sys/fs/cgroup/kgsm.slice/<name>` — a future, child-inclusive anchor the resolver does not yet key on.) |
| **Container** | has `ComposeFile` | `.pid` holds a **Docker container id** (not a PID) → resolve the container's cgroup / `docker` scope from it |

kgsm-lib deliberately does **not** expose a synthesized "container name" — Docker
derives the running container from the compose project + service, so any string we
invented would be a guess; resolving it from the container-id / compose file is the
monitor's job.

**Verification status:** native live-verified on `7dtd`. The container anchor is
confirmed **from KGSM source**, not exercised on a real container instance here:
`instances.sh:209` → `compose_file=<wd>/<name>.docker-compose.yml`;
`manage.container.d/03-lifecycle.sh` → the `.pid` file carries the container id.
Validate before relying on it.

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
