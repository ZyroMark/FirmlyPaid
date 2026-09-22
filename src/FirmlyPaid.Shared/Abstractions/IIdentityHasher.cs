namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// Turns an ID number into the salted hash we store instead of the number itself
/// (rule 10.4). Keyed and deterministic so enrolment can still spot a duplicate person.
/// </summary>
public interface IIdentityHasher
{
    string HashIdNumber(string idNumber);
}
