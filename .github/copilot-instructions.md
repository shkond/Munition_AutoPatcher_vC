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