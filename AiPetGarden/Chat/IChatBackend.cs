using AiPetGarden.Models;

namespace AiPetGarden.Chat;

public interface IChatBackend
{
    string BackendId { get; }

    Task<ChatBackendResult> SendAsync(ChatRequest request, CancellationToken cancellationToken);
}
