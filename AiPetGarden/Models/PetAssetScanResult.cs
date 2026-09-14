namespace AiPetGarden.Models;

public sealed record PetAssetScanIssue(string Path, string Message);

public sealed record PetAssetScanResult(
    string PetRoot,
    IReadOnlyList<PetAsset> Assets,
    IReadOnlyList<PetAssetScanIssue> Issues);
