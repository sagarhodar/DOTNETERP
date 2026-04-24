using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Ojaswat.Models;

public enum DocumentType
{
    Quotation, SalesOrder, SalesInvoice, CreditNote,
    PurchaseOrder, PurchaseInvoice, GRN, DebitNote
}

public enum GstMode        { CgstSgst, Igst }
public enum DocumentStatus { Open, Pending, Complete, Canceled }
public enum PaymentMode    { Cash, Cheque, NEFT, RTGS, UPI, Other }
public enum PartyType      { Customer, Vendor, Both }
public enum StockMovement  { IN, OUT, ADJUST_IN, ADJUST_OUT, OPENING }
public enum LedgerEntryType{ OPENING, DEBIT, CREDIT, PAYMENT_IN, PAYMENT_OUT }

public static class ModuleDocTypes
{
    public static readonly DocumentType[] Sales =
    {
        DocumentType.Quotation, DocumentType.SalesOrder,
        DocumentType.SalesInvoice, DocumentType.CreditNote
    };

    public static readonly DocumentType[] Purchase =
    {
        DocumentType.PurchaseOrder, DocumentType.PurchaseInvoice,
        DocumentType.GRN, DocumentType.DebitNote
    };

    // Doc types that REDUCE stock (outgoing)
    public static readonly DocumentType[] StockOut =
    {
        DocumentType.SalesInvoice, DocumentType.SalesOrder, DocumentType.CreditNote
    };

    // Doc types that INCREASE stock (incoming)
    public static readonly DocumentType[] StockIn =
    {
        DocumentType.PurchaseInvoice, DocumentType.GRN, DocumentType.DebitNote
    };

    // Doc types that create DEBIT in party ledger (party owes us)
    public static readonly DocumentType[] LedgerDebit =
    {
        DocumentType.SalesInvoice, DocumentType.DebitNote
    };

    // Doc types that create CREDIT in party ledger (we owe party)
    public static readonly DocumentType[] LedgerCredit =
    {
        DocumentType.PurchaseInvoice, DocumentType.CreditNote
    };
}

// ── Core entities ──────────────────────────────────────────────────────────────

public class CompanyProfile
{
    public int    Id         { get; set; } = 1;
    public string Name       { get; set; } = "";
    public string Phone      { get; set; } = "";
    public string GSTIN      { get; set; } = "";
    public string Address    { get; set; } = "";
    public string StateCode  { get; set; } = "";
    public string Email      { get; set; } = "";
    public string Bank       { get; set; } = "";
    public string Account    { get; set; } = "";
    public string IFSC       { get; set; } = "";
    public string Signatory  { get; set; } = "";
    public string LogoPath   { get; set; } = "";
    public string QRCodePath { get; set; } = "";
}

/// <summary>
/// Now serves both Customers and Vendors.
/// PartyType distinguishes them. Use Name lookup for ledger matching.
/// </summary>
public class Customer
{
    public int       Id                  { get; set; }
    public string    Name                { get; set; } = "";
    public string    GSTIN               { get; set; } = "";
    public string    BillingAddress      { get; set; } = "";
    public string    ShippingAddress     { get; set; } = "";
    public string    StateCode           { get; set; } = "";
    // New in V3
    public PartyType PartyType           { get; set; } = PartyType.Customer;
    public decimal   OpeningBalance      { get; set; } = 0;
    public string    OpeningBalanceDate  { get; set; } = "";
}

/// <summary>
/// Extended with stock management columns.
/// </summary>
public class ItemMaster : INotifyPropertyChanged
{
    private decimal _currentStock;

    public int     Id            { get; set; }
    public string  Name          { get; set; } = "";
    public string  HSN           { get; set; } = "";
    public decimal DefaultRate   { get; set; }
    public decimal GSTPercent    { get; set; } = 18;
    public string  UOM           { get; set; } = "Nos";
    // New in V3
    public decimal OpeningStock  { get; set; } = 0;
    public decimal ReorderLevel  { get; set; } = 0;

    public decimal CurrentStock
    {
        get => _currentStock;
        set
        {
            _currentStock = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentStock)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockColor)));
        }
    }

    public string StockStatus => CurrentStock <= 0 ? "Out of Stock"
        : CurrentStock <= ReorderLevel ? "Low Stock"
        : "In Stock";

    public string StockColor => CurrentStock <= 0 ? "#EF4444"
        : CurrentStock <= ReorderLevel ? "#F59E0B"
        : "#10B981";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class TandCMaster
{
    public int    Id           { get; set; }
    public string DocType      { get; set; } = "All";
    public string Label        { get; set; } = "";
    public string PaymentTerms { get; set; } = "";
    public string GeneralTerms { get; set; } = "";
}

