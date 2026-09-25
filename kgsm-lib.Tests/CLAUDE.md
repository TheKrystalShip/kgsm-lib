# Tests

xUnit (v2), run with `dotnet test kgsm-lib.sln`. Unit tests mock the collaborator the class under test
actually depends on: service tests mock `IKgsmCommandExecutor` (and `ILifecycleService` for the
operational verbs InstanceService forwards), `EventService` tests mock `IEventSource` and raise its
`EventReceived` event to drive the full wire→dispatch route.

`EventJournalReaderTests` runs the **real** reader against a temporary directory — the journal is
ordinary files, so its whole contract is unit-testable: start position, whole-line framing, segment
rolling, cursor resume, and gap reporting. It writes segments the way the engine does, one complete
line per append.

**Process-bound classes are intentionally not in the unit suite** — `LogSubscriptionService` spawns a
real `kgsm --follow` `Process`, needs a live KGSM, and belongs in an integration category rather than
here. Its one unit-testable dependency, `LogParser`, is covered (`Utilities/LogParserTests.cs`).
