using System.Security.Cryptography;
using System.Text.Json;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Security;
using Microsoft.Extensions.Logging;

namespace FirmlyPaid.Simulators.Keys;

/// <summary>
/// The development key vault: AES-256-GCM with keys held in a local file that only the
/// owning account can read. It stands in for AWS KMS, and later CloudHSM, behind the same
/// interface, so switching is a configuration change rather than a code change.
/// </summary>
/// <remarks>
/// For a developer laptop only. The key sits on disk next to the data it protects, which
/// is precisely what a hardware security module exists to avoid. Nothing here belongs in
/// production, and the deployment scripts in step 12 select the KMS adapter instead.
/// </remarks>
public sealed class LocalDevFileKeyVault : IKeyVault
{
    private const int KeyLengthBytes = 32;   // AES-256
    private const int NonceLengthBytes = 12; // The size AES-GCM expects
    private const int TagLengthBytes = 16;

    private readonly Dictionary<string, byte[]> _keysByVersion;
    private readonly Lock _gate = new();

    public LocalDevFileKeyVault(string keyFilePath, ILogger<LocalDevFileKeyVault> logger)
    {
        _keysByVersion = LoadOrCreate(keyFilePath, logger);
        CurrentKeyVersion = _keysByVersion.Keys.OrderBy(v => v, StringComparer.Ordinal).Last();
    }

    public string CurrentKeyVersion { get; }

    public Task<ProtectedPayload> ProtectAsync(ReadOnlyMemory<byte> plaintext, CancellationToken ct = default)
    {
        var key = KeyFor(CurrentKeyVersion);

        var nonce = RandomNumberGenerator.GetBytes(NonceLengthBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLengthBytes];

        using (var aes = new AesGcm(key, TagLengthBytes))
        {
            aes.Encrypt(nonce, plaintext.Span, ciphertext, tag);
        }

        // Nonce, then tag, then ciphertext. The caller treats the whole thing as opaque.
        var payload = new byte[NonceLengthBytes + TagLengthBytes + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceLengthBytes);
        ciphertext.CopyTo(payload, NonceLengthBytes + TagLengthBytes);

        return Task.FromResult(new ProtectedPayload(payload, CurrentKeyVersion));
    }

    public Task<byte[]> UnprotectAsync(ProtectedPayload payload, CancellationToken ct = default)
    {
        if (payload.Ciphertext.Length < NonceLengthBytes + TagLengthBytes)
        {
            throw new CryptographicException("The protected payload is too short to be valid.");
        }

        var key = KeyFor(payload.KeyVersion);

        var nonce = payload.Ciphertext.AsSpan(0, NonceLengthBytes);
        var tag = payload.Ciphertext.AsSpan(NonceLengthBytes, TagLengthBytes);
        var ciphertext = payload.Ciphertext.AsSpan(NonceLengthBytes + TagLengthBytes);

        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(key, TagLengthBytes))
        {
            // Throws if the ciphertext was altered: the tag will not verify.
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Task.FromResult(plaintext);
    }

    private byte[] KeyFor(string version)
    {
        lock (_gate)
        {
            return _keysByVersion.TryGetValue(version, out var key)
                ? key
                // Rotating a key must never make old rows unreadable, so this is a fault.
                : throw new CryptographicException($"No key is held for version '{version}'.");
        }
    }

    private static Dictionary<string, byte[]> LoadOrCreate(string keyFilePath, ILogger logger)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(keyFilePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(keyFilePath))
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keyFilePath))
                ?? throw new CryptographicException($"The key file at {keyFilePath} could not be read.");

            return stored.ToDictionary(pair => pair.Key, pair => Convert.FromBase64String(pair.Value));
        }

        logger.LogWarning(
            "No development key file found. Creating one at {KeyFilePath}. This is for local use only.",
            keyFilePath);

        var keys = new Dictionary<string, byte[]> { ["dev-1"] = RandomNumberGenerator.GetBytes(KeyLengthBytes) };

        File.WriteAllText(
            keyFilePath,
            JsonSerializer.Serialize(keys.ToDictionary(p => p.Key, p => Convert.ToBase64String(p.Value))));

        Protect(keyFilePath);

        return keys;
    }

    /// <summary>Takes the key file's permissions down to the owning account only.</summary>
    private static void Protect(string keyFilePath)
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows has no chmod. The file inherits the user profile's permissions,
            // and the deployment guidance is to keep the key outside the repository.
            return;
        }

        File.SetUnixFileMode(keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
