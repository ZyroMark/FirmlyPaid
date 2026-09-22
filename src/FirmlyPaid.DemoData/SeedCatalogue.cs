using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;

namespace FirmlyPaid.DemoData;

/// <summary>
/// The fixed cast for demos and tests: 3 merchants, 5 terminals and 10 customers, some
/// with one bank and some with three (part 7). Every identifier is a fixed GUID so a
/// demo script, a test and a screenshot all refer to the same rows after a reset.
/// </summary>
public static class SeedCatalogue
{
    private static Guid Id(string suffix) => Guid.Parse($"00000000-0000-0000-0000-{suffix}");

    /// <param name="First12Digits">The check digit is calculated, so every seeded ID number is genuinely valid.</param>
    public sealed record SeedPerson(
        string FullName,
        string First12Digits,
        string CellphoneNumber,
        CustomerStatus Status,
        bool SetDefaultAccount,
        IReadOnlyList<SeedAccount> Accounts)
    {
        public string IdNumber => SouthAfricanIdNumber.WithCheckDigit(First12Digits);

        public int Bucket => SouthAfricanIdNumber.Bucket(IdNumber);
    }

    public sealed record SeedAccount(string BankCode, string Nickname, string AccountNumber);

    public sealed record SeedMerchant(
        Guid MerchantId,
        string TradingName,
        string RegistrationNumber,
        MerchantTier Tier,
        decimal FeePercent,
        decimal AnnualLicenceFee,
        IReadOnlyList<SeedStore> Stores);

    public sealed record SeedStore(Guid StoreId, string Name, string Address, string Area, IReadOnlyList<SeedTerminal> Terminals);

    public sealed record SeedTerminal(
        Guid TerminalId,
        string SerialNumber,
        string CertificateThumbprint,
        TerminalType Type,
        decimal MonthlyRental);

    /// <summary>The agent whose id appears on every seeded consent and enrolment.</summary>
    public static readonly Guid DemoAgentId = Id("00000000a9e7");

    public static readonly IReadOnlyList<SeedMerchant> Merchants =
    [
        new(
            MerchantId: Id("00000000e001"),
            TradingName: "Ubuntu Foods",
            RegistrationNumber: "2019/114455/07",
            Tier: MerchantTier.Retailer,
            FeePercent: 1.20m,
            AnnualLicenceFee: 120000.00m,
            Stores:
            [
                new(
                    StoreId: Id("00000000c001"),
                    Name: "Ubuntu Foods Claremont",
                    Address: "12 Main Road, Claremont, Cape Town",
                    Area: "Southern Suburbs",
                    Terminals:
                    [
                        new(Id("000000007001"), "FP-0001", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345601", TerminalType.TillAddOn, 299.00m),
                        new(Id("000000007002"), "FP-0002", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345602", TerminalType.TillAddOn, 299.00m),
                    ]),
                new(
                    StoreId: Id("00000000c002"),
                    Name: "Ubuntu Foods Khayelitsha",
                    Address: "45 Spine Road, Khayelitsha, Cape Town",
                    Area: "Cape Flats",
                    Terminals:
                    [
                        new(Id("000000007003"), "FP-0003", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345603", TerminalType.TillAddOn, 299.00m),
                    ]),
            ]),
        new(
            MerchantId: Id("00000000e002"),
            TradingName: "Kasi Fresh Market",
            RegistrationNumber: "2022/556677/07",
            Tier: MerchantTier.Small,
            FeePercent: 1.20m,
            AnnualLicenceFee: 0m,
            Stores:
            [
                new(
                    StoreId: Id("00000000c003"),
                    Name: "Kasi Fresh Market Soweto",
                    Address: "78 Vilakazi Street, Orlando West, Soweto",
                    Area: "Soweto",
                    Terminals:
                    [
                        new(Id("000000007004"), "FP-0004", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345604", TerminalType.Standalone, 349.00m),
                    ]),
            ]),
        new(
            MerchantId: Id("00000000e003"),
            TradingName: "Cape Coast Pharmacy",
            RegistrationNumber: "2020/998877/07",
            Tier: MerchantTier.Small,
            FeePercent: 1.35m,
            AnnualLicenceFee: 0m,
            Stores:
            [
                new(
                    StoreId: Id("00000000c004"),
                    Name: "Cape Coast Pharmacy Sea Point",
                    Address: "210 Beach Road, Sea Point, Cape Town",
                    Area: "Atlantic Seaboard",
                    Terminals:
                    [
                        new(Id("000000007005"), "FP-0005", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345605", TerminalType.Kiosk, 249.00m),
                    ]),
            ]),
    ];

    /// <summary>
    /// Ten people. Buckets are deliberately mixed: two of them share bucket 5009 so the
    /// matching tests have a bucket with more than one candidate in it.
    /// </summary>
    public static readonly IReadOnlyList<SeedPerson> People =
    [
        new("Thandiwe Mokoena", "900215500908", "0821000001", CustomerStatus.Active, true,
            [new(BankCodes.Absa, "Salary", "4051234567"), new(BankCodes.Fnb, "Savings", "6212345678"), new(BankCodes.TymeBank, "Spending", "5150009911")]),

        new("Sipho Dlamini", "880710500908", "0821000002", CustomerStatus.Active, false,
            [new(BankCodes.Fnb, "Cheque", "6223334444"), new(BankCodes.Discovery, "Everyday", "7010002222"), new(BankCodes.Absa, "Household", "4059998888")]),

        new("Ayanda Nkosi", "950304122208", "0821000003", CustomerStatus.Active, false,
            [new(BankCodes.TymeBank, "Main", "5151112222")]),

        new("Lerato Mahlangu", "911122334408", "0821000004", CustomerStatus.Active, true,
            [new(BankCodes.Absa, "Salary", "4052223333"), new(BankCodes.Fnb, "Groceries", "6214445555")]),

        new("Johan van Wyk", "790618776608", "0821000005", CustomerStatus.Active, false,
            [new(BankCodes.Discovery, "Everyday", "7013334444")]),

        new("Nomvula Zulu", "030925445508", "0821000006", CustomerStatus.Active, false,
            [new(BankCodes.Fnb, "Student", "6215556666"), new(BankCodes.TymeBank, "Side hustle", "5153334444")]),

        new("Riaan Botha", "850203667708", "0821000007", CustomerStatus.Frozen, false,
            [new(BankCodes.Absa, "Salary", "4056667777")]),

        new("Precious Khumalo", "970812889908", "0821000008", CustomerStatus.Active, true,
            [new(BankCodes.TymeBank, "Main", "5157778888"), new(BankCodes.Absa, "Stokvel", "4058889999"), new(BankCodes.Discovery, "Medical", "7015556666")]),

        new("Fatima Adams", "820430223308", "0821000009", CustomerStatus.Active, false,
            [new(BankCodes.Discovery, "Everyday", "7017778888")]),

        new("Bongani Ndlovu", "930107991108", "0821000010", CustomerStatus.Active, false,
            [new(BankCodes.Fnb, "Cheque", "6219990000"), new(BankCodes.Absa, "Savings", "4051110000")]),
    ];

    public static int TerminalCount => Merchants.SelectMany(m => m.Stores).SelectMany(s => s.Terminals).Count();
}
