# Versioning & DevOps Implementation Plan

## Overview

This plan automates application versioning and formalizes the release process for Mogri's transition from private to public repository. It covers four areas:

1. **MSBuild version injection** — Allow CI to set version numbers at build time while keeping safe local defaults.
2. **GitHub Actions updates** — Inject tag-derived versions into the existing Android and iOS workflows.
3. **About Page version display** — Show the version below the logo with tap-to-copy functionality.
4. **Documentation** — Add branching strategy and version-reporting guidance.

### Codebase Observations & Decisions

- **Current versioning**: `ApplicationDisplayVersion` is `1.0` and `ApplicationVersion` is `1` in `Mogri.csproj` — both hardcoded with no conditional logic.
- **Semver migration**: We'll move from `1.0` (2-part) to `1.0.0` (3-part semver). Tags will follow the pattern `v1.0.0`.
- **`ApplicationVersion` must be ≥ 1 on Android** (it maps to `android:versionCode`). Gemini's plan defaulted it to `0`, which would break Android builds. We'll default to `1`.
- **No new NuGet packages needed**: `AppInfo.Current` (MAUI Essentials) provides version info, and `Clipboard.Default` provides clipboard access — both are already available. `CommunityToolkit.Maui` (already referenced) provides `Toast` for copy feedback.
- **No new service needed**: `AppInfo.Current.VersionString` and `AppInfo.Current.BuildString` are simple reads of platform values. Creating a dedicated service would be overengineering per our architecture guidelines. They can be used directly in the ViewModel.
- **Shell syntax**: Android workflow runs PowerShell on `windows-latest`; iOS workflow runs bash on `macos-latest`. The `-p:Property=Value` syntax works in both, but variable expansion differs (`$env:VAR` vs `$VAR`).
- **Workflow triggers**: Both workflows already trigger on `v*` tags and `workflow_dispatch`. The plan does not add PR triggers (Gemini mentioned PR-triggered builds but the repo doesn't currently use them, and adding them is out of scope).
- **Version display format**: `v1.0.0 (42)` where `42` is the build number. On local dev builds the build info will show as `1` (the default `ApplicationVersion`).
- **Clipboard toast**: Use `CommunityToolkit.Maui`'s `Toast.Make(...).Show()` for a brief, non-intrusive "Copied!" message. No alert dialogs needed.

---

## Phase 1: MSBuild Version Injection in .csproj

### Description
Update `Mogri.csproj` to accept version properties from the command line while preserving safe defaults for local development.

### Implementation Prompt

~~~
## Context
Mogri is a .NET MAUI app targeting net10.0-android and net10.0-ios. The `Mogri.csproj` file currently has hardcoded versioning:

```xml
<ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
<ApplicationVersion>1</ApplicationVersion>
```

Read `docs/Architecture.md` for coding standards. Read the existing `Mogri/Mogri.csproj` in full before making changes.

## Objective
Make versioning injectable from CI while keeping safe local defaults.

## Requirements
Replace the two hardcoded version lines in the `<!-- Versions -->` section of `Mogri.csproj` with MSBuild conditional defaults:

```xml
<!-- Versions: overridable from CLI via -p:ApplicationDisplayVersion=x.y.z -->
<ApplicationDisplayVersion Condition="'$(ApplicationDisplayVersion)' == ''">1.0.0-local</ApplicationDisplayVersion>
<ApplicationVersion Condition="'$(ApplicationVersion)' == ''">1</ApplicationVersion>
```

**Key rules:**
- `ApplicationDisplayVersion` defaults to `1.0.0-local` (signals a local dev build in the UI).
- `ApplicationVersion` defaults to `1` (NOT `0` — Android requires versionCode ≥ 1).
- Both can be overridden at build time via `-p:ApplicationDisplayVersion=1.2.3 -p:ApplicationVersion=42`.
- Do not change anything else in the csproj.

## Acceptance Criteria
- [ ] `dotnet build` with no extra args succeeds, and the app shows `1.0.0-local` as its version.
- [ ] `dotnet build -p:ApplicationDisplayVersion=2.5.0 -p:ApplicationVersion=99` overrides both values.
- [ ] No warnings or errors introduced.

## Files to Modify
- `Mogri/Mogri.csproj`
~~~

### Expected Outcomes
- Local builds show `1.0.0-local` as the version string.
- CI can inject any version via MSBuild properties.
- No impact on existing build behavior — builds still produce 0 warnings.

---

## Phase 2: GitHub Actions Version Injection

### Description
Update both existing GitHub Actions workflows to extract version from git tags and inject them into the build via MSBuild properties. Manual triggers get a `dev` suffix.

### Implementation Prompt

~~~
## Context
Phase 1 made `ApplicationDisplayVersion` and `ApplicationVersion` overridable in `Mogri.csproj` via `-p:` properties.

There are two existing workflows:
- `.github/workflows/android-release.yml` — runs on `windows-latest`, uses PowerShell, produces signed APK/AAB.
- `.github/workflows/ios-unsigned-release.yml` — runs on `macos-latest`, uses bash, produces unsigned IPA.

Both trigger on `push.tags: ['v*']` and `workflow_dispatch`.

Read both workflow files in full before making changes. Read `docs/Architecture.md` for project context.

## Objective
Inject version info into both workflows so builds are stamped with the correct version.

## Requirements

### Version extraction step (add to both workflows, after checkout)
Add a step called "Determine Version" that:
1. If triggered by a tag push (`github.ref_type == 'tag'`):
   - Extracts the semver from the tag (e.g., `refs/tags/v1.2.3` → `1.2.3`).
   - Sets `APP_DISPLAY_VERSION` to the extracted version (e.g., `1.2.3`).
2. If triggered manually (`workflow_dispatch`):
   - Sets `APP_DISPLAY_VERSION` to `0.0.0-dev`.
3. Always sets `APP_BUILD_NUMBER` to `${{ github.run_number }}`.

**For the Android workflow (PowerShell/Windows):**
- Use `echo "APP_DISPLAY_VERSION=..." >> $env:GITHUB_ENV` syntax.
- Use `echo "APP_BUILD_NUMBER=..." >> $env:GITHUB_ENV` syntax.

**For the iOS workflow (bash/macOS):**
- Use `echo "APP_DISPLAY_VERSION=..." >> $GITHUB_ENV` syntax.
- Use `echo "APP_BUILD_NUMBER=..." >> $GITHUB_ENV` syntax.

### Publish step updates
Append these properties to the existing `dotnet publish` commands in both workflows:
```
-p:ApplicationDisplayVersion=$APP_DISPLAY_VERSION
-p:ApplicationVersion=$APP_BUILD_NUMBER
```

**Android (PowerShell):** Use `$env:APP_DISPLAY_VERSION` and `$env:APP_BUILD_NUMBER`.
**iOS (bash):** Use `$APP_DISPLAY_VERSION` and `$APP_BUILD_NUMBER`.

### Artifact naming
Update the artifact upload step names to include the version for easier identification:
- Android APK: `Mogri-Android-APK-v${{ env.APP_DISPLAY_VERSION }}`
- Android AAB: `Mogri-Android-AAB-v${{ env.APP_DISPLAY_VERSION }}`
- iOS IPA: `Mogri-iOS-Unsigned-IPA-v${{ env.APP_DISPLAY_VERSION }}`
- iOS Zip: `Mogri-iOS-Payload-Zip-v${{ env.APP_DISPLAY_VERSION }}`

### Important
- Do NOT change the trigger conditions, runner OS, workload installation, keystore decoding, signing properties, or IPA packaging steps.
- Do NOT add PR triggers or any new triggers.
- Do NOT modify the checkout, .NET setup, or workload install steps.
- Preserve the exact existing `dotnet publish` arguments — only append the two new `-p:` properties.
- Make sure the version extraction handles the `refs/tags/v` prefix stripping correctly.

## Acceptance Criteria
- [ ] Tag-triggered builds extract the correct semver and inject it.
- [ ] Manual builds use `0.0.0-dev` with `run_number` as the build number.
- [ ] Android workflow syntax is valid PowerShell.
- [ ] iOS workflow syntax is valid bash.
- [ ] Artifact names include the version.
- [ ] All existing functionality is preserved (signing, IPA packaging, etc.).

## Files to Modify
- `.github/workflows/android-release.yml`
- `.github/workflows/ios-unsigned-release.yml`
~~~

### Expected Outcomes
- Tag `v1.2.3` produces builds with `ApplicationDisplayVersion=1.2.3` and `ApplicationVersion=<run_number>`.
- Manual runs produce builds with `0.0.0-dev` and `ApplicationVersion=<run_number>`.
- Artifacts are named with version for easy identification.

---

## Phase 3: About Page Version Display

### Description
Add a version label below the Mogri logo on the About page. Tapping it copies the version string to the clipboard with a toast confirmation.

### Implementation Prompt

~~~
## Context
Mogri is a .NET MAUI app following strict MVVM. Read `docs/Architecture.md` for conventions.

The About page already exists:
- **View**: `Mogri/Views/AboutPage.xaml` and `AboutPage.xaml.cs`
- **ViewModel**: `Mogri/ViewModels/Pages/AboutPageViewModel.cs`
- **Interface**: `Mogri/Interfaces/ViewModels/Pages/IAboutPageViewModel.cs`
- **Registrations**: Already registered in `ViewModelRegistrations.cs` and `ViewRegistrations.cs`.

The ViewModel extends `PageViewModel` (which extends `BaseViewModel : ObservableObject`) and uses CommunityToolkit.Mvvm source generators (`[RelayCommand]`).

Read all four About page files in full before making changes.

## Objective
Display the app version below the logo, right-aligned. Tapping it copies the version info to the clipboard with a brief toast.

## Requirements

### Interface (`IAboutPageViewModel.cs`)
Add:
```csharp
string AppVersion { get; }
IAsyncRelayCommand CopyVersionToClipboardCommand { get; }
```

### ViewModel (`AboutPageViewModel.cs`)
1. Add a read-only property `AppVersion` that returns the formatted version string.
   - Use `AppInfo.Current.VersionString` for the display version and `AppInfo.Current.BuildString` for the build number.
   - Format: `v{VersionString} ({BuildString})` — e.g., `v1.2.3 (42)`.
   - Decorate with `[ObservableProperty]` is NOT needed since this value never changes at runtime. Use a simple get-only property.
2. Add an async relay command `CopyVersionToClipboard`:
   - Copies `AppVersion` to the clipboard using `await Clipboard.Default.SetTextAsync(AppVersion)`.
   - Shows a toast: `await Toast.Make("Copied to clipboard").Show()`.
   - Add `using CommunityToolkit.Maui.Alerts;` for the Toast class.
3. Do NOT inject any new services. `AppInfo.Current` and `Clipboard.Default` are static MAUI Essentials APIs. `Toast` is from CommunityToolkit.Maui (already a dependency).
4. Do NOT modify the constructor signature.

### View (`AboutPage.xaml`)
Add a version label **immediately after** the `<Image x:Name="LogoImage" ... />` element (before the `<BoxView>`):

```xml
<Label 
    Text="{Binding AppVersion}"
    HorizontalOptions="End"
    Padding="0,0,24,0"
    FontFamily="ComfortaaRegular"
    FontSize="12"
    TextColor="{AppThemeBinding Light={StaticResource NeutralDarkGray}, Dark={StaticResource Secondary}}"
    Opacity="0.6">
    <Label.GestureRecognizers>
        <TapGestureRecognizer Command="{Binding CopyVersionToClipboardCommand}" />
    </Label.GestureRecognizers>
</Label>
```

**Style notes:**
- `HorizontalOptions="End"` right-aligns within the `VerticalStackLayout`.
- `Padding="0,0,24,0"` gives a bit of right margin.
- `FontFamily="ComfortaaRegular"` matches the page's existing font.
- `FontSize="12"` keeps it subtle/secondary.
- `Opacity="0.6"` makes it visually de-emphasized.
- The `TextColor` matches the existing labels on the page.

### Codebehind (`AboutPage.xaml.cs`)
No changes needed.

### Important
- Follow the naming conventions from `Architecture.md` (private methods in `camelCase`, properties in `PascalCase`, etc.)
- Do NOT add XML summary comments unless they add meaningful context.
- Do NOT create a new service for version info.
- Do NOT modify registration files (the ViewModel is already registered as transient).
- Verify that `CommunityToolkit.Maui.Alerts` namespace is available (it should be — `CommunityToolkit.Maui` v14.0.1 is already referenced).

## Acceptance Criteria
- [ ] The version label appears below the logo, right-aligned, on the About page.
- [ ] The version text matches the format `v{VersionString} ({BuildString})`.
- [ ] Tapping the version copies it to the clipboard.
- [ ] A toast "Copied to clipboard" is shown briefly after tap.
- [ ] No new NuGet packages added.
- [ ] No new services or registrations required.
- [ ] The label's style (font, color, opacity) is consistent and subtle.
- [ ] Build succeeds with 0 warnings.

## Files to Modify
- `Mogri/Interfaces/ViewModels/Pages/IAboutPageViewModel.cs`
- `Mogri/ViewModels/Pages/AboutPageViewModel.cs`
- `Mogri/Views/AboutPage.xaml`
~~~

### Expected Outcomes
- About page shows version like `v1.0.0-local (1)` for local builds, or `v1.2.3 (42)` for CI builds.
- Tap-to-copy works on both Android and iOS without additional packages.
- Visual appearance is subtle and right-aligned below the logo.

---

## Phase 4: Documentation Updates

### Description
Add branching strategy to Architecture.md, version-reporting guidance to README.md, and a changelog entry.

### Implementation Prompt

~~~
## Context
Mogri is transitioning from private to public. Read `docs/Architecture.md`, `README.md`, and `docs/Changelog.md` in full before making changes.

## Objective
Document the branching strategy, add version-reporting info for contributors/users, and log the changes.

## Requirements

### Architecture.md
Add a new `## Branching & Releases` section at the end of the file (before any future appendices, but after `## Other Patterns`). Content:

```markdown
## Branching & Releases

This project follows **GitHub Flow**:

1. **Feature branches** are created from `main` for all changes (e.g., `feature/version-display`, `fix/clipboard-crash`)
2. **Pull Requests** target `main` and are merged via **Squash & Merge** to keep history clean
3. **Releases** are created by tagging `main` with a semver tag (e.g., `v1.2.3`)
   - Tags trigger CI builds that produce versioned Android (signed APK/AAB) and iOS (unsigned IPA) artifacts
   - The tag version is injected into the app binary at build time

### Versioning

- **Display version** (`ApplicationDisplayVersion`): Semantic version matching the git tag (e.g., `1.2.3`). Defaults to `1.0.0-local` for local dev builds.
- **Build number** (`ApplicationVersion`): Auto-incremented by CI using `github.run_number`. Defaults to `1` locally.
- Both are overridable via MSBuild properties: `-p:ApplicationDisplayVersion=x.y.z -p:ApplicationVersion=N`
```

### README.md
Add a `### Reporting Issues` subsection inside the existing `### FAQ` section (at the end, after the last FAQ entry):

```markdown
#### How do I find my app version?

Tap the **Mogri logo** on the **About** page — the version is displayed just below it (e.g., `v1.2.3 (42)`). Tap the version text to copy it to your clipboard, then include it in any bug report or issue.
```

### Changelog.md
Add an entry at the top (below the `# Changelog` heading) for today's date. Follow the existing format:

```markdown
## 2026-04-XX (replace XX with actual implementation date)

*This update automates app versioning via CI and exposes the version on the About page.*

### Added
- **Automated Versioning**: App version is now injected at build time by GitHub Actions from git tags. Local builds display `1.0.0-local`.
- **Version Display**: The About page now shows the app version below the logo. Tapping it copies the version to the clipboard.

### Changed
- **GitHub Actions**: Android and iOS workflows now inject `ApplicationDisplayVersion` and `ApplicationVersion` into builds. Artifacts are named with the version.
- **Mogri.csproj**: Version properties are now overridable via MSBuild, with safe local defaults.
```

### Important
- Do NOT modify any existing content in these files — only append/insert.
- Match the existing formatting and heading hierarchy exactly.
- Use the date of actual implementation in the Changelog, not today's planning date.

## Acceptance Criteria
- [ ] Architecture.md has a new "Branching & Releases" section with versioning details.
- [ ] README.md has version-reporting guidance in the FAQ.
- [ ] Changelog.md has a new entry documenting all changes.
- [ ] No existing content is altered.
- [ ] Formatting is consistent with existing docs.

## Files to Modify
- `docs/Architecture.md`
- `README.md`
- `docs/Changelog.md`
~~~

### Expected Outcomes
- Contributors understand the branching and release workflow.
- Users know how to find and report their app version.
- Changes are tracked in the changelog.
