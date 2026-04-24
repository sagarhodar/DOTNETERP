using DuckDB.NET.Data;
using Ojaswat.Models;

namespace Ojaswat.Services;

/// <summary>
/// All database CRUD. Now calls InventoryService after every document
/// save/update so stock and ledger stay in sync automatically.
/// </summary>
public class ErpDocumentDbService : IDisposable
{
    private readonly DuckDbService         _db;
    private readonly DocumentNumberService _docNos;
    private readonly InventoryService      _inv;     // ← NEW

    public ErpDocumentDbService()
    {
        _db     = new DuckDbService();
        _docNos = new DocumentNumberService(_db);
        _inv    = new InventoryService(_db);          // ← NEW
    }

    public void Dispose() => GC.SuppressFinalize(this);

    public string GenerateDocNo(DocumentType t)     => _docNos.Generate(t);
    public string NextRevisionNo(string existingNo) => _docNos.NextRevision(existingNo);

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static string S(System.Data.IDataReader r, string col)
    { try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? ""; } catch { return ""; } }

    private static string S(System.Data.IDataReader r, int i)
    { try { return r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? ""; } catch { return ""; } }

    private static decimal D(System.Data.IDataReader r, string col)
    { try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? 0m : r.GetDecimal(i); } catch { return 0m; } }

    private static int GetNextId(DuckDBConnection conn, string table)
    {
        var c = conn.CreateCommand();
        c.CommandText = $"SELECT COALESCE(MAX(Id),0)+1 FROM {table};";
        return Convert.ToInt32(c.ExecuteScalar());
    }

    private static DuckDBCommand Cmd(DuckDBConnection conn, string sql, params object[] parms)
    {
        var c = conn.CreateCommand();
        c.CommandText = sql;
        foreach (var v in parms) c.Parameters.Add(new DuckDBParameter { Value = v });
        return c;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMPANY PROFILE
    // ═══════════════════════════════════════════════════════════════════════════

    public CompanyProfile LoadCompany()
    {
        using var conn = _db.GetConnection(); conn.Open();
        using var r    = Cmd(conn, "SELECT * FROM CompanyProfile WHERE Id=1 LIMIT 1;").ExecuteReader();
        if (!r.Read()) return new CompanyProfile();
        return new CompanyProfile
        {
            Name      = S(r,"Name"),      Phone     = S(r,"Phone"),
            GSTIN     = S(r,"GSTIN"),     Address   = S(r,"Address"),
            StateCode = S(r,"StateCode"), Email     = S(r,"Email"),
            Bank      = S(r,"Bank"),      Account   = S(r,"Account"),
            IFSC      = S(r,"IFSC"),      Signatory = S(r,"Signatory"),
            LogoPath  = S(r,"LogoPath"),  QRCodePath= S(r,"QRCodePath"),
        };
    }

    public void SaveCompany(CompanyProfile p)
    {
        using var conn = _db.GetConnection(); conn.Open();
        Cmd(conn, @"UPDATE CompanyProfile SET
            Name=?,Phone=?,GSTIN=?,Address=?,StateCode=?,Email=?,
            Bank=?,Account=?,IFSC=?,Signatory=?,LogoPath=?,QRCodePath=?
            WHERE Id=1;",
            p.Name, p.Phone, p.GSTIN, p.Address, p.StateCode, p.Email,
            p.Bank, p.Account, p.IFSC, p.Signatory, p.LogoPath, p.QRCodePath
        ).ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CUSTOMER / VENDOR MASTER  (now includes PartyType + OpeningBalance)
    // ═══════════════════════════════════════════════════════════════════════════

    public List<Customer> LoadCustomers()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<Customer>();
        using var r = Cmd(conn, @"SELECT Id,Name,GSTIN,BillingAddress,ShippingAddress,StateCode,
            COALESCE(PartyType,'Customer') AS PartyType,
            COALESCE(OpeningBalance,0) AS OpeningBalance,
            COALESCE(OpeningBalanceDate,'') AS OpeningBalanceDate
            FROM Customers ORDER BY Name;").ExecuteReader();
        while (r.Read())
        {
            Enum.TryParse<PartyType>(S(r,"PartyType"), out var pt);
            list.Add(new Customer
            {
                Id = r.GetInt32(0), Name = S(r,1), GSTIN = S(r,2),
                BillingAddress = S(r,3), ShippingAddress = S(r,4), StateCode = S(r,5),
                PartyType = pt,
                OpeningBalance = D(r,"OpeningBalance"),
                OpeningBalanceDate = S(r,"OpeningBalanceDate"),
            });
        }
        return list;
    }

    public void SaveCustomer(Customer c)
    {
        using var conn = _db.GetConnection(); conn.Open();
        if (c.Id == 0)
        {
            c.Id = GetNextId(conn, "Customers");
            Cmd(conn, @"INSERT INTO Customers
                (Id,Name,GSTIN,BillingAddress,ShippingAddress,StateCode,PartyType,OpeningBalance,OpeningBalanceDate)
                VALUES (?,?,?,?,?,?,?,?,?);",
                c.Id, c.Name, c.GSTIN, c.BillingAddress, c.ShippingAddress, c.StateCode,
                c.PartyType.ToString(), c.OpeningBalance,
                c.OpeningBalanceDate).ExecuteNonQuery();
        }
        else
        {
            Cmd(conn, @"UPDATE Customers SET Name=?,GSTIN=?,BillingAddress=?,ShippingAddress=?,StateCode=?,
                PartyType=?,OpeningBalance=?,OpeningBalanceDate=? WHERE Id=?;",
                c.Name, c.GSTIN, c.BillingAddress, c.ShippingAddress, c.StateCode,
                c.PartyType.ToString(), c.OpeningBalance, c.OpeningBalanceDate, c.Id).ExecuteNonQuery();
        }

        // Post/refresh opening balance in ledger
        if (c.OpeningBalance != 0)
        {
            var date = DateTime.TryParse(c.OpeningBalanceDate, out var d) ? d : DateTime.Now;
            _inv.PostOpeningBalance(c.Name, c.PartyType.ToString(), c.OpeningBalance, date);
        }
    }

    public void DeleteCustomer(int id)
    {
        using var conn = _db.GetConnection(); conn.Open();
        Cmd(conn, "DELETE FROM Customers WHERE Id=?;", id).ExecuteNonQuery();
    }

    public void BulkUpsertCustomers(IEnumerable<Customer> customers)
    {
        using var conn = _db.GetConnection(); conn.Open();
        foreach (var c in customers)
        {
            bool found = Convert.ToInt32(
                Cmd(conn, "SELECT COUNT(*) FROM Customers WHERE Name=?;", c.Name).ExecuteScalar()) > 0;
            if (!found)
            {
                c.Id = GetNextId(conn, "Customers");
                Cmd(conn, @"INSERT INTO Customers
                    (Id,Name,GSTIN,BillingAddress,ShippingAddress,StateCode,PartyType,OpeningBalance,OpeningBalanceDate)
                    VALUES (?,?,?,?,?,?,?,?,?);",
                    c.Id, c.Name, c.GSTIN, c.BillingAddress, c.ShippingAddress, c.StateCode,
                    c.PartyType.ToString(), c.OpeningBalance, c.OpeningBalanceDate).ExecuteNonQuery();
            }
            else
                Cmd(conn, @"UPDATE Customers SET GSTIN=?,BillingAddress=?,ShippingAddress=?,StateCode=?,
                    PartyType=?,OpeningBalance=?,OpeningBalanceDate=? WHERE Name=?;",
                    c.GSTIN, c.BillingAddress, c.ShippingAddress, c.StateCode,
                    c.PartyType.ToString(), c.OpeningBalance, c.OpeningBalanceDate, c.Name).ExecuteNonQuery();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ITEM MASTER  (now includes CurrentStock / ReorderLevel / OpeningStock)
    // ═══════════════════════════════════════════════════════════════════════════

    public List<ItemMaster> LoadItems()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<ItemMaster>();
        using var r = Cmd(conn, @"SELECT Id,Name,HSN,DefaultRate,GSTPercent,UOM,
            COALESCE(CurrentStock,0)  AS CurrentStock,
            COALESCE(ReorderLevel,0)  AS ReorderLevel,
            COALESCE(OpeningStock,0)  AS OpeningStock
            FROM ItemMaster ORDER BY Name;").ExecuteReader();
        while (r.Read())
            list.Add(new ItemMaster
            {
                Id = r.GetInt32(0), Name = S(r,1), HSN = S(r,2),
                DefaultRate = D(r,"DefaultRate"), GSTPercent = D(r,"GSTPercent"), UOM = S(r,5),
                CurrentStock = D(r,"CurrentStock"),
                ReorderLevel = D(r,"ReorderLevel"),
                OpeningStock = D(r,"OpeningStock"),
            });
        return list;
    }

    public void SaveItem(ItemMaster m)
    {
        using var conn = _db.GetConnection(); conn.Open();
        bool isNew = m.Id == 0;
        if (isNew)
        {
            m.Id = GetNextId(conn, "ItemMaster");
            Cmd(conn, @"INSERT INTO ItemMaster
                (Id,Name,HSN,DefaultRate,GSTPercent,UOM,CurrentStock,ReorderLevel,OpeningStock)
                VALUES (?,?,?,?,?,?,?,?,?);",
                m.Id, m.Name, m.HSN, m.DefaultRate, m.GSTPercent, m.UOM,
                m.OpeningStock, m.ReorderLevel, m.OpeningStock).ExecuteNonQuery();
        }
        else
        {
            Cmd(conn, @"UPDATE ItemMaster SET
                Name=?,HSN=?,DefaultRate=?,GSTPercent=?,UOM=?,ReorderLevel=?,OpeningStock=?
                WHERE Id=?;",
                m.Name, m.HSN, m.DefaultRate, m.GSTPercent, m.UOM,
                m.ReorderLevel, m.OpeningStock, m.Id).ExecuteNonQuery();
        }

        // Post opening stock to StockLedger when first created or when opening stock changes
        if (m.OpeningStock > 0)
        {
            _inv.PostOpeningStock(m.Name, m.HSN, m.UOM, m.OpeningStock, m.DefaultRate, DateTime.Now);
        }
    }

    public void DeleteItem(int id)
    {
        using var conn = _db.GetConnection(); conn.Open();
        Cmd(conn, "DELETE FROM ItemMaster WHERE Id=?;", id).ExecuteNonQuery();
    }

    public void BulkUpsertItems(IEnumerable<ItemMaster> items)
    {
        using var conn = _db.GetConnection(); conn.Open();
        foreach (var m in items)
        {
            bool found = Convert.ToInt32(
                Cmd(conn, "SELECT COUNT(*) FROM ItemMaster WHERE Name=?;", m.Name).ExecuteScalar()) > 0;
            if (!found)
            {
                m.Id = GetNextId(conn, "ItemMaster");
                Cmd(conn, @"INSERT INTO ItemMaster
                    (Id,Name,HSN,DefaultRate,GSTPercent,UOM,CurrentStock,ReorderLevel,OpeningStock)
                    VALUES (?,?,?,?,?,?,?,?,?);",
                    m.Id, m.Name, m.HSN, m.DefaultRate, m.GSTPercent, m.UOM,
                    m.OpeningStock, m.ReorderLevel, m.OpeningStock).ExecuteNonQuery();
                if (m.OpeningStock > 0)
                    _inv.PostOpeningStock(m.Name, m.HSN, m.UOM, m.OpeningStock, m.DefaultRate, DateTime.Now);
            }
            else
                Cmd(conn, "UPDATE ItemMaster SET HSN=?,DefaultRate=?,GSTPercent=?,UOM=?,ReorderLevel=? WHERE Name=?;",
                    m.HSN, m.DefaultRate, m.GSTPercent, m.UOM, m.ReorderLevel, m.Name).ExecuteNonQuery();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T&C MASTER
    // ═══════════════════════════════════════════════════════════════════════════

    public List<TandCMaster> LoadAllTandC()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<TandCMaster>();
        using var r = Cmd(conn, "SELECT Id,DocType,Label,PaymentTerms,GeneralTerms FROM TandCMaster ORDER BY DocType,Label;").ExecuteReader();
        while (r.Read())
            list.Add(new TandCMaster { Id = r.GetInt32(0), DocType = S(r,1), Label = S(r,2), PaymentTerms = S(r,3), GeneralTerms = S(r,4) });
        return list;
    }

    public void SaveTandC(TandCMaster t)
    {
        using var conn = _db.GetConnection(); conn.Open();
        if (t.Id == 0)
        {
            t.Id = GetNextId(conn, "TandCMaster");
            Cmd(conn, "INSERT INTO TandCMaster (Id,DocType,Label,PaymentTerms,GeneralTerms) VALUES (?,?,?,?,?);",
                t.Id, t.DocType, t.Label, t.PaymentTerms, t.GeneralTerms).ExecuteNonQuery();
        }
        else
            Cmd(conn, "UPDATE TandCMaster SET DocType=?,Label=?,PaymentTerms=?,GeneralTerms=? WHERE Id=?;",
                t.DocType, t.Label, t.PaymentTerms, t.GeneralTerms, t.Id).ExecuteNonQuery();
    }

    public void DeleteTandC(int id)
    { using var conn = _db.GetConnection(); conn.Open(); Cmd(conn, "DELETE FROM TandCMaster WHERE Id=?;", id).ExecuteNonQuery(); }

    // ═══════════════════════════════════════════════════════════════════════════
    // DOC TEMPLATES
    // ═══════════════════════════════════════════════════════════════════════════

    public DocTemplate? LoadTemplate(string docType)
    {
        using var conn = _db.GetConnection(); conn.Open();
        using var r = Cmd(conn, "SELECT DocType,HtmlTemplate,PdfTemplate FROM DocTemplates WHERE DocType=? LIMIT 1;", docType).ExecuteReader();
        if (!r.Read()) return null;
        return new DocTemplate { DocType = S(r,0), HtmlTemplate = S(r,1), PdfTemplate = S(r,2) };
    }

    public void SaveTemplate(DocTemplate t)
    {
        using var conn = _db.GetConnection(); conn.Open();
        bool exists = Convert.ToInt32(Cmd(conn, "SELECT COUNT(*) FROM DocTemplates WHERE DocType=?;", t.DocType).ExecuteScalar()) > 0;
        if (exists)
            Cmd(conn, "UPDATE DocTemplates SET HtmlTemplate=?,PdfTemplate=? WHERE DocType=?;", t.HtmlTemplate, t.PdfTemplate, t.DocType).ExecuteNonQuery();
        else
            Cmd(conn, "INSERT INTO DocTemplates (DocType,HtmlTemplate,PdfTemplate) VALUES (?,?,?);", t.DocType, t.HtmlTemplate, t.PdfTemplate).ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DOCUMENTS  (PostDocument called automatically after save/update)
    // ═══════════════════════════════════════════════════════════════════════════

    public void SaveDocument(ErpDocument doc)
    {
        using var conn = _db.GetConnection(); conn.Open();
        doc.LastModified = DateTime.Now;

        var ex = Cmd(conn, "SELECT Id FROM ErpDocuments WHERE DocumentNo=? LIMIT 1;", doc.DocumentNo).ExecuteScalar();
        if (ex != null && ex != DBNull.Value)
        {
            doc.Id = Convert.ToInt32(ex);
            UpdateDocumentCore(conn, doc);
        }
        else
        {
            doc.Id = Convert.ToInt32(Cmd(conn, "SELECT COALESCE(MAX(Id),0)+1 FROM ErpDocuments;").ExecuteScalar());
            InsertDocument(conn, doc);
            ReplaceItems(conn, doc.Id, doc.Items);
        }

        // ── AUTO-POST: stock + ledger ─────────────────────────────────────────
        //      REMOVE ALL OLD LEDGER FOR THIS DOCUMENT (ALL REVISIONS)
        string baseDocNo = doc.DocumentNo.Split("-Rev")[0];

        Cmd(conn, "DELETE FROM PartyLedger WHERE DocumentNo LIKE ?;", baseDocNo + "%").ExecuteNonQuery();
        Cmd(conn, "DELETE FROM StockLedger WHERE DocumentNo LIKE ?;", baseDocNo + "%").ExecuteNonQuery();

        // ✅ Now post fresh
        _inv.PostDocument(doc);
    }

    public void UpdateDocument(ErpDocument doc)
    {
        using var conn = _db.GetConnection(); conn.Open();
        doc.LastModified = DateTime.Now;
        string newNo = _docNos.NextRevision(doc.DocumentNo);
        int    oldId = doc.Id;

        doc.Id         = Convert.ToInt32(Cmd(conn, "SELECT COALESCE(MAX(Id),0)+1 FROM ErpDocuments;").ExecuteScalar());
        doc.DocumentNo = newNo;

        Cmd(conn, "DELETE FROM ErpDocumentItems WHERE DocumentId=?;", oldId).ExecuteNonQuery();
        Cmd(conn, "DELETE FROM ErpDocuments WHERE Id=?;", oldId).ExecuteNonQuery();
        InsertDocument(conn, doc);
        ReplaceItems(conn, doc.Id, doc.Items);

        // ── AUTO-POST: stock + ledger (reverses old, writes fresh) ────────────
        _inv.PostDocument(doc);
    }

    public void DeleteDocument(int docId)
    {
        using var conn = _db.GetConnection(); conn.Open();
        // Get DocumentNo before delete so we can clean ledger entries
        string docNo = "";
        using (var r = Cmd(conn, "SELECT DocumentNo FROM ErpDocuments WHERE Id=?;", docId).ExecuteReader())
            if (r.Read()) docNo = S(r,"DocumentNo");

        Cmd(conn, "DELETE FROM ErpDocumentItems WHERE DocumentId=?;", docId).ExecuteNonQuery();
        Cmd(conn, "DELETE FROM ErpDocuments WHERE Id=?;", docId).ExecuteNonQuery();

        if (!string.IsNullOrEmpty(docNo))
        {
            // Remove ledger entries for this document
            using var conn2 = _db.GetConnection(); conn2.Open();
            Cmd(conn2, "DELETE FROM StockLedger WHERE DocumentNo=?;", docNo).ExecuteNonQuery();
            Cmd(conn2, "DELETE FROM PartyLedger WHERE DocumentNo=?;", docNo).ExecuteNonQuery();
        }
    }

    public void UpdateStatusAndPending(int docId, DocumentStatus status, decimal pending)
    {
        using var conn = _db.GetConnection(); conn.Open();
        Cmd(conn, "UPDATE ErpDocuments SET Status=?,PendingAmount=?,LastModified=? WHERE Id=?;",
            status.ToString(), pending, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), docId).ExecuteNonQuery();
    }

    public List<DocumentListItem> LoadAllDocuments()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<DocumentListItem>();
        using var r = Cmd(conn, @"SELECT Id,DocumentNo,DocType,CustomerName,
            COALESCE(CustomerRefNo,'') AS CustomerRefNo, DocDate,
            GrandTotal,Status,PendingAmount,CreatedBy,GstPercent,GstMode,Discount,Freight
            FROM ErpDocuments ORDER BY Id DESC;").ExecuteReader();
        while (r.Read())
        {
            Enum.TryParse<DocumentStatus>(S(r,"Status"), out var status);
            decimal grand   = D(r,"GrandTotal"), pend = D(r,"PendingAmount");
            decimal gstPct  = D(r,"GstPercent"), disc = D(r,"Discount"), freight = D(r,"Freight");
            decimal taxable = grand - freight;
            decimal gstAmt  = gstPct > 0 ? Math.Round(taxable * gstPct / 100 / (1 + gstPct / 100), 2) : 0;
            list.Add(new DocumentListItem
            {
                Id = r.GetInt32(0), DocumentNo = S(r,1), DocTypeLabel = S(r,2), CustomerName = S(r,3),
                CustomerRefNo = S(r,4),
                Date = DateTime.TryParse(S(r,5), out var d) ? d : DateTime.Now,
                GrandTotal = grand, Status = status, PendingAmount = pend,
                CreatedBy = S(r,"CreatedBy"),
                Subtotal = Math.Round(taxable - gstAmt + disc, 2),
                GstAmount = gstAmt,
            });
        }
        return list;
    }

    public ErpDocument? LoadDocument(int id)
    {
        using var conn = _db.GetConnection(); conn.Open();
        using var r = Cmd(conn, "SELECT * FROM ErpDocuments WHERE Id=? LIMIT 1;", id).ExecuteReader();
        if (!r.Read()) return null;

        Enum.TryParse<DocumentType>(S(r,"DocType"),  out var dt);
        Enum.TryParse<GstMode>(S(r,"GstMode"),       out var gm);
        Enum.TryParse<DocumentStatus>(S(r,"Status"), out var st);

        var doc = new ErpDocument
        {
            Id = r.GetInt32(r.GetOrdinal("Id")), DocType = dt, DocumentNo = S(r,"DocumentNo"),
            Date = DateTime.TryParse(S(r,"DocDate"), out var dd) ? dd : DateTime.Now,
            CreatedBy = S(r,"CreatedBy"), CustomerRefNo = S(r,"CustomerRefNo"),
            EWayBillNo = S(r,"EWayBillNo"), PlaceOfSupply = S(r,"PlaceOfSupply"),
            Company = new() { Name=S(r,"CompanyName"), Phone=S(r,"CompanyPhone"), GSTIN=S(r,"CompanyGSTIN"),
                Address=S(r,"CompanyAddress"), StateCode=S(r,"CompanyStateCode"), Email=S(r,"CompanyEmail"),
                Bank=S(r,"CompanyBank"), Account=S(r,"CompanyAccount"), IFSC=S(r,"CompanyIFSC"),
                Signatory=S(r,"CompanySignatory"), LogoPath=S(r,"CompanyLogoPath"), QRCodePath=S(r,"CompanyQRCodePath") },
            Customer = new() { Name=S(r,"CustomerName"), GSTIN=S(r,"CustomerGSTIN"),
                BillingAddress=S(r,"CustomerAddress"), ShippingAddress=S(r,"CustomerShipping"),
                StateCode=S(r,"CustomerStateCode") },
            GstMode=gm, GstPercent=D(r,"GstPercent"), Discount=D(r,"Discount"), Freight=D(r,"Freight"),
            PaymentTerms=S(r,"PaymentTerms"), GeneralTerms=S(r,"GeneralTerms"),
            GrandTotal=D(r,"GrandTotal"), Status=st, PendingAmount=D(r,"PendingAmount"),
        };
        r.Close();

        using var ir = Cmd(conn, "SELECT * FROM ErpDocumentItems WHERE DocumentId=? ORDER BY ItemNumber;", doc.Id).ExecuteReader();
        while (ir.Read())
            doc.Items.Add(new InvoiceItem
            {
                ItemNumber=ir.GetInt32(ir.GetOrdinal("ItemNumber")), Description=ir.GetString(ir.GetOrdinal("Description")),
                HSN=S(ir,"HSN"), UOM=S(ir,"UOM"), Quantity=D(ir,"Quantity"), Rate=D(ir,"Rate"), GSTPercent=D(ir,"GSTPercent"),
            });
        return doc;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void InsertDocument(DuckDBConnection conn, ErpDocument doc)
    {
        Cmd(conn, @"INSERT INTO ErpDocuments
            (Id,DocType,DocumentNo,DocDate,CreatedBy,
             CompanyName,CompanyPhone,CompanyGSTIN,CompanyAddress,CompanyStateCode,CompanyEmail,
             CompanyBank,CompanyAccount,CompanyIFSC,CompanySignatory,CompanyLogoPath,CompanyQRCodePath,
             CustomerName,CustomerGSTIN,CustomerAddress,CustomerShipping,CustomerStateCode,
             CustomerRefNo,EWayBillNo,PlaceOfSupply,GstMode,GstPercent,Discount,Freight,
             PaymentTerms,GeneralTerms,GrandTotal,Status,PendingAmount,LastModified)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);",
            doc.Id, doc.DocType.ToString(), doc.DocumentNo, doc.Date.ToString("yyyy-MM-dd"), doc.CreatedBy,
            doc.Company.Name, doc.Company.Phone, doc.Company.GSTIN, doc.Company.Address, doc.Company.StateCode, doc.Company.Email,
            doc.Company.Bank, doc.Company.Account, doc.Company.IFSC, doc.Company.Signatory, doc.Company.LogoPath, doc.Company.QRCodePath,
            doc.Customer.Name, doc.Customer.GSTIN, doc.Customer.BillingAddress, doc.Customer.ShippingAddress, doc.Customer.StateCode,
            doc.CustomerRefNo, doc.EWayBillNo, doc.PlaceOfSupply,
            doc.GstMode.ToString(), doc.GstPercent, doc.Discount, doc.Freight,
            doc.PaymentTerms, doc.GeneralTerms, doc.GrandTotal, doc.Status.ToString(), doc.PendingAmount,
            doc.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "").ExecuteNonQuery();
    }

    private static void UpdateDocumentCore(DuckDBConnection conn, ErpDocument doc)
    {
        Cmd(conn, @"UPDATE ErpDocuments SET DocType=?,DocDate=?,CreatedBy=?,
            CompanyName=?,CompanyPhone=?,CompanyGSTIN=?,CompanyAddress=?,CompanyStateCode=?,CompanyEmail=?,
            CompanyBank=?,CompanyAccount=?,CompanyIFSC=?,CompanySignatory=?,CompanyLogoPath=?,CompanyQRCodePath=?,
            CustomerName=?,CustomerGSTIN=?,CustomerAddress=?,CustomerShipping=?,CustomerStateCode=?,
            CustomerRefNo=?,EWayBillNo=?,PlaceOfSupply=?,GstMode=?,GstPercent=?,Discount=?,Freight=?,
            PaymentTerms=?,GeneralTerms=?,GrandTotal=?,Status=?,PendingAmount=?,LastModified=? WHERE Id=?;",
            doc.DocType.ToString(), doc.Date.ToString("yyyy-MM-dd"), doc.CreatedBy,
            doc.Company.Name, doc.Company.Phone, doc.Company.GSTIN, doc.Company.Address, doc.Company.StateCode, doc.Company.Email,
            doc.Company.Bank, doc.Company.Account, doc.Company.IFSC, doc.Company.Signatory, doc.Company.LogoPath, doc.Company.QRCodePath,
            doc.Customer.Name, doc.Customer.GSTIN, doc.Customer.BillingAddress, doc.Customer.ShippingAddress, doc.Customer.StateCode,
            doc.CustomerRefNo, doc.EWayBillNo, doc.PlaceOfSupply,
            doc.GstMode.ToString(), doc.GstPercent, doc.Discount, doc.Freight,
            doc.PaymentTerms, doc.GeneralTerms, doc.GrandTotal, doc.Status.ToString(), doc.PendingAmount,
            doc.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "", doc.Id).ExecuteNonQuery();
        ReplaceItems(conn, doc.Id, doc.Items);
    }

    private static void ReplaceItems(DuckDBConnection conn, int docId, List<InvoiceItem> items)
    {
        Cmd(conn, "DELETE FROM ErpDocumentItems WHERE DocumentId=?;", docId).ExecuteNonQuery();
        foreach (var it in items)
            Cmd(conn, "INSERT INTO ErpDocumentItems (DocumentId,ItemNumber,Description,HSN,UOM,Quantity,Rate,GSTPercent) VALUES (?,?,?,?,?,?,?,?);",
                docId, it.ItemNumber, it.Description, it.HSN, it.UOM, it.Quantity, it.Rate, it.GSTPercent).ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PAYMENTS  (also posts to PartyLedger)
    // ═══════════════════════════════════════════════════════════════════════════

    public void SavePayment(PaymentEntry p)
    {
        using var conn = _db.GetConnection(); conn.Open();
        p.CreatedAt = DateTime.Now;
        p.Id = GetNextId(conn, "Payments");
        Cmd(conn, "INSERT INTO Payments (Id,DocumentNo,PartyName,PayDate,Amount,Mode,Reference,Notes,CreatedAt) VALUES (?,?,?,?,?,?,?,?,?);",
            p.Id, p.DocumentNo, p.PartyName, p.Date.ToString("yyyy-MM-dd"), p.Amount,
            p.Mode.ToString(), p.Reference, p.Notes, p.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")).ExecuteNonQuery();

        // Update PendingAmount on linked document
        if (!string.IsNullOrWhiteSpace(p.DocumentNo))
        {
            using var dr = Cmd(conn, "SELECT PendingAmount,GrandTotal FROM ErpDocuments WHERE DocumentNo=? LIMIT 1;", p.DocumentNo).ExecuteReader();
            decimal curPending = 0, grand = 0;
            if (dr.Read()) { curPending = dr.IsDBNull(0) ? 0 : dr.GetDecimal(0); grand = dr.IsDBNull(1) ? 0 : dr.GetDecimal(1); }
            dr.Close();
            decimal newPending = Math.Max(0, curPending - p.Amount);
            string  newStatus  = newPending <= 0 ? "Complete" : "Pending";
            Cmd(conn, "UPDATE ErpDocuments SET PendingAmount=?,Status=?,LastModified=? WHERE DocumentNo=?;",
                newPending, newStatus, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), p.DocumentNo).ExecuteNonQuery();
        }

        // ── AUTO-POST payment to party ledger ─────────────────────────────────
        // Determine party type from linked document
        string partyType = "Customer";
        if (!string.IsNullOrWhiteSpace(p.DocumentNo))
        {
            using var dr2 = Cmd(conn, "SELECT DocType FROM ErpDocuments WHERE DocumentNo=? LIMIT 1;", p.DocumentNo).ExecuteReader();
            if (dr2.Read())
            {
                string dt = S(dr2,"DocType");
                if (dt is "PurchaseInvoice" or "PurchaseOrder" or "GRN" or "DebitNote")
                    partyType = "Vendor";
            }
        }
        _inv.PostPayment(p, partyType);
    }

    public void DeletePayment(int id)
    {
        using var conn = _db.GetConnection(); conn.Open();
        string docNo = ""; decimal amount = 0;
        using (var r = Cmd(conn, "SELECT DocumentNo,Amount FROM Payments WHERE Id=?;", id).ExecuteReader())
            if (r.Read()) { docNo = S(r,0); amount = D(r,"Amount"); }

        Cmd(conn, "DELETE FROM Payments WHERE Id=?;", id).ExecuteNonQuery();

        // Remove ledger entry for this payment (matched by DocumentNo + amount, best effort)
        Cmd(conn, "DELETE FROM PartyLedger WHERE DocumentNo=? AND EntryType IN ('PAYMENT_IN','PAYMENT_OUT') AND ABS(Credit - CAST(? AS DECIMAL(18,2))) <= 0.01;",
            docNo, amount).ExecuteNonQuery();

        if (!string.IsNullOrWhiteSpace(docNo) && amount > 0)
        {
            using var dr2 = Cmd(conn, "SELECT PendingAmount,GrandTotal FROM ErpDocuments WHERE DocumentNo=? LIMIT 1;", docNo).ExecuteReader();
            decimal curPend2 = 0, grand2 = 0;
            if (dr2.Read()) { curPend2 = dr2.IsDBNull(0) ? 0 : dr2.GetDecimal(0); grand2 = dr2.IsDBNull(1) ? 0 : dr2.GetDecimal(1); }
            dr2.Close();
            decimal restored = Math.Min(grand2, curPend2 + amount);
            string  restoredStatus = restored > 0 ? "Pending" : "Complete";
            Cmd(conn, "UPDATE ErpDocuments SET PendingAmount=?,Status=?,LastModified=? WHERE DocumentNo=?;",
                restored, restoredStatus, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), docNo).ExecuteNonQuery();
        }
    }

    public List<PaymentListItem> LoadAllPayments()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<PaymentListItem>();
        using var r = Cmd(conn, "SELECT Id,DocumentNo,PartyName,PayDate,Amount,Mode,Reference,Notes FROM Payments ORDER BY PayDate DESC;").ExecuteReader();
        while (r.Read())
            list.Add(new PaymentListItem
            {
                Id = r.GetInt32(0), DocumentNo = S(r,1), PartyName = S(r,2),
                Date = DateTime.TryParse(S(r,3), out var d) ? d : DateTime.Now,
                Amount = r.IsDBNull(4) ? 0 : r.GetDecimal(4),
                ModeLabel = S(r,5), Reference = S(r,6), Notes = S(r,7)
            });
        return list;
    }

    // ── DELETE LEDGER ─────────────────────────────────────
    public void DeleteLedgerEntry(int id)
    {
        using var conn = _db.GetConnection(); conn.Open();

        // get details
        using var r = Cmd(conn, "SELECT DocumentNo, Credit FROM PartyLedger WHERE Id=?;", id).ExecuteReader();

        string docNo = "";
        decimal credit = 0;

        if (r.Read())
        {
            docNo = S(r, "DocumentNo");
            credit = D(r, "Credit");
        }
        r.Close();

        // delete ledger
        Cmd(conn, "DELETE FROM PartyLedger WHERE Id=?;", id).ExecuteNonQuery();

        // adjust pending if payment entry
        if (!string.IsNullOrEmpty(docNo) && credit > 0)
        {
            Cmd(conn, "UPDATE ErpDocuments SET PendingAmount = PendingAmount + ? WHERE DocumentNo=?;",
                credit, docNo).ExecuteNonQuery();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GST REPORT
    // ═══════════════════════════════════════════════════════════════════════════

    public List<GstReportRow> GetGstReport()
    {
        using var conn = _db.GetConnection(); conn.Open();
        var list = new List<GstReportRow>();
        using var r = Cmd(conn, @"
            SELECT substr(DocDate,1,7) AS YM, DocType, COUNT(*) AS Cnt,
                CAST(SUM((GrandTotal-Freight)-(GrandTotal-Freight)*GstPercent/100.0/(1.0+GstPercent/100.0)+Discount) AS DECIMAL(18,2)) AS Taxable,
                CAST(SUM(CASE WHEN GstMode='CgstSgst' THEN (GrandTotal-Freight)*GstPercent/100.0/(1.0+GstPercent/100.0)/2.0 ELSE 0.0 END) AS DECIMAL(18,2)) AS CGST,
                CAST(SUM(CASE WHEN GstMode='CgstSgst' THEN (GrandTotal-Freight)*GstPercent/100.0/(1.0+GstPercent/100.0)/2.0 ELSE 0.0 END) AS DECIMAL(18,2)) AS SGST,
                CAST(SUM(CASE WHEN GstMode='Igst' THEN (GrandTotal-Freight)*GstPercent/100.0/(1.0+GstPercent/100.0) ELSE 0.0 END) AS DECIMAL(18,2)) AS IGST,
                CAST(SUM(GrandTotal) AS DECIMAL(18,2)) AS Grand
            FROM ErpDocuments WHERE DocDate IS NOT NULL AND DocDate!=''
            GROUP BY YM,DocType ORDER BY YM DESC,DocType;").ExecuteReader();
        while (r.Read())
            list.Add(new GstReportRow
            {
                MonthYear = S(r,0), DocType = S(r,1),
                Count     = r.IsDBNull(2) ? 0 : Convert.ToInt32(r.GetValue(2)),
                Taxable   = r.IsDBNull(3) ? 0 : Convert.ToDecimal(r.GetValue(3)),
                CGST      = r.IsDBNull(4) ? 0 : Convert.ToDecimal(r.GetValue(4)),
                SGST      = r.IsDBNull(5) ? 0 : Convert.ToDecimal(r.GetValue(5)),
                IGST      = r.IsDBNull(6) ? 0 : Convert.ToDecimal(r.GetValue(6)),
                GrandTotal= r.IsDBNull(7) ? 0 : Convert.ToDecimal(r.GetValue(7)),
            });
        return list;
    }

    // ── Expose InventoryService for pages ─────────────────────────────────────
    public InventoryService Inventory => _inv;
    
    //---DELETE INVENTORY LEDGER ENTRY (e.g. for correcting stock) ---
    public void DeleteStockLedgerEntry(int id)
    {
        using var conn = _db.GetConnection(); conn.Open();

        // get item name before delete
        string item = "";
        using (var r = Cmd(conn, "SELECT ItemName FROM StockLedger WHERE Id=?;", id).ExecuteReader())
            if (r.Read()) item = S(r, "ItemName");

        // delete entry
        Cmd(conn, "DELETE FROM StockLedger WHERE Id=?;", id).ExecuteNonQuery();

        // 🔥 recalculate stock for that item
        RecalculateStock(conn, item);
    }

    //--- Recalculate stock ----
    private void RecalculateStock(DuckDBConnection conn, string item)
    {
        decimal stock = 0;

        using (var r = Cmd(conn,
            "SELECT MovementType, Quantity FROM StockLedger WHERE ItemName=?;",
            item).ExecuteReader())
        {
            while (r.Read())
            {
                string type = S(r, "MovementType");
                decimal qty = r.IsDBNull(1) ? 0 : r.GetDecimal(1);

                if (type == "IN" || type == "ADJUST_IN")
                    stock += qty;
                else
                    stock -= qty;
            }
        }

        // update item master
        Cmd(conn, "UPDATE ItemMaster SET CurrentStock=? WHERE Name=?;",
            stock, item).ExecuteNonQuery();
    }
}
