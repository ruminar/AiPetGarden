using AiPetGarden.Models;

namespace AiPetGarden.ViewModels;

public sealed class PetAssetRowViewModel
{
    public PetAssetRowViewModel(PetAsset asset)
    {
        Asset = asset;
        AssetId = asset.AssetId;
        DisplayName = asset.DisplayName;
        MetadataPath = asset.MetadataPath;
        SpriteCount = asset.SpritePaths.Count;
    }

    public PetAsset Asset { get; }
    public string AssetId { get; }
    public string DisplayName { get; }
    public string MetadataPath { get; }
    public int SpriteCount { get; }
    public string DisplayStatus => Asset.SpriteDefinition is null ? "表示定義なし" : "表示可能";
}
