namespace RagLab.Core.Models;

public record ConversationTurn(
    string UserMessage,
    string AssistantResponse,
    DateTimeOffset Timestamp)
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
}
