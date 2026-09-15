namespace AiPetGarden.Models;

public sealed record PetProfile
{
    public required string PetId { get; init; }
    public required string PetAssetId { get; init; }
    public string DisplayName { get; init; } = "";
    public PetProjectRef ProjectRef { get; init; } = new();
    public PetPersona Persona { get; init; } = new();
    public PetBackendSettings Backend { get; init; } = new();
    public PetRelaySettings Relay { get; init; } = new();
    public required PetViewSettings View { get; init; }

    public static PetProfile CreateDefault(PetAsset asset, PetViewSettings view) => new()
    {
        PetId = asset.AssetId,
        PetAssetId = asset.AssetId,
        DisplayName = asset.DisplayName,
        View = view
    };
}

public sealed record PetProjectRef
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
}

public sealed record PetPersona
{
    public string SystemPrompt { get; init; } = "";
    public string Greeting { get; init; } = "";
}

public sealed record PetBackendSettings
{
    public string Type { get; init; } = "Fake";
    public string Model { get; init; } = "";
    public string SecretKeyRef { get; init; } = "";
}

public sealed record PetRelaySettings
{
    public bool Enabled { get; init; } = true;
}

public sealed record PetViewSettings(
    PetLifeState LifeState,
    double X,
    double Y,
    double Scale,
    bool TopMost)
{
    public static PetViewSettings CreateDefault(double x, double y) =>
        new(PetLifeState.Awake, x, y, 1.0, true);
}
