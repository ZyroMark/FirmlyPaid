# FirmlyPaid architecture

Owner: ZYROMARK PTY LTD. Updated at the end of step 3 (part 11).

## 1. The shape of the system

```
  Customer touchscreen        Cashier screen         Agent            Customer          Merchant / Admin
  (Terminal.App)              (Terminal.App)         (Enrolment.App)  (React portal)    (Blazor web)
          |                          |                    |                 |                  |
          +--------------------------+--------------------+-----------------+------------------+
                                             |
                                    FirmlyPaid.Gateway
                        (JWT for people, device certificate for terminals,
                                   rate limits, one error shape)
                                             |
        +------------------+-----------------+-----------------+------------------+
        |                  |                 |                 |                  |
  Enrolment.Api     AccountLink.Api     Payments.Api       Risk.Api      TillIntegration.Api
        |                  |                 |                 |
        |                  |                 +--- internal ----+
        |                  |                 |
        +------------------+---------- Matching.Api  (the only service that may read the vault)
                           |                 |
                           |                 |
                 FirmlyPaidCore DB      FirmlyPaidVault DB
              (people, accounts,         (encrypted templates,
               payments, audit)           keyed by TemplateOwnerId only)

  Outside the boundary, behind interfaces in FirmlyPaid.Shared:
      IVeinScanner   IVeinMatcher   IFingerprintScanner   IHomeAffairsVerifier
      IBankGateway   IKeyVault      ISmsSender
```

RabbitMQ carries payment events between services. Every service publishes its own OpenAPI
document at `/swagger`.

## 2. Why two databases

`FirmlyPaidCore` holds people, accounts and payments. `FirmlyPaidVault` holds nothing but
encrypted vein templates, addressed by a random `TemplateOwnerId`, never by a name or an ID
number. Only `FirmlyPaid.Matching.Api` is given the vault connection string, which is
visible in `docker-compose.yml`: the other six services simply do not have it. A leak of
one database is not a leak of identities plus biometrics.

## 3. Swapping a simulator for the real thing

Every outside dependency sits behind an interface in `FirmlyPaid.Shared/Abstractions`. The
implementation is chosen by name in `appsettings.json`:

```json
"FirmlyPaid": {
  "Adapters": {
    "VeinScanner": "Simulator",
    "VeinMatcher": "Simulator",
    "FingerprintScanner": "Simulator",
    "HomeAffairsVerifier": "Simulator",
    "BankGateway": "Simulator",
    "KeyVault": "LocalDevFile",
    "SmsSender": "Simulator"
  }
}
```

To bring in a real device, bank or partner:

1. Write a new class in a new project (for example `FirmlyPaid.Adapters.Hitachi`) that
   implements the interface.
2. Register it under a new name in the adapter factory.
3. Change the value in `appsettings.json` (or the matching environment variable) for the
   environment you are switching.

No caller changes, because no caller knows which implementation it is holding.

| Interface | What it does | Simulator today | Real adapter later |
| --- | --- | --- | --- |
| `IVeinScanner` | Captures a finger vein pattern and returns it already encrypted | 20 test fingers, buttons for good read, poor quality, fake finger and no finger, plus noise on every read | Hitachi finger vein SDK |
| `IVeinMatcher` | Scores two decrypted templates against each other | Similarity over the simulated byte patterns, threshold from config | Hitachi matching engine |
| `IFingerprintScanner` | One fingerprint at enrolment, for Home Affairs | Stored sample per test person | Suprema BioMini Slim 2 SDK |
| `IHomeAffairsVerifier` | Confirms the person is who they say they are | 20 seeded ID numbers with match, no match, deceased and service down outcomes | Verification partner API |
| `IBankGateway` | Account confirmation, payment, refund over PayShap | Four fake banks with real balances so tests can prove money moved | Sponsor bank PayShap API |
| `IKeyVault` | Supplies the keys that protect templates and tokens | AES key from a protected local dev file | AWS KMS, then CloudHSM |
| `ISmsSender` | One time codes and notices | Writes to the console and the SmsMessages table the admin console reads | SMS provider |

The interface contract is the thing that has to stay honest. For example `IVeinScanner`
returns a `ProtectedPayload`, not an image, so a real driver has no way to hand a raw
image to the rest of the system even by accident (rule 10.1).

## 4. One error shape

Every service returns the same body on failure:

```json
{ "code": "NO_MATCH", "message": "We could not recognise that finger. Please try again.", "traceId": "0HN..." }
```

