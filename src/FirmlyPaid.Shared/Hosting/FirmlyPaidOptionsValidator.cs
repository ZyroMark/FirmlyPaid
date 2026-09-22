using Microsoft.Extensions.Options;
using FirmlyPaid.Shared.Configuration;

namespace FirmlyPaid.Shared.Hosting;

/// <summary>
/// Source-generated validator so a bad threshold in appsettings stops the service at
/// startup instead of surfacing as a strange decline at a till.
/// </summary>
[OptionsValidator]
internal sealed partial class FirmlyPaidOptionsValidator : IValidateOptions<FirmlyPaidOptions>;
