# JSON deserialization (source-generated — AOT/trim-safe)

The library is `IsAotCompatible` and **must stay reflection-free**: never call a reflection-based
`JsonSerializer.Deserialize<T>(json, options)` overload (it emits IL2026/IL3050 and breaks under Native
AOT). All deserialization flows through the System.Text.Json **source generator** in
`KgsmJsonContext.cs`.

- **Registering a new type:** add `[JsonSerializable(typeof(YourType))]` to `KgsmJsonContext`. An
  unregistered type throws `NotSupportedException` at runtime (there is no reflection fallback).
  `KgsmCommandExecutor.ExecuteForJson<T>` resolves the contract via
  `KgsmJson.ExecutorOptions.GetTypeInfo(typeof(T))`.
- **KGSM's unconventional scalars** are handled by hand-written `JsonConverter<T>`s (all AOT-safe, in
  `../Converters/`): `JsonStringToBoolConverter` ("0"/"1"/"active" → bool) and
  `JsonStringToIntConverter` ("123" → int) are registered globally on `KgsmJson.ExecutorOptions`;
  `JsonRecentLogsConverter` (string-or-`[]`) is applied per-property. KGSM emits some `Instance`
  bools/ints as strings, so the global converters are load-bearing, not optional.
- **Enums** are string-valued on the wire and decorated at the type with
  `[JsonConverter(typeof(JsonStringEnumConverter<TEnum>))]` (the generic, AOT-safe converter).
  Read-matching is case-insensitive, so KGSM's lowercase `"systemd"` binds to
  `LifecycleManager.Systemd`. This applies on both the executor and event paths — do not rely on
  options-level enum converters.
- **KGSM may return empty strings for missing fields** — always null-coalesce: `?? new()`.

`InstanceStatusDeserializationTests` holds the wire-shape coverage. `SystemService.GetInfo<T>()` is the
one consumer-open generic — it only works for types registered in the context; pass a `JsonTypeInfo<T>`
if you need arbitrary `T` under AOT.