`ErrorHandlingMiddleware` (in `FirmlyPaid.Shared/Hosting`) is the first thing in every
pipeline. A `FirmlyPaidException` keeps its code and its plain English message. Anything
else becomes `INTERNAL_ERROR` with a generic message, so an unexpected fault can never
push a connection string or a stack trace to a till (rule 10.5). `ErrorStatusCodes` maps
each code to an HTTP status in one place, so every client behaves the same way.

## 5. Configuration

`FirmlyPaidOptions` holds every tunable rule: the R500 PIN threshold, the R3 000 payment
limit, the 1.2% merchant fee, three match attempts, five payments in two minutes, and so
on. The values are defaults in code, validated at startup, and overridable per environment
through `appsettings.json` or environment variables such as
`FirmlyPaid__Payments__PinRequiredFromAmount`. Keeping the defaults in one class means two
services can never quietly disagree about when a PIN is required.

Secrets are never in that class and never in source control. Locally they come from
`dotnet user-secrets` or the git-ignored `.env`; in deployment they come from environment
variables.

## 6. Money and formatting

Money is `decimal` everywhere and `decimal(18,2)` in the database. `Rand.Format2` prints
amounts the same way on every machine (`R3 000.00`) rather than following the server's
culture, and `Rand.RoundToCents` rounds half away from zero, which is the rule the merchant
fee test in FR-11 checks.

## 7. The two databases in detail

`FirmlyPaidCore` has 14 tables, created by one EF Core migration in
`src/FirmlyPaid.Data.Core/Migrations`:

| Table | Holds | Notable constraints |
| --- | --- | --- |
| Customers | People | `IdNumberHash` unique, index on (bucket, status) for checkout |
| Consents | POPIA consent per customer | Kept after withdrawal as evidence |
| LinkedAccounts | Bank accounts, 1 to 5 per customer | Token stored encrypted, only last 4 digits in the clear |
| Merchants, Stores, Terminals | The estate | Terminal serial and certificate thumbprint both unique |
| Payments | Every attempt, approved or not | `IdempotencyKey` unique, so a retry cannot charge twice |
| Refunds, Disputes | After the fact | Restrict delete, a payment cannot vanish under them |
| RiskEvents | Every rule that fired | Indexed by customer and by terminal over time |
| Settlements | What a merchant is owed | Indexed by merchant and period |
| Enrolments | Sign-ups in progress | Holds the person's details until the PIN step completes |
| AuditLog | Append-only, hash chained | `Sequence` identity, 64 character hashes |
| SmsMessages | What the SMS simulator sent | Masked number only, indexed by time |

`FirmlyPaidVault` has exactly two:

| Table | Holds |
| --- | --- |
| VeinTemplates | Encrypted template, key version, transform seed, quality, revocation time |
| Buckets | Template owner to bucket, so a match searches four digits worth of people |

Nothing in the vault names a person. A test asserts that no vault column contains Name,
IdNumber, Cellphone, CustomerId or Email, and another test reads every `.csproj` and fails
if any project other than the Matching service references the vault.

### Why enrolment has its own table

A half-finished sign-up must never look like a live customer. An `Enrolment` row carries
the person's hashed ID, bucket and a freshly allocated `TemplateOwnerId` while samples are
being captured. Only when the PIN step succeeds does a `Customer` row appear, reusing the
same `TemplateOwnerId` so the templates already in the vault belong to it.

### The audit hash chain

`AppendAuditAsync` reads the last row's hash, computes
`SHA256(previousHash, actor, action, entityType, entityId, details, createdAt)` over a
fixed field order, and stores both. `AuditChainVerifier` walks the rows in `Sequence` order
and recomputes. Editing a row breaks its own hash; deleting one breaks the next row's
`PreviousHash`. Both cases are covered by tests. The writer also refuses details that trip
the `SensitiveDataPatterns` check, so rule 10.5 is enforced at the point of writing rather
than left to each caller's memory.

### Times and money

Every `DateTime` column goes through a value converter that writes UTC and reads back with
`DateTimeKind.Utc`. SQL Server does not store the kind, and a value read as Unspecified
silently becomes local time the first time it is formatted. Money is `decimal(18,2)`
by convention on the model builder, so a new money column cannot get it wrong by omission.

## 8. How the simulators behave

All seven live in `src/FirmlyPaid.Simulators`, one folder per dependency, and are chosen by
`AdapterRegistration.AddFirmlyPaidAdapters`. An adapter name with no implementation throws
at startup rather than falling back to a simulator: a fallback would mean a production
deployment quietly paying with fake money.

