# Munition AutoPatcher vC - Development Guidelines

## Code Quality Standards

### Naming Conventions
- **Classes**: PascalCase with descriptive names (e.g., `WeaponOmodExtractor`, `LinkCacheHelper`)
- **Methods**: PascalCase with action-oriented names (e.g., `ExtractCandidatesAsync`, `TryResolveViaLinkCache`)
- **Properties**: PascalCase (e.g., `GameDataPath`, `IsProcessing`)
- **Fields**: camelCase with underscore prefix for private fields (e.g., `_configService`, `_logger`)
- **Parameters**: camelCase (e.g., `linkLike`, `formKeyObj`)
- **Local Variables**: camelCase (e.g., `weaponCount`, `candidates`)

### Documentation Standards
- **XML Documentation**: All public classes, methods, and properties must have XML documentation
- **Inline Comments**: Complex logic blocks should have explanatory comments
- **Japanese UI Messages**: User-facing messages are in Japanese for localization
- **Error Messages**: Include context and actionable information

```csharp
/// <summary>
/// Orchestrator for OMOD/COBJ candidate extraction. Delegates to specialized providers, confirmer, and diagnostic writer.
/// </summary>
public class WeaponOmodExtractor : IWeaponOmodExtractor
```

### Error Handling Patterns
- **Defensive Programming**: Always validate inputs and handle null cases
- **Graceful Degradation**: Continue processing when non-critical operations fail
- **Structured Logging**: Use ILogger with structured messages and exception details
- **Exception Wrapping**: Preserve original exceptions while adding context

```csharp
try
{
    // Operation
}
catch (OperationCanceledException)
{
    _logger.LogInformation("Operation was cancelled");
    throw; // Re-throw cancellation
}
catch (Exception ex)
{
    _logger.LogError(ex, "Operation failed: {Message}", ex.Message);
    // Handle or re-throw based on criticality
}
```

## Architectural Patterns

### Dependency Injection
- **Constructor Injection**: All dependencies injected via constructor
- **Interface Segregation**: Services implement focused interfaces
- **Null Validation**: All injected dependencies validated for null

```csharp
public WeaponOmodExtractor(
    ILoadOrderService loadOrderService,
    IConfigService configService,
    // ... other dependencies
    ILogger<WeaponOmodExtractor> logger)
{
    _loadOrderService = loadOrderService ?? throw new ArgumentNullException(nameof(loadOrderService));
    _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

### MVVM Implementation
- **ViewModelBase**: All ViewModels inherit from base class with INotifyPropertyChanged
- **Property Change Notification**: Use SetProperty helper for automatic notification
- **Command Pattern**: RelayCommand and AsyncRelayCommand for user actions
- **Progress Reporting**: Use IProgress<string> for long-running operations

```csharp
public bool IsProcessing
{
    get => _isProcessing;
    set => SetProperty(ref _isProcessing, value);
}
```

### Asynchronous Programming
- **Async/Await**: Use async/await for I/O and long-running operations
- **CancellationToken**: Support cancellation for user-initiated operations
- **ConfigureAwait**: Use ConfigureAwait(false) for library code
- **Task.Run**: Offload CPU-intensive work to thread pool

```csharp
public async Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress, CancellationToken cancellationToken)
{
    // Implementation with proper async patterns
}
```

## Service Layer Patterns

### Service Interface Design
- **Single Responsibility**: Each service has one clear purpose
- **Async Methods**: All I/O operations are asynchronous
- **Optional Parameters**: Use nullable parameters for optional functionality
- **Generic Return Types**: Use appropriate generic types for flexibility

### Configuration Management
- **JSON Serialization**: Use System.Text.Json for configuration persistence
- **Lazy Loading**: Load configuration on first access
- **Automatic Persistence**: Save changes immediately via background tasks
- **Default Values**: Provide sensible defaults for all configuration options

```csharp
private void EnsureLoaded()
{
    if (_loaded != null) return;
    // Load configuration logic
}
```

### Resource Management
- **Using Statements**: Proper disposal of resources with using statements
- **Factory Pattern**: Use factories for complex object creation
- **Environment Abstraction**: Abstract external dependencies through interfaces

```csharp
using var environment = _mutagenEnvironmentFactory.Create();
// Use environment within scope
```

## Mutagen Integration Patterns

### Reflection-Based Access
- **Safe Property Access**: Use helper methods for reflection-based property access
- **Type Guards**: Validate types before casting or accessing properties
- **Fallback Strategies**: Implement multiple strategies for data extraction

```csharp
if (MutagenReflectionHelpers.TryGetPropertyValue<string>(weaponGetter, "EditorID", out var editorId))
{
    // Use editorId
}
```

### LinkCache Resolution
- **Multiple Resolution Strategies**: Try multiple approaches for record resolution
- **Caching**: Cache resolution results to improve performance
- **Error Suppression**: Suppress expected resolution failures with logging

```csharp
public static object? TryResolveViaLinkCache(object? linkLike, object? linkCache)
{
    // Multiple resolution strategies with fallbacks
}
```

### FormKey Handling
- **Centralized Extraction**: Use helper methods for FormKey extraction
- **String Representation**: Consistent format for FormKey string representation
- **Validation**: Validate FormKey components before use

## UI Development Patterns

### Progress Reporting
- **Centralized Logging**: Route all progress messages through AppLogger
- **Japanese Localization**: User messages in Japanese
- **Non-blocking UI**: Use async operations to prevent UI freezing

```csharp
var progress = new Progress<string>(msg => AppLogger.Log(msg));
```

### Collection Management
- **ObservableCollection**: Use ObservableCollection for data-bound collections
- **Batch Updates**: Update collections in batches to prevent UI freezing
- **Dispatcher Access**: Use Dispatcher for cross-thread UI updates

```csharp
const int batchSize = 200;
for (int i = 0; i < list.Count; i += batchSize)
{
    var chunk = list.Skip(i).Take(batchSize).ToList();
    Application.Current.Dispatcher.Invoke(() =>
    {
        foreach (var item in chunk) Collection.Add(item);
    });
    await Task.Delay(30); // Allow UI processing
}
```

### Command Implementation
- **CanExecute Logic**: Implement proper CanExecute logic for commands
- **Exception Handling**: Handle exceptions within command execution
- **State Management**: Update UI state during command execution

## Testing Patterns

### Unit Test Structure
- **Arrange-Act-Assert**: Follow AAA pattern for test organization
- **Descriptive Names**: Test method names describe the scenario and expected outcome
- **Mock Dependencies**: Use mocking for external dependencies
- **Cancellation Testing**: Test cancellation token behavior

### Integration Testing
- **Service Integration**: Test service interactions and data flow
- **Error Scenarios**: Test error handling and recovery scenarios
- **Performance Testing**: Include performance validation for critical paths

## Logging and Diagnostics

### Structured Logging
- **ILogger Usage**: Use Microsoft.Extensions.Logging throughout
- **Log Levels**: Appropriate log levels (Debug, Information, Warning, Error)
- **Structured Messages**: Use structured logging with parameters
- **Exception Details**: Include full exception details in error logs

```csharp
_logger.LogInformation("Processing {Count} candidates from {Provider}", 
    candidates.Count, provider.GetType().Name);
