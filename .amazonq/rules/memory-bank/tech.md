# Munition AutoPatcher vC - Technology Stack

## Core Technologies

### .NET Platform
- **.NET 8.0**: Target framework with Windows-specific features
- **C# 12**: Latest language features with nullable reference types enabled
- **Windows Presentation Foundation (WPF)**: Desktop UI framework
- **Implicit Usings**: Enabled for cleaner code organization

### Key Dependencies
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Mutagen.Bethesda.Fallout4" Version="0.51.5" />
```

### Mutagen Integration
- **Mutagen.Bethesda.Fallout4 0.51.5**: Bethesda plugin manipulation library
- **GameEnvironment**: Automatic game detection and plugin loading
- **LinkCache**: Efficient record resolution and caching
- **WinningOverrides**: Conflict resolution for overlapping modifications
- **FormKey System**: Safe record identification and resolution

## Development Environment

### Prerequisites
- **.NET 8.0 SDK** or later
- **Windows 10/11** (WPF requirement)
- **Visual Studio 2022** or **Visual Studio Code** (recommended)
- **Fallout 4** installation (for runtime functionality)

### Project Configuration
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <Nullable>enable</Nullable>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
  <ImplicitUsings>enable</ImplicitUsings>
  <UseWPF>true</UseWPF>
  <EnableWindowsTargeting>true</EnableWindowsTargeting>
</PropertyGroup>
```

## Build System

### Development Commands
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run application
dotnet run --project MunitionAutoPatcher/MunitionAutoPatcher.csproj

# Run tests
dotnet test

# Build release
dotnet build -c Release

# Publish application
dotnet publish MunitionAutoPatcher/MunitionAutoPatcher.csproj -c Release -r win-x64 --self-contained false
```

### Solution Structure
```
MunitionAutoPatcher.sln
├── MunitionAutoPatcher.csproj    # Main WPF application
├── tests/AutoTests.csproj        # Integration tests
├── tests/LinkCacheHelperTests.csproj  # Unit tests
└── tests/WeaponDataExtractorTests.csproj  # Specialized tests
```

## Architecture Frameworks

### MVVM Pattern
- **ViewModelBase**: Base class with `INotifyPropertyChanged` implementation
- **RelayCommand**: Synchronous command implementation
- **AsyncRelayCommand**: Asynchronous command implementation with cancellation support
- **Data Binding**: Two-way binding between Views and ViewModels

### Dependency Injection
- **Microsoft.Extensions.DependencyInjection**: Service container
- **Microsoft.Extensions.Hosting**: Application hosting and lifecycle management
- **Service Lifetimes**: Singleton for stateful services, Transient for stateless operations
- **Interface-based Design**: All services implement contracts

### Logging Infrastructure
- **Microsoft.Extensions.Logging**: Structured logging framework
- **AppLogger**: Custom application logger with UI integration
- **AppLoggerProvider**: Custom logging provider for UI display
- **File Logging**: Persistent log files in artifacts directory

## Data Management

### Configuration System
- **JSON Configuration**: `config/config.json` for runtime settings
- **ConfigService**: Centralized configuration management
- **Type-safe Configuration**: Strongly-typed configuration models
- **Environment-specific Settings**: Sample configuration templates

### Data Models
- **Record Types**: Immutable data structures where appropriate
- **Nullable Reference Types**: Enabled for null safety
- **Value Objects**: FormKey, AmmoData, WeaponData as value types
- **Entity Models**: WeaponMapping, StrategyConfig as entities

## UI Technology

### WPF Features
- **XAML Markup**: Declarative UI definition
- **Data Binding**: Automatic UI updates via property change notification
- **Value Converters**: Custom converters for data presentation
- **Styles and Templates**: Consistent visual design
- **User Controls**: Reusable UI components

### User Experience
- **Asynchronous Operations**: Non-blocking UI during long operations
- **Progress Reporting**: Visual feedback for lengthy processes
- **Error Handling**: User-friendly error messages and recovery
- **Real-time Logging**: Live log display in UI

## Testing Framework

### Test Technologies
- **MSTest**: Primary testing framework
- **xUnit**: Alternative testing framework for some test projects
- **Moq**: Mocking framework for unit tests
- **FluentAssertions**: Expressive assertion library

### Test Categories
- **Unit Tests**: Individual component testing
- **Integration Tests**: Service interaction testing
- **End-to-End Tests**: Complete workflow validation
- **Performance Tests**: Load and stress testing

## File System Integration

### Input/Output
- **Plugin Reading**: Bethesda .esp/.esm/.esl file parsing via Mutagen
- **ESP Generation**: ESL-flagged ESP patch creation
- **INI Generation**: RobCo Patcher configuration file creation
- **JSON Serialization**: Configuration and data persistence
- **CSV Export**: Diagnostic data export

### Directory Structure
```
artifacts/                    # Generated outputs
├── MunitionAutoPatcher_Patch.esp  # ESP patches
├── munition_autopatcher_*.ini     # INI configurations
├── munition_autopatcher_ui.log    # Application logs
└── RobCo_Patcher/                 # Diagnostic outputs

config/                       # Configuration files
├── config.json              # Runtime configuration (gitignored)
├── config.sample.json       # Configuration template
└── README.md                # Configuration documentation
```

## Development Tools

### Code Quality
- **Nullable Reference Types**: Compile-time null safety
- **XML Documentation**: Comprehensive API documentation
- **EditorConfig**: Consistent code formatting
- **Analyzer Rules**: Static code analysis

### Debugging Support
- **Debug Console**: Console allocation for debug builds
- **Comprehensive Logging**: Detailed operation logging
- **Exception Handling**: Structured error handling with context
- **Diagnostic Output**: Detailed diagnostic file generation

## Deployment

### Build Outputs
- **Self-contained**: Optional self-contained deployment
- **Framework-dependent**: Requires .NET 8.0 runtime
- **Windows-specific**: WPF requires Windows platform
- **Single File**: Optional single-file deployment

### Runtime Requirements
- **.NET 8.0 Runtime**: Windows desktop runtime
- **Windows 10/11**: Operating system requirement
- **Fallout 4**: Game installation for full functionality
- **Write Permissions**: For output directory access

## Performance Considerations

### Optimization Strategies
- **Asynchronous Operations**: Non-blocking UI operations
- **Memory Management**: Proper disposal of Mutagen resources
- **Caching**: LinkCache for efficient record resolution
- **Lazy Loading**: Deferred initialization where appropriate
- **Cancellation Support**: User-cancellable long operations