public class DocTemplate
{
    public string DocType      { get; set; } = "";
    public string HtmlTemplate { get; set; } = "";
    public string PdfTemplate  { get; set; } = "";
}

public class InvoiceItem
{
    public int     ItemNumber  { get; set; }
    public string  Description { get; set; } = "";
    public string  HSN         { get; set; } = "";
    public string  UOM         { get; set; } = "Nos";
    public decimal Quantity    { get; set; }
    public decimal Rate        { get; set; }
    public decimal GSTPercent  { get; set; }
    public decimal LineTotal   => Quantity * Rate;
    public decimal Total       => Quantity * Rate * (1 + GSTPercent / 100);
}

public class ErpDocument
{
    public int               Id            { get; set; }
    public DocumentType      DocType       { get; set; } = DocumentType.SalesInvoice;
    public string            DocumentNo    { get; set; } = "";
    public DateTime          Date          { get; set; } = DateTime.Now;
    public string            CreatedBy     { get; set; } = "";
    public CompanyProfile    Company       { get; set; } = new();
    public Customer          Customer      { get; set; } = new();
    public List<InvoiceItem> Items         { get; set; } = new();
    public GstMode           GstMode       { get; set; } = GstMode.CgstSgst;
    public decimal           GstPercent    { get; set; } = 18;
    public decimal           Discount      { get; set; }
    public decimal           Freight       { get; set; }
    public decimal           GrandTotal    { get; set; }
    public string            PaymentTerms  { get; set; } = "";
    public string            GeneralTerms  { get; set; } = "";
    public DocumentStatus    Status        { get; set; } = DocumentStatus.Open;
    public decimal           PendingAmount { get; set; }
    public DateTime?         LastModified  { get; set; }
    public string            CustomerRefNo { get; set; } = "";
    public string            EWayBillNo    { get; set; } = "";
    public string            PlaceOfSupply { get; set; } = "";
}

public class PaymentEntry
{
    public int         Id         { get; set; }
    public string      DocumentNo { get; set; } = "";
    public DateTime    Date       { get; set; } = DateTime.Now;
    public decimal     Amount     { get; set; }
    public PaymentMode Mode       { get; set; } = PaymentMode.Cash;
    public string      Reference  { get; set; } = "";
    public string      Notes      { get; set; } = "";
    public string      PartyName  { get; set; } = "";
    public DateTime?   CreatedAt  { get; set; }
}

// ── NEW: Stock Ledger Entry ───────────────────────────────────────────────────

public class StockLedgerEntry
{
    public int           Id           { get; set; }
    public DateTime      EntryDate    { get; set; } = DateTime.Now;
    public string        ItemName     { get; set; } = "";
    public string        HSN          { get; set; } = "";
    public string        UOM          { get; set; } = "Nos";
    public StockMovement MovementType { get; set; } = StockMovement.IN;
    public decimal       Quantity     { get; set; }
    public decimal       Rate         { get; set; }
    public decimal       Value        => Quantity * Rate;
    public string        DocumentNo   { get; set; } = "";
    public string        DocType      { get; set; } = "";
    public string        PartyName    { get; set; } = "";
    public string        Narration    { get; set; } = "";

    // Display helpers
    public string DateLabel    => EntryDate.ToString("dd MMM yyyy");
    public string MovementLabel => MovementType.ToString();
    public string MovementColor => MovementType switch
    {
        StockMovement.IN         => "#10B981",
        StockMovement.OPENING    => "#3B82F6",
        StockMovement.ADJUST_IN  => "#06B6D4",
        StockMovement.OUT        => "#EF4444",
        StockMovement.ADJUST_OUT => "#F59E0B",
        _                        => "#6B7280"
    };
}

// ── NEW: Party Ledger Entry ───────────────────────────────────────────────────

public class PartyLedgerEntry
{
    public int            Id         { get; set; }
    public DateTime       EntryDate  { get; set; } = DateTime.Now;
    public string         PartyName  { get; set; } = "";
    public string         PartyType  { get; set; } = "Customer";
    public LedgerEntryType EntryType { get; set; } = LedgerEntryType.DEBIT;
    public decimal        Debit      { get; set; }
    public decimal        Credit     { get; set; }
    public decimal        Balance    { get; set; }
    public string         DocumentNo { get; set; } = "";
    public string         DocType    { get; set; } = "";
    public string         Narration  { get; set; } = "";

