using System.Security.Cryptography;
using System.Text;
using FirmlyPaid.Shared.Abstractions;
using Konscious.Security.Cryptography;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// Hashes customer PINs with Argon2id (rule 10.6). A PIN is four to six digits, so the
/// whole space can be walked in moments with a fast hash; the cost parameters below are
/// what stop a stolen database becoming a list of working PINs.
/// </summary>
/// <remarks>
/// Parameters follow the OWASP minimum for Argon2id: 19 MiB of memory, 2 passes, 2 lanes.
/// They are written into every stored hash, so raising them later does not invalidate
/// PINs that were hashed with the old settings.
/// </remarks>
public sealed class Argon2idPinHasher : IPinHasher
{
    private const int SaltLengthBytes = 16;
    private const int HashLengthBytes = 32;

    private const int DefaultMemoryKib = 19 * 1024;
    private const int DefaultIterations = 2;
    private const int DefaultParallelism = 2;

    public string Hash(string pin)
    {
        PinRules.EnsureValid(pin);

        var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        var hash = Derive(pin, salt, DefaultMemoryKib, DefaultIterations, DefaultParallelism);

        // The PHC string format: the parameters travel with the hash.
        return string.Concat(
            "$argon2id$v=19$m=", DefaultMemoryKib.ToString(),
            ",t=", DefaultIterations.ToString(),
            ",p=", DefaultParallelism.ToString(),
            "$", Convert.ToBase64String(salt),
            "$", Convert.ToBase64String(hash));
    }

    public bool Verify(string pin, string storedHash)
    {
        // A wrong shaped PIN can never match, and must not cost a full Argon2 pass.
        if (!PinRules.IsValid(pin) || !TryParse(storedHash, out var parsed))
        {
            return false;
        }

        var candidate = Derive(pin, parsed.Salt, parsed.MemoryKib, parsed.Iterations, parsed.Parallelism);

        return CryptographicOperations.FixedTimeEquals(candidate, parsed.Hash);
    }

    private static byte[] Derive(string pin, byte[] salt, int memoryKib, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(pin))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon2.GetBytes(HashLengthBytes);
    }

    private sealed record ParsedHash(byte[] Salt, byte[] Hash, int MemoryKib, int Iterations, int Parallelism);

    /// <summary>Reads back a stored hash. A hash we cannot read is a hash that cannot match.</summary>
    private static bool TryParse(string storedHash, out ParsedHash parsed)
    {
        parsed = null!;

        // "", "argon2id", "v=19", "m=19456,t=2,p=2", salt, hash
        var parts = storedHash.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id")
        {
            return false;
        }

        var settings = parts[3].Split(',');
        if (settings.Length != 3)
        {
            return false;
        }

        if (!TryReadSetting(settings[0], "m=", out var memoryKib) ||
            !TryReadSetting(settings[1], "t=", out var iterations) ||
            !TryReadSetting(settings[2], "p=", out var parallelism))
        {
            return false;
        }

        try
        {
            parsed = new ParsedHash(
                Convert.FromBase64String(parts[4]),
                Convert.FromBase64String(parts[5]),
                memoryKib,
                iterations,
                parallelism);
        }
        catch (FormatException)
        {
            return false;
        }

        return true;
    }

    private static bool TryReadSetting(string setting, string prefix, out int value)
    {
        value = 0;

        return setting.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(setting.AsSpan(prefix.Length), out value)
            && value > 0;
    }
}
