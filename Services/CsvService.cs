using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using Ojaswat.Models;

namespace Ojaswat.Services;

// ── Class maps ────────────────────────────────────────────────────────────────

public sealed class InvoiceItemMap : ClassMap<InvoiceItem>
{
    public InvoiceItemMap()
    {
        Map(m => m.ItemNumber).Name("ItemNumber",  "item_number", "No",  "no");
        Map(m => m.Description).Name("Description","description", "Desc","desc");
        Map(m => m.HSN).Name("HSN",       "hsn",        "HsnCode");
        Map(m => m.Quantity).Name("Quantity",  "quantity",   "Qty", "qty");
        Map(m => m.Rate).Name("Rate",      "rate",       "UnitPrice","unit_price");
        Map(m => m.GSTPercent).Name("GSTPercent","gst_percent", "GST%", "GST");
    }
}

public sealed class CustomerMap : ClassMap<Customer>
{
    public CustomerMap()
    {
        Map(m => m.Name).Name("Name", "CustomerName", "customer_name");
        Map(m => m.GSTIN).Name("GSTIN", "gstin", "GST");
        Map(m => m.BillingAddress).Name("BillingAddress", "Address", "address");
        Map(m => m.ShippingAddress).Name("ShippingAddress", "Shipping", "shipping_address");
        Map(m => m.StateCode).Name("StateCode", "state_code", "State");
    }
}

public sealed class ItemMasterMap : ClassMap<ItemMaster>
{
    public ItemMasterMap()
    {
        Map(m => m.Name).Name("Name",        "ItemName",    "item_name",    "Description");
        Map(m => m.HSN).Name("HSN",         "hsn",         "HsnCode");
        Map(m => m.DefaultRate).Name("DefaultRate", "Rate",        "rate",         "UnitPrice");
        Map(m => m.GSTPercent).Name("GSTPercent",  "GST%",        "GST",          "gst_percent");
        Map(m => m.UOM).Name("UOM",         "Unit",        "unit",         "uom");
    }
}

/// <summary>
/// Single consolidated CSV service for invoice items, customers, items,
/// payments, and T&amp;C.  Replaces both CsvImportService and MasterDataCsvService.
/// </summary>
public class CsvService
{
    private static CsvConfiguration Cfg() =>
    new(CultureInfo.InvariantCulture)
    { 
        HasHeaderRecord = true, 
        MissingFieldFound = _ => { }, 
        HeaderValidated = _ => { } 
    };


    // ── Import ────────────────────────────────────────────────────────────────

    public List<InvoiceItem> ImportInvoiceItems(string filePath)
    {
        if (!File.Exists(filePath)) return new();
        using var r   = new StreamReader(filePath);
        using var csv = new CsvReader(r, Cfg());
        csv.Context.RegisterClassMap<InvoiceItemMap>();
        var items = csv.GetRecords<InvoiceItem>().ToList();
        for (int i = 0; i < items.Count; i++)
            if (items[i].ItemNumber == 0) items[i].ItemNumber = i + 1;
        return items;
    }

    public List<Customer> ImportCustomers(string filePath)
    {
        if (!File.Exists(filePath)) return new();
        using var r   = new StreamReader(filePath);
        using var csv = new CsvReader(r, Cfg());
        csv.Context.RegisterClassMap<CustomerMap>();
        var list = csv.GetRecords<Customer>().ToList();
        for (int i = 0; i < list.Count; i++) list[i].Id = i + 1;
        return list;
    }

    public List<ItemMaster> ImportItems(string filePath)
    {
        if (!File.Exists(filePath)) return new();
        using var r   = new StreamReader(filePath);
        using var csv = new CsvReader(r, Cfg());
        csv.Context.RegisterClassMap<ItemMasterMap>();
        var list = csv.GetRecords<ItemMaster>().ToList();
        for (int i = 0; i < list.Count; i++) list[i].Id = i + 1;
        return list;
    }

    public List<TandCMaster> ImportTandC(string filePath)
    {
        if (!File.Exists(filePath)) return new();
        using var r   = new StreamReader(filePath);
        using var csv = new CsvReader(r, Cfg());
        return csv.GetRecords<TandCMaster>().ToList();
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public void ExportCustomers(IEnumerable<Customer> list, string filePath)
    {
        using var w   = new StreamWriter(filePath);
        using var csv = new CsvWriter(w, Cfg());
        csv.WriteHeader<Customer>(); csv.NextRecord();
        csv.WriteRecords(list);
    }

    public void ExportItems(IEnumerable<ItemMaster> list, string filePath)
    {
        using var w   = new StreamWriter(filePath);
        using var csv = new CsvWriter(w, Cfg());
        csv.WriteHeader<ItemMaster>(); csv.NextRecord();
        csv.WriteRecords(list);
    }

    public void ExportPayments(IEnumerable<PaymentListItem> list, string filePath)
    {
        using var w   = new StreamWriter(filePath);
        using var csv = new CsvWriter(w, Cfg());
        csv.WriteHeader<PaymentListItem>(); csv.NextRecord();
        csv.WriteRecords(list);
    }

    public void ExportTandC(IEnumerable<TandCMaster> list, string filePath)
    {
        using var w   = new StreamWriter(filePath);
        using var csv = new CsvWriter(w, Cfg());
        csv.WriteHeader<TandCMaster>(); csv.NextRecord();
        csv.WriteRecords(list);
    }

    /// <summary>
    /// Exports the item-level report as CSV.
    /// Caller builds the anonymous rows; this method writes the header + data.
    /// </summary>
    public void ExportItemReport(
        IEnumerable<DocumentListItem> allDocs,
        ErpDocumentDbService db,
        string filePath)
    {
        using var w = new StreamWriter(filePath);
        w.WriteLine("DocumentNo,DocType,Party,Date,Description,HSN,Qty,Rate,Amount");
        foreach (var d in allDocs)
        {
            var doc = db.LoadDocument(d.Id);
            if (doc == null) continue;
            foreach (var it in doc.Items)
                w.WriteLine(
                    $"\"{d.DocumentNo}\",\"{d.DocTypeLabel}\",\"{d.CustomerName}\"," +
                    $"\"{d.DateLabel}\",\"{it.Description.Replace("\"", "\"\"")}\",\"{it.HSN}\"," +
                    $"{it.Quantity:F2},{it.Rate:F2},{it.LineTotal:F2}");
        }
    }

    public void ExportGstReport(IEnumerable<GstReportRow> rows, string filePath)
    {
        using var w = new StreamWriter(filePath);
        w.WriteLine("Month,DocType,Count,Taxable,CGST,SGST,IGST,TotalTax,GrandTotal");
        foreach (var r in rows)
            w.WriteLine(
                $"{r.MonthYear},{r.DocType},{r.Count}," +
                $"{r.Taxable:F2},{r.CGST:F2},{r.SGST:F2},{r.IGST:F2}," +
                $"{r.TotalTax:F2},{r.GrandTotal:F2}");
    }
}
