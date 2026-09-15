using AiPetGarden.Models;

namespace AiPetGarden.Chat;

public sealed class ChatService
{
    private readonly List<ChatMessage> _messages = [];
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private PetProfile _profile;
    private IChatBackend _backend;

    public ChatService(PetProfile profile)
    {
        _profile = profile;
        _backend = ChatBackendFactory.Create(profile.Backend.Type);
        if (!string.IsNullOrWhiteSpace(profile.Persona.Greeting))
        {
            AddMessage(MessageOrigin.Assistant, profile.Persona.Greeting);
        }
    }

    public event Action<ChatMessage>? MessageAdded;
    public event Action<PetActivityState>? ActivityStateChanged;

    public IReadOnlyList<ChatMessage> Messages => _messages;
    public PetActivityState ActivityState { get; private set; } = PetActivityState.Idle;
    public string BackendId => _backend.BackendId;
    public PetProfile Profile => _profile;

    public void UpdateProfile(PetProfile profile)
    {
        _profile = profile;
        _backend = ChatBackendFactory.Create(profile.Backend.Type);
    }

    public async Task SendHumanAsync(string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input)) return;
        if (!await _sendLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("このペットは応答処理中です。");
        }

        try
        {
            var conversation = _messages.ToArray();
            AddMessage(MessageOrigin.Human, input);
            SetActivity(PetActivityState.Thinking);
            var request = new ChatRequest(
                _profile.PetId,
                _profile.Persona.SystemPrompt,
                conversation,
                input,
                _profile.Backend.Model,
                ChatRequestOrigin.Human);
            var result = await _backend.SendAsync(request, cancellationToken);
            if (string.IsNullOrWhiteSpace(result.ResponseText))
            {
                throw new InvalidOperationException("Backend returned an empty response.");
            }

            AddMessage(MessageOrigin.Assistant, result.ResponseText);
            SetActivity(PetActivityState.Talking);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddMessage(MessageOrigin.SystemEvent, "送信をキャンセルしました。");
            SetActivity(PetActivityState.Idle);
            return;
        }
        catch (Exception exception)
        {
            AddMessage(MessageOrigin.SystemEvent, $"送信に失敗しました: {exception.Message}");
            SetActivity(PetActivityState.Error);
            return;
        }
        finally
        {
            _sendLock.Release();
        }

        SetActivity(PetActivityState.Idle);
    }

    private void AddMessage(MessageOrigin origin, string text)
    {
        var message = new ChatMessage(Guid.NewGuid(), _profile.PetId, origin, text, DateTimeOffset.Now);
        _messages.Add(message);
        MessageAdded?.Invoke(message);
    }

    private void SetActivity(PetActivityState state)
    {
        ActivityState = state;
        ActivityStateChanged?.Invoke(state);
    }
}
