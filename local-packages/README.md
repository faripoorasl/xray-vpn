# Local NuGet Packages

This folder contains pre-downloaded NuGet packages so the build can run **completely offline** — no internet access required.

## Why?

`api.nuget.org` is blocked or unreliable in some regions (Iran, China, etc.).
By bundling the .nupkg files here, the build can succeed without any network access.

## How it works

The `nuget.config` file in the project root tells `dotnet restore` to look in this folder FIRST, and only fall back to `api.nuget.org` if a package isn't found locally.

## Files

| Package | Version | Purpose |
|---------|---------|---------|
| `communitytoolkit.mvvm.8.2.2.nupkg` | 8.2.2 | MVVM framework (ObservableObject, RelayCommand) |
| `newtonsoft.json.13.0.3.nupkg` | 13.0.3 | JSON parsing (used by ConfigParserService) |
| `microsoft.bcl.asyncinterfaces.7.0.0.nupkg` | 7.0.0 | Required by CommunityToolkit.Mvvm |
| `system.componentmodel.annotations.5.0.0.nupkg` | 5.0.0 | Required by CommunityToolkit.Mvvm |
| `system.memory.4.5.5.nupkg` | 4.5.5 | Required by CommunityToolkit.Mvvm |
| `system.numerics.vectors.4.5.0.nupkg` | 4.5.0 | Required by CommunityToolkit.Mvvm |
| `system.runtime.compilerservices.unsafe.6.0.0.nupkg` | 6.0.0 | Required by CommunityToolkit.Mvvm |
| `system.threading.tasks.extensions.4.5.4.nupkg` | 4.5.4 | Required by CommunityToolkit.Mvvm |

## Updating packages

If you need to add or update a package:

1. Download the .nupkg file from https://www.nuget.org/packages/<PackageName>
2. Rename it to lowercase: `<packagename>.<version>.nupkg`
   - Example: `CommunityToolkit.Mvvm` → `communitytoolkit.mvvm.8.2.2.nupkg`
3. Place it in this folder
4. The next `dotnet restore` will pick it up automatically

## Notes

- NuGet package filenames must be lowercase (NuGet convention)
- Version must match exactly what's referenced in `.csproj`
- The `.nupkg` file is just a ZIP archive with a different extension
