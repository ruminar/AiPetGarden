namespace AiPetGarden.Models;

public sealed record PetProfile(
    string PetId,
    string PetAssetId,
    PetViewSettings View);

public sealed record PetViewSettings(
    string LifeState,
    double X,
    double Y,
    double Scale,
    bool TopMost)
{
    public static PetViewSettings CreateDefault(double x, double y) =>
        new("Awake", x, y, 1.0, true);
}
