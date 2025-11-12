# Munition AutoPatcher vC - Project Structure

## Solution Architecture
The project follows a clean MVVM (Model-View-ViewModel) architecture with dependency injection, organized as a single WPF application with comprehensive test coverage.

```
MunitionAutoPatcher.sln
├── MunitionAutoPatcher/          # Main WPF Application
├── tests/
│   ├── AutoTests/                # Automated integration tests
│   ├── LinkCacheHelperTests/     # Core service unit tests
│   └── WeaponDataExtractorTests/ # Data extraction tests
```

## Core Application Structure

### `/MunitionAutoPatcher/` - Main Application
```
MunitionAutoPatcher/
├── Models/                    # Data models and entities
│   ├── AmmoCategory.cs       # Ammunition categorization
│   ├── AmmoData.cs          # Ammunition data structure
│   ├── WeaponData.cs        # Weapon information model
│   ├── WeaponMapping.cs     # Weapon-to-ammo mapping
│   ├── FormKey.cs           # Bethesda FormKey wrapper
│   ├── OmodCandidate.cs     # Object modification candidate
│   ├── StrategyConfig.cs    # Mapping strategy configuration
│   ├── ConfirmationContext.cs # User confirmation context
│   └── ExtractionContext.cs # Data extraction context

├── ViewModels/               # MVVM ViewModels
│   ├── MainViewModel.cs     # Main window coordination
│   ├── SettingsViewModel.cs # Configuration management
│   ├── MapperViewModel.cs   # Mapping interface logic
│   ├── AmmoViewModel.cs     # Ammunition display logic
│   ├── WeaponMappingViewModel.cs # Individual mapping logic
│   └── ViewModelBase.cs     # Base ViewModel with INotifyPropertyChanged

├── Views/                    # WPF User Interface
│   ├── MainWindow.xaml      # Primary application window
│   ├── SettingsView.xaml    # Configuration interface
│   ├── MapperView.xaml      # Mapping management interface
│   └── InputDialog.xaml     # User input dialogs

├── Services/                 # Business logic and data access
│   ├── Interfaces/          # Service contracts
│   ├── Implementations/     # Service implementations
│   └── Helpers/             # Utility services

├── Commands/                 # MVVM Command implementations
│   ├── RelayCommand.cs      # Synchronous command wrapper
│   └── AsyncRelayCommand.cs # Asynchronous command wrapper

├── Converters/              # WPF value converters
│   ├── BoolToVisibilityConverter.cs
│   └── InverseBoolConverter.cs

├── Logging/                 # Logging infrastructure
│   └── AppLoggerProvider.cs # Custom logging provider

├── Utilities/               # Cross-cutting utilities
│   ├── RepoUtils.cs         # Repository utilities
│   ├── MutagenReflectionHelpers.cs # Mutagen reflection utilities
│   └── MutagenTypeGuards.cs # Type safety utilities

├── App.xaml.cs              # Application entry point and DI configuration
├── AppLogger.cs             # Application-wide logging
└── DebugConsole.cs          # Debug console allocation
```

## Service Layer Architecture

### `/Services/Interfaces/` - Service Contracts
- `IWeaponsService` - Weapon data extraction and management
- `IConfigService` - Configuration persistence and retrieval
- `ILoadOrderService` - Plugin load order management
- `IOrchestrator` - High-level workflow coordination
- `IRobCoIniGenerator` - INI file generation
- `IWeaponOmodExtractor` - Object modification extraction
- `IAmmunitionChangeDetector` - Ammunition change detection

### `/Services/Implementations/` - Core Services
- `WeaponsService` - Primary weapon data extraction using Mutagen
- `ConfigService` - JSON-based configuration management
- `LoadOrderService` - Plugin load order resolution
- `OrchestratorService` - Workflow coordination and orchestration
- `RobCoIniGenerator` - RobCo Patcher INI file generation
- `WeaponOmodExtractor` - Object modification data extraction
- `MutagenV51Detector` - Ammunition change detection using Mutagen 0.51.5
- `LinkCacheHelper` - Mutagen LinkCache management utilities

### `/Services/Helpers/` - Utility Services
- `CandidateEnumerator` - Object modification candidate enumeration
- `DiagnosticWriter` - Diagnostic output generation
- `ReverseMapBuilder` - Reverse mapping construction

## Mutagen Integration Layer

### Environment Management
- `IMutagenEnvironment` - Mutagen environment abstraction
- `MutagenEnvironmentFactory` - Environment factory pattern
- `ResourcedMutagenEnvironment` - Resource-managed environment
- `MutagenV51EnvironmentAdapter` - Version-specific adapter

### Data Access Patterns
- **LinkCache Pattern**: Centralized record resolution and caching
- **WinningOverrides**: Conflict resolution for overlapping modifications
- **GameEnvironment**: Automatic game installation detection and plugin loading
- **FormKey Resolution**: Safe FormKey to record resolution with error handling

## Configuration Management

### `/config/` - Configuration Files
- `config.json` - Runtime configuration (gitignored)
- `config.sample.json` - Configuration template
- `README.md` - Configuration documentation

### Configuration Structure
```json
{
  "gamePath": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Fallout 4",
  "outputPath": "artifacts",
  "output": {
    "mode": "esp",           // "esp" or "ini"
    "directory": "artifacts"
  },
  "excludedPlugins": ["Dank_ECO.esp"],
  "mappingStrategy": "automatic"
}
```

## Output Management

### `/artifacts/` - Generated Output
- `MunitionAutoPatcher_Patch.esp` - Generated ESP patch (ESP mode)
- `munition_autopatcher_*.ini` - Generated INI files (INI mode)
- `munition_autopatcher_ui.log` - Application log file
- `RobCo_Patcher/` - Detailed diagnostic outputs
- `archive/` - Archived outputs and cleanup scripts

## Test Structure

### `/tests/AutoTests/` - Integration Tests
- End-to-end workflow testing
- Cancellation token testing
- Service integration validation

### `/tests/LinkCacheHelperTests/` - Unit Tests
- Core service functionality testing
- Mutagen integration testing
- Error handling validation
- Performance testing

### `/tests/WeaponDataExtractorTests/` - Specialized Tests
- Weapon data extraction validation
- ESP patch generation testing
- Data integrity verification

## Architectural Patterns

### MVVM Implementation
- **Models**: Pure data structures with minimal logic
- **ViewModels**: Presentation logic, command handling, property change notification
- **Views**: XAML-based UI with data binding to ViewModels
- **Commands**: Encapsulated user actions with async support

### Dependency Injection
- **Service Registration**: Configured in `App.xaml.cs`
- **Lifetime Management**: Singleton services for stateful components
- **Interface Segregation**: Small, focused service interfaces
- **Constructor Injection**: Dependencies injected via constructors

### Service Layer Pattern
- **Abstraction**: All services implement interfaces
- **Separation of Concerns**: Each service has a single responsibility
- **Composition**: Complex operations composed from simpler services
- **Error Handling**: Consistent error handling across service boundaries

### Factory Pattern
- **Environment Factory**: Creates appropriate Mutagen environments
- **Detector Factory**: Creates ammunition change detectors
- **Adapter Pattern**: Adapts between different Mutagen versions