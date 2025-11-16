# Integration Tests for WeaponDataExtractor

This project contains comprehensive integration tests for the `WeaponDataExtractor` service using Mutagen's virtual environment capabilities. These tests verify the complete workflow of weapon-ammunition relationship resolution without requiring actual Fallout 4 game files.

## Overview

The integration tests use a custom `TestEnvironmentBuilder` to create in-memory Fallout 4 plugin environments with virtual weapons, ammunition, and constructible objects. This approach provides several advantages:

- **No Game Dependencies**: Tests run without requiring Fallout 4 installation
- **Controlled Environment**: Precise control over test data and scenarios
- **Real Mutagen Integration**: Tests use actual Mutagen LinkCache and WinningOverrides systems
- **Performance**: Fast test execution with in-memory operations
- **Isolation**: Each test creates its own isolated environment

## Architecture

### TestEnvironmentBuilder

The `TestEnvironmentBuilder` class provides a fluent API for creating virtual Mutagen environments:

```csharp
var gameEnv = new TestEnvironmentBuilder()
    .WithPlugin("TestMod.esp", mod =>
    {
        var ammo = mod.Ammunitions.AddNew();
        ammo.EditorID = "TestAmmo";
        
        var weapon = mod.Weapons.AddNew();
        weapon.EditorID = "TestWeapon";
        weapon.Ammo = ammo.ToLink();
        
        var cobj = mod.ConstructibleObjects.AddNew();
        cobj.EditorID = "cobj_TestWeapon";
        cobj.CreatedObject = weapon.ToLink();
    })
    .Build();
```

### TestDataFactory

The `TestDataFactory` provides pre-configured test scenarios:

- `CreateBasicWeaponAmmoScenario()`: Simple weapon-ammo-COBJ setup
- `CreateComplexWeaponAmmoScenario()`: Multiple weapons with different ammunition
- `CreateCrossPluginScenario()`: Cross-plugin references
- `CreateExclusionTestScenario()`: Plugin exclusion testing
- `CreateNoAmmoScenario()`: Weapons without ammunition
- `CreateErrorTestScenario()`: Error handling scenarios

## Test Categories

### Basic Integration Tests (`WeaponDataExtractorIntegrationTests.cs`)

- **Basic Extraction**: Verifies simple weapon-ammo extraction
- **Complex Scenarios**: Multiple weapons and ammunition types
- **Plugin Exclusion**: Tests exclusion functionality
- **No Ammunition**: Handles weapons without ammo
- **Error Handling**: Graceful error recovery
- **Empty Environment**: Handles empty environments
- **LinkCache Verification**: Ensures proper Mutagen integration

### Advanced Integration Tests (`AdvancedIntegrationTests.cs`)

- **Performance Testing**: Large dataset handling
- **Cross-Plugin References**: Load order and plugin interactions
- **Memory Management**: Resource cleanup verification
- **Concurrent Access**: Thread safety testing

## Running the Tests

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or Visual Studio Code (recommended)

### Command Line

```bash
# Run all integration tests
dotnet test tests/IntegrationTests

# Run with detailed output
dotnet test tests/IntegrationTests --logger "console;verbosity=detailed"

# Run specific test class
dotnet test tests/IntegrationTests --filter "ClassName=WeaponDataExtractorIntegrationTests"

# Run specific test method
dotnet test tests/IntegrationTests --filter "MethodName=ExtractAsync_WithBasicWeaponAmmoScenario_ExtractsWeaponAndAmmo"
```

### Visual Studio

1. Open the solution in Visual Studio
2. Build the solution (Ctrl+Shift+B)
3. Open Test Explorer (Test → Test Explorer)
4. Run all tests or select specific tests

## Test Data Scenarios

### Basic Scenario
- Single plugin with one weapon, one ammunition, and one COBJ
- Verifies fundamental extraction functionality

### Complex Scenario
- Multiple weapons with different ammunition types
- Tests handling of diverse weapon-ammo combinations

