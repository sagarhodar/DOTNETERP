using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Ojaswat.Models;
using Ojaswat.Services;

namespace Ojaswat.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public readonly ErpDocumentDbService  ErpDb      = new();
    public readonly ExportService         Export     = new();
    public readonly DocumentNumberService DocNumbers;

    private ObservableCollection<DocumentListItem> _allDocs = new();
    public  ObservableCollection<DocumentListItem>  AllDocs
    {
        get => _allDocs;
        set { _allDocs = value; PC(nameof(AllDocs)); RefreshStats(); }
    }

    private string _logoPath = string.Empty;
    public  string  LogoPath
    {
        get => _logoPath;
        set { _logoPath = value; PC(nameof(LogoPath)); }
    }

    private string _statusText = string.Empty;
    public  string  StatusText
    {
        get => _statusText;
        set { _statusText = value; PC(nameof(StatusText)); }
    }

    private string _currentPageKey = "dash";
    public  string  CurrentPageKey
    {
        get => _currentPageKey;
        set { _currentPageKey = value; PC(nameof(CurrentPageKey)); }
    }

    private string _pageTitle = "Dashboard";
    public  string  PageTitle
    {
        get => _pageTitle;
        set { _pageTitle = value; PC(nameof(PageTitle)); }
    }

    public DocumentType[]? PendingModuleFilter  { get; private set; }
    public DocumentType?   PendingDocTypeFilter { get; private set; }
    public bool            PendingPendingOnly   { get; private set; }

    // Stats
    private int     _statTotal, _statSales, _statPurchase;
    private decimal _statPending, _statPayments;
    public int     StatTotal    { get => _statTotal;    private set { _statTotal    = value; PC(nameof(StatTotal));    } }
    public int     StatSales    { get => _statSales;    private set { _statSales    = value; PC(nameof(StatSales));    } }
    public int     StatPurchase { get => _statPurchase; private set { _statPurchase = value; PC(nameof(StatPurchase)); } }
    public decimal StatPending  { get => _statPending;  private set { _statPending  = value; PC(nameof(StatPending));  } }
    public decimal StatPayments { get => _statPayments; private set { _statPayments = value; PC(nameof(StatPayments)); } }

    // Navigation commands
    public ICommand NavDashboardCommand        { get; }
    public ICommand NavSalesAllCommand         { get; }
    public ICommand NavQuotationCommand        { get; }
    public ICommand NavSalesOrderCommand       { get; }
    public ICommand NavSalesInvoiceCommand     { get; }
    public ICommand NavCreditNoteCommand       { get; }
    public ICommand NavPurchaseAllCommand      { get; }
    public ICommand NavPurchaseOrderCommand    { get; }
    public ICommand NavPurchaseInvoiceCommand  { get; }
    public ICommand NavGRNCommand              { get; }
    public ICommand NavDebitNoteCommand        { get; }
    public ICommand NavFinanceCommand          { get; }
    public ICommand NavGstReportCommand        { get; }
    public ICommand NavReportAllCommand        { get; }
    public ICommand NavReportPendingCommand    { get; }
    public ICommand NavReportItemsCommand      { get; }
    public ICommand NavInventoryCommand        { get; }   // ← NEW
    public ICommand NavLedgerCommand           { get; }   // ← NEW
    public ICommand NavConfigCommand           { get; }
    public ICommand NavMasterDataCommand       { get; }
    public ICommand NavTandCCommand            { get; }
    public ICommand NavHelpCommand             { get; }

    public MainViewModel()
    {
        DocNumbers = new DocumentNumberService(new DuckDbService());
        LoadLogoFromSettings();

        NavDashboardCommand       = NavCmd("dash",            "Dashboard",            refresh: true);
        NavSalesAllCommand        = NavCmd("sales",           "Sales",                moduleFilter: ModuleDocTypes.Sales);
        NavQuotationCommand       = NavCmdDoc("quotation",    "Quotations",           DocumentType.Quotation);
        NavSalesOrderCommand      = NavCmdDoc("salesorder",   "Sales Orders",         DocumentType.SalesOrder);
        NavSalesInvoiceCommand    = NavCmdDoc("salesinvoice", "Sales Invoices",       DocumentType.SalesInvoice);
        NavCreditNoteCommand      = NavCmdDoc("creditnote",   "Credit Notes",         DocumentType.CreditNote);
        NavPurchaseAllCommand     = NavCmd("purchase",        "Purchase",             moduleFilter: ModuleDocTypes.Purchase);
        NavPurchaseOrderCommand   = NavCmdDoc("purchaseorder",   "Purchase Orders",   DocumentType.PurchaseOrder);
        NavPurchaseInvoiceCommand = NavCmdDoc("purchaseinvoice", "Purchase Invoices", DocumentType.PurchaseInvoice);
        NavGRNCommand             = NavCmdDoc("grn",          "GRN",                  DocumentType.GRN);
        NavDebitNoteCommand       = NavCmdDoc("debitnote",    "Debit Notes",          DocumentType.DebitNote);
        NavFinanceCommand         = NavCmd("finance",         "Payment Ledger");
        NavGstReportCommand       = NavCmd("gst",             "GST Report");
        NavReportAllCommand       = NavCmd("doclist",         "All Documents");
        NavReportPendingCommand   = new RelayCommand(() => { ClearFilters(); PendingPendingOnly = true; Navigate("pending", "Pending / Open"); });
        NavReportItemsCommand     = NavCmd("items",           "Items Report");
        NavInventoryCommand       = NavCmd("inventory",       "Inventory");           // ← NEW
        NavLedgerCommand          = NavCmd("ledger",          "Party Ledger");        // ← NEW
        NavConfigCommand          = NavCmd("config",          "Company & Settings");
        NavMasterDataCommand      = NavCmd("master",          "Master Data");
        NavTandCCommand           = NavCmd("tandc",           "T&C Master");
        NavHelpCommand            = NavCmd("help",            "Help & About");
    }

    private RelayCommand NavCmd(string key, string title,
        bool refresh = false, DocumentType[]? moduleFilter = null) =>
        new(() =>
        {
            ClearFilters();
            if (moduleFilter != null) PendingModuleFilter = moduleFilter;
            if (refresh) LoadDocuments();
            Navigate(key, title);
        });

    private RelayCommand NavCmdDoc(string key, string title, DocumentType dt) =>
        new(() => { ClearFilters(); PendingDocTypeFilter = dt; Navigate(key, title); });

    private void Navigate(string key, string title)
    { PageTitle = title; CurrentPageKey = key; }

    private void ClearFilters()
    { PendingModuleFilter = null; PendingDocTypeFilter = null; PendingPendingOnly = false; }

    public void LoadDocuments()
    {
        try
        {
            var docs = ErpDb.LoadAllDocuments();
            AllDocs    = new ObservableCollection<DocumentListItem>(docs);
            StatusText = $"{docs.Count} docs  •  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { StatusText = $"Load error: {ex.Message}"; }
    }

    public void RefreshStats()
    {
        StatTotal    = AllDocs.Count;
        StatSales    = AllDocs.Count(d => ModuleDocTypes.Sales.Any(t => d.DocTypeLabel == t.ToString()));
        StatPurchase = AllDocs.Count(d => ModuleDocTypes.Purchase.Any(t => d.DocTypeLabel == t.ToString()));
        StatPending  = AllDocs.Sum(d => d.PendingAmount);
        try { StatPayments = ErpDb.LoadAllPayments().Sum(p => p.Amount); } catch { StatPayments = 0; }
    }

    public void ApplyLogo(string path) { if (!File.Exists(path)) return; LogoPath = path; }

    private static string SettingsFile => Path.Combine(DuckDbService.DbFolder, "settings.txt");

    public void SaveLogoSetting(string path)
    {
        Directory.CreateDirectory(DuckDbService.DbFolder);
        var lines = File.Exists(SettingsFile)
            ? File.ReadAllLines(SettingsFile).Where(l => !l.StartsWith("Logo=")).ToList()
            : new System.Collections.Generic.List<string>();
        lines.Add($"Logo={path}");
        File.WriteAllLines(SettingsFile, lines);
    }

    private void LoadLogoFromSettings()
    {
        if (!File.Exists(SettingsFile)) return;
        foreach (var line in File.ReadAllLines(SettingsFile))
            if (line.StartsWith("Logo=")) { ApplyLogo(line.Substring(5).Trim()); break; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void PC(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
