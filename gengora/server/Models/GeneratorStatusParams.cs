namespace BITS.Gengora.Server.Models;

/// <summary>
/// Parameters for generator status notifications.
/// </summary>
public class GeneratorStatusParams
{
    public string State { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Path { get; set; }
}
