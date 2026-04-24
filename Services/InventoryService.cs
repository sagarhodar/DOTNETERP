using DuckDB.NET.Data;
using Ojaswat.Models;

namespace Ojaswat.Services;

/// <summary>
/// Inventory Service — manages StockLedger and PartyLedger.
///
/// KEY RULES:
///   • Every time a document is SAVED, PostDocument() is called automatically.
///   • PostDocument() reverses any prior posting for that DocumentNo, then
///     re-posts fresh — so editing a document always gives correct ledger.
///   • Stock MOVEMENTS:
///       SalesInvoice / SalesOrder / CreditNote  → OUT (reduce stock)
///       PurchaseInvoice / GRN / DebitNote        → IN  (increase stock)
///       Quotation / PurchaseOrder                → No stock movement
///   • Party LEDGER:
///       SalesInvoice / DebitNote  → DEBIT  party (party owes us)
///       PurchaseInvoice / CreditNote → CREDIT party (we owe party)
///       Payment received  → PAYMENT_IN  (reduces debit balance)
///       Payment made      → PAYMENT_OUT (reduces credit balance)
/// </summary>
public class InventoryService
{
    private readonly DuckDbService _db;

    public InventoryService(DuckDbService? db = null)
    {
        _db = db ?? new DuckDbService();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DOCUMENT POSTING  (called from ErpDocumentDbService after save/update)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reverse old entries for this document and write fresh ones.
    /// Safe to call on new documents (nothing to reverse) and edits alike.
    /// </summary>
    public void PostDocument(ErpDocument doc)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        // 1. Delete previous stock + ledger entries for this document
        Cmd(conn, "DELETE FROM StockLedger  WHERE DocumentNo=?;", doc.DocumentNo).ExecuteNonQuery();
        Cmd(conn, "DELETE FROM PartyLedger  WHERE DocumentNo=?;", doc.DocumentNo).ExecuteNonQuery();

        // 2. Post stock movements
        PostStock(conn, doc);

        // 3. Post party ledger entry
        PostPartyLedger(conn, doc);

        // 4. Rebuild running stock balances for affected items
        foreach (var item in doc.Items)
            RebuildItemStock(conn, item.Description);
    }

    private static void PostStock(DuckDBConnection conn, ErpDocument doc)
    {
        bool isOut = ModuleDocTypes.StockOut.Contains(doc.DocType);
        bool isIn  = ModuleDocTypes.StockIn.Contains(doc.DocType);
        if (!isOut && !isIn) return;   // Quotation / PurchaseOrder — no stock movement

        var movType = isIn ? StockMovement.IN : StockMovement.OUT;

        foreach (var item in doc.Items)
        {
            if (item.Quantity <= 0) continue;
            InsertStockEntry(conn, new StockLedgerEntry
            {
                EntryDate    = doc.Date,
                ItemName     = item.Description,
                HSN          = item.HSN,
                UOM          = item.UOM,
                MovementType = movType,
                Quantity     = item.Quantity,
                Rate         = item.Rate,
                DocumentNo   = doc.DocumentNo,
                DocType      = doc.DocType.ToString(),
                PartyName    = doc.Customer.Name,
                Narration    = $"{doc.DocType} — {doc.DocumentNo}",
            });
        }
    }

