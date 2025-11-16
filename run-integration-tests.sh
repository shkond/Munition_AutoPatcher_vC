#!/bin/bash
# Bash script to run integration tests for WeaponDataExtractor
# This script provides an easy way to run the new integration tests and verify the implementation

echo "=== Munition AutoPatcher Integration Tests ==="
echo ""

# Check if .NET SDK is available
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo "✓ .NET SDK Version: $DOTNET_VERSION"
else
    echo "✗ .NET SDK not found. Please install .NET 8.0 SDK or later."
    exit 1
fi

echo ""

# Restore packages
echo "Restoring NuGet packages..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "✗ Package restore failed"
    exit 1
fi
echo "✓ Packages restored successfully"
echo ""

# Build the solution
echo "Building solution..."
dotnet build --no-restore
if [ $? -ne 0 ]; then
    echo "✗ Build failed"
    exit 1
fi
echo "✓ Build completed successfully"
echo ""

# Run integration tests
echo "Running Integration Tests..."
echo "----------------------------------------"

# Run all integration tests with detailed output
dotnet test tests/IntegrationTests --no-build --logger "console;verbosity=normal"

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ All integration tests passed!"
    echo ""
    echo "Integration test implementation is working correctly."
    echo "WeaponDataExtractor can successfully process virtual Mutagen environments."
else
    echo ""
    echo "✗ Some integration tests failed"
    echo "Please check the test output above for details."
    exit 1
fi

echo ""
echo "=== Test Summary ==="
echo "• TestEnvironmentBuilder: Creates virtual Mutagen environments"
echo "• WeaponDataExtractor: Processes weapon-ammo relationships"
echo "• Integration Tests: Validate end-to-end functionality"
echo "• Virtual Environments: No game files required"
echo ""
echo "For more details, see:"
echo "• tests/IntegrationTests/README.md"
echo "• INTEGRATION_TESTS_IMPLEMENTATION_SUMMARY.md"