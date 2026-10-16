# CareTogether

CareTogether is a modular care-coordination starter for iOS and Android. It includes a .NET MAUI app, ASP.NET Core API, separate AI-assistance service, background reminder worker, EF Core migration, and SQL Server-compatible database running in Docker.

## Phase 1–4 scope

1. **Foundation:** JWT authentication with rotating refresh tokens, care circles, role-aware team membership, dashboard, Docker infrastructure, migration, and sample data.
2. **Daily coordination:** shared tasks, medication list and dose logging, appointments, and care-team roster.
3. **Communication:** conversations/messages and explicit care handoffs with sender, recipient, risks, next steps, and acceptance.
4. **Assistance:** independently deployable AI summary/handoff-draft service plus a reminder worker. AI output is always labeled as draft content requiring human review.

The mobile project contains 16 screens: Login, Onboarding, Dashboard, Care Circle, Care Plan, Task Detail, Medications, Medication Detail, Appointments, Appointment Detail, Conversations, Chat, Handoff List, Handoff Editor, AI Assistant, and Profile & Settings.

> The original GUI image assets are not present in this workspace. The screens implement the original information architecture and a reusable CareTogether visual system, but exact pixel matching requires those 16 images to be reattached.

## Architecture

```text
CareTogether.Mobile  ──HTTP/JWT──> CareTogether.Api ──EF Core──> SQL Server
                                 └> CareTogether.Ai (independent HTTP service)
CareTogether.Worker ────────────────EF Core──────────> SQL Server
```

Each executable has its own project and container boundary. `Domain` has entities only, `Application` has transport contracts/interfaces, and `Infrastructure` owns persistence/auth implementations.

## Prerequisites (Apple silicon Mac)

- Docker Desktop with at least 4 GB assigned
- Microsoft-distributed .NET 10 SDK (this repository is pinned to `10.0.401` or its latest patch by `global.json`)
- Xcode 15+ (open once and accept its license)
- Android Studio with Android SDK/API 34 and an arm64 emulator
- MAUI workload: `sudo dotnet workload install maui`

If Homebrew's isolated `dotnet@8` appears first in `PATH`, select the Microsoft host first with `export PATH="/usr/local/share/dotnet:$PATH"`. Check with `dotnet --version`, `dotnet workload list`, `xcode-select -p`, and `docker version`.

## Start the backend

```bash
cp .env.example .env
docker compose up --build
```

Services:

- API and Swagger: `http://localhost:8080/swagger`
- API health: `http://localhost:8080/health`
- AI service: `http://localhost:8081/health`
- SQL Server 2022: `localhost:1433` (runs under Docker Desktop's amd64 emulation on Apple silicon)

The API applies the checked-in `202610110001_InitialCreate` migration and seeds data on its first start.

Demo accounts (development only):

- `alex@caretogether.local` / `CareTogether123!`
- `dr.lee@caretogether.local` / `CareTogether123!`

Never reuse the sample password or development JWT key outside local development.

## Verify the API

```bash
TOKEN=$(curl -s http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"alex@caretogether.local","password":"CareTogether123!"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])')
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/dashboard
```

Other authenticated resources are under `/api/tasks`, `/api/medications`, `/api/appointments`, `/api/conversations`, `/api/messages`, and `/api/handoffs`.

## Database migrations

The first migration is checked in. For model changes:

```bash
dotnet tool install --global dotnet-ef --version 8.*
dotnet ef migrations add DescribeChange \
  --project src/CareTogether.Infrastructure \
  --startup-project src/CareTogether.Api
dotnet ef database update \
  --project src/CareTogether.Infrastructure \
  --startup-project src/CareTogether.Api
```

Production deployments should run migrations as a controlled release step and set `Database__ApplyMigrations=false` on ordinary API replicas.

### Empty the local database

To delete all CareTogether application data while preserving the database schema and Entity Framework migration history:

```bash
./scripts/empty-database.sh --confirm
```

The script stops the API and background worker first and leaves them stopped. After reviewing the result, restart them with `docker compose up -d api worker`. This operation is irreversible without a database backup.

## Run and test iOS

For local development, start every backend service, wait for the API, build the app, and launch it in the currently booted Simulator with:

```bash
./scripts/run-ios-local.sh
```

The mobile build does not implicitly start the independently deployable backend services. To run the steps manually:

1. Start the backend with `docker compose up -d --build`.
2. Install workloads and restore: `dotnet workload restore src/CareTogether.Mobile/CareTogether.Mobile.csproj`.
3. List simulators: `xcrun simctl list devices available`.
4. Build, install, and launch the app:

   ```bash
   /usr/local/share/dotnet/dotnet build src/CareTogether.Mobile/CareTogether.Mobile.csproj \
     -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
   xcrun simctl install booted src/CareTogether.Mobile/bin/Debug/net10.0-ios/iossimulator-arm64/CareTogether.Mobile.app
   xcrun simctl launch booted com.caretogether.mobile
   ```

5. Sign in with the Alex demo account.
6. Verify dashboard data, mark a task/dose, inspect appointments, send a message, create/accept a handoff, and review an AI draft.

The iOS simulator reaches the host API at `http://localhost:8080`. Local-network HTTP is enabled only for development; use HTTPS in release builds. A physical iPhone must use the Mac’s LAN IP and an HTTPS development certificate trusted by the device.

## Run and test Android

1. In Android Studio, create an ARM64 API 34 emulator and start it.
2. Start the backend with Docker Compose.
3. Run `dotnet build src/CareTogether.Mobile/CareTogether.Mobile.csproj -t:Run -f net10.0-android`.
4. Sign in and repeat the workflow checks above.

The Android emulator uses `http://10.0.2.2:8080` to reach the host. Cleartext traffic is enabled in the development manifest; replace it with HTTPS/network security configuration for release. A physical Android device must use the Mac’s LAN IP or `adb reverse tcp:8080 tcp:8080`.

## Tests and quality gates

```bash
dotnet build src/CareTogether.Api/CareTogether.Api.csproj
dotnet test tests/CareTogether.Tests/CareTogether.Tests.csproj
docker compose config
curl --fail http://localhost:8080/health
curl --fail http://localhost:8081/health
```

Before production, add a managed secret store, HTTPS termination, rate limits, audit/event retention, push-notification provider, encrypted PHI policy, backups, observability, and a formal HIPAA/security assessment. This repository is an application foundation, not a compliance certification or clinical decision system.
