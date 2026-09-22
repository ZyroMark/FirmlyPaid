using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Simulators;

/// <summary>
/// The switchboard behind the demo. Every simulator reads from here before it decides what
/// to return, so an operator can force a decline, a fake finger or a Home Affairs outage
/// mid-demonstration without restarting anything. The admin console drives it from step 10.
/// </summary>
/// <remarks>
/// Registered as a singleton and written from request threads, so every setting is behind
/// a lock. The values are small and read far more often than written.
/// </remarks>
public sealed class SimulatorControlState
{
    private readonly Lock _gate = new();

    private VeinScannerMode _veinScanner = VeinScannerMode.Automatic;
    private HomeAffairsOutcome? _homeAffairsOverride;
    private BankPaymentMode _bankPayment = BankPaymentMode.Automatic;
    private AccountConfirmationMode _accountConfirmation = AccountConfirmationMode.AutoApprove;
    private int _veinNoiseAmplitude = 6;
    private TimeSpan _accountConfirmationDelay = TimeSpan.FromSeconds(5);

    /// <summary>What the next finger read should produce.</summary>
    public VeinScannerMode VeinScanner
    {
        get { lock (_gate) { return _veinScanner; } }
        set { lock (_gate) { _veinScanner = value; } }
    }

    /// <summary>Forces every Home Affairs answer. Null means use the roster (part 7).</summary>
    public HomeAffairsOutcome? HomeAffairsOverride
    {
        get { lock (_gate) { return _homeAffairsOverride; } }
        set { lock (_gate) { _homeAffairsOverride = value; } }
    }

    /// <summary>What the fake bank should do with the next payment.</summary>
    public BankPaymentMode BankPayment
    {
        get { lock (_gate) { return _bankPayment; } }
        set { lock (_gate) { _bankPayment = value; } }
    }

    /// <summary>Whether the customer approves or rejects the next account confirmation.</summary>
    public AccountConfirmationMode AccountConfirmation
    {
        get { lock (_gate) { return _accountConfirmation; } }
        set { lock (_gate) { _accountConfirmation = value; } }
    }

    /// <summary>
    /// How much each read differs from the stored pattern, 0 to 255. Real sensors never
    /// return the same bytes twice, so matching must not be a byte comparison. Raising
    /// this past the matcher's tolerance is how a demo shows a genuine near miss.
    /// </summary>
    public int VeinNoiseAmplitude
    {
        get { lock (_gate) { return _veinNoiseAmplitude; } }
        set { lock (_gate) { _veinNoiseAmplitude = Math.Clamp(value, 0, 255); } }
    }

    /// <summary>
    /// How long the customer takes to tap approve in their banking app. Five seconds in a
    /// demo (part 7); tests set it to zero.
    /// </summary>
    public TimeSpan AccountConfirmationDelay
    {
        get { lock (_gate) { return _accountConfirmationDelay; } }
        set { lock (_gate) { _accountConfirmationDelay = value; } }
    }

    /// <summary>Puts every switch back where it started, between demos.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _veinScanner = VeinScannerMode.Automatic;
            _homeAffairsOverride = null;
            _bankPayment = BankPaymentMode.Automatic;
            _accountConfirmation = AccountConfirmationMode.AutoApprove;
            _veinNoiseAmplitude = 6;
            _accountConfirmationDelay = TimeSpan.FromSeconds(5);
        }
    }
}

/// <summary>The buttons on the simulated scanner panel (part 7).</summary>
public enum VeinScannerMode
{
    /// <summary>A good read of whichever test finger was chosen.</summary>
    Automatic = 0,
    GoodRead = 1,
    PoorQuality = 2,

    /// <summary>A fake finger. The scanner reports a liveness failure and returns nothing.</summary>
    FakeFinger = 3,
    NoFinger = 4,
}

public enum BankPaymentMode
{
    /// <summary>Approve when the payer has the money, decline for insufficient funds when not.</summary>
    Automatic = 0,
    AlwaysApprove = 1,
    InsufficientFunds = 2,
    AlwaysDecline = 3,

    /// <summary>The bank never answers, so the payment fails on timeout (FR-10).</summary>
    Timeout = 4,
}

public enum AccountConfirmationMode
{
    AutoApprove = 0,
    Reject = 1,

    /// <summary>The customer never opens their banking app, so the account stays pending.</summary>
    NeverAnswer = 2,
}
