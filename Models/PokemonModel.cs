namespace Pokemon.Models;

public class PokemonSummary
{
    public string Name { get; set; } = null!;
    public string? SpeciesName { get; set; }
    public string? ImageUrl { get; set; }
}

public class PokemonDetails
{
    public string Name { get; set; } = null!;
    public string? SpeciesName { get; set; }
    public string? ImageUrl { get; set; }
    public int Height { get; set; }
    public int Weight { get; set; }
    public IEnumerable<string> Abilities { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Types { get; set; } = Array.Empty<string>();
}

public class SmtpSettings
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public string User { get; set; } = null!;
    public string Pass { get; set; } = null!;
    public string From { get; set; } = null!;
}