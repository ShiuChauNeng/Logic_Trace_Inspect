using LegacySystem.Services;

namespace LegacySystem.Data;

public sealed class OrderRepository
{
    private readonly List<Order> _orders =
    [
        new Order { Id = 1001, OwnerUserId = 7, Status = OrderStatus.Pending }
    ];

    public Order? Find(int id) => _orders.SingleOrDefault(order => order.Id == id);

    public void Update(Order order)
    {
        // The real application persists the changed entity here.
    }

    public IReadOnlyList<Order> GetAll() => _orders;
}
