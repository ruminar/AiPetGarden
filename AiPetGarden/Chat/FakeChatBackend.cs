using AiPetGarden.Models;

namespace AiPetGarden.Chat;

public sealed class FakeChatBackend : IChatBackend
{
    public string BackendId => "Fake";

    public async Task<ChatBackendResult> SendAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(450, cancellationToken);
        var personaStatus = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? "System Prompt: 未設定"
            : $"System Prompt: 適用済み（{request.SystemPrompt.Length}文字）";
        var model = string.IsNullOrWhiteSpace(request.Model) ? "未指定" : request.Model;
        return new ChatBackendResult(
            $"「{request.CurrentInput}」を受け取ったよ。\n\n[{personaStatus} / Model: {model}]\nこれはFake Backendによる動作確認用の応答です。");
    }
}
