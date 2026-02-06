# ZIP Code Lookup (Same as Mental Health App)

ZIP-to-lat/lon lookup is done **from the database** only—no external API (Nominatim, etc.), so there is **no cost or rate limits**.

## Data source

- **Entity**: `ZipCodeLookup` (table `ZipCodeLookup`) — same structure as the mental health app.
- **Seeding**: `SeedData.SeedAsync()` seeds the same sample ZIPs as mental health (KC metro 66062, 66221, 64138, etc.; CA, NY, TX, IL).
- **Adding more ZIPs**: You can:
  1. **From mental health DB**: Export `ZipCodeLookup` from the mental health database and insert into this service’s `ZipCodeLookup` table (same schema).
  2. **From mental health SQL**: Run the same INSERT script used in mental health (e.g. `AddLocationMatchingSystem.sql` or `AddServiceRequestMigration_Consolidated.sql` section for ZipCodeLookup) against this service’s database, or copy those INSERTs into a new migration/seed.
  3. **Extend SeedData**: Add more `ZipCodeLookup` entries in `Data/SeedData.cs` (only runs when the table is empty).

## Schema (matches mental health)

- `ZipCode` (PK, VARCHAR 10)
- `Latitude`, `Longitude` (DECIMAL)
- `City`, `State` (optional)
- `CreatedAt`

Migration: `AddZipCodeLookup`. On startup, `Program.cs` runs `db.Database.Migrate()` then `SeedData.SeedAsync(db)`.
