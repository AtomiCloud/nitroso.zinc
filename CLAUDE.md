# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Setup

```bash
# Initialize repository and restore dependencies
task setup
task setup:dotnet

# Alternative setup commands
dotnet restore --use-lock-file
dotnet tool restore
./scripts/local/secrets.sh
```

### Build and Test

```bash
# Build the application
task build
# or
dotnet build --no-restore

# Run tests
dotnet test

# Run unit tests specifically
dotnet test UnitTest/UnitTest.csproj

# Run integration tests specifically
dotnet test IntTest/IntTest.csproj
```

### Development Server

```bash
# Start development environment with cluster
task dev
# or
./scripts/local/dev.sh ./config/dev.yaml dotnet watch run --project App

# Run application locally without cluster
task run
# or
dotnet run --project App
```

### Database Operations

```bash
# Create new migration
task migration:create -- MigrationName
# or
dotnet ef migrations --project ./App add MigrationName
```

### Kubernetes Development

```bash
# Create k3d cluster
./scripts/local/create-k3d-cluster.sh

# Delete k3d cluster
task tear
# or
./scripts/local/delete-k3d-cluster.sh

# Execute commands in cluster
task exec -- [command]
```

## Architecture Overview

This is a .NET 8 microservice API for "BunnyBooker" - a booking and payment platform. The application follows a strict three-layer architecture with pure domain models and functional error handling.

### Layered Architecture

**Three-Layer Separation:**

- **Domain Layer** (`Domain/`) - Pure business logic with immutable records
- **Data Layer** (`App/Modules/*/Data/`) - EF Core entities and database mapping
- **API Layer** (`App/Modules/*/API/V1/`) - HTTP contracts and request/response models

**Project Structure:**

