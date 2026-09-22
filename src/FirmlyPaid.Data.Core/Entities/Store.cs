namespace FirmlyPaid.Data.Core.Entities;

/// <summary>One branch of a merchant. Terminals belong to a store, not to a merchant.</summary>
public class Store
{
    public Guid StoreId { get; set; } = Guid.NewGuid();

    public Guid MerchantId { get; set; }

    public Merchant? Merchant { get; set; }

    public required string Name { get; set; }

    public required string Address { get; set; }

    /// <summary>Suburb or region, used to group sales on the merchant dashboard.</summary>
    public required string Area { get; set; }

    public ICollection<Terminal> Terminals { get; set; } = [];
}
