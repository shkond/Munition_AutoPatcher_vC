// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure;
using IntegrationTests.Infrastructure.Models;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Tests;

/// <summary>
/// T021: Tests for BaselineDiff - compares generated artifacts against baseline.
/// </summary>
public class BaselineDiffTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempRoot;

    public BaselineDiffTests(ITestOutputHelper output)
    {
        _output = output;
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "MunitionAutoPatcher_Diff_Tests",
            Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    /// <summary>
    /// Tests that diff detects identical content as no changes.
    /// </summary>
    [Fact]
    public void Compare_ReturnsNoDifference_WhenContentIdentical()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.txt");
        var actualPath = Path.Combine(_tempRoot, "actual.txt");
        var content = "Line 1\nLine 2\nLine 3";
        File.WriteAllText(baselinePath, content);
        File.WriteAllText(actualPath, content);

        var diff = new BaselineDiff();

        // Act
        var result = diff.Compare(baselinePath, actualPath);

        // Assert
        Assert.True(result.AreEqual);
        Assert.Empty(result.Differences);
        _output.WriteLine("Files are identical - no differences detected");
    }

    /// <summary>
    /// Tests that diff detects added lines.
    /// </summary>
    [Fact]
    public void Compare_DetectsAddedLines_WhenActualHasMore()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.txt");
        var actualPath = Path.Combine(_tempRoot, "actual.txt");
        File.WriteAllText(baselinePath, "Line 1\nLine 2");
        File.WriteAllText(actualPath, "Line 1\nLine 2\nLine 3");

        var diff = new BaselineDiff();

        // Act
        var result = diff.Compare(baselinePath, actualPath);

        // Assert
        Assert.False(result.AreEqual);
        Assert.Contains(result.Differences, d => d.Type == DiffType.Added);
        _output.WriteLine($"Detected {result.Differences.Count} differences");
        foreach (var d in result.Differences)
        {
            _output.WriteLine($"  {d.Type}: {d.Content}");
        }
    }

    /// <summary>
    /// Tests that diff detects removed lines.
    /// </summary>
    [Fact]
    public void Compare_DetectsRemovedLines_WhenActualHasLess()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.txt");
        var actualPath = Path.Combine(_tempRoot, "actual.txt");
        File.WriteAllText(baselinePath, "Line 1\nLine 2\nLine 3");
        File.WriteAllText(actualPath, "Line 1\nLine 2");

        var diff = new BaselineDiff();

        // Act
        var result = diff.Compare(baselinePath, actualPath);

        // Assert
        Assert.False(result.AreEqual);
        Assert.Contains(result.Differences, d => d.Type == DiffType.Removed);
        _output.WriteLine($"Detected {result.Differences.Count} differences");
    }

    /// <summary>
    /// Tests that diff detects modified lines.
    /// </summary>
    [Fact]
    public void Compare_DetectsModifiedLines_WhenContentChanged()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.txt");
        var actualPath = Path.Combine(_tempRoot, "actual.txt");
        File.WriteAllText(baselinePath, "Line 1\nLine 2\nLine 3");
        File.WriteAllText(actualPath, "Line 1\nModified Line\nLine 3");

        var diff = new BaselineDiff();

        // Act
        var result = diff.Compare(baselinePath, actualPath);

        // Assert
        Assert.False(result.AreEqual);
        Assert.Contains(result.Differences, d => d.Type == DiffType.Modified);
        _output.WriteLine($"Detected {result.Differences.Count} differences");
    }

    /// <summary>
    /// Tests that diff handles missing baseline gracefully.
    /// </summary>
    [Fact]
    public void Compare_HandlesGracefully_WhenBaselineMissing()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "nonexistent.txt");
        var actualPath = Path.Combine(_tempRoot, "actual.txt");
        File.WriteAllText(actualPath, "Content");

        var diff = new BaselineDiff();

        // Act
        var result = diff.Compare(baselinePath, actualPath);

        // Assert
        Assert.False(result.AreEqual);
        Assert.True(result.BaselineMissing);
        _output.WriteLine("Baseline missing - handled gracefully");
    }

    /// <summary>
    /// Tests that diff handles missing actual file.
    /// </summary>
    [Fact]
    public void Compare_ReportsError_WhenActualMissing()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.txt");
        var actualPath = Path.Combine(_tempRoot, "nonexistent.txt");
        File.WriteAllText(baselinePath, "Content");

        var diff = new BaselineDiff();

        // Act
        var result = diff.Compare(baselinePath, actualPath);

        // Assert
        Assert.False(result.AreEqual);
        Assert.True(result.ActualMissing);
        _output.WriteLine("Actual file missing - reported error");
    }

    /// <summary>
    /// Tests that diff ignores specified patterns.
    /// </summary>
    [Fact]
    public void Compare_IgnoresPatterns_WhenConfigured()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.txt");
        var actualPath = Path.Combine(_tempRoot, "actual.txt");
        File.WriteAllText(baselinePath, "Line 1\nTimestamp: 2024-01-01\nLine 3");
        File.WriteAllText(actualPath, "Line 1\nTimestamp: 2024-12-01\nLine 3");

        var diff = new BaselineDiff();
        diff.AddIgnorePattern(@"Timestamp: \d{4}-\d{2}-\d{2}");

        // Act
        var result = diff.Compare(baselinePath, actualPath);

        // Assert
        Assert.True(result.AreEqual);
        _output.WriteLine("Timestamp differences ignored as configured");
    }

    /// <summary>
    /// Tests that diff generates human-readable report.
    /// </summary>
    [Fact]
    public void GenerateReport_CreatesReadableOutput_WithAllDifferences()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.txt");
        var actualPath = Path.Combine(_tempRoot, "actual.txt");
        File.WriteAllText(baselinePath, "A\nB\nC");
        File.WriteAllText(actualPath, "A\nX\nC\nD");

        var diff = new BaselineDiff();
        var result = diff.Compare(baselinePath, actualPath);

        // Act
        var report = diff.GenerateReport(result);

        // Assert
        Assert.False(string.IsNullOrEmpty(report));
        Assert.Contains("Modified", report);
        Assert.Contains("Added", report);
        _output.WriteLine($"Report:\n{report}");
    }

    /// <summary>
    /// Tests that diff can compare binary files by hash.
    /// </summary>
    [Fact]
    public void CompareBinary_DetectsChanges_ByHash()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.bin");
        var actualPath = Path.Combine(_tempRoot, "actual.bin");
        File.WriteAllBytes(baselinePath, [0x00, 0x01, 0x02, 0x03]);
        File.WriteAllBytes(actualPath, [0x00, 0x01, 0xFF, 0x03]);

        var diff = new BaselineDiff();

        // Act
        var result = diff.CompareBinary(baselinePath, actualPath);

        // Assert
        Assert.False(result.AreEqual);
        Assert.NotEqual(result.BaselineHash, result.ActualHash);
        _output.WriteLine($"Baseline hash: {result.BaselineHash}");
        _output.WriteLine($"Actual hash: {result.ActualHash}");
    }

    /// <summary>
    /// Tests that binary comparison succeeds for identical files.
    /// </summary>
    [Fact]
    public void CompareBinary_ReturnsEqual_WhenFilesIdentical()
    {
        // Arrange
        var baselinePath = Path.Combine(_tempRoot, "baseline.bin");
        var actualPath = Path.Combine(_tempRoot, "actual.bin");
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        File.WriteAllBytes(baselinePath, bytes);
        File.WriteAllBytes(actualPath, bytes);

        var diff = new BaselineDiff();

        // Act
        var result = diff.CompareBinary(baselinePath, actualPath);

        // Assert
        Assert.True(result.AreEqual);
        Assert.Equal(result.BaselineHash, result.ActualHash);
        _output.WriteLine($"Files match - hash: {result.BaselineHash}");
    }
}
