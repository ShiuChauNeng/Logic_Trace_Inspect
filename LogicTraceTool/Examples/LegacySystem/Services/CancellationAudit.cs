namespace LegacySystem.Services;

public sealed class CancellationAudit
{
    private readonly List<string> _entries = [];

    public void Write(int orderId, int userId, DateTime cancelledAt)
    {
        var entry = $"Order {orderId} cancelled by {userId} at {cancelledAt:O}";
        _entries.Add(entry);
    }
}
