# FirmlyPaid setup on a clean Windows laptop

Owner: ZYROMARK PTY LTD. This guide covers everything built so far (step 1 of part 11).

## 1. Install the tools

Run each installer, then close and reopen your terminal so the new commands are on your PATH.

1. **.NET 10 SDK (LTS)** - https://dotnet.microsoft.com/download/dotnet/10.0
2. **Docker Desktop** - https://www.docker.com/products/docker-desktop/ (choose the WSL 2 backend when asked)
3. **Node.js LTS** - https://nodejs.org (needed from step 10 for the customer portal)
4. **Git** - https://git-scm.com/download/win

Check each one:

```powershell
dotnet --version   # expect 10.0.x
docker --version   # expect 29.x or newer
node --version     # expect v22 or newer
git --version
```

## 2. Get the code

```powershell
git clone <repository-url> C:\Server\FirmlyPaid
cd C:\Server\FirmlyPaid
```

## 3. Set your local passwords

Secrets never live in source control (rule 10.15). Copy the example file and edit it:

```powershell
Copy-Item .env.example .env
notepad .env
```

Set `MSSQL_SA_PASSWORD` to at least 8 characters with an upper case letter, a lower case
letter, a digit and a symbol, or SQL Server will refuse to start.

Set `FIRMLYPAID_ID_PEPPER` to at least 32 random characters. It is the secret that makes
a stored ID number hash useless to anyone who steals the database. Set it once per
environment: changing it later makes every existing hash unmatchable.

## 4. Build and test

```powershell
dotnet build
dotnet test
```

Both must finish with no errors before you go further.

## 5. Start the whole stack

```powershell
.\scripts\dev-up.ps1 -Rebuild
```

The first run downloads the SQL Server and RabbitMQ images and builds seven service
images, so allow about five to ten minutes. Later runs take seconds.

Behind the scenes this is:

```powershell
docker compose up -d --build
```

## 6. Create and fill the databases

With the stack running:

```powershell
.\scripts\db-setup.ps1
```

This creates `FirmlyPaidCore` and `FirmlyPaidVault` from the EF Core migrations, then adds
the demo data: 3 merchants, 4 stores, 5 terminals, 10 customers and 19 linked accounts.
Running it again is safe, because seeding skips if the demo data is already there.

Every seeded customer is given the demo PIN `1234`, so a demonstration never stalls on a
forgotten number. If your database was seeded before step 4 those customers have no PIN
yet: run `.\scripts\db-setup.ps1 -Command reset` to rebuild them.

Other commands:

```powershell
.\scripts\db-setup.ps1 -Command status         # count the rows in every table
.\scripts\db-setup.ps1 -Command verify-audit   # check the audit log has not been tampered with
.\scripts\db-setup.ps1 -Command reset          # delete everything and seed again, asks first
```

### Looking at the data in SQL Server Management Studio

The databases live in the Docker container, not in any SQL Server you already have
installed. Connect to the container:

| Field | Value |
| --- | --- |
| Server name | `localhost,14330` |
| Authentication | SQL Server Authentication |
| Login | `sa` |
| Password | your `MSSQL_SA_PASSWORD` from `.env` |
| Trust server certificate | ticked |

The container uses port 14330 because a SQL Server installed directly on Windows usually
holds 1433 already. If you would rather use your own instance, point the tool at it:

```powershell
.\scripts\db-setup.ps1 -SqlServer "localhost"
```

The containers themselves always talk to `sqlserver:1433` inside the Docker network, so
they keep using the container either way.

## 7. Check that everything answers

```powershell
.\scripts\check-health.ps1
```

You should see `OK` for all seven services.

## 8. Open the services

| Service | Health | API docs |
| --- | --- | --- |
| Gateway | http://localhost:5100/health | http://localhost:5100/swagger |
| Enrolment API | http://localhost:5101/health | http://localhost:5101/swagger |
| Matching API | http://localhost:5102/health | http://localhost:5102/swagger |
| AccountLink API | http://localhost:5103/health | http://localhost:5103/swagger |
| Payments API | http://localhost:5104/health | http://localhost:5104/swagger |
| Risk API | http://localhost:5105/health | http://localhost:5105/swagger |
| Till Integration API | http://localhost:5106/health | http://localhost:5106/swagger |

