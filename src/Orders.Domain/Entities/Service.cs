namespace Orders.Domain.Entities;

/// <summary>
/// Reference data: the category a product belongs to, e.g. "Email".
/// </summary>
public sealed class Service
{
    public Service(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }
}
