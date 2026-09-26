using LegacySystem.Services;

namespace LegacySystem.Forms;

public sealed class OrderForm
{
    private readonly OrderService _service = new();

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        var orderId = GetSelectedOrderId();
        _service.Cancel(orderId, CurrentUser.Id);
        RefreshOrderList();
    }

    private void RefreshOrderList()
    {
        OrdersGrid.DataSource = _service.GetOrders();
        OrdersGrid.Refresh();
    }

    private int GetSelectedOrderId() => 1001;
    private User CurrentUser { get; } = new();
    private Grid OrdersGrid { get; } = new();
}

internal sealed class User { public int Id => 7; }
internal sealed class Grid { public object? DataSource { get; set; } public void Refresh() { } }
