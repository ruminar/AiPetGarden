using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AiPetGarden.Models;

namespace AiPetGarden.Views;

public partial class SettingsWindow : Window
{
    private readonly PetProfile _profile;

    public SettingsWindow(PetProfile profile)
    {
        InitializeComponent();
        _profile = profile;
        Title = $"{GetDisplayName(profile)} - 設定";
        DisplayNameTextBox.Text = profile.DisplayName;
        SystemPromptTextBox.Text = profile.Persona.SystemPrompt;
        GreetingTextBox.Text = profile.Persona.Greeting;
        ProjectNameTextBox.Text = profile.ProjectRef.Name;
        ProjectUrlTextBox.Text = profile.ProjectRef.Url;
        BackendTypeComboBox.Text = profile.Backend.Type;
        ModelTextBox.Text = profile.Backend.Model;
        RelayEnabledCheckBox.IsChecked = profile.Relay.Enabled;
        TopMostCheckBox.IsChecked = profile.View.TopMost;
        ScaleTextBox.Text = profile.View.Scale.ToString(CultureInfo.CurrentCulture);
    }

    public PetProfile? SavedProfile { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var projectUrl = ProjectUrlTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(projectUrl) &&
            (!Uri.TryCreate(projectUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
        {
            ShowValidationError("Project URLには http または https のURLを指定してください。", ProjectUrlTextBox);
            return;
        }

        if (!double.TryParse(ScaleTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var scale) ||
            !double.IsFinite(scale) || scale <= 0)
        {
            ShowValidationError("Scaleには0より大きい数値を指定してください。", ScaleTextBox);
            return;
        }

        SavedProfile = _profile with
        {
            DisplayName = DisplayNameTextBox.Text.Trim(),
            Persona = new PetPersona
            {
                SystemPrompt = SystemPromptTextBox.Text,
                Greeting = GreetingTextBox.Text
            },
            ProjectRef = new PetProjectRef
            {
                Name = ProjectNameTextBox.Text.Trim(),
                Url = projectUrl
            },
            Backend = _profile.Backend with
            {
                Type = BackendTypeComboBox.Text.Trim(),
                Model = ModelTextBox.Text.Trim()
            },
            Relay = _profile.Relay with { Enabled = RelayEnabledCheckBox.IsChecked == true },
            View = _profile.View with
            {
                Scale = scale,
                TopMost = TopMostCheckBox.IsChecked == true
            }
        };
        DialogResult = true;
    }

    private void ShowValidationError(string message, Control control)
    {
        MessageBox.Show(this, message, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        control.Focus();
    }

    private static string GetDisplayName(PetProfile profile) =>
        string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.PetId : profile.DisplayName;
}
