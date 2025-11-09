# Copilot Coding Agent Instructions

This file provides instructions and context for GitHub Copilot Coding Agent when working with this repository.

## Project Overview

**Munition AutoPatcher vC** is a WPF desktop application for Fallout 4 modding. It automatically generates RobCo Patcher configuration files (INI) for weapon mods by extracting weapon data from plugins and mapping ammunition.

### Technology Stack
- **.NET 8.0** (Windows-only, WPF)
- **MVVM Pattern** with dependency injection (Microsoft.Extensions.DependencyInjection)
- **Mutagen.Bethesda.Fallout4** (v0.51.5) for plugin parsing
- **xUnit** for testing

### Key Directories
- `MunitionAutoPatcher/` - Main WPF application
  - `Models/` - Data models (WeaponData, AmmoData, ExtractionContext, ConfirmationContext, etc.)
  - `ViewModels/` - MVVM view models
  - `Views/` - XAML views
  - `Services/` - Business logic (orchestrator pattern with specialized services)
    - `Interfaces/` - Service abstractions (ICandidateProvider, ICandidateConfirmer, IDiagnosticWriter, etc.)
    - `Implementations/` - Service implementations (WeaponOmodExtractor orchestrator, providers, confirmers)
    - `Helpers/` - Utility helpers
- `tests/` - Unit and integration tests
  - `AutoTests/` - General tests
  - `LinkCacheHelperTests/` - LinkCache-specific tests
  - `WeaponDataExtractorTests/` - Weapon extractor tests

### Architecture Pattern: Orchestrator with Strategy
- **WeaponOmodExtractor** - Thin orchestrator (520 lines) that delegates to specialized services
- **Candidate Providers** - Strategy pattern implementations (ICandidateProvider)
  - `CobjCandidateProvider` - Extracts candidates from COBJ records
  - `ReverseReferenceCandidateProvider` - Discovers candidates via reflection scanning
- **Candidate Confirmer** - Validates candidates (ICandidateConfirmer)
  - `ReverseMapConfirmer` - Confirms via reverse-reference analysis
- **Supporting Services**
  - `DiagnosticWriter` - All diagnostic file I/O (markers, CSVs, reports)
  - `MutagenAccessor` - Abstraction for Mutagen API with error handling
  - `PathService` - Repository and artifact path resolution

## Build and Test Commands

### Build
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build MunitionAutoPatcher.sln -c Release

# Run application
dotnet run --project MunitionAutoPatcher/MunitionAutoPatcher.csproj
```

### Test
```bash
# Run all tests
dotnet test tests/AutoTests/AutoTests.csproj -c Release --verbosity normal

