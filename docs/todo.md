# Versioning & DevOps — Implementation Checklist

## Phase 1: MSBuild Version Injection in .csproj
- [x] Replace hardcoded `ApplicationDisplayVersion` with conditional default (`1.0.0-local`)
- [x] Replace hardcoded `ApplicationVersion` with conditional default (`1`)
- [x] Verify `dotnet build` succeeds with no extra args
- [x] Verify `-p:` overrides work correctly
- [x] Verify 0 warnings

## Phase 2: GitHub Actions Version Injection
- [x] Add "Determine Version" step to `android-release.yml` (PowerShell syntax)
- [x] Add "Determine Version" step to `ios-unsigned-release.yml` (bash syntax)
- [x] Append `-p:ApplicationDisplayVersion` and `-p:ApplicationVersion` to Android `dotnet publish`
- [x] Append `-p:ApplicationDisplayVersion` and `-p:ApplicationVersion` to iOS `dotnet publish`
- [x] Update artifact names to include version
- [x] Verify tag-triggered version extraction logic
- [x] Verify manual trigger uses `0.0.0-dev`

## Phase 3: About Page Version Display
- [x] Add `AppVersion` property to `IAboutPageViewModel`
- [x] Add `CopyVersionToClipboardCommand` to `IAboutPageViewModel`
- [x] Implement `AppVersion` property in `AboutPageViewModel` using `AppInfo.Current`
- [x] Implement `CopyVersionToClipboard` command with clipboard + toast
- [x] Add version `Label` to `AboutPage.xaml` below logo, right-aligned
- [x] Verify version format: `v{VersionString} ({BuildString})`
- [x] Verify tap-to-copy works
- [x] Verify toast displays
- [x] Verify no new NuGet packages needed
- [x] Verify 0 warnings

## Phase 4: Documentation Updates
- [x] Add "Branching & Releases" section to `docs/Architecture.md`
- [x] Add version-reporting FAQ entry to `README.md`
- [x] Add changelog entry to `docs/Changelog.md`
- [x] Verify no existing content was altered

## Notes
<!-- Use this space for implementation notes, blockers, or decisions made during implementation -->
