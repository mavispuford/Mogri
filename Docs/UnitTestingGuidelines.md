# Unit Testing Guidelines

This document covers the unit testing conventions for the Mogri project. Tests live in `Mogri.Tests/` and use **xUnit** + **Moq**.

For the concise rule set that Copilot applies automatically, see `.github/instructions/tests.instructions.md`.

---

## Arrange / Act / Assert

Every test method should be organized into three labeled sections:

```csharp
[Fact]
public void Parse_EmptyInput_ReturnsDefaultPromptSettings()
{
    // Arrange
    var input = string.Empty;

    // Act
    var result = ForgeMetadataParser.Parse(input);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(string.Empty, result.Prompt);
}
```

- **Arrange** — set up inputs, create mocks, build the system under test.
- **Act** — call the single method being tested.
- **Assert** — verify the result. Keep assertions focused on one logical outcome.

When a test is trivial (e.g., a one-liner with no setup), the comments can be omitted, but the logical separation should still be clear.

---

## Naming Convention

Use the pattern: **`MethodName_Scenario_ExpectedResult`**

| Example | What it tells you |
|---|---|
| `Parse_NullInput_ReturnsDefaultPromptSettings` | `Parse` with null input returns defaults |
| `GetBackend_UnknownName_ReturnsNull` | `GetBackend` with an unknown name returns null |
| `ConstrainDimensionValue_RoundsUpToNearestMultiple` | Clarifies rounding-up behavior |
| `CalculateAspectRatio_ZeroWidth_ReturnsEmptyResult` | Edge case: zero input |

The name should make the failure obvious without reading the test body.

---

## No Shared Mutable State

**Each test must be entirely self-contained.** Do not use instance fields or properties to share data between tests.

```csharp
// BAD — shared field can leak state between tests
public class MyServiceTests
{
    private readonly List<string> _items = new() { "a", "b" };

    [Fact]
    public void Add_AppendsItem()
    {
        _items.Add("c");
        Assert.Equal(3, _items.Count); // Passes alone, but fragile
    }

    [Fact]
    public void Count_ReturnsTwo()
    {
        Assert.Equal(2, _items.Count); // Could fail if Add test ran first
    }
}
```

```csharp
// GOOD — each test creates its own data
public class MyServiceTests
{
    [Fact]
    public void Add_AppendsItem()
    {
        // Arrange
        var items = new List<string> { "a", "b" };

        // Act
        items.Add("c");

        // Assert
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void Count_ReturnsTwo()
    {
        // Arrange
        var items = new List<string> { "a", "b" };

        // Assert
        Assert.Equal(2, items.Count);
    }
}
```

When multiple tests need the same complex object graph, use a **`static` helper method** rather than a shared field:

```csharp
private static Mock<IImageGenerationBackend> CreateBackend(string name)
{
    var backend = new Mock<IImageGenerationBackend>();
    backend.SetupGet(b => b.Name).Returns(name);
    return backend;
}
```

---

## `[Fact]` vs `[Theory]`

- **`[Fact]`** — a single scenario with a fixed set of inputs.
- **`[Theory]` + `[InlineData]`** — the same logic tested against multiple input/output combinations.

```csharp
// Single scenario
[Fact]
public void Parse_SingleLineOnly_ReturnsNull()
{
    var result = ForgeMetadataParser.Parse("single line only");
    Assert.Null(result);
}

// Multiple input/output variations
[Theory]
[InlineData(12, 8, 4)]
[InlineData(1920, 1080, 120)]
[InlineData(7, 13, 1)]
public void GreatestCommonDivisor_ReturnsExpectedValues(int a, int b, int expected)
{
    var result = MathHelper.GreatestCommonDivisor(a, b);
    Assert.Equal(expected, result);
}
```

Prefer `[Theory]` when you have three or more data variations for the same assertion logic. Use `[Fact]` when the test has unique setup or assertions.

---

## Setup and Cleanup

