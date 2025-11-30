// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T023: Compares generated artifacts against baseline files.
/// Supports text and binary comparison with configurable ignore patterns.
/// </summary>
public partial class BaselineDiff
{
    private readonly List<Regex> _ignorePatterns = [];

    /// <summary>
    /// Adds a pattern to ignore during comparison.
    /// Lines matching this pattern will be excluded from diff.
    /// </summary>
    /// <param name="pattern">Regular expression pattern to ignore.</param>
    public void AddIgnorePattern(string pattern)
    {
        _ignorePatterns.Add(new Regex(pattern, RegexOptions.Compiled));
    }

    /// <summary>
    /// Compares two text files and returns the differences.
    /// </summary>
    /// <param name="baselinePath">Path to the baseline file.</param>
    /// <param name="actualPath">Path to the actual file to compare.</param>
    /// <returns>Result containing all differences.</returns>
    public DiffResult Compare(string baselinePath, string actualPath)
    {
        var result = new DiffResult();

        // Handle missing files
        if (!File.Exists(baselinePath))
        {
            result.BaselineMissing = true;
            result.AreEqual = false;
            return result;
        }

        if (!File.Exists(actualPath))
        {
            result.ActualMissing = true;
            result.AreEqual = false;
            return result;
        }

        var baselineLines = FilterLines(File.ReadAllLines(baselinePath));
        var actualLines = FilterLines(File.ReadAllLines(actualPath));

        // Simple LCS-based diff algorithm
        ComputeDiff(baselineLines, actualLines, result);

        result.AreEqual = result.Differences.Count == 0;
        return result;
    }

    /// <summary>
    /// Compares two binary files by hash.
    /// </summary>
    /// <param name="baselinePath">Path to the baseline file.</param>
    /// <param name="actualPath">Path to the actual file to compare.</param>
    /// <returns>Result with hash comparison.</returns>
    public BinaryDiffResult CompareBinary(string baselinePath, string actualPath)
    {
        var result = new BinaryDiffResult();

        if (!File.Exists(baselinePath))
        {
            result.BaselineMissing = true;
            result.AreEqual = false;
            return result;
        }

        if (!File.Exists(actualPath))
        {
            result.ActualMissing = true;
            result.AreEqual = false;
            return result;
        }

        result.BaselineHash = ComputeFileHash(baselinePath);
        result.ActualHash = ComputeFileHash(actualPath);
        result.AreEqual = result.BaselineHash == result.ActualHash;

        return result;
    }

    /// <summary>
    /// Generates a human-readable report of differences.
    /// </summary>
    /// <param name="result">The diff result to report.</param>
    /// <returns>Formatted report string.</returns>
    public string GenerateReport(DiffResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Diff Report ===");
        sb.AppendLine();

        if (result.BaselineMissing)
        {
            sb.AppendLine("⚠️ Baseline file is missing - this may be a new scenario.");
            return sb.ToString();
        }

        if (result.ActualMissing)
        {
            sb.AppendLine("❌ Actual file is missing - generation may have failed.");
            return sb.ToString();
        }

        if (result.AreEqual)
        {
            sb.AppendLine("✅ Files are identical.");
            return sb.ToString();
        }

        sb.AppendLine($"Found {result.Differences.Count} difference(s):");
        sb.AppendLine();

        foreach (var diff in result.Differences)
        {
            var symbol = diff.Type switch
            {
                DiffType.Added => "+",
                DiffType.Removed => "-",
                DiffType.Modified => "~",
                _ => "?"
            };

            sb.AppendLine($"[{diff.Type}] Line {diff.LineNumber}: {symbol} {diff.Content}");
            if (diff.Type == DiffType.Modified && diff.OriginalContent != null)
            {
                sb.AppendLine($"       Was: {diff.OriginalContent}");
            }
        }

        return sb.ToString();
    }

    private string[] FilterLines(string[] lines)
    {
        if (_ignorePatterns.Count == 0)
        {
            return lines;
        }

        return lines
            .Select(line =>
            {
                foreach (var pattern in _ignorePatterns)
                {
                    if (pattern.IsMatch(line))
                    {
                        return "[IGNORED]";
                    }
                }
                return line;
            })
            .ToArray();
    }

    private void ComputeDiff(string[] baseline, string[] actual, DiffResult result)
    {
        var i = 0;
        var j = 0;

        while (i < baseline.Length || j < actual.Length)
        {
            if (i >= baseline.Length)
            {
                // Remaining lines in actual are additions
                while (j < actual.Length)
                {
                    result.Differences.Add(new Difference
                    {
                        Type = DiffType.Added,
                        LineNumber = j + 1,
                        Content = actual[j]
                    });
                    j++;
                }
                break;
            }

            if (j >= actual.Length)
            {
                // Remaining lines in baseline are removals
                while (i < baseline.Length)
                {
                    result.Differences.Add(new Difference
                    {
                        Type = DiffType.Removed,
                        LineNumber = i + 1,
                        Content = baseline[i]
                    });
                    i++;
                }
                break;
            }

            if (baseline[i] == actual[j])
            {
                // Lines match
                i++;
                j++;
            }
            else
            {
                // Try to find if this is a modification or add/remove
                var foundInActual = Array.IndexOf(actual, baseline[i], j);
                var foundInBaseline = Array.IndexOf(baseline, actual[j], i);

                if (foundInActual == -1 && foundInBaseline == -1)
                {
                    // Modified line
                    result.Differences.Add(new Difference
                    {
                        Type = DiffType.Modified,
                        LineNumber = i + 1,
                        Content = actual[j],
                        OriginalContent = baseline[i]
                    });
                    i++;
                    j++;
                }
                else if (foundInActual == -1 || (foundInBaseline != -1 && foundInBaseline - i < foundInActual - j))
                {
                    // Line was added
                    result.Differences.Add(new Difference
                    {
                        Type = DiffType.Added,
                        LineNumber = j + 1,
                        Content = actual[j]
                    });
                    j++;
                }
                else
                {
                    // Line was removed
                    result.Differences.Add(new Difference
                    {
                        Type = DiffType.Removed,
                        LineNumber = i + 1,
                        Content = baseline[i]
                    });
                    i++;
                }
            }
        }
    }

    private static string ComputeFileHash(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Result of a text file comparison.
/// </summary>
public class DiffResult
{
    public bool AreEqual { get; set; }
    public bool BaselineMissing { get; set; }
    public bool ActualMissing { get; set; }
    public List<Difference> Differences { get; } = [];
}

/// <summary>
/// Result of a binary file comparison.
/// </summary>
public class BinaryDiffResult
{
    public bool AreEqual { get; set; }
    public bool BaselineMissing { get; set; }
    public bool ActualMissing { get; set; }
    public string? BaselineHash { get; set; }
    public string? ActualHash { get; set; }
}

/// <summary>
/// Represents a single difference between files.
/// </summary>
public class Difference
{
    public DiffType Type { get; set; }
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? OriginalContent { get; set; }
}

/// <summary>
/// Type of difference.
/// </summary>
public enum DiffType
{
    Added,
    Removed,
    Modified
}
