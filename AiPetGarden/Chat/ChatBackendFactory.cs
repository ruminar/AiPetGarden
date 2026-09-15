namespace AiPetGarden.Chat;

public static class ChatBackendFactory
{
    public static IChatBackend Create(string? backendType) =>
        string.Equals(backendType, "Fake", StringComparison.OrdinalIgnoreCase)
            ? new FakeChatBackend()
            : new UnavailableChatBackend(string.IsNullOrWhiteSpace(backendType) ? "未設定" : backendType);
}
