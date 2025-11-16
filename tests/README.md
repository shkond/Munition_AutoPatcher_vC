# Integration Tests Directory

This directory contains integration tests that use Mutagen's virtual environment capabilities to test the complete workflow of WeaponDataExtractor with in-memory plugin environments.

## Test Projects

- **IntegrationTests**: Comprehensive integration tests using TestEnvironmentBuilder for virtual Mutagen environments
- **AutoTests**: Existing automated integration tests
- **LinkCacheHelperTests**: Unit tests for core services
- **WeaponDataExtractorTests**: Specialized tests for weapon data extraction

## Integration Testing Approach

The integration tests create virtual Fallout 4 plugin environments using Mutagen's testing infrastructure, allowing us to:

1. Create in-memory .esp files with weapons, ammunition, and constructible objects
2. Test WeaponDataExtractor against real Mutagen LinkCache and WinningOverrides systems
3. Verify weapon-ammo relationship resolution without requiring actual game files
4. Test complex plugin hierarchies and load order scenarios

## Running Integration Tests

```bash
# Run all tests
dotnet test

# Run only integration tests
dotnet test tests/IntegrationTests

# Run with verbose output
dotnet test tests/IntegrationTests --logger "console;verbosity=detailed"
```