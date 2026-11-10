namespace Orders.Domain.Entities;

/// <summary>
/// Reference data: a state an order can be in. Status ids are generated per database,
/// so statuses are identified by their name.
/// </summary>
public sealed class OrderStatus
{
    public const string Created = "Created";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";

    public OrderStatus(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public bool Is(string name) => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
}