### The vein scanner and matcher

A test finger is a fixed 256 byte pattern derived from hashing its label and finger
position, so the same finger reads the same way in every process without a lookup table.
Each read adds a little random noise, which is the point: no real sensor returns the same
bytes twice, so matching has to score similarity rather than compare bytes. If the
simulator returned identical reads, the flow would pass every test and break on the day the
Hitachi unit arrives.

The matcher scores one minus the average per byte difference, on the same 0 to 1 scale the
real engine reports. Two reads of one finger land near 0.99; two different fingers land
near 0.67. The threshold sits at 0.85, and a test runs fifty reads to check the margin
holds rather than passing by luck.

### The fake bank

`FakeBankLedger` keeps real balances. An approved payment moves money, an insufficient
funds decline moves nothing, and a refund puts it back, so a test can assert on the
balance instead of trusting a status code. A balance never leaves the ledger: rule 10.7
forbids one appearing in any API response, so the gateway reads it to decide and reports
only approved or declined.

Account confirmation matches the real flow. The bank returns Pending straight away and
answers Confirmed only once the customer has had time to tap approve in their own app,
five seconds by default. Tests move an injected clock instead of waiting.

Idempotency is honoured: the same key returns the original answer without a second debit.
That is rule 10.11, and it is the one bank behaviour that has to be right in the simulator,
because otherwise the retry path would first be exercised in production.

### The key vault

AES-256-GCM with keys in a local file, standing in for AWS KMS and later CloudHSM. GCM
authenticates as well as encrypts, so a template edited directly in the database fails to
open rather than decrypting to something different. Each encryption uses a fresh nonce, so
two customers with the same template do not look the same in the vault. An unknown key
version is an error, never a silent failure, because rotation must not orphan old rows.

This is for a developer laptop only. The key sits on disk beside the data it protects,
which is exactly what a hardware security module exists to prevent.

### Home Affairs

Twenty test identities, every one with a genuinely valid check digit. The first ten are the
seeded customers, so anyone already in the database verifies cleanly; the other ten force a
no match, a deceased record or the service being down. An ID number with a bad checksum
throws before the call is made, because FR-03 requires the check first and a real partner
charges per query. The reference it returns is derived from the ID number rather than
containing it, so an audit row can quote it safely.

### SMS

Messages go to the console and to the `SmsMessages` table, so a demo can read a one-time
code without a SIM card. Only the masked number is stored. A real provider adapter would
keep no message bodies at all; this table exists purely so a demonstration can show the
code on screen, and the deployment scripts do not create it outside development.

### The demo switchboard

`SimulatorControlState` is a singleton every simulator reads before it decides what to
return. An operator can force a decline, a fake finger, a Home Affairs outage or a rejected
account confirmation mid-demonstration without restarting anything. The admin console
drives it from step 10.

| Switch | Choices |
| --- | --- |
| VeinScanner | Automatic, GoodRead, PoorQuality, FakeFinger, NoFinger |
| HomeAffairsOverride | None, Match, NoMatch, Deceased, ServiceUnavailable |
| BankPayment | Automatic, AlwaysApprove, InsufficientFunds, AlwaysDecline, Timeout |
| AccountConfirmation | AutoApprove, Reject, NeverAnswer |
| VeinNoiseAmplitude | 0 to 255, for showing a genuine near miss |
| AccountConfirmationDelay | How long the customer takes to tap approve |

### Where the demo cast lives

`src/FirmlyPaid.DemoData` holds the ten seeded people, three merchants, five terminals and
the Home Affairs roster. Both the database seeder and the Home Affairs simulator read from
it, so the twenty test ID numbers cannot drift apart from the ten customers in the
database. Neither the persistence layer nor the adapter layer depends on the other.

## 9. What is not built yet



Steps 4 to 12 of part 11: all service endpoints, terminal and enrolment apps, the web
front ends, and mutual TLS between terminals and the Gateway. Inside the
compose network the services currently speak plain HTTP; TLS terminates at the Gateway
from step 7.

Two columns are still left empty by the seed: `Customers.PinHash` waits for the Argon2id
hasher in step 4, and `LinkedAccounts.AccountTokenCiphertext` waits for the AccountLink
service in step 5 to run a real confirmation through the fake bank. The vault is also still
empty, because templates are written during enrolment, which is step 4.
