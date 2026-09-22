# FirmlyPaid Master Prompt (Software Build, Phase 0)

Owner: ZYROMARK PTY LTD. Version 1.0, 21 September 2026.

Paste everything below into any AI coding tool or hand it to a developer. It builds the full FirmlyPaid software with the hardware, bank and Home Affairs replaced by simulators until they arrive. Tip: keep this file in the project root as `BUILD-SPEC.md` so it is read at the start of every session.

---

## 1. Role, goal and how to work

You are the lead software engineer building FirmlyPaid, a biometric payment platform owned by ZYROMARK PTY LTD in South Africa. Your goal is a complete, working, tested software system that runs end to end on a developer laptop, with every piece of hardware and every outside party (scanner, sponsor bank, Home Affairs) replaced by a simulator that uses the same interface the real one will use later.

How to work:

1. Read this whole prompt before writing any code. If something is unclear or two rules conflict, stop and ask; do not guess.
2. Build in the order given in part 11. After each step, stop, show what you built, how to run it and the test results, and wait for approval before the next step.
3. For every step, give numbered setup and run instructions a junior developer can follow on Windows, including exact commands.
4. Write automated tests as you go. A step is not done until its tests pass.
5. Keep code simple and readable. Comment the why, not the what.
6. Never weaken a rule in part 10 to make something easier. If a rule blocks you, stop and explain.

## 2. Product context

FirmlyPaid lets a customer pay at a till with one finger: no card, phone or cash. It works like palm and face payment in China (Alipay, WeChat Pay), adapted to South African banks.

- Enrolment (once): an agent verifies the customer's ID against Home Affairs (fingerprint check), captures the customer's finger vein pattern (2 fingers, 3 samples each), records POPIA consent, and links 1 to 5 bank accounts. Each account is confirmed by the customer in their own banking app.
- Checkout: the merchant enters the amount. On the FirmlyPaid unit's own customer touchscreen, the customer places a finger and enters digits 7 to 10 of their South African ID number (the 4 digits after the birth date). FirmlyPaid matches the vein pattern inside that small group.
- Bank choice: if the customer has more than 1 linked account, the touchscreen shows the list (bank logo, nickname, last 4 account digits, never balances) and the customer taps one. If only 1 account, or a default is set, the list is skipped with a "change bank" button visible for 3 seconds.
- PIN: required above a set amount (default R3,000 per payment limit in pilot, PIN above R500) or when the risk score is high.
- Payment: sent to the sponsor bank as a PayShap payment. FirmlyPaid never holds customer money.
- Money: ZYROMARK earns a merchant fee per payment (default 1.2%), terminal rental, and retailer licence fees. The system must calculate and report these.

South African ID number format: YYMMDD SSSS C A Z (13 digits). Digits 7 to 10 are SSSS.

## 3. Scope for this build

Build all software for phase 0 and phase 1 behaviour. Hardware and outside parties are simulated; swapping in the real ones later must need only a new adapter class and a config change.

| In scope | Out of scope (later) |
| --- | --- |
| Enrolment app (kiosk, agent-assisted) | Real Hitachi finger vein driver and SDK |
| Checkout terminal app with customer touchscreen UI | Real Suprema fingerprint driver |
| Till integration API for big retailers (Pick n Pay, Woolworths style) | Real sponsor bank and PayShap connection |
| Matching service, payment service, account link service, risk engine | Real Home Affairs verification |
| Customer web portal, merchant dashboard, admin console | Card tokens, palm vein, person to person payments, mobile app |
| Simulators for scanner, bank and Home Affairs | Offline payments (never allowed in this version) |
| Fees, settlement and revenue reports | Production cloud deployment (provide scripts only) |

## 4. Tech stack and solution structure

| Layer | Technology |
| --- | --- |
| Backend services | C# on .NET 10 (LTS), ASP.NET Core Web API, Entity Framework Core |
| Database | SQL Server 2022 (Developer edition locally); a separate database for biometric templates |
| Terminal and enrolment apps | .NET MAUI Blazor Hybrid (runs on Windows now, Android later) |
| Customer portal | React with TypeScript and Vite |
| Merchant dashboard and admin console | Blazor Web App |
| Messaging | RabbitMQ (local, via Docker) for payment events |
| Auth | ASP.NET Core Identity plus JWT for users; mutual TLS certificates for terminals |
| Keys | An IKeyVault interface; local version uses a file-protected dev key; production version uses AWS KMS or CloudHSM |
| Tests | xUnit, FluentAssertions, Testcontainers for SQL Server, Playwright for UI flows |
| Local run | Docker Compose starts SQL Server, RabbitMQ and all services with one command |

