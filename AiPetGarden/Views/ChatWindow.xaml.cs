using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AiPetGarden.Chat;
using AiPetGarden.Models;

namespace AiPetGarden.Views;

public partial class ChatWindow : Window
{
    private readonly ChatService _chatService;
    private readonly ObservableCollection<ChatMessageRow> _messages = [];
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    public ChatWindow(ChatService chatService)
    {
        InitializeComponent();
        _chatService = chatService;
        MessageList.ItemsSource = _messages;
        foreach (var message in chatService.Messages) AddMessage(message);
        _chatService.MessageAdded += ChatService_MessageAdded;
        _chatService.ActivityStateChanged += ChatService_ActivityStateChanged;
        ApplyProfile(chatService.Profile);
        Closed += ChatWindow_Closed;
        Loaded += (_, _) => InputTextBox.Focus();
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? ProjectRequested;

    public void ApplyProfile(PetProfile profile)
    {
        var displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.PetId : profile.DisplayName;
        Title = $"{displayName} - チャット";
        PetNameTextBlock.Text = displayName;
        BackendTextBlock.Text = $"Backend: {_chatService.BackendId} / Model: {GetModelName(profile)}";
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        e.Handled = true;
        await SendAsync();
    }

    private async Task SendAsync()
    {
        var input = InputTextBox.Text.Trim();
        if (input.Length == 0 || !SendButton.IsEnabled) return;

        InputTextBox.Clear();
        SendButton.IsEnabled = false;
        InputTextBox.IsEnabled = false;
        try
        {
            await _chatService.SendHumanAsync(input, _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                SendButton.IsEnabled = true;
                InputTextBox.IsEnabled = true;
                InputTextBox.Focus();
            }
        }
    }

    private void ChatService_MessageAdded(ChatMessage message)
    {
        Dispatcher.InvokeAsync(() => AddMessage(message));
    }

    private void ChatService_ActivityStateChanged(PetActivityState state)
    {
        Dispatcher.InvokeAsync(() => StatusTextBlock.Text = state switch
        {
            PetActivityState.Thinking => "考え中…",
            PetActivityState.Talking => "応答を受信しました",
            PetActivityState.Error => "直近の送信でエラーが発生しました",
            _ => "待機中（会話履歴はまだ実行中のみ保持）"
        });
    }

    private void AddMessage(ChatMessage message)
    {
        var displayName = string.IsNullOrWhiteSpace(_chatService.Profile.DisplayName)
            ? _chatService.Profile.PetId
            : _chatService.Profile.DisplayName;
        var sender = message.Origin switch
        {
            MessageOrigin.Human => "あなた",
            MessageOrigin.Assistant => displayName,
            MessageOrigin.RelayInput => "Relay受信",
            MessageOrigin.RelayResponse => displayName,
            _ => "システム"
        };
        _messages.Add(new ChatMessageRow(sender, message.Text, message.CreatedAt.ToLocalTime().ToString("HH:mm:ss")));
        MessageList.ScrollIntoView(_messages[^1]);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void ProjectButton_Click(object sender, RoutedEventArgs e) =>
        ProjectRequested?.Invoke(this, EventArgs.Empty);

    private void ChatWindow_Closed(object? sender, EventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
        _chatService.MessageAdded -= ChatService_MessageAdded;
        _chatService.ActivityStateChanged -= ChatService_ActivityStateChanged;
    }

    private static string GetModelName(PetProfile profile) =>
        string.IsNullOrWhiteSpace(profile.Backend.Model) ? "未指定" : profile.Backend.Model;
}

public sealed record ChatMessageRow(string Sender, string Text, string TimeText);