- **App/** - Web application hosting API controllers, data access, startup configuration
- **Domain/** - Pure business domain with services, models, interfaces (no external dependencies)
- **UnitTest/** - xUnit tests with FluentAssertions
- **IntTest/** - Integration tests

### Principal/Record Pattern

**Core Domain Model Structure:**

- **Record** = Pure business data without identity (e.g., `UserRecord { Username }`)
- **Principal** = Entity identity + metadata + Record (e.g., `UserPrincipal { Id, Record }`)
- **Aggregate Root** = Principal + related Principals from other domains

**Example Domain Models:**

```csharp
// Pure business data
public record UserRecord {
  public required string Username { get; init; }
}

// Entity with identity
public record UserPrincipal {
  public required string Id { get; init; }
  public required UserRecord Record { get; init; }
}

// Cross-domain aggregate
public record User {
  public required UserPrincipal Principal { get; init; }
  public required WalletPrincipal Wallet { get; init; }
}
```

### Mapping Strategy

**Four-Way Mapping Flow:**

1. **API → Domain**: `CreateUserReq.ToRecord()` → `UserRecord`
2. **Domain → Data**: `UserRecord.ToData()` → `UserData` entity
3. **Data → Domain**: `UserData.ToDomain()` → `User` aggregate
4. **Domain → API**: `User.ToRes()` → `UserRes` response

**Mapper Conventions:**

- `ToRecord()` - Convert requests to pure domain records
- `ToPrincipal()` - Build domain principal from data entity
- `ToDomain()` - Construct full aggregate with cross-domain references
- `ToRes()` - Transform domain models to API responses
- `ToData()` - Convert domain records to EF entities
- `Update()` - Mutate existing data entities with new records

### Result Monad Pattern

**Library**: `CSharp-Result` for railway-oriented programming

**Core Usage:**

```csharp
// Synchronous chaining
return walletRepo.GetByUserId(userId)
  .NullToError(userId)
  .Then(wallet => processWallet(wallet), Errors.MapAll);

// Async chaining
return service.Search(query.ToDomain())
  .ThenAwait(results => enrich.Enrich(results))
  .Then(x => x.Select(u => u.ToRes()), Errors.MapAll);

// Transaction wrapping
return transaction.Start(() =>
  walletRepo.GetByUserId(userId)
    .NullToError(userId)
    .ThenAwait(x => walletRepo.BookStart(x.Principal.Id, cost))
    .ThenAwait(w => transactionRepo.Create(w.Id, record))
);
```

**Key Extensions:**

- `.NullToError(id)` - Convert null results to `NotFoundException`
- `.ThenAwait()` - Chain async operations
- `.ValidateAsyncResult()` - FluentValidation integration
- `.GuardOrAnyAsync()` - Authorization checks
- `.DoAwait()` - Side effects (CDC events, logging)

**Controller Pattern:**

```csharp
var result = await this.GuardOrAnyAsync(userId, roles)
  .ThenAwait(_ => validator.ValidateAsyncResult(query, "Invalid Query"))
  .ThenAwait(q => service.Search(q.ToDomain()));
return this.ReturnResult(result);
```

### Domain Model Purity

**Immutable Records**: All domain models use `record` types with `init` properties
**No External Dependencies**: Domain layer references no infrastructure concerns
**Pure Functions**: Business logic operates on immutable data structures
**Aggregate Boundaries**: Clear ownership between domain entities

### Key Modules

- **Bookings** - Core booking with CDC (Change Data Capture)
- **Users** - User management and authentication
- **Payments** - Airwallex payment gateway integration
- **Wallets** - Digital wallet with transaction reserves
- **Transactions** - Financial transaction management
- **Schedules/Timings** - Booking schedule management
- **Passengers** - Passenger information management
- **Costs/Discounts** - Pricing and discount calculations

### Technology Stack

- **.NET 8** with ASP.NET Core Web API
- **Entity Framework Core** with PostgreSQL
- **Redis** for caching and streaming
- **MinIO** for object storage
- **OpenTelemetry** for observability (metrics, traces, logs)
- **JWT Authentication** via Descope
- **CSharp-Result** for functional error handling
- **Kubernetes** deployment with Helm charts

### Development Environment

- **Nix** for reproducible development environment
- **Tilt** for Kubernetes development workflow
- **k3d** for local Kubernetes clusters
- **mirrord** for local development with cluster connectivity
- **Infisical** for secrets management

### Configuration

- YAML-based settings with environment overrides
- Landscape-based configuration (lapras, pichu, pikachu, raichu, tauros)
- JSON schema validation with auto-generation
- Server/Migration mode support

## Coding Standards

### Always Use Result Monad Pattern

Write all business logic using the Result pattern for error handling:

```csharp
// ✅ Correct - Chain operations with Result
public Task<Result<BookingPrincipal>> CreateBooking(string userId, BookingRecord record)
{
  return walletRepo.GetByUserId(userId)
    .NullToError(userId)
    .ThenAwait(wallet => walletRepo.Reserve(wallet.Principal.Id, cost))
    .ThenAwait(w => bookingRepo.Create(userId, record));
}

// ❌ Wrong - Don't use exceptions for business logic
public async Task<BookingPrincipal> CreateBooking(string userId, BookingRecord record)
{
  var wallet = await walletRepo.GetByUserId(userId);
  if (wallet == null) throw new NotFoundException("User not found");
  // ... rest of logic
}
```

### Follow Principal/Record Mapping Conventions

Always maintain clear separation between domain models and data entities:

```csharp
// ✅ Correct - Use established mapper methods
public static class UserMapper
{
  public static UserRecord ToRecord(this CreateUserReq req) =>
    new() { Username = req.Username };

  public static UserPrincipal ToPrincipal(this UserData data) =>
    new() { Id = data.Id, Record = data.ToRecord() };

  public static User ToDomain(this UserData data) => new()
  {
    Principal = data.ToPrincipal(),
    Wallet = data.Wallet?.ToPrincipal() ?? throw new ApplicationException("Inconsistent state")
  };
}

// ❌ Wrong - Don't bypass mapping conventions
public User GetUser(UserData data)
{
  return new User
  {
    Principal = new UserPrincipal { Id = data.Id, Record = new UserRecord { Username = data.Username } },
    Wallet = new WalletPrincipal { Id = data.Wallet.Id, Record = new WalletRecord { ... } }
  };
}
```

### Controller Pattern

Always follow the established controller pattern with authorization, validation, and result handling:

```csharp
[Authorize, HttpPost]
public async Task<ActionResult<UserRes>> CreateUser([FromBody] CreateUserReq req)
{
  var result = await this.GuardAsync(AuthRoles.Admin)
    .ThenAwait(_ => validator.ValidateAsyncResult(req, "Invalid CreateUserReq"))
    .ThenAwait(validReq => service.Create(validReq.ToRecord()));

  return this.ReturnResult(result.Then(user => user.ToRes(), Errors.MapAll));
}
```

### Environment Variables

Set `LANDSCAPE` environment variable to target specific environments (lapras, pichu, pikachu, raichu, tauros, corsola).