Solution layout:

```
FirmlyPaid/
  FirmlyPaid.sln
  docker-compose.yml
  src/
    FirmlyPaid.Shared/            (DTOs, enums, result types, interfaces)
    FirmlyPaid.Gateway/           (API gateway, auth, rate limits)
    FirmlyPaid.Enrolment.Api/
    FirmlyPaid.Matching.Api/      (biometric vault, isolated)
    FirmlyPaid.AccountLink.Api/
    FirmlyPaid.Payments.Api/
    FirmlyPaid.Risk.Api/
    FirmlyPaid.TillIntegration.Api/
    FirmlyPaid.Simulators/        (scanner, bank, Home Affairs)
    FirmlyPaid.Terminal.App/      (MAUI Blazor Hybrid)
    FirmlyPaid.Enrolment.App/     (MAUI Blazor Hybrid)
    FirmlyPaid.Admin.Web/         (Blazor)
    FirmlyPaid.Merchant.Web/      (Blazor)
    firmlypaid-customer-portal/   (React + TypeScript)
  tests/
    (one test project per service, plus FirmlyPaid.EndToEnd.Tests)
  docs/
    api/  setup.md  architecture.md
```

## 5. Data model

Use two databases. FirmlyPaidCore holds people, accounts and payments. FirmlyPaidVault holds only encrypted templates, linked by a random TemplateOwnerId, never by name or ID number. Only the Matching service can connect to FirmlyPaidVault. Use GUIDs for keys, UTC times, and money as decimal(18,2) in rand.

FirmlyPaidCore:

| Table | Main fields |
| --- | --- |
| Customers | CustomerId, FullName, IdNumberHash (salted hash), IdDigits7to10Bucket (int 0 to 9999), CellphoneNumber, PinHash, Status (Active, Frozen, Deleted), DefaultLinkedAccountId, TemplateOwnerId, CreatedAt |
| Consents | ConsentId, CustomerId, ConsentTextVersion, AgentId, AcceptedAt, WithdrawnAt |
| LinkedAccounts | LinkedAccountId, CustomerId, BankCode, AccountNickname, AccountLast4, AccountToken (from bank, encrypted), Status (Pending, Confirmed, Removed), ConfirmedAt |
| Merchants | MerchantId, TradingName, RegistrationNumber, FeePercent (default 1.20), SettlementAccountToken, Tier (Small, Retailer), Status |
| Stores | StoreId, MerchantId, Name, Address, Area |
| Terminals | TerminalId, StoreId, SerialNumber, CertificateThumbprint, Type (Standalone, TillAddOn, Kiosk), MonthlyRental, Status, LastSeenAt |
| Payments | PaymentId, TerminalId, MerchantId, CustomerId, LinkedAccountId, Amount, FeeAmount, Status (Pending, Approved, Declined, Refunded, Failed), DeclineReason, BankReference, RiskScore, PinUsed, IdempotencyKey, CreatedAt, CompletedAt |
| Refunds | RefundId, PaymentId, Amount, Reason, Status, CreatedAt |
| Disputes | DisputeId, PaymentId, CustomerId, Reason, Status, ResolvedAt |
| RiskEvents | RiskEventId, CustomerId, TerminalId, Rule, Score, Action, CreatedAt |
| Settlements | SettlementId, MerchantId, PeriodStart, PeriodEnd, GrossAmount, FeeTotal, NetAmount, Status |
| AuditLog | AuditId, Actor, Action, EntityType, EntityId, Details (no personal data), PreviousHash, Hash, CreatedAt |
| Enrolments | EnrolmentId, CustomerId, AgentId, StoreId, HomeAffairsResult, HomeAffairsReference, CreatedAt |

FirmlyPaidVault:

