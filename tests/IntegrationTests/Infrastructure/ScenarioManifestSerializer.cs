// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using IntegrationTests.Infrastructure.Models;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T016: Serializer for scenario manifests with strict validation.
/// Reads and writes E2EScenarioDefinition to/from JSON with proper error handling.
/// </summary>
public static class ScenarioManifestSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new CountRangeJsonConverter()
        }
    };

    /// <summary>
    /// Deserializes a JSON string to an E2EScenarioDefinition.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized scenario definition.</returns>
    /// <exception cref="JsonException">Thrown when JSON is invalid or schema validation fails.</exception>
    public static E2EScenarioDefinition? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("JSON content cannot be null or empty");
        }

        var dto = JsonSerializer.Deserialize<ScenarioManifestDto>(json, s_options);
        if (dto == null)
        {
            return null;
        }

        return ConvertFromDto(dto);
    }

    /// <summary>
    /// Serializes an E2EScenarioDefinition to a JSON string.
    /// </summary>
    /// <param name="scenario">The scenario to serialize.</param>
    /// <returns>The JSON string representation.</returns>
    public static string Serialize(E2EScenarioDefinition scenario)
    {
        var dto = ConvertToDto(scenario);
        return JsonSerializer.Serialize(dto, s_options);
    }

    /// <summary>
    /// Validates a JSON string against the scenario schema without fully deserializing.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <returns>List of validation errors, empty if valid.</returns>
    public static IReadOnlyList<string> Validate(string json)
    {
        var errors = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Required fields
            if (!root.TryGetProperty("id", out _))
                errors.Add("Missing required field: id");
            if (!root.TryGetProperty("displayName", out _))
                errors.Add("Missing required field: displayName");
            if (!root.TryGetProperty("pluginSeeds", out var seeds) || seeds.GetArrayLength() == 0)
                errors.Add("Missing or empty required field: pluginSeeds");
            if (!root.TryGetProperty("expectedEspName", out _))
                errors.Add("Missing required field: expectedEspName");
            if (!root.TryGetProperty("validationProfile", out _))
                errors.Add("Missing required field: validationProfile");
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
        }

        return errors;
    }

    private static E2EScenarioDefinition ConvertFromDto(ScenarioManifestDto dto)
    {
        return new E2EScenarioDefinition
        {
            Id = dto.Id ?? throw new JsonException("id is required"),
            DisplayName = dto.DisplayName ?? throw new JsonException("displayName is required"),
            Description = dto.Description,
            PluginSeeds = dto.PluginSeeds?.Select(ConvertPluginSeed).ToList() 
                ?? throw new JsonException("pluginSeeds is required"),
            GameDataRoot = dto.GameDataRoot,
            OutputRelativePath = dto.OutputRelativePath,
            ExpectedEspName = dto.ExpectedEspName ?? throw new JsonException("expectedEspName is required"),
            ValidationProfile = ConvertValidationProfile(dto.ValidationProfile) 
                ?? throw new JsonException("validationProfile is required"),
            ScenarioAssertions = dto.ScenarioAssertions?.Select(ConvertAssertion).ToList(),
            ConfigOverrides = dto.ConfigOverrides,
            TimeoutSeconds = dto.TimeoutSeconds
        };
    }

    private static PluginSeed ConvertPluginSeed(PluginSeedDto dto)
    {
        return new PluginSeed
        {
            Name = dto.Name ?? throw new JsonException("pluginSeed.name is required"),
            BuilderActionName = dto.BuilderActionName,
            BaselineCopySource = dto.BaselineCopySource,
            OwnsEnvironment = dto.OwnsEnvironment
        };
    }

    private static ESPValidationProfile? ConvertValidationProfile(ValidationProfileDto? dto)
    {
        if (dto == null) return null;

        return new ESPValidationProfile
        {
            ProfileId = dto.ProfileId ?? "default",
            BaselineArtifacts = dto.BaselineArtifacts,
            IgnoreHeaderFields = dto.IgnoreHeaderFields ?? [HeaderField.Timestamp, HeaderField.NextFormId],
            AllowedWarnings = dto.AllowedWarnings,
            FatalErrorPatterns = dto.FatalErrorPatterns,
            StructuralExpectations = ConvertStructuralExpectations(dto.StructuralExpectations)
        };
    }

    private static StructuralExpectation ConvertStructuralExpectations(StructuralExpectationDto? dto)
    {
        if (dto == null) return new StructuralExpectation();

        return new StructuralExpectation
        {
            WeaponCount = dto.WeaponCount,
            AmmoCount = dto.AmmoCount,
            CobjCount = dto.CobjCount
        };
    }

    private static ScenarioAssertion ConvertAssertion(ScenarioAssertionDto dto)
    {
        return new ScenarioAssertion
        {
            Target = dto.Target ?? throw new JsonException("assertion.target is required"),
            ExpectedValue = dto.ExpectedValue ?? throw new JsonException("assertion.expectedValue is required"),
            Description = dto.Description
        };
    }

    private static ScenarioManifestDto ConvertToDto(E2EScenarioDefinition scenario)
    {
        return new ScenarioManifestDto
        {
            Id = scenario.Id,
            DisplayName = scenario.DisplayName,
            Description = scenario.Description,
            PluginSeeds = scenario.PluginSeeds.Select(s => new PluginSeedDto
            {
                Name = s.Name,
                BuilderActionName = s.BuilderActionName,
                BaselineCopySource = s.BaselineCopySource,
                OwnsEnvironment = s.OwnsEnvironment
            }).ToList(),
            GameDataRoot = scenario.GameDataRoot,
            OutputRelativePath = scenario.OutputRelativePath,
            ExpectedEspName = scenario.ExpectedEspName,
            ValidationProfile = new ValidationProfileDto
            {
                ProfileId = scenario.ValidationProfile.ProfileId,
                BaselineArtifacts = scenario.ValidationProfile.BaselineArtifacts,
                IgnoreHeaderFields = scenario.ValidationProfile.IgnoreHeaderFields.ToList(),
                AllowedWarnings = scenario.ValidationProfile.AllowedWarnings?.ToList(),
                FatalErrorPatterns = scenario.ValidationProfile.FatalErrorPatterns?.ToList(),
                StructuralExpectations = new StructuralExpectationDto
                {
                    WeaponCount = scenario.ValidationProfile.StructuralExpectations.WeaponCount,
                    AmmoCount = scenario.ValidationProfile.StructuralExpectations.AmmoCount,
                    CobjCount = scenario.ValidationProfile.StructuralExpectations.CobjCount
                }
            },
            ScenarioAssertions = scenario.ScenarioAssertions?.Select(a => new ScenarioAssertionDto
            {
                Target = a.Target,
                ExpectedValue = a.ExpectedValue,
                Description = a.Description
            }).ToList(),
            ConfigOverrides = scenario.ConfigOverrides as Dictionary<string, string>,
            TimeoutSeconds = scenario.TimeoutSeconds
        };
    }

    #region DTOs for JSON serialization

    private sealed class ScenarioManifestDto
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public List<PluginSeedDto>? PluginSeeds { get; set; }
        public string? GameDataRoot { get; set; }
        public string? OutputRelativePath { get; set; }
        public string? ExpectedEspName { get; set; }
        public ValidationProfileDto? ValidationProfile { get; set; }
        public List<ScenarioAssertionDto>? ScenarioAssertions { get; set; }
        public Dictionary<string, string>? ConfigOverrides { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    private sealed class PluginSeedDto
    {
        public string? Name { get; set; }
        public string? BuilderActionName { get; set; }
        public string? BaselineCopySource { get; set; }
        public bool OwnsEnvironment { get; set; }
    }

    private sealed class ValidationProfileDto
    {
        public string? ProfileId { get; set; }
        public string? BaselineArtifacts { get; set; }
        public List<HeaderField>? IgnoreHeaderFields { get; set; }
        public List<string>? AllowedWarnings { get; set; }
        public List<string>? FatalErrorPatterns { get; set; }
        public StructuralExpectationDto? StructuralExpectations { get; set; }
    }

    private sealed class StructuralExpectationDto
    {
        public CountRange? WeaponCount { get; set; }
        public CountRange? AmmoCount { get; set; }
        public CountRange? CobjCount { get; set; }
    }

    private sealed class ScenarioAssertionDto
    {
        public string? Target { get; set; }
        public string? ExpectedValue { get; set; }
        public string? Description { get; set; }
    }

    #endregion
}

