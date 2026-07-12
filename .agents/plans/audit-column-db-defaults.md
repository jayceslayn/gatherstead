# Plan: Add database-side default values for audit columns

## Context

Today all four audit-ish fields are populated **only in C#**, never by the database:

- `Id` — EF Core's client-side `SequentialGuidValueGenerator` (`ValueGeneratedOnAdd`, no DB default).
- `CreatedAt` / `UpdatedAt` (`DateTimeOffset`, non-nullable) — stamped by `AuditingSaveChangesInterceptor` using `DateTimeOffset.UtcNow` (`src/Gatherstead.Data/Interceptors/AuditingSaveChangesInterceptor.cs:151-161`).
- `IsDeleted` (`bit`) — managed by the same interceptor (soft-delete) + a global query filter (`src/Gatherstead.Data/GathersteadDbContext.cs:273-279`).

The columns carry **no DB default** (confirmed: zero `HasDefaultValueSql`/`HasDefaultValue` in the codebase). The goal is a **defense-in-depth backstop**: rows inserted *outside EF* — data seeding, raw SQL, manual DB fixes — that omit these columns should still get sane values. The interceptor / EF generator remain authoritative for all normal API writes.

### How this interacts with the API (why it neither breaks nor changes normal writes)

`HasDefaultValueSql(...)` flips a property to `ValueGeneratedOnAdd`; EF then omits the column from the `INSERT` **only** when it holds the CLR-default sentinel, otherwise it sends the explicit value and bypasses the DB default. Because the interceptor sets `CreatedAt`/`UpdatedAt` to non-sentinel values *before* EF builds the `INSERT`, those explicit values win for API writes; the DB default only fires for inserts that omit the column (seeding). For `IsDeleted`, the app value on insert is `false` (= sentinel) so EF omits it and the DB default `0` fills in — same result. Two caveats, both acceptable for a backstop:

- A `DEFAULT` fires on `INSERT` only, **never on `UPDATE`** — so it can't keep `UpdatedAt` current; the interceptor still must. Fine (backstop only).
- Columns are `datetimeoffset`; `SYSUTCDATETIME()` returns `datetime2`. Implicit conversion yields offset `+00:00` (correct for UTC).

### The Id exception — must be out-of-model to preserve "EF value wins"

Configuring `Id` with model-integrated `HasDefaultValueSql("NEWID()")` would **disable** EF's client-side `SequentialGuidValueGenerator`. Since app code never sets `Id`, every normal API insert would then fall through to **random** `NEWID()` on the (clustered) PK of temporal tables → page splits / fragmentation. To honor the stated intent (EF's sequential value wins; `NEWID()` only for seed inserts), `Id`'s default is added **out-of-model** as a raw `DEFAULT` constraint EF doesn't know about. EF keeps sending its sequential GUID; `NEWID()` only fills seed inserts that omit `Id`.

## Approach

Two mechanisms, shipped in one migration.

### 1. Model-integrated defaults for `CreatedAt` / `UpdatedAt` / `IsDeleted`

In `GathersteadDbContext.OnModelCreating`, add a loop over `IAuditableEntity` types (these are the only types that have these columns), guarded by `Database.IsSqlServer()` — mirroring the existing temporal guard at `GathersteadDbContext.cs:91` so the **SQLite test schema is not fed SQL-Server-only default expressions**:

```csharp
if (Database.IsSqlServer())
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType))
            continue;
        var eb = modelBuilder.Entity(entityType.ClrType);
        eb.Property(nameof(IAuditableEntity.CreatedAt)).HasDefaultValueSql("SYSUTCDATETIME()");
        eb.Property(nameof(IAuditableEntity.UpdatedAt)).HasDefaultValueSql("SYSUTCDATETIME()");
        eb.Property(nameof(IAuditableEntity.IsDeleted)).HasDefaultValueSql("0");
    }
}
```

Place it alongside the existing reflective helpers (near `ApplyGlobalFilters`, `GathersteadDbContext.cs:242-257`) and invoke from `OnModelCreating`. The non-generic `modelBuilder.Entity(clrType).Property(string)` avoids the generic-reflection dance used by `ApplyAuditableFilters`.

### 2. Out-of-model `NEWID()` default for `Id`