| Table | Main fields |
| --- | --- |
| VeinTemplates | TemplateId, TemplateOwnerId, FingerPosition, EncryptedTemplate (varbinary), KeyVersion, TransformSeedId (for cancellable templates), QualityScore, CreatedAt, RevokedAt |
| Buckets | TemplateOwnerId, IdDigits7to10Bucket (so matching searches only one bucket) |

AuditLog is append-only; each row's Hash covers its content plus PreviousHash so tampering is detectable. Keep audit rows for 5 years.

## 6. Services and API contracts

All calls go through the Gateway over HTTPS. Terminals authenticate with a device certificate; people with JWT. Every error response uses one shape: `{ "code": "STRING_CODE", "message": "plain English", "traceId": "..." }`. Publish OpenAPI (Swagger) docs for every service.

| Service | Endpoint | Input | Output |
| --- | --- | --- | --- |
| Enrolment | POST /enrolments | ID number, name, cellphone, agent ID, consent version, fingerprint sample (for Home Affairs) | EnrolmentId, Home Affairs result |
| Enrolment | POST /enrolments/{id}/vein-samples | Finger position, 3 encrypted vein samples | Quality score, accepted or rejected |
| Enrolment | POST /enrolments/{id}/complete | PIN (sent encrypted) | CustomerId, status Active |
| AccountLink | POST /customers/{id}/accounts | Bank code, account number, nickname | LinkedAccountId, status Pending, confirmation sent to bank app |
| AccountLink | POST /bank-callbacks/confirmations | Bank confirmation message (signed) | 200 OK |
| AccountLink | PUT /customers/{id}/default-account | LinkedAccountId | 204 |
| Matching (internal only) | POST /match | Encrypted vein template, digits 7 to 10 bucket | Match or no match, TemplateOwnerId, score |
| Payments | POST /payments/start | TerminalId, amount, merchant reference, idempotency key | PaymentId, prompt: place finger |
| Payments | POST /payments/{id}/identify | Encrypted vein template, ID digits 7 to 10 | List of accounts to show (nickname, bank, last 4) or auto-selected account; PIN required yes or no |
| Payments | POST /payments/{id}/confirm | LinkedAccountId, encrypted PIN if required | Approved or declined, receipt data |
| Payments | POST /payments/{id}/refund | Amount, reason (merchant user) | RefundId, status |
| Risk (internal only) | POST /risk/score | Customer, terminal, amount, recent history | Score 0 to 100, actions (allow, require PIN, block) |
| TillIntegration | POST /till/v1/sales | Store ID, till ID, amount, basket reference | Sale ID; the till then polls or receives a webhook |
| TillIntegration | GET /till/v1/sales/{id} | none | Pending, approved (with receipt) or declined |

Error codes to support at minimum: NO_MATCH, TOO_MANY_ATTEMPTS, LIVENESS_FAILED, PIN_REQUIRED, PIN_WRONG, ACCOUNT_NOT_CONFIRMED, BANK_DECLINED, BANK_TIMEOUT, LIMIT_EXCEEDED, CUSTOMER_FROZEN, TERMINAL_NOT_TRUSTED.

## 7. Simulators (hardware and outside parties)

Each outside dependency sits behind an interface in FirmlyPaid.Shared. Build a simulator for each now; the real adapters come later. Choose the implementation in appsettings.json (for example `"VeinScanner": "Simulator"`).

| Interface | Simulator behaviour | Real adapter later |
| --- | --- | --- |
| IVeinScanner | Shows a panel in the terminal app with a list of test "fingers" (each a fixed random byte pattern per test customer) plus buttons: good read, poor quality, fake finger (liveness fail), no finger. Adds small random noise to each read so matching is not a simple byte compare | Hitachi finger vein SDK |
| IVeinMatcher | Compares simulated templates with a similarity score; threshold set in config | Hitachi matching engine |
| IFingerprintScanner | Returns a stored test fingerprint sample for the chosen test person | Suprema BioMini Slim 2 SDK |
| IHomeAffairsVerifier | Seeded list of 20 test ID numbers (valid checksums) with outcomes: match, no match, deceased, service down | Verification partner API |
| IBankGateway | Fake PayShap bank for 4 banks (ABSA, FNB, Discovery, TymeBank). Supports account confirmation (auto-approve after 5 seconds, or reject), payment approve, insufficient funds, timeout, and refund. Keeps fake balances so tests can check money moved | Sponsor bank PayShap API |
| IKeyVault | Local AES key from a protected dev file | AWS KMS, then CloudHSM |
| ISmsSender | Writes messages to the console and a table | SMS provider |

