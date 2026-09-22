namespace FirmlyPaid.Data.Vault.Entities;

/// <summary>
/// Which bucket a template owner falls in, so a match searches only the customers who
/// share those four ID digits instead of the whole estate (FR-07).
/// </summary>
public class Bucket
{
    public Guid TemplateOwnerId { get; set; }

    public int IdDigits7to10Bucket { get; set; }
}