### Cross-Plugin Scenario
- Weapons in one plugin referencing ammunition from another
- Verifies load order and cross-reference resolution

### Exclusion Scenario
- Multiple plugins with some excluded from processing
- Tests plugin exclusion logic

### Error Scenario
- Invalid references and broken data
- Verifies graceful error handling

## Key Features Tested

### WeaponDataExtractor Functionality
- ✅ COBJ record processing
- ✅ Weapon-ammunition relationship resolution
- ✅ Plugin exclusion logic
- ✅ Error handling and recovery
- ✅ FormKey extraction and validation
- ✅ Editor ID resolution

### Mutagen Integration
- ✅ LinkCache resolution
- ✅ WinningOverrides processing
- ✅ Virtual environment creation
- ✅ Record enumeration
- ✅ Cross-plugin references

### Performance Characteristics
- ✅ Large dataset handling (100+ weapons)
- ✅ Memory efficiency
- ✅ Execution time validation
- ✅ Resource cleanup

## Extending the Tests

### Adding New Test Scenarios

1. Create new test data in `TestDataFactory`:

```csharp
public static TestEnvironmentBuilder CreateMyScenario(this TestEnvironmentBuilder builder)
{
    return builder.WithPlugin("MyMod.esp", mod =>
    {
        // Configure your test scenario
    });
}
```

2. Add test method in appropriate test class:

```csharp
[Fact]
public async Task ExtractAsync_WithMyScenario_ExpectedBehavior()
{
    // Arrange
    var gameEnv = new TestEnvironmentBuilder()
        .CreateMyScenario()
        .Build();
    
    // Act & Assert
    // Your test logic here
}
```

### Custom Test Environments

For specialized testing needs, create custom environments:

```csharp
var builder = new TestEnvironmentBuilder();

// Add multiple plugins
builder
    .WithPlugin("BaseWeapons.esp", mod => { /* base weapons */ })
    .WithPlugin("AmmoExpansion.esp", mod => { /* ammunition */ })
    .WithPlugin("WeaponMods.esp", mod => { /* weapon modifications */ });

// Build and test
var gameEnv = builder.Build();
```

## Troubleshooting

### Common Issues

**Test Failures Due to Missing Dependencies**
- Ensure all NuGet packages are restored: `dotnet restore`
- Verify .NET 8.0 SDK is installed

**Memory Issues with Large Tests**
- Reduce test data size for development
- Ensure proper disposal of environments (`using` statements)

**Mutagen Version Conflicts**
- Verify all projects use consistent Mutagen versions
- Check for package version conflicts in solution

### Debugging Tips

1. **Enable Detailed Logging**:
   ```csharp
   var logger = new ConsoleLogger<WeaponDataExtractor>();
   var extractor = new WeaponDataExtractor(logger);
   ```

2. **Inspect Virtual Environment**:
   ```csharp
   var weapons = resourcedEnv.GetWinningWeaponOverrides().ToList();
   var cobjs = resourcedEnv.GetWinningConstructibleObjectOverrides().ToList();
   // Set breakpoints to inspect collections
   ```

3. **Validate Test Data**:
   ```csharp
   var mod = builder.GetMod("TestMod.esp");
   Assert.NotNull(mod);
   Assert.NotEmpty(mod.Weapons);
   ```

## Contributing

When adding new integration tests:

1. Follow the existing naming conventions
2. Use descriptive test method names that explain the scenario and expected outcome
3. Include comprehensive assertions
4. Add appropriate documentation
5. Consider performance implications for CI/CD pipelines
6. Ensure proper resource cleanup

## Performance Benchmarks

Typical performance expectations:

- **Basic Scenario**: < 100ms
- **Complex Scenario (3 weapons)**: < 200ms
- **Large Dataset (100 weapons)**: < 5000ms
- **Memory Usage**: < 50MB per test

These benchmarks help ensure the integration tests remain suitable for continuous integration environments.