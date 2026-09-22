namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// Hashes customer PINs with a deliberately slow algorithm (rule 10.6).
/// PINs arrive encrypted from the terminal and are hashed before storage.
/// </summary>
public interface IPinHasher
{
    string Hash(string pin);

    bool Verify(string pin, string storedHash);
}
