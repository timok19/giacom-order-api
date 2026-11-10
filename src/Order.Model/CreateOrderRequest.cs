using System;
using System.Collections.Generic;

namespace Order.Model;

public class CreateOrderRequest
{
    public Guid ResellerId { get; set; }

    public Guid CustomerId { get; set; }

    public IEnumerable<CreateOrderItemRequest> Items { get; set; }
}
