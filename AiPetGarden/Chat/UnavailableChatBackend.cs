using AiPetGarden.Models;

namespace AiPetGarden.Chat;

public sealed class UnavailableChatBackend(string backendId) : IChatBackend
{
    public string BackendId { get; } = backendId;

    public Task<ChatBackendResult> SendAsync(ChatRequest request, CancellationToken cancellationToken) =>
        Task.FromException<ChatBackendResult>(
            new InvalidOperationException($"Backend '{BackendId}' はまだ実装されていません。設定で Fake を選択してください。"));
}
