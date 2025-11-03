# Contributing to Munition AutoPatcher vC

Thank you for your interest in contributing! This document provides guidelines for contributing to this project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)

## Code of Conduct

This project follows standard open source community guidelines:
- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Assume good intentions

## Getting Started

### Prerequisites

- Windows 10/11 (WPF requirement)
- .NET 8.0 SDK or later
- Visual Studio 2022 or VS Code (recommended)
- Git for version control
- Fallout 4 installed (for testing)

### Development Setup

1. **Fork and Clone**
   ```bash
   git clone https://github.com/your-username/Munition_AutoPatcher_vC.git
   cd Munition_AutoPatcher_vC
   ```

2. **Restore Dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the Solution**
   ```bash
   dotnet build MunitionAutoPatcher.sln -c Debug
   ```

4. **Run Tests**
   ```bash
   dotnet test tests/AutoTests/AutoTests.csproj
   ```

5. **Run the Application**
   ```bash
   dotnet run --project MunitionAutoPatcher/MunitionAutoPatcher.csproj
   ```

## How to Contribute

### Types of Contributions

We welcome various types of contributions:

- **Bug Fixes**: Fix existing issues
- **New Features**: Add new functionality
- **Documentation**: Improve docs, comments, or README
- **Tests**: Add or improve test coverage
- **Refactoring**: Improve code quality without changing functionality
- **Performance**: Optimize existing code

### Workflow

1. **Check Existing Issues**: Look for existing issues or create a new one
2. **Create a Branch**: `git checkout -b feature/your-feature-name` or `git checkout -b fix/issue-number`
3. **Make Changes**: Implement your changes following our coding standards
4. **Test**: Ensure all tests pass and add new tests if needed
5. **Commit**: Use clear, descriptive commit messages
6. **Push**: Push your changes to your fork
7. **Pull Request**: Create a PR with a clear description

## Coding Standards

### C# Style Guidelines

- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Enable nullable reference types
- Use async/await for I/O operations
- Prefer LINQ for collections when readable

### MVVM Pattern

- **Models**: Pure data classes, no business logic
  - Located in `Models/`
  - Implement `INotifyPropertyChanged` if needed
  
- **ViewModels**: Presentation logic
  - Inherit from `ViewModelBase`
  - Use `RelayCommand` or `AsyncRelayCommand` for commands
  - Inject dependencies via constructor
  
- **Views**: UI only
  - XAML files in `Views/`
  - Minimal code-behind
  - DataContext bound to ViewModel

### Dependency Injection

- Define interfaces in `Services/Interfaces/`
- Implement in `Services/Implementations/`
- Register in `App.xaml.cs` using `ServiceCollection`
- Use constructor injection

### Code Organization

```
MunitionAutoPatcher/
├── Models/           # Data models
├── ViewModels/       # MVVM ViewModels
├── Views/            # XAML views
├── Services/         # Business logic
│   ├── Interfaces/
│   └── Implementations/
├── Commands/         # Command handlers
├── Converters/       # Value converters
└── Utilities/        # Helper utilities
```

### Naming Conventions

- **Classes**: PascalCase (e.g., `WeaponData`)
- **Methods**: PascalCase (e.g., `ExtractWeaponData()`)
- **Properties**: PascalCase (e.g., `AmmoType`)
- **Private fields**: _camelCase (e.g., `_weaponsService`)
- **Local variables**: camelCase (e.g., `weaponCount`)
- **Interfaces**: IPascalCase (e.g., `IWeaponsService`)

### Comments

- Use XML documentation comments for public APIs:
  ```csharp
  /// <summary>
  /// Extracts weapon data from the specified plugin.
  /// </summary>
  /// <param name="pluginPath">Path to the plugin file.</param>
  /// <returns>A collection of weapon data.</returns>
  public async Task<IEnumerable<WeaponData>> ExtractWeaponDataAsync(string pluginPath)
  ```

- Add inline comments only when code intent isn't clear
- Keep comments up-to-date with code changes

## Testing Guidelines

### Test Structure

- Use xUnit framework
- Follow Arrange-Act-Assert (AAA) pattern
- One assertion per test (when practical)

### Test Naming

Format: `MethodName_Scenario_ExpectedBehavior`

Examples:
```csharp
[Fact]
public void ExtractWeaponData_ValidPlugin_ReturnsWeaponList()

[Fact]
public void GenerateIni_EmptyMapping_ThrowsArgumentException()

[Theory]
[InlineData("TestPlugin.esp", 10)]
public void LoadPlugin_ValidFile_ReturnsExpectedCount(string plugin, int expected)
```

### Test Organization

```
tests/
├── AutoTests/                  # General tests
├── LinkCacheHelperTests/       # LinkCache specific
└── WeaponDataExtractorTests/   # Extractor tests
```

### Mocking

- Mock external dependencies (file system, Mutagen)
- Use interfaces for testability
- Example:
  ```csharp
  var mockService = new Mock<IWeaponsService>();
  mockService.Setup(s => s.GetWeaponsAsync())
             .ReturnsAsync(expectedWeapons);
  ```

### Coverage

- Aim for >70% code coverage for new features
- Cover edge cases and error paths
- Test null inputs, empty collections, etc.

## Pull Request Process

### Before Submitting

1. **Build Succeeds**: `dotnet build -c Release`
2. **All Tests Pass**: `dotnet test`
3. **Code is Formatted**: `dotnet format` (if configured)
4. **Documentation Updated**: Update README, comments, or docs if needed
5. **No Debug Code**: Remove console writes, commented code, etc.

### PR Description

Use the PR template and include:
- Clear description of changes
- Related issue number
- Type of change (bug fix, feature, etc.)
- Testing performed
- Screenshots for UI changes

### Review Process

1. **Automated Checks**: CI builds and tests must pass
2. **Code Review**: At least one maintainer reviews
3. **Changes Requested**: Address feedback and update PR
4. **Approval**: Once approved, maintainers will merge

### Commit Messages

Write clear, descriptive commit messages:

```
Add weapon filtering by ammo type

- Implement filtering logic in WeaponsService
- Add unit tests for filter functionality
- Update UI to display filter options

Fixes #123
```

Guidelines:
- First line: brief summary (50 chars or less)
- Blank line
- Detailed explanation if needed
- Reference issues with `Fixes #123` or `Related to #456`

## Issue Reporting

### Before Creating an Issue

1. **Search Existing Issues**: Check if it's already reported
2. **Verify the Issue**: Reproduce on latest version
3. **Gather Information**: Collect error messages, logs, screenshots

### Issue Templates

Use the appropriate template:
- **Bug Report**: For bugs and errors
- **Feature Request**: For new functionality
- **Configuration Issue**: For setup/build problems

### Good Issue Qualities

- Clear, descriptive title
- Detailed description
- Steps to reproduce (for bugs)
- Expected vs actual behavior
- Environment details (OS, .NET version, etc.)
- Screenshots or error logs

## Questions?

- Check the [README](README.md) for documentation
- Review [ARCHITECTURE.md](ARCHITECTURE.md) for design details
- Check [DECISIONS.md](DECISIONS.md) for past design decisions
- Open a discussion issue for questions

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (see LICENSE file).

---

Thank you for contributing to Munition AutoPatcher vC! 🎮