    private static void PostPartyLedger(DuckDBConnection conn, ErpDocument doc)
    {
        bool isDebit  = ModuleDocTypes.LedgerDebit.Contains(doc.DocType);
        bool isCredit = ModuleDocTypes.LedgerCredit.Contains(doc.DocType);
        if (!isDebit && !isCredit) return;

        // Determine party type
        string partyType = ModuleDocTypes.Sales.Contains(doc.DocType) ? "Customer" : "Vendor";

        decimal debit  = isDebit  ? doc.GrandTotal : 0;
        decimal credit = isCredit ? doc.GrandTotal : 0;

        // Running balance for this party
        decimal prevBalance = GetPartyBalance(conn, doc.Customer.Name);
        decimal newBalance  = prevBalance + debit - credit;

        InsertLedgerEntry(conn, new PartyLedgerEntry
        {
            EntryDate  = doc.Date,
            PartyName  = doc.Customer.Name,
            PartyType  = partyType,
            EntryType  = isDebit ? LedgerEntryType.DEBIT : LedgerEntryType.CREDIT,
            Debit      = debit,
            Credit     = credit,
            Balance    = newBalance,
            DocumentNo = doc.DocumentNo,
            DocType    = doc.DocType.ToString(),
            Narration  = $"{doc.DocType} — {doc.DocumentNo}",
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PAYMENT POSTING  (called from ErpDocumentDbService.SavePayment)
    // ═══════════════════════════════════════════════════════════════════════════

    public void PostPayment(PaymentEntry payment, string partyType = "Customer")
    {
        using var conn = _db.GetConnection();
        conn.Open();

        bool   isIn       = partyType == "Customer";
        decimal debit      = isIn ? 0 : payment.Amount;
        decimal credit     = isIn ? payment.Amount : 0;
        decimal prevBal    = GetPartyBalance(conn, payment.PartyName);
        decimal newBal     = prevBal - credit + debit; // payment reduces balance

        InsertLedgerEntry(conn, new PartyLedgerEntry
        {
            EntryDate  = payment.Date,
            PartyName  = payment.PartyName,
            PartyType  = partyType,
            EntryType  = isIn ? LedgerEntryType.PAYMENT_IN : LedgerEntryType.PAYMENT_OUT,
            Debit      = debit,
            Credit     = credit,
            Balance    = newBal,
            DocumentNo = payment.DocumentNo,
            DocType    = "Payment",
            Narration  = $"Payment via {payment.Mode} — {(string.IsNullOrEmpty(payment.Reference) ? "" : payment.Reference)}",
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STOCK QUERIES
    // ═══════════════════════════════════════════════════════════════════════════

    public List<StockLedgerEntry> GetStockLedger(string? itemFilter = null)
    {
        using var conn = _db.GetConnection(); conn.Open();
        string where = string.IsNullOrEmpty(itemFilter)
            ? "" : " WHERE ItemName=?";
        using var r = string.IsNullOrEmpty(itemFilter)
            ? Cmd(conn, $"SELECT * FROM StockLedger ORDER BY EntryDate DESC, Id DESC;").ExecuteReader()
            : Cmd(conn, $"SELECT * FROM StockLedger WHERE ItemName=? ORDER BY EntryDate DESC, Id DESC;", itemFilter!).ExecuteReader();
        return ReadStockEntries(r);
    }

    public List<StockSummary> GetStockSummary()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<StockSummary>();
        using var r = Cmd(conn, @"
            SELECT
                im.Name, im.HSN, im.UOM,
                COALESCE(im.OpeningStock,0) AS OpeningStock,
                COALESCE(im.CurrentStock,0) AS CurrentStock,
                COALESCE(im.ReorderLevel,0) AS ReorderLevel,
                COALESCE(SUM(CASE WHEN sl.MovementType IN ('IN','OPENING','ADJUST_IN') THEN sl.Quantity ELSE 0 END),0) AS TotalIn,
                COALESCE(SUM(CASE WHEN sl.MovementType IN ('OUT','ADJUST_OUT') THEN sl.Quantity ELSE 0 END),0) AS TotalOut
            FROM ItemMaster im
            LEFT JOIN StockLedger sl ON sl.ItemName = im.Name
            GROUP BY im.Name, im.HSN, im.UOM, im.OpeningStock, im.CurrentStock, im.ReorderLevel
            ORDER BY im.Name;").ExecuteReader();
        while (r.Read())
            list.Add(new StockSummary
            {
                ItemName     = S(r,"Name"),
                HSN          = S(r,"HSN"),
                UOM          = S(r,"UOM"),
                OpeningStock = D(r,"OpeningStock"),
                CurrentStock = D(r,"CurrentStock"),
                ReorderLevel = D(r,"ReorderLevel"),
                TotalIn      = D(r,"TotalIn"),
                TotalOut     = D(r,"TotalOut"),
            });
        return list;
    }

    public void PostOpeningStock(string itemName, string hsn, string uom,
                                  decimal qty, decimal rate, DateTime date)
    {
        using var conn = _db.GetConnection(); conn.Open();
        Cmd(conn, "DELETE FROM StockLedger WHERE ItemName=? AND MovementType='OPENING';",
            itemName).ExecuteNonQuery();
        InsertStockEntry(conn, new StockLedgerEntry
        {
            EntryDate    = date,
            ItemName     = itemName,
            HSN          = hsn,
            UOM          = uom,
            MovementType = StockMovement.OPENING,
            Quantity     = qty,
            Rate         = rate,
            Narration    = "Opening Stock",
        });
        RebuildItemStock(conn, itemName);
    }

    public void PostManualAdjustment(string itemName, string hsn, string uom,
                                      decimal qty, StockMovement type, string narration)
    {
        using var conn = _db.GetConnection(); conn.Open();
        InsertStockEntry(conn, new StockLedgerEntry
        {
            EntryDate    = DateTime.Now,
            ItemName     = itemName,
            HSN          = hsn,
            UOM          = uom,
            MovementType = type,
            Quantity     = qty,
            Narration    = narration,
        });
        RebuildItemStock(conn, itemName);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTY LEDGER QUERIES
    // ═══════════════════════════════════════════════════════════════════════════

    public List<PartyLedgerEntry> GetPartyLedger(string? partyFilter = null)
    {
        using var conn = _db.GetConnection(); conn.Open();
        using var r = string.IsNullOrEmpty(partyFilter)
            ? Cmd(conn, "SELECT * FROM PartyLedger ORDER BY EntryDate DESC, Id DESC;").ExecuteReader()
            : Cmd(conn, "SELECT * FROM PartyLedger WHERE PartyName=? ORDER BY EntryDate ASC, Id ASC;", partyFilter!).ExecuteReader();
        return ReadLedgerEntries(r);
    }

    public List<PartySummary> GetPartySummary()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<PartySummary>();
        using var r = Cmd(conn, @"
            SELECT PartyName, PartyType,
                   SUM(Debit)  AS TotalDebit,
                   SUM(Credit) AS TotalCredit,
                   SUM(Debit) - SUM(Credit) AS Balance
            FROM PartyLedger
            GROUP BY PartyName, PartyType
            ORDER BY PartyName;").ExecuteReader();
        while (r.Read())
            list.Add(new PartySummary
            {
                PartyName   = S(r,"PartyName"),
                PartyType   = S(r,"PartyType"),
                TotalDebit  = D(r,"TotalDebit"),
                TotalCredit = D(r,"TotalCredit"),
                Balance     = D(r,"Balance"),
            });
        return list;
    }

    public void PostOpeningBalance(string partyName, string partyType,
                                    decimal amount, DateTime date)
    {
        using var conn = _db.GetConnection(); conn.Open();
        Cmd(conn, "DELETE FROM PartyLedger WHERE PartyName=? AND EntryType='OPENING';",
            partyName).ExecuteNonQuery();
        if (amount == 0) return;
        InsertLedgerEntry(conn, new PartyLedgerEntry
        {
            EntryDate  = date,
            PartyName  = partyName,
            PartyType  = partyType,
            EntryType  = LedgerEntryType.OPENING,
            Debit      = amount > 0 ? amount : 0,
            Credit     = amount < 0 ? Math.Abs(amount) : 0,
            Balance    = amount,
            Narration  = "Opening Balance",
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rebuilds CurrentStock on ItemMaster by summing StockLedger movements.
    /// Called after every stock posting.
    /// </summary>
    private static void RebuildItemStock(DuckDBConnection conn, string itemName)
    {
        using var r = Cmd(conn, @"
            SELECT
                COALESCE(SUM(CASE WHEN MovementType IN ('IN','OPENING','ADJUST_IN') THEN Quantity ELSE 0 END),0) -
                COALESCE(SUM(CASE WHEN MovementType IN ('OUT','ADJUST_OUT')           THEN Quantity ELSE 0 END),0)
            FROM StockLedger WHERE ItemName=?;", itemName).ExecuteReader();
        decimal stock = r.Read() && !r.IsDBNull(0) ? Convert.ToDecimal(r.GetValue(0)) : 0;
        r.Close();
        Cmd(conn, "UPDATE ItemMaster SET CurrentStock=? WHERE Name=?;", stock, itemName).ExecuteNonQuery();
    }

    private static decimal GetPartyBalance(DuckDBConnection conn, string partyName)
    {
        var c = Cmd(conn, "SELECT COALESCE(SUM(Debit)-SUM(Credit),0) FROM PartyLedger WHERE PartyName=?;", partyName);
        var v = c.ExecuteScalar();
        return v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);
    }

    private static int GetNextId(DuckDBConnection conn, string table)
    {
        var c = conn.CreateCommand();
        c.CommandText = $"SELECT COALESCE(MAX(Id),0)+1 FROM {table};";
        return Convert.ToInt32(c.ExecuteScalar());
    }

    private static void InsertStockEntry(DuckDBConnection conn, StockLedgerEntry e)
    {
        int nextId = GetNextId(conn, "StockLedger");
        Cmd(conn, @"INSERT INTO StockLedger
            (Id,EntryDate,ItemName,HSN,UOM,MovementType,Quantity,Rate,Value,DocumentNo,DocType,PartyName,Narration,CreatedAt)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?);",
            nextId, e.EntryDate.ToString("yyyy-MM-dd"), e.ItemName, e.HSN, e.UOM,
            e.MovementType.ToString(), e.Quantity, e.Rate, e.Quantity * e.Rate,
            e.DocumentNo, e.DocType, e.PartyName, e.Narration,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).ExecuteNonQuery();
    }

    private static void InsertLedgerEntry(DuckDBConnection conn, PartyLedgerEntry e)
    {
        int nextId = GetNextId(conn, "PartyLedger");
        Cmd(conn, @"INSERT INTO PartyLedger
            (Id,EntryDate,PartyName,PartyType,EntryType,Debit,Credit,Balance,DocumentNo,DocType,Narration,CreatedAt)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?);",
            nextId, e.EntryDate.ToString("yyyy-MM-dd"), e.PartyName, e.PartyType,
            e.EntryType.ToString(), e.Debit, e.Credit, e.Balance,
            e.DocumentNo, e.DocType, e.Narration,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).ExecuteNonQuery();
    }

    private static List<StockLedgerEntry> ReadStockEntries(System.Data.IDataReader r)
    {
        var list = new List<StockLedgerEntry>();
        while (r.Read())
        {
            Enum.TryParse<StockMovement>(S(r,"MovementType"), out var mt);
            list.Add(new StockLedgerEntry
            {
                Id           = r.IsDBNull(r.GetOrdinal("Id")) ? 0 : Convert.ToInt32(r.GetValue(r.GetOrdinal("Id"))),
                EntryDate    = DateTime.TryParse(S(r,"EntryDate"), out var d) ? d : DateTime.Now,
                ItemName     = S(r,"ItemName"),
                HSN          = S(r,"HSN"),
                UOM          = S(r,"UOM"),
                MovementType = mt,
                Quantity     = D(r,"Quantity"),
                Rate         = D(r,"Rate"),
                DocumentNo   = S(r,"DocumentNo"),
                DocType      = S(r,"DocType"),
                PartyName    = S(r,"PartyName"),
                Narration    = S(r,"Narration"),
            });
        }
        return list;
    }

    private static List<PartyLedgerEntry> ReadLedgerEntries(System.Data.IDataReader r)
    {
        var list = new List<PartyLedgerEntry>();
        while (r.Read())
        {
            Enum.TryParse<LedgerEntryType>(S(r,"EntryType"), out var et);
            list.Add(new PartyLedgerEntry
            {
                Id         = r.IsDBNull(r.GetOrdinal("Id")) ? 0 : Convert.ToInt32(r.GetValue(r.GetOrdinal("Id"))),
                EntryDate  = DateTime.TryParse(S(r,"EntryDate"), out var d) ? d : DateTime.Now,
                PartyName  = S(r,"PartyName"),
                PartyType  = S(r,"PartyType"),
                EntryType  = et,
                Debit      = D(r,"Debit"),
                Credit     = D(r,"Credit"),
                Balance    = D(r,"Balance"),
                DocumentNo = S(r,"DocumentNo"),
                DocType    = S(r,"DocType"),
                Narration  = S(r,"Narration"),
            });
        }
        return list;
    }

    private static string  S(System.Data.IDataReader r, string col)
    { try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? ""; } catch { return ""; } }

    private static decimal D(System.Data.IDataReader r, string col)
    { try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i)); } catch { return 0m; } }

    private static DuckDBCommand Cmd(DuckDBConnection conn, string sql, params object[] parms)
    {
        var c = conn.CreateCommand();
        c.CommandText = sql;
        foreach (var v in parms) c.Parameters.Add(new DuckDBParameter { Value = v });
        return c;
    }
}
