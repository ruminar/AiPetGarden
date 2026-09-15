using AiPetGarden.Models;
using System.ComponentModel;

namespace AiPetGarden.ViewModels;

public sealed class PetAssetRowViewModel : INotifyPropertyChanged
{
    private string _displayName;
    private PetLifeState _lifeState = PetLifeState.Awake;

    public PetAssetRowViewModel(PetAsset asset)
    {
        Asset = asset;
        AssetId = asset.AssetId;
        _displayName = asset.DisplayName;
        MetadataPath = asset.MetadataPath;
        SpriteCount = asset.SpritePaths.Count;
    }

    public PetAsset Asset { get; }
    public string AssetId { get; }
    public string DisplayName => _displayName;
    public string MetadataPath { get; }
    public int SpriteCount { get; }
    public PetLifeState LifeState
    {
        get => _lifeState;
        set
        {
            if (_lifeState == value) return;
            _lifeState = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayStatus)));
        }
    }

    public string DisplayStatus => Asset.SpriteDefinition is null
        ? "表示定義なし"
        : LifeState switch
        {
            PetLifeState.Awake => "起きている",
            PetLifeState.Sleeping => "睡眠中",
            PetLifeState.Hidden => "非表示",
            _ => "不明"
        };

    public void ApplyProfile(PetProfile profile)
    {
        var displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? Asset.DisplayName : profile.DisplayName;
        if (!string.Equals(_displayName, displayName, StringComparison.Ordinal))
        {
            _displayName = displayName;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
        LifeState = profile.View.LifeState;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