    // Display helpers
    public string DateLabel    => EntryDate.ToString("dd MMM yyyy");
    public string DebitLabel   => Debit  > 0 ? $"₹{Debit:N2}"  : "";
    public string CreditLabel  => Credit > 0 ? $"₹{Credit:N2}" : "";
    public string BalanceLabel => $"₹{Balance:N2}";
    public string EntryTypeLabel => EntryType.ToString().Replace("_", " ");
}

// ── View models (list items shown in DataGrids) ───────────────────────────────

public class DocumentListItem : INotifyPropertyChanged
{
    private DocumentStatus _status;
    private decimal        _pendingAmount;

    public int            Id            { get; set; }
    public string         DocumentNo    { get; set; } = "";
    public string         DocTypeLabel  { get; set; } = "";
    public string         CustomerName  { get; set; } = "";
    public string         CustomerRefNo { get; set; } = "";
    public DateTime       Date          { get; set; }
    public decimal        GrandTotal    { get; set; }
    public string         CreatedBy     { get; set; } = "";
    public decimal        Subtotal      { get; set; }
    public decimal        GstAmount     { get; set; }
    public string         DateLabel     => Date.ToString("dd MMM yyyy");

    public DocumentStatus Status
    {
        get => _status;
        set { _status = value; PC(nameof(Status)); PC(nameof(StatusLabel)); PC(nameof(StatusColor)); }
    }

    public decimal PendingAmount
    {
        get => _pendingAmount;
        set { _pendingAmount = value; PC(nameof(PendingAmount)); }
    }

    public string StatusLabel => Status.ToString();
    public string StatusColor => Status switch
    {
        DocumentStatus.Open     => "#3B82F6",
        DocumentStatus.Pending  => "#F59E0B",
        DocumentStatus.Complete => "#10B981",
        DocumentStatus.Canceled => "#EF4444",
        _                       => "#6B7280"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void PC(string n) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class PaymentListItem
{
    public int      Id         { get; set; }
    public string   DocumentNo { get; set; } = "";
    public string   PartyName  { get; set; } = "";
    public DateTime Date       { get; set; }
    public decimal  Amount     { get; set; }
    public string   ModeLabel  { get; set; } = "";
    public string   Reference  { get; set; } = "";
    public string   Notes      { get; set; } = "";
    public string   DateLabel  => Date.ToString("dd MMM yyyy");
}

public class GstReportRow
{
    public string  MonthYear  { get; set; } = "";
    public string  DocType    { get; set; } = "";
    public int     Count      { get; set; }
    public decimal Taxable    { get; set; }
    public decimal CGST       { get; set; }
    public decimal SGST       { get; set; }
    public decimal IGST       { get; set; }
    public decimal TotalTax   => CGST + SGST + IGST;
    public decimal GrandTotal { get; set; }
}

// ── NEW: Party summary (for ledger summary view) ──────────────────────────────

public class PartySummary
{
    public string  PartyName      { get; set; } = "";
    public string  PartyType      { get; set; } = "";
    public decimal TotalDebit     { get; set; }
    public decimal TotalCredit    { get; set; }
    public decimal Balance        { get; set; }
    public string  BalanceLabel   => $"₹{Math.Abs(Balance):N2}";
    public string  BalanceType    => Balance > 0 ? "Dr" : Balance < 0 ? "Cr" : "Nil";
    public string  BalanceColor   => Balance > 0 ? "#EF4444" : Balance < 0 ? "#10B981" : "#6B7280";
}

// ── NEW: Stock summary (for inventory summary view) ───────────────────────────

public class StockSummary
{
    public string  ItemName      { get; set; } = "";
    public string  HSN           { get; set; } = "";
    public string  UOM           { get; set; } = "";
    public decimal OpeningStock  { get; set; }
    public decimal TotalIn       { get; set; }
    public decimal TotalOut      { get; set; }
    public decimal CurrentStock  { get; set; }
    public decimal ReorderLevel  { get; set; }
    public string  StockStatus   => CurrentStock <= 0 ? "Out of Stock"
        : CurrentStock <= ReorderLevel ? "Low Stock" : "In Stock";
    public string  StockColor    => CurrentStock <= 0 ? "#EF4444"
        : CurrentStock <= ReorderLevel ? "#F59E0B" : "#10B981";
}
