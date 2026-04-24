using DuckDB.NET.Data;

namespace Ojaswat.Database.Migrations;

/// <summary>
/// Migration 001 — Baseline schema (everything that existed in V12.4).
/// Safe to run multiple times only because DbInitializer checks the
/// _Migrations table first.
/// </summary>
public static class DbMigration_001
{
    public static void Up(DuckDBConnection conn)
    {
        Exec(conn, @"CREATE TABLE IF NOT EXISTS ErpDocuments (
            Id              INTEGER PRIMARY KEY,
            DocumentNo      TEXT    NOT NULL UNIQUE,
            DocType         TEXT    NOT NULL,
            DocDate         TEXT    NOT NULL,
            CreatedBy       TEXT    DEFAULT '',
            CompanyName     TEXT    DEFAULT '',
            CompanyPhone    TEXT    DEFAULT '',
            CompanyGSTIN    TEXT    DEFAULT '',
            CompanyAddress  TEXT    DEFAULT '',
            CompanyStateCode TEXT   DEFAULT '',
            CompanyEmail    TEXT    DEFAULT '',
            CompanyBank     TEXT    DEFAULT '',
            CompanyAccount  TEXT    DEFAULT '',
            CompanyIFSC     TEXT    DEFAULT '',
            CompanySignatory TEXT   DEFAULT '',
            CompanyLogoPath TEXT    DEFAULT '',
            CompanyQRCodePath TEXT  DEFAULT '',
            CustomerName    TEXT    DEFAULT '',
            CustomerGSTIN   TEXT    DEFAULT '',
            CustomerAddress TEXT    DEFAULT '',
            CustomerShipping TEXT   DEFAULT '',
            CustomerStateCode TEXT  DEFAULT '',
            CustomerRefNo   TEXT    DEFAULT '',
            EWayBillNo      TEXT    DEFAULT '',
            PlaceOfSupply   TEXT    DEFAULT '',
            GstMode         TEXT    DEFAULT 'CgstSgst',
            GstPercent      DECIMAL(5,2)  DEFAULT 18,
            Discount        DECIMAL(18,2) DEFAULT 0,
            Freight         DECIMAL(18,2) DEFAULT 0,
            PaymentTerms    TEXT    DEFAULT '',
            GeneralTerms    TEXT    DEFAULT '',
            GrandTotal      DECIMAL(18,2) DEFAULT 0,
            Status          TEXT    DEFAULT 'Open',
            PendingAmount   DECIMAL(18,2) DEFAULT 0,
            LastModified    TEXT    DEFAULT '');");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS ErpDocumentItems (
            DocumentId  INTEGER NOT NULL,
            ItemNumber  INTEGER NOT NULL,
            Description TEXT    NOT NULL,
            HSN         TEXT    DEFAULT '',
            UOM         TEXT    DEFAULT 'Nos',
            Quantity    DECIMAL(18,4) NOT NULL,
            Rate        DECIMAL(18,2) NOT NULL,
            GSTPercent  DECIMAL(5,2)  DEFAULT 0);");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS Customers (
            Id              INTEGER PRIMARY KEY,
            Name            TEXT    NOT NULL DEFAULT '',
            GSTIN           TEXT    DEFAULT '',
            BillingAddress  TEXT    DEFAULT '',
            ShippingAddress TEXT    DEFAULT '',
            StateCode       TEXT    DEFAULT '');");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS ItemMaster (
            Id          INTEGER PRIMARY KEY,
            Name        TEXT    NOT NULL DEFAULT '',
            HSN         TEXT    DEFAULT '',
            DefaultRate DECIMAL(18,2) DEFAULT 0,
            GSTPercent  DECIMAL(5,2)  DEFAULT 18,
            UOM         TEXT    DEFAULT 'Nos');");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS CompanyProfile (
            Id          INTEGER PRIMARY KEY DEFAULT 1,
            Name        TEXT DEFAULT '', Phone      TEXT DEFAULT '',
            GSTIN       TEXT DEFAULT '', Address    TEXT DEFAULT '',
            StateCode   TEXT DEFAULT '', Email      TEXT DEFAULT '',
            Bank        TEXT DEFAULT '', Account    TEXT DEFAULT '',
            IFSC        TEXT DEFAULT '', Signatory  TEXT DEFAULT '',
            LogoPath    TEXT DEFAULT '', QRCodePath TEXT DEFAULT '');");

        // Ensure exactly one company row
        Exec(conn, @"INSERT INTO CompanyProfile (Id)
                     SELECT 1 WHERE NOT EXISTS
                     (SELECT 1 FROM CompanyProfile WHERE Id=1);");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS TandCMaster (
            Id           INTEGER PRIMARY KEY,
            DocType      TEXT DEFAULT 'All',
            Label        TEXT DEFAULT '',
            PaymentTerms TEXT DEFAULT '',
            GeneralTerms TEXT DEFAULT '');");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS DocTemplates (
            DocType      TEXT PRIMARY KEY,
            HtmlTemplate TEXT DEFAULT '',
            PdfTemplate  TEXT DEFAULT '');");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS Payments (
            Id         INTEGER PRIMARY KEY,
            DocumentNo TEXT    DEFAULT '',
            PartyName  TEXT    DEFAULT '',
            PayDate    TEXT    NOT NULL,
            Amount     DECIMAL(18,2) DEFAULT 0,
            Mode       TEXT    DEFAULT 'Cash',
            Reference  TEXT    DEFAULT '',
            Notes      TEXT    DEFAULT '',
            CreatedAt  TEXT    DEFAULT '');");
    }

    private static void Exec(DuckDBConnection conn, string sql)
    {
        var c = conn.CreateCommand();
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }
}
