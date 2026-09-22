using System.Security.Cryptography;

namespace FirmlyPaid.Matching.Api;

/// <summary>
/// The per-customer transform applied to a vein template before it is stored (rule 10.3).
/// A stored template is never the raw reading: it is the reading shuffled by a permutation
/// derived from that customer's TransformSeedId. If a template ever leaks, the seed is
/// revoked, the customer re-enrols under a new seed, and the leaked copy matches nothing.
/// </summary>
/// <remarks>
/// A seeded permutation is used because it preserves exactly the distance the matching
/// engine measures, so the threshold means the same thing transformed or not. A real
/// deployment uses the vendor's own non-invertible transform behind this same seed, and
/// only this class changes.
///
/// The seed is stored beside the template on purpose. It is a revocation handle, not a
/// second secret: what keeps a stolen template unreadable is the encryption from
/// IKeyVault, and what makes a leak survivable is being able to issue a new seed.
/// </remarks>
public static class CancellableTemplateTransform
{
    /// <summary>A fresh transform for a customer who has no live templates.</summary>
    public static Guid NewSeed() => Guid.NewGuid();

    /// <summary>
    /// Shuffles the template with the permutation belonging to <paramref name="transformSeedId"/>.
    /// Applied to both the stored template and the probe, so like still matches like.
    /// </summary>
    public static byte[] Apply(ReadOnlySpan<byte> template, Guid transformSeedId)
    {
        var order = BuildPermutation(template.Length, transformSeedId);
        var transformed = new byte[template.Length];

        for (var i = 0; i < template.Length; i++)
        {
            transformed[i] = template[order[i]];
        }

        return transformed;
    }

    /// <summary>
    /// Fisher-Yates over a keystream derived from the seed. Deterministic across machines
    /// and .NET versions, which a seeded System.Random is not promised to be.
    /// </summary>
    private static int[] BuildPermutation(int length, Guid transformSeedId)
    {
        var order = new int[length];
        for (var i = 0; i < length; i++)
        {
            order[i] = i;
        }

        var stream = new SeedKeyStream(transformSeedId);

        for (var i = length - 1; i > 0; i--)
        {
            var swapWith = (int)(stream.Next() % (uint)(i + 1));
            (order[i], order[swapWith]) = (order[swapWith], order[i]);
        }

        return order;
    }

    /// <summary>An endless run of numbers from one seed: SHA-256 chained over itself.</summary>
    private sealed class SeedKeyStream
    {
        private byte[] _block;
        private int _offset;

        public SeedKeyStream(Guid seed)
        {
            _block = SHA256.HashData(seed.ToByteArray());
            _offset = 0;
        }

        public uint Next()
        {
            if (_offset + sizeof(uint) > _block.Length)
            {
                _block = SHA256.HashData(_block);
                _offset = 0;
            }

            var value = BitConverter.ToUInt32(_block, _offset);
            _offset += sizeof(uint);
            return value;
        }
    }
}
