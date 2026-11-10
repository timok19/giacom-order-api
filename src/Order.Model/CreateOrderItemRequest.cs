using System;

namespace Order.Model;

public class CreateOrderItemRequest
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
}
