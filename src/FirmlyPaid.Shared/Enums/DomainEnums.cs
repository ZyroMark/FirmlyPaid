namespace FirmlyPaid.Shared.Enums;

/// <summary>Which finger a vein template belongs to. Two fingers are enrolled per customer.</summary>
public enum FingerPosition
{
    Unknown = 0,
    LeftThumb = 1,
    LeftIndex = 2,
    LeftMiddle = 3,
    RightThumb = 4,
    RightIndex = 5,
    RightMiddle = 6,
}

public enum CustomerStatus
{
    Active = 1,
    Frozen = 2,
    Deleted = 3,
}

/// <summary>Sets the per payment limit when a customer has no explicit override (FR-15).</summary>
public enum CustomerTier
{
    Pilot = 1,
    Standard = 2,
    Premium = 3,
}

/// <summary>Where an agent-assisted sign-up has got to.</summary>
public enum EnrolmentStatus
{
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Abandoned = 4,
}

public enum LinkedAccountStatus
{
    Pending = 1,
    Confirmed = 2,
    Removed = 3,
}

public enum PaymentStatus
{
    Pending = 1,
    Approved = 2,
    Declined = 3,
    Refunded = 4,
    Failed = 5,
}

public enum RefundStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
}

public enum DisputeStatus
{
    Open = 1,
    UnderReview = 2,
    Resolved = 3,
    Rejected = 4,
}

public enum SettlementStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
}

public enum TerminalType
{
    Standalone = 1,
    TillAddOn = 2,
    Kiosk = 3,
}

public enum TerminalStatus
{
    Active = 1,
    Suspended = 2,
    Revoked = 3,
}

public enum MerchantTier
{
    Small = 1,
    Retailer = 2,
}

public enum MerchantStatus
{
    Active = 1,
    Suspended = 2,
    Closed = 3,
}

/// <summary>What the risk engine tells the payment flow to do.</summary>
public enum RiskAction
{
    Allow = 1,
    RequirePin = 2,
    Block = 3,
}

/// <summary>Outcome of a Home Affairs identity check.</summary>
public enum HomeAffairsOutcome
{
    Match = 1,
    NoMatch = 2,
    Deceased = 3,
    ServiceUnavailable = 4,
}

/// <summary>What the vein scanner saw. Only <see cref="Good"/> produces a usable template.</summary>
public enum VeinCaptureOutcome
{
    Good = 1,
    PoorQuality = 2,
    LivenessFailed = 3,
    NoFinger = 4,
}

/// <summary>Result of asking a bank to confirm that an account belongs to the customer.</summary>
public enum AccountConfirmationOutcome
{
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
}

/// <summary>What the sponsor bank said about a payment instruction.</summary>
public enum BankPaymentOutcome
{
    Approved = 1,
    InsufficientFunds = 2,
    Declined = 3,
    Timeout = 4,
}

/// <summary>Where a till sale stands, for the retailer integration API.</summary>
public enum TillSaleStatus
{
    Pending = 1,
    Approved = 2,
    Declined = 3,
    Cancelled = 4,
}
