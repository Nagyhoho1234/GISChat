namespace GISChat.Models;

public enum MessageRole
{
    User,
    Assistant,
    System
}

public class ChatMessage
{
    public MessageRole Role { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? CodeBlock { get; set; }
    public bool IsExecuting { get; set; }
    public string? ExecutionResult { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public ChatMessage() { }

    public ChatMessage(MessageRole role, string text)
    {
        Role = role;
        Text = text;
    }

    public bool IsUser => Role == MessageRole.User;
    public bool HasCode => !string.IsNullOrEmpty(CodeBlock);
    public bool HasResult => !string.IsNullOrEmpty(ExecutionResult);
    public bool IsResultSuccess => HasResult && !ExecutionResult!.StartsWith("Error");
    public bool IsResultError => HasResult && ExecutionResult!.StartsWith("Error");
}
