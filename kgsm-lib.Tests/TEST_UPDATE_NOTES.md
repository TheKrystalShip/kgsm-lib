# Test Updates Required After Command Executor Refactoring

## Overview
The BlueprintService and InstanceService classes have been refactored to use the new `IKgsmCommandExecutor` interface instead of directly using `IProcessRunner`. This significantly reduces boilerplate code and centralizes JSON deserialization logic.

## Test Files Requiring Updates

### 1. BlueprintServiceTests.cs
**Status**: Partially updated
**Changes Required**:
- ✅ Constructor tests updated to use `IKgsmCommandExecutor`
- ✅ First three GetAll tests updated 
- ⚠️ Remaining tests need similar updates:
  - `GetAll_EmptyJson_ReturnsEmptyDictionary`
  - `GetAll_MultipleBlueprints_ReturnsAllBlueprints`
  - `List_*` tests
  - `ListDefault_*` tests
  - `ListCustom_*` tests
  - `ListDetailed_*` tests
  - `GetInfo_*` tests
  - `FindPath_*` tests

**Pattern to Follow**:
```csharp
// OLD:
_mockProcessRunner
    .Setup(x => x.Execute(KgsmPath, "blueprints", "list", "--json"))
    .Returns(new ProcessResult(0, jsonResponse, string.Empty));

// NEW:
_mockCommandExecutor
    .Setup(x => x.ExecuteForJson<List<string>>(
        It.Is<string[]>(args => args.SequenceEqual(new[] { "blueprints", "list", "--json" })),
        It.IsAny<Action<JsonSerializerOptions>?>(),
        It.IsAny<List<string>?>()))
    .Returns(expectedResult);
```

For methods that return `KgsmResult`, use:
```csharp
_mockCommandExecutor
    .Setup(x => x.Execute(It.Is<string[]>(args => args.SequenceEqual(expectedArgs))))
    .Returns(new KgsmResult(0, "output", ""));
```

### 2. InstanceServiceTests.cs
**Status**: Not updated
**Changes Required**:
- Update constructor to accept `IKgsmCommandExecutor`, `IProcessRunner`, `string kgsmPath`, `ILogger`
- Update all test methods that mock `_mockProcessRunner.Execute()` to instead mock `_mockCommandExecutor`
- Methods returning typed objects (Instance, InstanceRuntimeStatus) → use `ExecuteForJson<T>()`
- Methods returning KgsmResult → use `Execute()`
- Methods using async → use `ExecuteAsync()` or `ExecuteForJsonAsync<T>()`

### 3. KgsmClientTests.cs
**Status**: Unknown - needs investigation
**Potential Impact**: Low (KgsmClient uses services, not ProcessRunner directly)

### 4. ProcessRunnerTests.cs
**Status**: No changes needed
**Reason**: ProcessRunner itself hasn't changed, only how it's used

## Migration Checklist

For each test method:

1. ✅ Identify what the test is mocking (`ProcessRunner.Execute` or `ProcessRunner.ExecuteAsync`)
2. ✅ Determine return type:
   - JSON objects → `ExecuteForJson<T>()`
   - KgsmResult/raw output → `Execute()`
   - Async JSON → `ExecuteForJsonAsync<T>()`
   - Async raw → `ExecuteAsync()`
3. ✅ Update mock setup to use `IKgsmCommandExecutor`
4. ✅ Adjust assertion logic if needed (command executor handles logging internally)

## Benefits of This Refactoring

1. **Reduced Boilerplate**: Test methods become simpler as JSON deserialization is handled internally
2. **Better Type Safety**: Generic methods ensure compile-time type checking
3. **Centralized Logic**: All process execution, validation, and deserialization in one place
4. **Easier Maintenance**: Changes to command execution pattern only need updates in one class

## Next Steps

1. Complete BlueprintServiceTests.cs updates
2. Update InstanceServiceTests.cs
3. Run full test suite to ensure coverage
4. Update integration tests if needed
