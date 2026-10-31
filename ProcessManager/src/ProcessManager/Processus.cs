using System.Text.Json;

namespace ProcessManager;

/// <summary>Processus portant un état typé, sérialisé en JSON dans l'instance.</summary>
public abstract class Processus<TEtat> : ProcessusBase where TEtat : new()
{
    protected TEtat Etat { get; private set; } = new();

    internal override string? SerialiserEtat() => JsonSerializer.Serialize(Etat);

    internal override void RestaurerEtat(string? json) =>
        Etat = string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<TEtat>(json) ?? new();
}
