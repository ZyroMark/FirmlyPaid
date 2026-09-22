using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Clients;
using FirmlyPaid.Shared.Configuration;
using FirmlyPaid.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FirmlyPaid.Shared.Hosting;

/// <summary>
/// The pieces a service opts into: hashing for ID numbers and PINs, and the client for
/// the Matching service. Not in the shared defaults, because a service that needs none of
/// them should not be made to carry a pepper it never uses.
/// </summary>
public static class SecurityRegistration
{
    /// <summary>The environment setting holding the ID number pepper (rule 10.4).</summary>
    public const string IdPepperSetting = "FIRMLYPAID_ID_PEPPER";

    /// <summary>
    /// Registers the keyed ID number hasher. Missing or short peppers stop the service at
    /// startup: quietly hashing with a weak pepper would be worse than not starting.
    /// </summary>
    public static WebApplicationBuilder AddFirmlyPaidIdentityHashing(this WebApplicationBuilder builder)
    {
        var pepper = builder.Configuration[IdPepperSetting];

        if (string.IsNullOrWhiteSpace(pepper))
        {
            throw new InvalidOperationException(
                $"{IdPepperSetting} is not set. It must be at least 32 characters and come from the " +
                "environment or user-secrets, never from source control (rule 10.15).");
        }

        builder.Services.AddSingleton<IIdentityHasher>(new HmacIdentityHasher(pepper));

        return builder;
    }

    /// <summary>The environment setting holding the bank callback secret.</summary>
    public const string BankCallbackSecretSetting = "FIRMLYPAID_BANK_CALLBACK_SECRET";

    /// <summary>
    /// Registers the checker for signed bank callbacks. A missing secret stops the service
    /// at startup: accepting unsigned confirmations would let anyone attach someone else's
    /// bank account to their own profile.
    /// </summary>
    public static WebApplicationBuilder AddFirmlyPaidBankCallbackSignature(this WebApplicationBuilder builder)
    {
        var secret = builder.Configuration[BankCallbackSecretSetting];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"{BankCallbackSecretSetting} is not set. It must be at least 32 characters and come from " +
                "the environment or user-secrets, never from source control (rule 10.15).");
        }

        builder.Services.AddSingleton(new BankCallbackSignature(secret));

        return builder;
    }

    /// <summary>Registers Argon2id PIN hashing (rule 10.6).</summary>
    public static WebApplicationBuilder AddFirmlyPaidPinHashing(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IPinHasher, Argon2idPinHasher>();

        return builder;
    }

    /// <summary>
    /// Registers the HTTP client for the Matching service, pointed at
    /// FirmlyPaid:Services:MatchingBaseUrl.
    /// </summary>
    public static WebApplicationBuilder AddFirmlyPaidMatchingClient(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddHttpClient<IMatchingClient, MatchingClient>(MatchingClient.HttpClientName, (provider, http) =>
            {
                var options = provider.GetRequiredService<IOptions<FirmlyPaidOptions>>().Value;
                http.BaseAddress = new Uri(options.Services.MatchingBaseUrl);

                // A customer is standing at a till. Waiting longer than this helps nobody.
                http.Timeout = TimeSpan.FromSeconds(10);
            });

        return builder;
    }
}