Include a seed script that creates 3 merchants, 5 terminals, and 10 test customers (some with 1 bank, some with 3), so every flow can be demonstrated in a few minutes. Also provide an admin page to switch simulator outcomes live during demos.

## 8. Screens and user flows

All customer screens use large touch targets (at least 48 px), plain English, and must also support isiXhosa and Afrikaans text files (English first; keep all text in resource files). Design for a 5 inch customer touchscreen.

Terminal app, customer screen (in order):

1. Amount: "Pay R250.00 to Store Name".
2. "Place your finger on the scanner" with progress. On fail: "Try again" (max 3 tries, then "Please use another payment method").
3. "Enter the 4 digits after your birth date in your ID number" with a number pad. Digits are masked after entry.
4. Bank choice (only if more than 1 account and no default): cards showing bank logo, nickname and last 4 account digits. Never show balances. With a default, show the default bank and a "Change bank" button for 3 seconds.
5. PIN pad (only when required).
6. Result: "Paid" with receipt number, or a clear decline reason in plain words.

Terminal app, cashier side: enter amount, see status (waiting for customer, approved, declined), refund with manager PIN, end-of-day totals. The cashier never sees the customer's banks, ID digits or PIN.

Enrolment app (agent): capture ID, fingerprint for Home Affairs, consent screen with full text and "I agree", vein capture for 2 fingers with quality feedback, link banks, set PIN, done.

Customer portal (React): log in with cellphone plus one-time SMS code; view payments; manage linked banks and default; freeze or unfreeze profile; raise a dispute; download my data; delete my biometrics (with warning).

Merchant dashboard (Blazor): sales by day and store, fees, settlements, refunds, terminal status and rental, export to CSV.

Admin console (Blazor): customers (masked data), merchants, terminals, disputes, risk alerts, audit log viewer, simulator controls, revenue report (merchant fees, rental, licences).

## 9. Functional requirements with acceptance tests

Each requirement needs at least the automated tests listed. Name tests after the requirement ID (for example FR08_TwoBanks_ShowsPicker).

| ID | Requirement | Acceptance tests |
| --- | --- | --- |
| FR-01 | Capture 2 fingers, 3 vein samples each, reject low quality | Poor quality sample is rejected with a retry message; enrolment cannot complete with fewer than 2 fingers |
| FR-02 | Record POPIA consent | Enrolment without consent fails; consent row stores version, agent and time |
| FR-03 | Verify ID with Home Affairs | Invalid ID checksum rejected before calling; "no match" and "deceased" block enrolment; "service down" shows retry later |
| FR-04 | Link up to 5 accounts, confirmed in bank app | 6th account refused; unconfirmed account never appears at checkout |
| FR-05 | Default bank and nicknames | Setting default works; removing the default account clears it |
| FR-06 | Checkout captures vein plus ID digits 7 to 10 | Wrong digits with right finger returns NO_MATCH; right digits with wrong finger returns NO_MATCH |
| FR-07 | Match, liveness and risk before showing banks | Fake finger returns LIVENESS_FAILED; 3 failed tries locks the terminal session; matching only searches one bucket (check with a query count or log) |
| FR-08 | Bank picker on the customer touchscreen | 1 account: no picker; 3 accounts, no default: picker with 3 cards; default set: default shown with change option; no balances in any response |
| FR-09 | PIN step-up | R499.99 no PIN; R500.00 PIN required; high risk score requires PIN at any amount; 3 wrong PINs freezes the customer |
| FR-10 | Payment to bank, result within 5 seconds | Approved flow updates Payment and fake bank balances; timeout marks Failed and never double-charges on retry (idempotency key) |
| FR-11 | Merchant dashboard | Fee equals 1.2% of amount rounded to cents; daily totals match the sum of payments; CSV export matches the screen |
| FR-12 | Customer portal | Freeze blocks checkout immediately; delete biometrics removes vault rows and the customer must re-enrol |
| FR-13 | Admin console | Every admin action writes an audit row; tampering with one audit row is detected by the chain check |
| FR-14 | Till integration API | Till creates sale, customer pays on the FirmlyPaid unit, till receives approved within 6 seconds |
| FR-15 | Limits | Payment above R3,000 returns LIMIT_EXCEEDED; limit configurable per customer tier |
| FR-16 | Revenue reports | Monthly report shows merchant fees, terminal rental and licence income per merchant |