# Run specific test project
dotnet test tests/LinkCacheHelperTests/LinkCacheHelperTests.csproj
```

### Format
```bash
# Format code (if needed)
dotnet format
```

## Code Conventions

### C# Style
- Use C# 12 features with .NET 8.0
- Enable nullable reference types
- Follow Microsoft C# coding conventions
- Use `async`/`await` for I/O operations

### MVVM Pattern
- **Models**: Pure data classes in `Models/`
- **ViewModels**: Inherit from `ViewModelBase`, implement `INotifyPropertyChanged`
- **Views**: XAML files with minimal code-behind
- Use `RelayCommand` and `AsyncRelayCommand` for commands

### Dependency Injection
- Register services in `App.xaml.cs` using `ServiceCollection`
- Use constructor injection for dependencies
- Interface-based design (e.g., `IWeaponsService`, `IConfigService`)

### Testing
- Use xUnit for all tests
- Follow Arrange-Act-Assert pattern
- Mock external dependencies (file system, Mutagen APIs)
- Test names: `MethodName_Scenario_ExpectedBehavior`

## CI/CD Guidance

⚠️ **Important**: This is a Windows-only WPF application.

### GitHub Actions
- Use `runs-on: windows-latest` for all jobs
- GUI-dependent tests may fail in headless environments
- Example workflow:

```yaml
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build
```

## File Structure Best Practices

### Configuration Files
- `config/config.json` - Runtime configuration (gitignored, environment-specific)
- Avoid committing absolute paths or credentials

### Build Artifacts
The following directories are gitignored:
- `bin/`, `obj/` - Build outputs
- `artifacts/` - Generated test files
- `.vs/`, `.serena/` - IDE/tool-specific

### Documentation
- `README.md` - Main documentation
- `ARCHITECTURE.md` - Architecture details
- `DECISIONS.md` - Design decisions log
- `CONTRIBUTING.md` - Contribution guidelines

## Security Considerations

- **Never commit** API keys, tokens, or credentials
- **Never commit** environment-specific paths (e.g., `E:\Munition_AutoPatcher_vC`)
- Validate user inputs in ViewModels before passing to Services
- Handle Mutagen exceptions gracefully (plugin parsing can fail)

## Common Tasks for Copilot

### Adding a New Service
1. Create interface in `Services/Interfaces/`
2. Implement in `Services/Implementations/`
3. Register in `App.xaml.cs` DI container
4. Inject into constructors via DI (use interfaces, not concrete types)

### Adding a Strategy Provider (e.g., new ICandidateProvider)
1. Create implementation in `Services/Implementations/`
2. Implement `ICandidateProvider` interface with `ProvideCandidatesAsync()`
3. Register as `services.AddSingleton<ICandidateProvider, YourProvider>()` in `App.xaml.cs`
4. WeaponOmodExtractor automatically uses all registered providers via `IEnumerable<ICandidateProvider>`

### Adding a New View
1. Create XAML in `Views/`
2. Create ViewModel in `ViewModels/` inheriting `ViewModelBase`
3. Register ViewModel in DI container
4. Set DataContext in XAML or code-behind

### Adding Tests
1. Create test class in appropriate test project
2. Use `[Fact]` or `[Theory]` attributes
3. Mock dependencies using interfaces
4. Follow existing test patterns in the project

### Working with WeaponOmodExtractor Architecture
- **Do NOT** add business logic to WeaponOmodExtractor - keep it as a thin orchestrator
- **Extract providers** - Create new ICandidateProvider implementations for extraction logic
- **Confirmation logic** - Implement ICandidateConfirmer for validation logic
- **Diagnostic output** - Use IDiagnosticWriter for all file I/O (CSVs, markers, reports)
- **Mutagen access** - Use IMutagenAccessor abstraction instead of direct Mutagen calls
- **Logging** - Use ILogger<T> injected via DI, not static AppLogger

### Working with Mutagen
- Use `ILinkCache` for efficient record lookups
- Handle `ModKey` and `FormKey` carefully
- Check for null/missing records
- Use `WinningOverrides` for conflict resolution
- Prefer IMutagenAccessor abstraction for testability

## References

- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [WPF Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [Mutagen Documentation](https://github.com/Mutagen-Modding/Mutagen)
- See `README.md` for more details
- See `REFACTORING_SUMMARY.md` for WeaponOmodExtractor architecture details

## Mutagen API Reference (generated)

A generated API reference for the `Mutagen` libraries is available in the repository for quick offline browsing and for tools (such as GitHub Copilot) to index XML documentation.

- Location (generated outputs): `mutagen-tmp/docs/api_site/` (static HTML site) and `mutagen-tmp/docs/api_xml/` (raw XML documentation files).
- MkDocs integration: a top-level navigation entry `API Reference` has been added to `mutagen-tmp/mkdocs.yml` that links to `docs/api_site/index.html` so the generated site is accessible from the main site navigation when browsing locally.
- Viewing locally: open the generated HTML index in your browser, for example (PowerShell):

```powershell
ii E:\Munition_AutoPatcher_vC\mutagen-tmp\docs\api_site\index.html
```

- Regenerating the docs (summary):
  1. Build the Mutagen assemblies with XML docs enabled (example uses the `Mutagen.Bethesda` project):

```powershell
dotnet build mutagen-tmp/Mutagen.Bethesda/Mutagen.Bethesda.csproj -c Release
```

  2. Run DocFX metadata and build pointing at the produced DLLs and XML files. DocFX may be run from an extracted binary (`tools/docfx/docfx.exe`) or via your preferred DocFX installation. Example (if `docfx.exe` is available):

```powershell
& .\mutagen-tmp\tools\docfx\docfx.exe metadata docfx.json
& .\mutagen-tmp\tools\docfx\docfx.exe build docfx.json
```

Notes:
- The `mutagen-tmp/` directory is intended as a temporary local clone used to generate API docs; it is ignored by `.gitignore` by default. If you want to commit the generated HTML site into the repository, remove or adjust the `.gitignore` entry and commit the files.
- DocFX may emit warnings about unresolved external references; supplying referenced dependency assemblies to the metadata step reduces those warnings.
- If you want CI to publish the API site automatically (e.g., to `gh-pages`), add a GitHub Actions workflow that performs the build and DocFX steps on the runner and publishes the `docs/api_site` output.

Note: This project uses the Mutagen API (for example `Mutagen.Bethesda.Fallout4`). A local copy of the Mutagen source and generated API documentation is kept under `mutagen-tmp/` for offline reference (see `mutagen-tmp/docs/api_site/` and `mutagen-tmp/docs/api_xml/` when generated). The `mutagen-tmp/` directory is intended as a temporary local clone and may be ignored by `.gitignore`; generated docs are not tracked in the remote repository unless explicitly committed.

## Serena MCP Server

This repository is used with a Serena MCP Server integration to provide IDE-assist tools (onboarding, project memories, and symbolic editing helpers). The following notes explain how to activate the project and use the MCP tools for onboarding and memory management.

- Activate the project in Serena (example project name in this repo): `Munition_AutoPatcher_vC`.
  - Tool: `mcp_serena_activate_project` (provides project context to the MCP tools).
- Check whether onboarding was already performed:
  - Tool: `mcp_serena_check_onboarding_performed` (returns whether onboarding exists and which memories are present).
- Useful memory tools:
  - `mcp_serena_read_memory` — read an onboarding or project memory by name when you need context.
  - `mcp_serena_write_memory` — persist short onboarding notes or new onboarding steps for future sessions.
  - `mcp_serena_delete_memory` — remove stale onboarding/memory files when they are no longer needed.

- Recommended workflow for updating onboarding with new docs (like the Mutagen API reference):
  1. Activate the project with `mcp_serena_activate_project`.
  2. Run `mcp_serena_check_onboarding_performed` to see existing onboarding and memory names.
  3. Use `mcp_serena_read_memory` to inspect relevant memories (for example `project_overview`, `code_conventions`, `suggested_commands`).
  4. Create a new memory summarizing the change (example: `mutagen_api_reference_onboarding`) with `mcp_serena_write_memory` and include regeneration steps, paths, and CI recommendations.

- Additional MCP tools helpful during development:
  - `manage_todo_list` — track multi-step tasks and progress inside the MCP tooling.
  - `apply_patch` — use to edit repository files programmatically (preferred for deterministic edits).
  - `file_search`, `read_file`, `create_file` — explore and modify repository contents safely.

Notes:
- The list of available memories may change; always run `mcp_serena_check_onboarding_performed` after activating the project to get the current set.
- Keep onboarding memories concise and actionable — include where generated artifacts live, how to regenerate them, and any `.gitignore` decisions.