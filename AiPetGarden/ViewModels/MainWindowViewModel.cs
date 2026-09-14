using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AiPetGarden.Services;
using System.IO;

namespace AiPetGarden.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly PetAssetLoader _assetLoader = new();
    private string _statusText = "Ready to scan Codex pets.";
    private bool _isBusy;

    public MainWindowViewModel()
    {
        PetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "pets");
    }

    public ObservableCollection<PetAssetRowViewModel> Pets { get; } = [];
    public ObservableCollection<string> Issues { get; } = [];
    public string PetRoot { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Scanning pet assets (read-only)…";
        try
        {
            var result = await _assetLoader.ScanAsync(PetRoot, CancellationToken.None);
            Pets.Clear();
            foreach (var asset in result.Assets) Pets.Add(new PetAssetRowViewModel(asset));
            Issues.Clear();
            foreach (var issue in result.Issues) Issues.Add($"{issue.Path}: {issue.Message}");
            StatusText = $"Found {Pets.Count} pet asset(s); {Issues.Count} issue(s).";
        }
        catch (Exception exception)
        {
            Issues.Add(exception.Message);
            StatusText = "Pet scan failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
