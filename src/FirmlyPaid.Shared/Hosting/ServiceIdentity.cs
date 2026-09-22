namespace FirmlyPaid.Shared.Hosting;

/// <summary>Who this process is, for /health responses and log scopes.</summary>
public sealed record ServiceIdentity(string Name, string Version);