/// <summary>
/// JSON converter for CountRange struct.
/// Supports formats: { "min": 0, "max": 10 }, "exact:5", "atleast:3"
/// </summary>
public sealed class CountRangeJsonConverter : JsonConverter<CountRange?>
{
    public override CountRange? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return null;

            // Parse shorthand: "exact:5", "atleast:3"
            if (value.StartsWith("exact:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value.AsSpan(6), out var exact))
                    return CountRange.Exact(exact);
            }
            else if (value.StartsWith("atleast:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value.AsSpan(8), out var min))
                    return CountRange.AtLeast(min);
            }
            else if (int.TryParse(value, out var single))
            {
                return CountRange.Exact(single);
            }

            throw new JsonException($"Invalid CountRange string format: {value}");
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int min = 0, max = int.MaxValue;
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    if (string.Equals(propertyName, "min", StringComparison.OrdinalIgnoreCase))
                        min = reader.GetInt32();
                    else if (string.Equals(propertyName, "max", StringComparison.OrdinalIgnoreCase))
                        max = reader.GetInt32();
                }
            }

            return new CountRange(min, max);
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return CountRange.Exact(reader.GetInt32());
        }

        throw new JsonException($"Unexpected token type for CountRange: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, CountRange? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        var range = value.Value;
        if (range.Min == range.Max)
        {
            writer.WriteStringValue($"exact:{range.Min}");
        }
        else if (range.Max == int.MaxValue)
        {
            writer.WriteStringValue($"atleast:{range.Min}");
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteNumber("min", range.Min);
            writer.WriteNumber("max", range.Max);
            writer.WriteEndObject();
        }
    }
}
