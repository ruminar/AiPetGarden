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

    private async void WakePetButton_Click(object sender, RoutedEventArgs e) =>
        await ChangeSelectedLifeStateAsync(PetLifeState.Awake);

    private async void SleepPetButton_Click(object sender, RoutedEventArgs e) =>
        await ChangeSelectedLifeStateAsync(PetLifeState.Sleeping);

    private async void HidePetButton_Click(object sender, RoutedEventArgs e) =>
        await ChangeSelectedLifeStateAsync(PetLifeState.Hidden);

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (PetList.SelectedItem is PetAssetRowViewModel selected) await OpenSettingsAsync(selected);
    }

    private async void PetList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => await ShowSelectedPetAsync();

    private async Task ShowSelectedPetAsync()
    {
        if (PetList.SelectedItem is not PetAssetRowViewModel selected) return;
        await ShowPetAsync(selected, selected.LifeState == PetLifeState.Hidden ? PetLifeState.Awake : null);
    }

    private async Task ShowPetAsync(PetAssetRowViewModel selected, PetLifeState? requestedState = null)
    {
        if (selected.Asset.SpriteDefinition is null)
        {
            MessageBox.Show(this, "このペットには対応可能な表示定義がありません。", "AI Pet Garden",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_petWindows.TryGetValue(selected.MetadataPath, out var existing))
        {
            if (requestedState is not null && requestedState != existing.LifeState)
            {
                await ChangePetLifeStateAsync(selected, requestedState.Value);
            }
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

        if (profile is not null) selected.ApplyProfile(profile);

        var lifeState = requestedState ?? profile?.View.LifeState ?? PetLifeState.Awake;
        selected.LifeState = lifeState;
        if (lifeState == PetLifeState.Hidden) return;

        // A quick second click can finish after the asynchronous profile read.
        if (_petWindows.TryGetValue(selected.MetadataPath, out existing))
        {
            existing.Activate();
            return;
        }

        try
        {
            var petWindow = new PetWindow(selected.Asset) { Owner = this };
            petWindow.ApplyDisplayName(selected.DisplayName);
            petWindow.Closed += (_, _) => _petWindows.Remove(selected.MetadataPath);
            petWindow.PositionCommitted += async (_, _) => await SavePetViewAsync(selected, petWindow);
            petWindow.LifeStateChangeRequested += async (_, args) =>
                await ChangePetLifeStateAsync(selected, args.RequestedState);
            petWindow.SettingsRequested += async (_, _) => await OpenSettingsAsync(selected);
            _petWindows.Add(selected.MetadataPath, petWindow);
            petWindow.Show();
            var initialView = profile?.View is { } savedView ? savedView with { LifeState = lifeState } : null;
            petWindow.ApplyInitialView(initialView, _petWindows.Count - 1);
            if (requestedState is not null) await SavePetViewAsync(selected, petWindow);
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
            await _profileRepository.SaveViewAsync(
                pet.AssetId, pet.Asset.AssetId, pet.Asset.DisplayName, window.CaptureView());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"ペットの位置を保存できませんでした。\n\n{exception.Message}",
                "位置の保存エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ChangeSelectedLifeStateAsync(PetLifeState lifeState)
    {
        if (PetList.SelectedItem is not PetAssetRowViewModel selected) return;
        await ChangePetLifeStateAsync(selected, lifeState);
    }

    private async Task ChangePetLifeStateAsync(PetAssetRowViewModel pet, PetLifeState lifeState)
    {
        if (!_petWindows.TryGetValue(pet.MetadataPath, out var window))
        {
            if (lifeState != PetLifeState.Hidden)
            {
                await ShowPetAsync(pet, lifeState);
                return;
            }

            try
            {
                var profile = await _profileRepository.LoadAsync(pet.AssetId);
                var view = (profile?.View ?? PetViewSettings.CreateDefault(0, 0)) with { LifeState = lifeState };
                await _profileRepository.SaveViewAsync(
                    pet.AssetId, pet.Asset.AssetId, pet.Asset.DisplayName, view);
                pet.LifeState = lifeState;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                ShowLifeStateError(exception);
            }
            return;
        }

        try
        {
            var view = window.CaptureView() with { LifeState = lifeState };
            await _profileRepository.SaveViewAsync(
                pet.AssetId, pet.Asset.AssetId, pet.Asset.DisplayName, view);
            pet.LifeState = lifeState;
            if (lifeState == PetLifeState.Hidden)
            {
                window.Close();
            }
            else
            {
                window.ApplyLifeState(lifeState);
                window.Activate();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowLifeStateError(exception);
        }
    }

    private void ShowLifeStateError(Exception exception) =>
        MessageBox.Show(this, $"ペットの状態を変更できませんでした。\n\n{exception.Message}",
            "状態変更エラー", MessageBoxButton.OK, MessageBoxImage.Warning);

    private async Task OpenSettingsAsync(PetAssetRowViewModel pet)
    {
        try
        {
            var profile = await _profileRepository.LoadAsync(pet.AssetId);
            if (profile is null)
            {
                var view = _petWindows.TryGetValue(pet.MetadataPath, out var currentWindow)
                    ? currentWindow.CaptureView()
                    : PetViewSettings.CreateDefault(0, 0) with { LifeState = pet.LifeState };
                profile = PetProfile.CreateDefault(pet.Asset, view);
            }

            var settingsWindow = new SettingsWindow(profile) { Owner = this };
            if (settingsWindow.ShowDialog() != true || settingsWindow.SavedProfile is not { } savedProfile) return;

            await _profileRepository.SaveAsync(savedProfile);
            pet.ApplyProfile(savedProfile);
            if (_petWindows.TryGetValue(pet.MetadataPath, out var petWindow))
            {
                petWindow.ApplyDisplayName(pet.DisplayName);
                petWindow.ApplyInitialView(savedProfile.View, _petWindows.Values.ToList().IndexOf(petWindow));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, $"ペット設定を保存できませんでした。\n\n{exception.Message}",
                "設定エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