Non-functional targets to test: identify plus confirm under 3 seconds on a laptop with 5,000 seeded customers; 50 payments per minute load test with no errors.

## 10. Security and privacy rules (never break these)

1. Never store, log or send raw biometric images. Only encrypted templates leave the scanner adapter.
2. Templates are encrypted with keys from IKeyVault and stored only in FirmlyPaidVault. Only the Matching service may read that database.
3. Use cancellable templates: apply a per-customer transform (TransformSeedId) before storing, so a leaked template can be revoked and replaced.
4. Never store a full ID number in plain text. Store a salted hash, plus the digits 7 to 10 bucket only.
5. Never write ID numbers, ID digits, PINs, templates, account numbers or tokens to logs, exceptions or audit details. Add a test that scans log output for these patterns.
6. PINs are hashed with a slow algorithm (Argon2id or PBKDF2 with a high iteration count). PINs travel encrypted from the terminal.
7. No balances ever appear on any screen or in any API response.
8. The cashier and till never receive the customer's bank list, ID digits or PIN.
9. Every terminal call is authenticated with its device certificate; unknown or revoked terminals get TERMINAL_NOT_TRUSTED.
10. Rate limits: 3 match attempts per payment; 5 payments per customer in 2 minutes triggers a risk block.
11. Every payment request carries an idempotency key; a retry must never charge twice.
12. Customers can freeze instantly and delete biometrics; deletion removes vault rows within 30 days (hard delete in this build immediately).
13. All personal data stays in South African hosting regions in deployment scripts.
14. No offline payments.
15. Secrets never go in source code; use user-secrets locally and environment variables in deployment.

## 11. Build order, checkpoints and deliverables

Stop after each step, report, and wait for approval.

| Step | Build | Checkpoint to show |
| --- | --- | --- |
| 1 | Solution skeleton, Docker Compose (SQL Server, RabbitMQ), Shared project with interfaces, error shape, config | `docker compose up` works; empty services answer /health |
| 2 | Both databases, EF Core migrations, seed script | Tables created; seed data visible |
| 3 | Simulators (all interfaces in part 7) | Unit tests for each simulator outcome |
| 4 | Enrolment API and Matching API with vault encryption and buckets | FR-01 to FR-03 tests pass |
| 5 | AccountLink API with fake bank confirmations | FR-04, FR-05 tests pass |
| 6 | Risk API and Payments API | FR-06 to FR-10, FR-15 tests pass |
| 7 | Gateway with JWT and terminal certificates, rate limits | Untrusted terminal refused; limits enforced |
| 8 | Terminal app (customer and cashier screens) and Enrolment app | Full demo: enrol a customer with 3 banks, pay, choose bank, get receipt |
| 9 | TillIntegration API with a sample "fake till" console app | FR-14 test passes |
| 10 | Merchant dashboard, admin console, customer portal | FR-11 to FR-13, FR-16 tests pass |
| 11 | Security review, log scanning test, load test, end-to-end Playwright tests | All tests green; security checklist in part 10 ticked with evidence |
| 12 | Documentation and deployment scripts (AWS Cape Town or Azure South Africa North) | Docs complete |

Final deliverables:

1. Full source code in the layout from part 4.
2. Database migrations and seed scripts.
3. OpenAPI docs for every service in docs/api.
4. docs/setup.md: step by step setup on a clean Windows laptop (install .NET 10 SDK, Docker Desktop, Node.js LTS, clone, run one command, open each app).
5. docs/architecture.md with a diagram and a section per interface explaining how to swap a simulator for the real device, bank or Home Affairs adapter.
6. A test report listing every FR test and its result.
7. A short demo script (click by click) for showing FirmlyPaid to a bank.