Use the **class constructor** for shared setup and `IDisposable` for cleanup. Do not use `[SetUp]` or `[TearDown]` attributes (those are NUnit concepts that don't exist in xUnit).

```csharp
public class MyServiceTests : IDisposable
{
    private readonly MyService _sut;

    public MyServiceTests()
    {
        _sut = new MyService();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
```

**How this relates to the no-shared-state rule:** xUnit creates a **new test class instance for every test method**. If you have 5 `[Fact]` methods, 5 separate `MyServiceTests` objects are constructed, each with its own `_sut`. So constructor-initialized fields are not actually shared between tests — they are isolated by design.

This means `readonly` fields assigned in the constructor are safe and don't violate the no-shared-state rule. What *would* violate it is a field that tests mutate (e.g., adding items to a list), since that pattern is confusing and breaks if someone later moves to a framework that reuses instances. The rule of thumb:

- **OK**: `readonly` field set in the constructor (SUT, mocks) — never mutated by tests
- **Not OK**: field that any test method writes to — create it locally instead

### Stateful SUTs: ViewModels and Stateful Services

Constructor setup works well for **stateless or immutable** SUTs (static helpers, parsers, registries) because every test starts from the same clean state.

For **stateful** SUTs like ViewModels, **create the SUT locally in each test method**. ViewModels naturally accumulate state through page interactions — one test might need a freshly constructed ViewModel while another needs one that has already loaded data. Putting a stateful SUT in a constructor field means every test starts from the same state, which either forces tests into a single precondition or tempts you to mutate the shared field.

Use a `static` factory helper to keep the setup concise:

```csharp
public class MainPageViewModelTests
{
    [Fact]
    public async Task LoadAsync_PopulatesModels()
    {
        // Arrange
        var (vm, imageSvc) = CreateViewModel();
        imageSvc.Setup(s => s.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IModelViewModel> { CreateModel("v1") });

        // Act
        await vm.LoadAsync(CancellationToken.None);

        // Assert
        Assert.Single(vm.Models);
    }

    [Fact]
    public void DeleteCommand_NoSelection_IsDisabled()
    {
        // Arrange
        var (vm, _) = CreateViewModel();

        // Assert
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    private static (MainPageViewModel vm, Mock<IImageService> imageSvc) CreateViewModel()
    {
        var imageSvc = new Mock<IImageService>();
        var popupSvc = new Mock<IPopupService>();
        var vm = new MainPageViewModel(imageSvc.Object, popupSvc.Object);
        return (vm, imageSvc);
    }

    private static Mock<IModelViewModel> CreateModel(string name)
    {
        var model = new Mock<IModelViewModel>();
        model.SetupGet(m => m.Name).Returns(name);
        return model;
    }
}
```

**When to use which approach:**

| SUT type | Setup approach | Example |
|---|---|---|
| Stateless / immutable | Constructor `readonly` field | `ForgeMetadataParser`, `BackendRegistry`, `MathHelper` |
| Stateful (ViewModels, stateful services) | Local variable via `static` factory | `MainPageViewModel`, `ImageGenerationService` |

---

## Mocking with Moq

- Mock only the **direct dependencies** of the system under test. Don't mock the SUT itself.
- Use `MockBehavior.Loose` (the default) unless you need strict interaction verification.
- Use `Verify()` sparingly — prefer asserting on return values over verifying that internal methods were called.

```csharp
[Fact]
public void GetAllBackends_ReturnsAllRegistered()
{
    // Arrange
    var forgeBackend = CreateBackend("SD Forge Neo");
    var comfyBackend = CreateBackend("ComfyUI");
    var registry = new BackendRegistry(new List<IImageGenerationBackend>
    {
        forgeBackend.Object,
        comfyBackend.Object
    });

    // Act
    var allBackends = registry.GetAllBackends().ToList();

    // Assert
    Assert.Equal(2, allBackends.Count);
    Assert.Contains(forgeBackend.Object, allBackends);
    Assert.Contains(comfyBackend.Object, allBackends);
}
```

---

## Common Pitfalls

| Pitfall | Guidance |
|---|---|
| Testing private methods | Test through public interfaces. If the private logic is complex, extract it into its own class. |
| Logic in tests | No `if`, `switch`, or loops. Tests should be linear and declarative. |
| Over-mocking | If you need to mock five or more dependencies, the SUT may need refactoring. |
| Asserting too much | Each test should verify one logical behavior. Multiple `Assert` calls are fine if they validate different facets of the same outcome. |
