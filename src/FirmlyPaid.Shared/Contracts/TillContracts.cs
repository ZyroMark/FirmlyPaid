using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Contracts;

/// <summary>
/// POST /till/v1/sales. The retailer's till never learns the customer's banks,
/// ID digits or PIN (rule 10.8) - only whether the sale was paid.
/// </summary>
public sealed record CreateTillSaleRequest(
    Guid StoreId,
    string TillId,
    decimal Amount,
    string BasketReference);

public sealed record CreateTillSaleResponse(Guid SaleId, TillSaleStatus Status);

/// <summary>GET /till/v1/sales/{id}</summary>
public sealed record TillSaleStatusResponse(
    Guid SaleId,
    TillSaleStatus Status,
    string? DeclineReason,
    TillReceiptDto? Receipt);

public sealed record TillReceiptDto(string ReceiptNumber, decimal Amount, DateTime CompletedAtUtc);