```

### Diagnostic Output
- **File-based Diagnostics**: Write diagnostic information to files
- **CSV Export**: Use CSV format for tabular diagnostic data
- **Timestamped Output**: Include timestamps in diagnostic file names
- **Error Suppression**: Suppress repeated errors to prevent log spam

### Performance Monitoring
- **Stopwatch Usage**: Monitor performance of critical operations
- **Batch Processing**: Process large datasets in manageable chunks
- **Memory Management**: Proper disposal of large objects and collections

## File System Operations

### Path Management
- **Repository Root Detection**: Automatically detect repository root
- **Cross-platform Paths**: Use Path.Combine for path construction
- **Directory Creation**: Ensure directories exist before file operations
- **Relative Paths**: Use relative paths where appropriate

### File I/O Patterns
- **Async File Operations**: Use async methods for file I/O
- **UTF-8 Encoding**: Consistent UTF-8 encoding for text files
- **Exception Handling**: Handle file system exceptions gracefully
- **Backup Strategies**: Consider backup strategies for critical files

## Security Considerations

### Input Validation
- **Parameter Validation**: Validate all input parameters
- **Path Traversal Prevention**: Validate file paths to prevent traversal attacks
- **Format Validation**: Validate data formats before processing
- **Sanitization**: Sanitize user input before file operations

### Error Information
- **Information Disclosure**: Avoid exposing sensitive information in error messages
- **Log Sanitization**: Sanitize sensitive data in logs
- **Exception Details**: Balance debugging information with security

## Performance Guidelines

### Memory Management
- **Dispose Pattern**: Implement IDisposable for resource-heavy classes
- **Large Collections**: Process large collections in chunks
- **String Operations**: Use StringBuilder for multiple string concatenations
- **Caching**: Cache expensive computations and lookups

### Concurrency
- **Thread Safety**: Ensure thread safety for shared resources
- **Async Patterns**: Use async/await instead of blocking operations
- **Cancellation**: Support cancellation for long-running operations
- **Parallel Processing**: Use parallel processing where appropriate

### I/O Optimization
- **Batch Operations**: Batch file I/O operations when possible
- **Stream Processing**: Use streams for large data processing
- **Connection Pooling**: Reuse expensive resources like database connections
- **Lazy Loading**: Load data only when needed