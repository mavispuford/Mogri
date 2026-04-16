---
applyTo: "**/*Tests*/**"
---

# Test Rules

- Follow the AAA pattern with `// Arrange`, `// Act`, `// Assert` section comments
- Name tests: `MethodName_Scenario_ExpectedResult` (e.g., `Parse_WithLoraHashes_ExtractsLoraNames`)
- Do not share mutable state between tests — no shared fields or properties; all data must be self-contained within each test method
- Use `[Fact]` for single scenarios, `[Theory]` with `[InlineData]` for parameterized variations
- Constructor `readonly` fields (SUT, mocks) are OK — xUnit creates a new class instance per test so they are isolated. Never mutate these fields from test methods.
- For stateful SUTs (ViewModels, stateful services), create the SUT locally in each test — different tests need different preconditions. Use a `static` factory helper to reduce duplication.
- Use `IDisposable` for cleanup — no `[SetUp]` or `[TearDown]`
- Test behavior through public interfaces — don't test private methods directly
- Keep tests declarative — no `if`, `switch`, or loops in test methods
- Each test must be independent and safe to run in parallel
- Mock only direct dependencies of the system under test
- Use `static` helper methods (e.g., `CreateBackend()`, `CreateViewModel()`) for repetitive object creation instead of shared fields
