using System.Windows;
using System.Windows.Input;
using AiPetGarden.ViewModels;
using AiPetGarden.Views;

namespace AiPetGarden;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly Dictionary<string, PetWindow> _petWindows = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.ReloadAsync();
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e) => await _viewModel.ReloadAsync();

    private void ShowPetButton_Click(object sender, RoutedEventArgs e) => ShowSelectedPet();

    private void PetList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ShowSelectedPet();

    private void ShowSelectedPet()
    {
        if (PetList.SelectedItem is not PetAssetRowViewModel selected) return;
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

        try
        {
            var petWindow = new PetWindow(selected.Asset) { Owner = this };
            petWindow.Closed += (_, _) => _petWindows.Remove(selected.MetadataPath);
            _petWindows.Add(selected.MetadataPath, petWindow);
            petWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "ペット表示エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
