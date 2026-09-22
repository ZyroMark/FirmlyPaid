# FirmlyPaid

Pay at a till with one finger. No card, no phone, no cash.

FirmlyPaid is a biometric payment platform for South Africa, owned by ZYROMARK PTY LTD.
A customer places a finger on the unit's own touchscreen, types the four digits after their
birth date in their ID number, picks a bank if they have more than one linked, and the
payment goes to the sponsor bank over PayShap. FirmlyPaid never holds customer money.

This repository is the phase 0 build. Every piece of hardware and every outside party is
replaced by a simulator that uses the same interface the real one will use, so the whole
system runs end to end on a developer laptop.

## Where to start

| You want to | Read |
| --- | --- |
| Set it up on a clean Windows laptop | [docs/setup.md](docs/setup.md) |
| Understand how it fits together | [docs/architecture.md](docs/architecture.md) |
| See the API contracts | [docs/api](docs/api) |
| Know what is being built and in what order | [CLAUDE.md](CLAUDE.md) |

## Quick start

```powershell
dotnet tool restore
Copy-Item .env.example .env   # then edit it and set your own passwords
dotnet build
dotnet test
.\scripts\dev-up.ps1 -Rebuild
.\scripts\db-setup.ps1
.\scripts\check-health.ps1
```

## What is simulated

Nothing about the hardware, the bank or Home Affairs is real yet. Each one sits behind an
interface in `FirmlyPaid.Shared`, and the implementation is chosen by name in
configuration, so swapping in the real thing is a new adapter class plus a config change.

| Interface | Today | Later |
| --- | --- | --- |
| `IVeinScanner`, `IVeinMatcher` | Simulated test fingers with noise on every read | Hitachi finger vein SDK |
| `IFingerprintScanner` | Stored sample per test person | Suprema BioMini Slim 2 |
| `IHomeAffairsVerifier` | 20 test identities covering every outcome | Verification partner API |
| `IBankGateway` | Four fake banks with real balances | Sponsor bank PayShap API |
| `IKeyVault` | AES key in a local dev file | AWS KMS, then CloudHSM |
| `ISmsSender` | Console and a table | SMS provider |

## Build progress

Part 11 of [CLAUDE.md](CLAUDE.md) lists twelve steps. Steps 1 to 3 are done: the solution
skeleton and Docker Compose stack, both databases with migrations and a seed script, and
all seven simulators. Service endpoints start at step 4.

## Security

The rules in part 10 of [CLAUDE.md](CLAUDE.md) are not negotiable, and several of them are
enforced by tests rather than by convention. No raw biometric image is ever stored, logged
or sent. Templates are encrypted and live in a separate database that only the Matching
service can reach. No ID number, PIN, template, account number or token may reach a log.

No secret is in this repository. Connection strings, the ID number pepper and every key
come from environment variables or a local `.env` file that is not committed.

If you find a security problem, please contact ZYROMARK PTY LTD directly rather than
opening a public issue.

## Licence

Copyright ZYROMARK PTY LTD. All rights reserved. This source is published for review and
is not licensed for reuse.
