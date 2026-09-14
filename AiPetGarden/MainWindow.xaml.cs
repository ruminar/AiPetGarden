using System.IO;
using System.Windows;
using System.Windows.Input;
using AiPetGarden.Models;
using AiPetGarden.Services;
using AiPetGarden.ViewModels;
using AiPetGarden.Views;

namespace AiPetGarden;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly ProfileRepository _profileRepository = new();
    private readonly Dictionary<string, PetWindow> _petWindows = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.ReloadAsync();
        foreach (var pet in _viewModel.Pets.Where(pet => pet.Asset.SpriteDefinition is not null))
        {
            await ShowPetAsync(pet);
        }
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e) => await _viewModel.ReloadAsync();

    private async void ShowPetButton_Click(object sender, RoutedEventArgs e) => await ShowSelectedPetAsync();

    private async void PetList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => await ShowSelectedPetAsync();

    private async Task ShowSelectedPetAsync()
    {
        if (PetList.SelectedItem is not PetAssetRowViewModel selected) return;
        await ShowPetAsync(selected);
    }

    private async Task ShowPetAsync(PetAssetRowViewModel selected)
    {
        if (selected.Asset.SpriteDefinition is null)
        {
            MessageBox.Show(this, "このペットには対応可能な表示定義がありません。", "AI Pet Garden",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_petWindows.TryGetValue(selected.MetadataPath, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        PetProfile? profile = null;
        try
        {
            profile = await _profileRepository.LoadAsync(selected.AssetId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this,
                $"保存済みの位置を読み込めなかったため、初期位置で表示します。\n\n{exception.Message}",
                "位置の復元エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // A quick second click can finish after the asynchronous profile read.
        if (_petWindows.TryGetValue(selected.MetadataPath, out existing))
        {
            existing.Activate();
            return;
        }

        try
        {
            var petWindow = new PetWindow(selected.Asset) { Owner = this };
            petWindow.Closed += (_, _) => _petWindows.Remove(selected.MetadataPath);
            petWindow.PositionCommitted += async (_, _) => await SavePetViewAsync(selected, petWindow);
            _petWindows.Add(selected.MetadataPath, petWindow);
            petWindow.Show();
            petWindow.ApplyInitialView(profile?.View, _petWindows.Count - 1);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "ペット表示エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SavePetViewAsync(PetAssetRowViewModel pet, PetWindow window)
    {
        try
        {
            var view = PetViewSettings.CreateDefault(window.Left, window.Top) with
            {
                TopMost = window.Topmost
            };
            await _profileRepository.SaveViewAsync(pet.AssetId, pet.Asset.AssetId, view);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"ペットの位置を保存できませんでした。\n\n{exception.Message}",
                "位置の保存エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
