using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AiPetGarden.Models;

namespace AiPetGarden.Views;

public partial class PetWindow : Window
{
    public event EventHandler? PositionCommitted;
    public event EventHandler? SettingsRequested;
    public event EventHandler<PetLifeStateChangeRequestedEventArgs>? LifeStateChangeRequested;

    public PetLifeState LifeState { get; private set; } = PetLifeState.Awake;

    public PetWindow(PetAsset asset)
    {
        InitializeComponent();
        Title = asset.DisplayName;
        PetImage.Source = LoadNeutralFrame(asset);
    }

    public void ApplyInitialView(PetViewSettings? view, int cascadeIndex)
    {
        var scale = view?.Scale ?? 1.0;
        PetImage.LayoutTransform = new ScaleTransform(scale, scale);
        Topmost = view?.TopMost ?? true;
        ApplyLifeState(view?.LifeState ?? PetLifeState.Awake);
        UpdateLayout();

        var windowWidth = Math.Max(ActualWidth, 1);
        var windowHeight = Math.Max(ActualHeight, 1);
        if (view is null)
        {
            const double margin = 24;
            var offset = cascadeIndex * 28;
            Left = SystemParameters.WorkArea.Right - windowWidth - margin - offset;
            Top = SystemParameters.WorkArea.Bottom - windowHeight - margin - offset;
            return;
        }

        // Keep at least part of the pet inside the current virtual desktop after monitor changes.
        const double minimumVisible = 48;
        var minimumLeft = SystemParameters.VirtualScreenLeft - windowWidth + minimumVisible;
        var maximumLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - minimumVisible;
        var minimumTop = SystemParameters.VirtualScreenTop - windowHeight + minimumVisible;
        var maximumTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - minimumVisible;
        Left = Math.Clamp(view.X, minimumLeft, maximumLeft);
        Top = Math.Clamp(view.Y, minimumTop, maximumTop);
    }

    public void ApplyLifeState(PetLifeState state)
    {
        if (state == PetLifeState.Hidden)
        {
            throw new ArgumentException("A hidden pet must not have an open window.", nameof(state));
        }

        LifeState = state;
        var isSleeping = state == PetLifeState.Sleeping;
        PetImage.Opacity = isSleeping ? 0.76 : 1.0;
        SleepIndicator.Visibility = isSleeping ? Visibility.Visible : Visibility.Collapsed;
        WakeMenuItem.Visibility = isSleeping ? Visibility.Visible : Visibility.Collapsed;
        SleepMenuItem.Visibility = isSleeping ? Visibility.Collapsed : Visibility.Visible;
    }

    public PetViewSettings CaptureView() => new(
        LifeState,
        Left,
        Top,
        PetImage.LayoutTransform is ScaleTransform scale ? scale.ScaleX : 1.0,
        Topmost);

    public void ApplyDisplayName(string displayName) => Title = displayName;

    private static BitmapSource LoadNeutralFrame(PetAsset asset)
    {
        var definition = asset.SpriteDefinition
            ?? throw new InvalidOperationException("This pet has no supported sprite definition.");

        var spriteSheet = new BitmapImage();
        spriteSheet.BeginInit();
        spriteSheet.CacheOption = BitmapCacheOption.OnLoad;
        spriteSheet.UriSource = new Uri(definition.SpriteSheetPath, UriKind.Absolute);
        spriteSheet.EndInit();
        spriteSheet.Freeze();

        var sourceRect = new Int32Rect(
            definition.NeutralColumnIndex * definition.CellWidth,
            definition.NeutralRowIndex * definition.CellHeight,
            definition.CellWidth,
            definition.CellHeight);
        if (sourceRect.X + sourceRect.Width > spriteSheet.PixelWidth ||
            sourceRect.Y + sourceRect.Height > spriteSheet.PixelHeight)
        {
            throw new InvalidDataException("The neutral frame lies outside the spritesheet image.");
        }

        var frame = new CroppedBitmap(spriteSheet, sourceRect);
        frame.Freeze();
        return frame;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        PositionCommitted?.Invoke(this, EventArgs.Empty);
    }

    private void WakeMenuItem_Click(object sender, RoutedEventArgs e) =>
        LifeStateChangeRequested?.Invoke(this, new PetLifeStateChangeRequestedEventArgs(PetLifeState.Awake));

    private void SleepMenuItem_Click(object sender, RoutedEventArgs e) =>
        LifeStateChangeRequested?.Invoke(this, new PetLifeStateChangeRequestedEventArgs(PetLifeState.Sleeping));

    private void HideMenuItem_Click(object sender, RoutedEventArgs e) =>
        LifeStateChangeRequested?.Invoke(this, new PetLifeStateChangeRequestedEventArgs(PetLifeState.Hidden));

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);
}

public sealed class PetLifeStateChangeRequestedEventArgs(PetLifeState requestedState) : EventArgs
{
    public PetLifeState RequestedState { get; } = requestedState;
}
