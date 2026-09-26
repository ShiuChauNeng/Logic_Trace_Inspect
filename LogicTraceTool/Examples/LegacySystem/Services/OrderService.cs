using LegacySystem.Data;

namespace LegacySystem.Services;

public sealed class OrderService
{
    private readonly OrderRepository _repository = new();
    private readonly CancellationAudit _audit = new();

    public void Cancel(int orderId, int userId)
    {
        var order = _repository.Find(orderId);
        if (order is null)
            throw new InvalidOperationException("Order not found.");

        if (order.Status is OrderStatus.Shipped or OrderStatus.Completed)
            throw new InvalidOperationException("Order cannot be cancelled.");

        if (!CanCancel(userId, order))
            throw new UnauthorizedAccessException("Cancellation denied.");

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        _repository.Update(order);
        _audit.Write(order.Id, userId, order.CancelledAt.Value);
    }

    private static bool CanCancel(int userId, Order order)
    {
        return userId == order.OwnerUserId || userId == 1;
    }

    public IReadOnlyList<Order> GetOrders() => _repository.GetAll();
}

public enum OrderStatus { Pending, Processing, Shipped, Completed, Cancelled }
public sealed class Order
{
    public int Id { get; set; }
    public int OwnerUserId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime? CancelledAt { get; set; }
}
