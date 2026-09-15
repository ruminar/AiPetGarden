namespace AiPetGarden.Models;

public enum MessageOrigin
{
    Human,
    Assistant,
    RelayInput,
    RelayResponse,
    SystemEvent
}

public enum ChatRequestOrigin
{
    Human,
    Relay
}

public sealed record ChatMessage(
    Guid Id,
    string PetId,
    MessageOrigin Origin,
    string Text,
    DateTimeOffset CreatedAt);

public sealed record ChatRequest(
    string PetId,
    string SystemPrompt,
    IReadOnlyList<ChatMessage> ConversationMessages,
    string CurrentInput,
    string Model,
    ChatRequestOrigin RequestOrigin,
    object? RelayMetadata = null);

public sealed record ChatBackendResult(string ResponseText);
