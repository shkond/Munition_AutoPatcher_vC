#!/bin/bash
# Bash script to run integration tests for Munition AutoPatcher
# This script provides options to run different test suites and collect artifacts

# Default values
SUITE="All"
ARTIFACT_PATH=""
VERBOSE=false
NO_BUILD=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -s|--suite)
            SUITE="$2"
            shift 2
            ;;
        -a|--artifact-path)
            ARTIFACT_PATH="$2"
            shift 2
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        --no-build)
            NO_BUILD=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  -s, --suite SUITE        Test suite: All, WeaponExtractor, ViewModelE2E (default: All)"
            echo "  -a, --artifact-path PATH Output directory for test artifacts"
            echo "  -v, --verbose            Enable verbose output"
            echo "  --no-build               Skip build step"
            echo "  -h, --help               Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

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
echo "Test Suite: $SUITE"

# Set up artifact path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -z "$ARTIFACT_PATH" ]; then
    ARTIFACT_PATH="$SCRIPT_DIR/test-artifacts"
fi
echo "Artifact Path: $ARTIFACT_PATH"
export MUNITION_TEST_ARTIFACT_PATH="$ARTIFACT_PATH"

# Create artifact directory
mkdir -p "$ARTIFACT_PATH"

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

# Build the solution (unless skipped)
if [ "$NO_BUILD" = false ]; then
    echo "Building solution..."
    dotnet build --no-restore
    if [ $? -ne 0 ]; then
        echo "✗ Build failed"
        exit 1
    fi
    echo "✓ Build completed successfully"
    echo ""
fi

# Determine test filter
case $SUITE in
    WeaponExtractor)
        TEST_FILTER="--filter FullyQualifiedName~WeaponDataExtractor"
        ;;
    ViewModelE2E)
        TEST_FILTER="--filter FullyQualifiedName~ViewModelE2ETests"
        ;;
    *)
        TEST_FILTER=""
        ;;
esac

# Determine verbosity
if [ "$VERBOSE" = true ]; then
    VERBOSITY="detailed"
else
    VERBOSITY="normal"
fi

# Run integration tests
echo "Running Integration Tests ($SUITE)..."
echo "----------------------------------------"

TEST_RESULTS_PATH="$ARTIFACT_PATH/test-results"
mkdir -p "$TEST_RESULTS_PATH"

# Execute tests
dotnet test tests/IntegrationTests \
    --no-build \
    --logger "console;verbosity=$VERBOSITY" \
    --logger "trx;LogFileName=integration-tests.trx" \
    --results-directory "$TEST_RESULTS_PATH" \
    $TEST_FILTER

TEST_EXIT_CODE=$?

if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo ""
    echo "✓ All integration tests passed!"
    echo ""
    echo "Integration test implementation is working correctly."
else
    echo ""
    echo "✗ Some integration tests failed"
    echo "Please check the test output above for details."
fi

echo ""
echo "=== Test Summary ==="
echo "• Suite: $SUITE"
echo "• Artifacts: $ARTIFACT_PATH"
echo "• Test Results: $TEST_RESULTS_PATH"
echo ""

# List artifacts if ViewModelE2E
if [ "$SUITE" = "ViewModelE2E" ] || [ "$SUITE" = "All" ]; then
    SCENARIO_ARTIFACTS="$ARTIFACT_PATH/scenarios"
    if [ -d "$SCENARIO_ARTIFACTS" ]; then
        echo "=== Scenario Artifacts ==="
        find "$SCENARIO_ARTIFACTS" -type f | while read -r file; do
            echo "  ${file#$ARTIFACT_PATH/}"
        done
        echo ""
    fi
fi

echo "For more details, see:"
echo "• tests/IntegrationTests/README.md"
echo "• tests/IntegrationTests/Infrastructure/README.md (E2E harness docs)"
echo ""

exit $TEST_EXIT_CODE