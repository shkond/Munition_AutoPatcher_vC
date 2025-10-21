namespace MunitionAutoPatcher.Models;

/// <summary>
/// Represents a FormKey (Plugin + FormID) for identifying game records
/// </summary>
public record FormKey
{
    public string PluginName { get; init; } = string.Empty;
    public uint FormId { get; init; }

    public override string ToString() => $"{PluginName}:{FormId:X8}";

    public static FormKey Parse(string input)
    {
        var parts = input.Split(':');
        if (parts.Length != 2)
            throw new ArgumentException("Invalid FormKey format. Expected 'PluginName:FormID'");

        return new FormKey
        {
            PluginName = parts[0],
            FormId = Convert.ToUInt32(parts[1], 16)
        };
    }
}