Supporting services:

| Service | Address | Login |
| --- | --- | --- |
| SQL Server | `localhost,14330` | user `sa`, password from your `.env` |
| RabbitMQ management | http://localhost:15672 | user and password from your `.env` |

## 9. Enrol a customer

The Enrolment and Matching services are live from step 4. The three calls are documented
at http://localhost:5101/swagger, and they run in this order:

1. `POST /enrolments` with the ID number, name, cellphone, agent id, store id, consent
   version and a fingerprint sample. The reply carries the `EnrolmentId` and what Home
   Affairs said.
2. `POST /enrolments/{id}/vein-samples`, twice: three encrypted samples of one finger each
   time, for two different fingers. A poor read comes back with `accepted: false` and a
   retry message rather than an error.
3. `POST /enrolments/{id}/complete` with the encrypted PIN. The reply is the new
   `CustomerId`.

The samples have to be encrypted with the same key the services hold, so the easiest way
to run the whole flow by hand is from the enrolment app, which arrives in step 8. Until
then, the flow is exercised end to end by the tests in
`tests\FirmlyPaid.Enrolment.Api.Tests`, which run both services against a real database.

The twenty test identities the Home Affairs simulator answers for are in
`src\FirmlyPaid.DemoData\HomeAffairsRoster.cs`: twelve that match, three that do not, two
marked deceased and three where the service is down.

## 10. Stop the stack

```powershell
.\scripts\dev-down.ps1          # stop, keep the data
.\scripts\dev-down.ps1 -Clean   # stop and wipe the local databases and queues
```

## 11. Refresh the API documents

With the stack up:

```powershell
.\scripts\export-openapi.ps1
```

This writes one OpenAPI file per service into `docs\api`.

## Running a single service without Docker

Useful while you are working on one service:

```powershell
dotnet run --project src\FirmlyPaid.Payments.Api
```

It listens on the port in that project's `Properties\launchSettings.json` (5104 for
Payments). Databases are still expected at `localhost,14330`, so leave the `sqlserver`
container running.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `docker compose` says the daemon is not running | Start Docker Desktop and wait for the whale icon to stop animating. |
| SQL Server container keeps restarting | Your `MSSQL_SA_PASSWORD` is too weak. Change it in `.env`, then `.\scripts\dev-down.ps1 -Clean` and start again. |
| A service shows `DOWN` | `docker compose logs <service>` - for example `docker compose logs payments-api`. |
| Enrolment or Matching will not start | They need more than a connection string from step 4. Enrolment wants `FIRMLYPAID_ID_PEPPER` and Matching wants `ConnectionStrings__Vault`; both want `FirmlyPaid__Security__KeyFilePath`. Compose sets all of these from your `.env`. |
| Matching answers `INTERNAL_ERROR` on every match | The two services are not sharing a key file, so Matching cannot decrypt what Enrolment sent. In compose they share the `devkeys` volume; outside it, point both at the same `FirmlyPaid__Security__KeyFilePath`. |
| Port already in use | Something else holds 5100-5106, 14330, 5672 or 15672. Stop it, or change the left-hand port in `docker-compose.yml`. |
| The databases are not in SQL Server Management Studio | You are probably looking at a SQL Server installed on Windows. The FirmlyPaid databases are in the container: connect to `localhost,14330` as `sa`. |
| `Login failed for user 'sa'` | Your `.env` password no longer matches the container. Run `.\scripts\dev-down.ps1 -Clean`, then start again so the container is created with the current password. |

## Working on the database schema

The EF Core tools are pinned in `.config/dotnet-tools.json`, so everyone uses the same
version. Restore them once after cloning:

```powershell
dotnet tool restore
```

After changing an entity, create a migration:

```powershell
dotnet ef migrations add <Name> --project src\FirmlyPaid.Data.Core --context FirmlyPaidCoreDbContext --output-dir Migrations
```

Then apply it with `.\scripts\db-setup.ps1 -Command migrate`. Use `FirmlyPaid.Data.Vault`
and `FirmlyPaidVaultDbContext` for the vault.
