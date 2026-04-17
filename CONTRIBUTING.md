# Contributing to Mogri

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) with MAUI workload (`dotnet workload install maui`)
- [Git LFS](https://git-lfs.github.com/) — required to fetch the ONNX model files bundled with the app

After cloning, run:

```sh
git lfs install
git lfs pull
```

## Project Structure & Architecture

See [Docs/Architecture.md](Docs/Architecture.md) for a full breakdown of the MVVM structure, DI patterns, key libraries, and branching strategy.

## Coding Conventions

Follow the patterns in [Docs/Architecture.md](Docs/Architecture.md#naming). The short version:

- **Interface-first**: all Services and ViewModels are exposed via interfaces
- **No business logic in ViewModels**, no view logic in Services
- Naming: `IPascalCase` interfaces, `_camelCase` private fields, `Async` suffix on all async methods
- One file per class/interface/enum
- 0 warnings, 0 errors

## Submitting Changes

1. Branch from `main` using a descriptive name (e.g., `feature/my-feature`, `fix/some-bug`)
2. Open a PR targeting `main`
3. PRs are merged via **Squash & Merge**

## Adding a Backend

See [Docs/HowToAddBackend.md](Docs/HowToAddBackend.md).

## Code of Conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
