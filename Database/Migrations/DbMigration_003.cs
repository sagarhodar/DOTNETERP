using DuckDB.NET.Data;

namespace Ojaswat.Database.Migrations;

/// <summary>
/// Migration 003 — Inventory Management + Party Ledger.
///
/// NEW TABLES:
///   StockLedger   — every stock movement (IN / OUT) linked to a document
///   PartyLedger   — every debit/credit entry per party linked to a document
///
/// NEW COLUMNS on ItemMaster:
///   CurrentStock  — running balance (updated automatically via trigger service)
///   ReorderLevel  — alert threshold
///   OpeningStock  — stock at the time the item was created
///
/// NEW COLUMNS on Customers:
///   PartyType     — Customer / Vendor / Both
///   OpeningBalance — opening debit balance
/// </summary>
public static class DbMigration_003
{
    public static void Up(DuckDBConnection conn)
    {
        // ── Extend ItemMaster with stock columns ──────────────────────────────
        TryAlter(conn, "ALTER TABLE ItemMaster ADD COLUMN CurrentStock  DECIMAL(18,4) DEFAULT 0;");
        TryAlter(conn, "ALTER TABLE ItemMaster ADD COLUMN ReorderLevel  DECIMAL(18,4) DEFAULT 0;");
        TryAlter(conn, "ALTER TABLE ItemMaster ADD COLUMN OpeningStock  DECIMAL(18,4) DEFAULT 0;");

        // ── Extend Customers with party type + opening balance ─────────────────
        TryAlter(conn, "ALTER TABLE Customers ADD COLUMN PartyType       TEXT    DEFAULT 'Customer';");
        TryAlter(conn, "ALTER TABLE Customers ADD COLUMN OpeningBalance  DECIMAL(18,2) DEFAULT 0;");
        TryAlter(conn, "ALTER TABLE Customers ADD COLUMN OpeningBalanceDate TEXT DEFAULT '';");

        // ── Stock Ledger ───────────────────────────────────────────────────────
        // Records every individual stock movement.
        // MovementType: IN (purchase/GRN/opening) | OUT (sales/credit note)
        //               ADJUST_IN | ADJUST_OUT (manual adjustments)
        Exec(conn, @"CREATE TABLE IF NOT EXISTS StockLedger (
            Id           INTEGER PRIMARY KEY,
            EntryDate    TEXT    NOT NULL,
            ItemName     TEXT    NOT NULL,
            HSN          TEXT    DEFAULT '',
            UOM          TEXT    DEFAULT 'Nos',
            MovementType TEXT    NOT NULL,
            Quantity     DECIMAL(18,4) NOT NULL,
            Rate         DECIMAL(18,2) DEFAULT 0,
            Value        DECIMAL(18,2) DEFAULT 0,
            DocumentNo   TEXT    DEFAULT '',
            DocType      TEXT    DEFAULT '',
            PartyName    TEXT    DEFAULT '',
            Narration    TEXT    DEFAULT '',
            CreatedAt    TEXT    DEFAULT '');");

        // ── Party Ledger ───────────────────────────────────────────────────────
        // Double-entry: every document creates a Debit or Credit entry per party.
        // EntryType:
        //   DEBIT  = party owes us money  (Sales Invoice, Debit Note)
        //   CREDIT = we owe party money   (Purchase Invoice, Credit Note)
        //   PAYMENT_IN  = party paid us
        //   PAYMENT_OUT = we paid party
        //   OPENING     = opening balance entry
        Exec(conn, @"CREATE TABLE IF NOT EXISTS PartyLedger (
            Id          INTEGER PRIMARY KEY,
            EntryDate   TEXT    NOT NULL,
            PartyName   TEXT    NOT NULL,
            PartyType   TEXT    DEFAULT 'Customer',
            EntryType   TEXT    NOT NULL,
            Debit       DECIMAL(18,2) DEFAULT 0,
            Credit      DECIMAL(18,2) DEFAULT 0,
            Balance     DECIMAL(18,2) DEFAULT 0,
            DocumentNo  TEXT    DEFAULT '',
            DocType     TEXT    DEFAULT '',
            Narration   TEXT    DEFAULT '',
            CreatedAt   TEXT    DEFAULT '');");
    }

    // DuckDB does not support IF NOT EXISTS on ALTER TABLE — catch and ignore
    private static void TryAlter(DuckDBConnection conn, string sql)
    {
        try { Exec(conn, sql); } catch { /* column already exists — safe to ignore */ }
    }

    private static void Exec(DuckDBConnection conn, string sql)
    {
        var c = conn.CreateCommand();
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }
}
