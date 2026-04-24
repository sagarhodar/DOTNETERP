using DuckDB.NET.Data;
using Ojaswat.Models;

namespace Ojaswat.Services;

/// <summary>
/// Generates and validates unique document numbers.
/// Extracted from ErpDocumentDbService so numbering rules can change
/// without touching CRUD logic.
/// </summary>
public class DocumentNumberService
{
    private readonly DuckDbService _db;

    public DocumentNumberService(DuckDbService db) => _db = db;

    private static string Prefix(DocumentType t) => t switch
    {
        DocumentType.Quotation       => "QT",
        DocumentType.SalesOrder      => "SO",
        DocumentType.SalesInvoice    => "SI",
        DocumentType.CreditNote      => "CN",
        DocumentType.PurchaseOrder   => "PO",
        DocumentType.PurchaseInvoice => "PI",
        DocumentType.GRN             => "GRN",
        DocumentType.DebitNote       => "DN",
        _                            => "DOC"
    };

    /// <summary>
    /// Generates the next available document number for the given type.
    /// Format: PREFIX-YYYY-NNNN  e.g. SI-2025-0001
    /// Falls back to a timestamp suffix if the DB is unreachable.
    /// </summary>
    public string Generate(DocumentType t)
    {
        string prefix = Prefix(t);
        string year   = DateTime.Now.ToString("yyyy");

        try
        {
            using var conn = _db.GetConnection();
            conn.Open();
            int next = Convert.ToInt32(
                Cmd(conn, "SELECT COALESCE(MAX(Id),0)+1 FROM ErpDocuments;")
                    .ExecuteScalar());

            for (int seq = next; seq < next + 9999; seq++)
            {
                string cand = $"{prefix}-{year}-{seq:D4}";
                if (!Exists(conn, cand)) return cand;
            }
        }
        catch { /* fall through */ }

        return $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}";
    }

    /// <summary>
    /// Appends or increments a -RevNN suffix, avoiding collisions.
    /// SI-2025-0001 → SI-2025-0001-Rev01 → SI-2025-0001-Rev02 …
    /// </summary>
    public string NextRevision(string existingDocNo)
    {
        string baseNo = existingDocNo;
        int    rev    = 1;

        if (baseNo.Contains("-Rev", StringComparison.OrdinalIgnoreCase))
        {
            int idx = baseNo.LastIndexOf("-Rev", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(baseNo[(idx + 4)..], out int n))
            {
                baseNo = baseNo[..idx];
                rev    = n + 1;
            }
        }

        try
        {
            using var conn = _db.GetConnection();
            conn.Open();
            int existing = Convert.ToInt32(
                Cmd(conn, "SELECT COUNT(*) FROM ErpDocuments WHERE DocumentNo LIKE ?;",
                    baseNo + "-Rev%").ExecuteScalar());
            if (existing >= rev) rev = existing + 1;
        }
        catch { /* fall through */ }

        return $"{baseNo}-Rev{rev:D2}";
    }

    /// <summary>Returns true if the document number already exists in the DB.</summary>
    public bool IsUnique(string docNo)
    {
        try
        {
            using var conn = _db.GetConnection();
            conn.Open();
            return !Exists(conn, docNo);
        }
        catch { return true; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool Exists(DuckDBConnection conn, string docNo) =>
        Convert.ToInt32(
            Cmd(conn, "SELECT COUNT(*) FROM ErpDocuments WHERE DocumentNo=?;", docNo)
                .ExecuteScalar()) > 0;

    private static DuckDBCommand Cmd(DuckDBConnection conn, string sql, params object[] parms)
    {
        var c = conn.CreateCommand();
        c.CommandText = sql;
        foreach (var v in parms)
            c.Parameters.Add(new DuckDBParameter { Value = v });
        return c;
    }
}