Do **not** put `Id` in `OnModelCreating`. Instead, in the generated migration's `Up()`, add a raw `DEFAULT` constraint per auditable temporal table; drop them in `Down()`. Adding a `DEFAULT` constraint does **not** require toggling `SYSTEM_VERSIONING`. Guard each with an existence check so the idempotent CI script is safe:

```csharp
// For each auditable table <T>:
migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_<T>_Id')
    ALTER TABLE [dbo].[<T>] ADD CONSTRAINT [DF_<T>_Id] DEFAULT NEWID() FOR [Id];");
```

Generate the per-table list from the model rather than hand-typing 40+ tables (e.g. a small `foreach` over `Model.GetEntityTypes()` filtered to `IAuditableEntity`, emitting one `migrationBuilder.Sql` per table name).

## Files to modify

- **`src/Gatherstead.Data/GathersteadDbContext.cs`** — add the guarded defaults loop (mechanism 1) and its invocation in `OnModelCreating`.
- **New migration `src/Gatherstead.Data/Migrations/<timestamp>_AddAuditColumnDefaults.cs`** — generated by EF for mechanism 1, then hand-edited to append the mechanism-2 `Id` raw SQL to `Up()`/`Down()`.
- **`GathersteadDbContextModelSnapshot.cs`** — auto-updated by the migration for the three model-integrated defaults (not for the out-of-model `Id` constraint, which stays EF-invisible and therefore stable across future scaffolds).

## Scope notes

- The timestamp/flag defaults apply only to `IAuditableEntity` types (only they declare those columns). `SecurityEvent` and `DietaryTag` are non-auditable, non-temporal, and already supply their own `Id` (`SecurityEvent.Id = Guid.NewGuid()`; `DietaryTag` seeded with explicit GUIDs) — excluded; add later only if a seeding need arises.
- Model-integrated `AlterColumn` on temporal tables wraps `SYSTEM_VERSIONING OFF/ON` in the migration; the CI migration identity already has `db_owner` (`infrastructure/ci-grant.sql`), so this deploys as-is.
- Deployment path is unchanged: CI `deploy-migrations` job runs `dotnet ef migrations script --idempotent` → `sqlcmd` (`.github/workflows/ci-cd.yml:145-210`). No app-startup migration.

## Verification

1. **Generate & inspect**: `dotnet ef migrations add AddAuditColumnDefaults --project src/Gatherstead.Data`; hand-edit the `Id` raw SQL; review `git diff` — confirm the three `AlterColumn`s carry `defaultValueSql`, the `SYSTEM_VERSIONING` wrapping is present, and the `Id` `DEFAULT NEWID()` block is guarded.
2. **Build gates**: `dotnet build Gatherstead.sln` (0 errors / 0 warnings) and `dotnet test Gatherstead.sln` (all pass).
3. **Model-config assertion test** (the test suite uses **SQLite**, which cannot execute `SYSUTCDATETIME()`/`NEWID()` defaults, so behavior can't be exercised there): add a unit test that builds the context with the **SQL Server** provider (no live DB needed — the model is built in memory) and asserts, e.g., `context.Model.FindEntityType(typeof(Household))!.FindProperty("CreatedAt")!.GetDefaultValueSql() == "SYSUTCDATETIME()"` for `CreatedAt`/`UpdatedAt`/`IsDeleted` across a representative auditable entity.
4. **Regression guard**: confirm a normal EF insert still yields a **sequential** client `Id` and interceptor-stamped `CreatedAt`/`UpdatedAt` (unchanged behavior — the model-integrated defaults must not steal these; the `Id` out-of-model choice guarantees the sequential generator survives).
5. **DB backstop (manual, against SQL Server / LocalDB)**: raw `INSERT` into an auditable table omitting `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted` → row gets a `NEWID()` id, `SYSUTCDATETIME()` timestamps, and `IsDeleted = 0`. This is the behavior the whole change exists to add.

## Stop signals

- If EF scaffolds `AlterColumn` for `Id` (means a store default leaked into the model / client generator got disabled) — stop; `Id` must stay out-of-model.
- If applying `HasDefaultValueSql` breaks SQLite test schema creation despite the `IsSqlServer()` guard — stop and reassess the guard placement.
