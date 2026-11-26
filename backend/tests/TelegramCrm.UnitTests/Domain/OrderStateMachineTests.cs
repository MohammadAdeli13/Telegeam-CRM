using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Enums;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.UnitTests.Domain;

/// <summary>
/// Contract v1.2 §3 — the order status state machine. Every legal edge is asserted, and
/// every illegal edge (including any transition out of a terminal state) must throw.
/// </summary>
public sealed class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    public void TransitionTo_OnALegalEdge_MovesTheOrder(OrderStatus from, OrderStatus to)
    {
        var order = OrderIn(from);

        order.TransitionTo(to);

        Assert.Equal(to, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Pending)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    public void TransitionTo_OnAnIllegalEdge_ThrowsAndLeavesStatusUnchanged(OrderStatus from, OrderStatus to)
    {
        var order = OrderIn(from);

        var ex = Assert.Throws<InvalidOrderStatusTransitionException>(() => order.TransitionTo(to));

        Assert.Equal("invalid_status_transition", ex.Code);
        Assert.Equal(from, ex.From);
        Assert.Equal(to, ex.To);
        Assert.Equal(from, order.Status);
    }

    [Fact]
    public void Delivered_IsTerminal()
    {
        var order = OrderIn(OrderStatus.Delivered);

        foreach (var target in Enum.GetValues<OrderStatus>())
        {
            Assert.Throws<InvalidOrderStatusTransitionException>(() => order.TransitionTo(target));
        }
    }

    [Fact]
    public void Cancelled_IsTerminal()
    {
        var order = OrderIn(OrderStatus.Cancelled);

        foreach (var target in Enum.GetValues<OrderStatus>())
        {
            Assert.Throws<InvalidOrderStatusTransitionException>(() => order.TransitionTo(target));
        }
    }

    [Fact]
    public void EveryTransitionIntoCancelled_RestoresStock()
    {
        // v1.2 fix: Pending -> Cancelled must restore stock too, not only later states.
        Assert.True(Order.RestoresStock(OrderStatus.Cancelled));
        Assert.False(Order.RestoresStock(OrderStatus.Delivered));
        Assert.False(Order.RestoresStock(OrderStatus.Confirmed));
    }

    [Fact]
    public void CanTransition_MatchesTheContractTable()
    {
        Assert.True(Order.CanTransition(OrderStatus.Pending, OrderStatus.Confirmed));
        Assert.True(Order.CanTransition(OrderStatus.Shipped, OrderStatus.Cancelled));
        Assert.False(Order.CanTransition(OrderStatus.Delivered, OrderStatus.Cancelled));
        Assert.False(Order.CanTransition(OrderStatus.Cancelled, OrderStatus.Pending));
    }

    [Fact]
    public void NewOrder_StartsPending()
    {
        Assert.Equal(OrderStatus.Pending, new Order().Status);
    }

    private static Order OrderIn(OrderStatus status)
    {
        var order = new Order();
        foreach (var step in PathTo(status))
        {
            order.TransitionTo(step);
        }

        Assert.Equal(status, order.Status);
        return order;
    }

    private static IEnumerable<OrderStatus> PathTo(OrderStatus status) => status switch
    {
        OrderStatus.Pending => [],
        OrderStatus.Confirmed => [OrderStatus.Confirmed],
        OrderStatus.Preparing => [OrderStatus.Confirmed, OrderStatus.Preparing],
        OrderStatus.Shipped => [OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Shipped],
        OrderStatus.Delivered =>
            [OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Shipped, OrderStatus.Delivered],
        OrderStatus.Cancelled => [OrderStatus.Cancelled],
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
