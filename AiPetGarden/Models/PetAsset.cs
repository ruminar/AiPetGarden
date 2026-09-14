namespace AiPetGarden.Models;

/// <summary>
/// A read-only description of a pet asset discovered under the Codex pet root.
/// </summary>
public sealed record PetAsset(
    string AssetId,
    string DisplayName,
    string DirectoryPath,
    string MetadataPath,
    IReadOnlyList<string> SpritePaths,
    PetSpriteDefinition? SpriteDefinition);
