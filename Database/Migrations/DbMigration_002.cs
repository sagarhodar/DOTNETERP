using DuckDB.NET.Data;

namespace Ojaswat.Database.Migrations;

/// <summary>
/// Migration 002 — Example future migration.
/// Add your next schema change here. DbInitializer runs it exactly once.
/// </summary>
public static class DbMigration_002
{
    public static void Up(DuckDBConnection conn)
    {
        // Example: add a DeliveryDate column to ErpDocuments
        // ALTER TABLE ErpDocuments ADD COLUMN DeliveryDate TEXT DEFAULT '';
        //
        // Add your real change below:
    }
}
