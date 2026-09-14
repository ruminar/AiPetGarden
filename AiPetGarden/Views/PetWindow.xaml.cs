using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AiPetGarden.Models;

namespace AiPetGarden.Views;

public partial class PetWindow : Window
{
    public PetWindow(PetAsset asset)
    {
        InitializeComponent();
        Title = asset.DisplayName;
        PetImage.Source = LoadNeutralFrame(asset);
    }

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
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e) => Close();
}
