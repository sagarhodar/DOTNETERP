using System;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Ojaswat;          
using Ojaswat.Models;
using Ojaswat.Services;

namespace Ojaswat.ViewModels;

/// <summary>
/// Editable line-item row bound to the DataGrid in CreateDocumentWindow.
/// </summary>
public class EditableItem : INotifyPropertyChanged
{
    private decimal _qty, _rate, _gst;
    private string  _desc = "", _hsn = "", _uom = "Nos";

    public int ItemNumber { get; set; }

    public string Description
    { get => _desc; set { _desc = value; PC(nameof(Description)); } }
    public string HSN
    { get => _hsn;  set { _hsn  = value; PC(nameof(HSN));  } }
    public string UOM
    { get => _uom;  set { _uom  = value; PC(nameof(UOM));  } }
    public decimal Quantity
    { get => _qty;  set { _qty  = value; PC(nameof(Quantity)); PC(nameof(LineTotal)); } }
    public decimal Rate
    { get => _rate; set { _rate = value; PC(nameof(Rate)); PC(nameof(LineTotal)); } }
    public decimal GSTPercent
    { get => _gst;  set { _gst  = value; PC(nameof(GSTPercent)); PC(nameof(LineTotal)); } }
    public decimal LineTotal => Quantity * Rate;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void PC(string n) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// All form logic for CreateDocumentWindow — totals, build, save, export.
/// The code-behind only handles file pickers and DataGrid commit.
/// </summary>
public class DocumentViewModel : INotifyPropertyChanged
{
    // ── Services ──────────────────────────────────────────────────────────────
    private readonly ErpDocumentDbService  _db;
    private readonly ExportService         _export;
    private readonly DocumentNumberService _docNos;

    // ── State ─────────────────────────────────────────────────────────────────
    public  bool         IsEdit   { get; private set; }
    private ErpDocument? _editDoc;
    private GstMode      _gstMode = GstMode.CgstSgst;

    public ObservableCollection<EditableItem> Items { get; } = new();

    // ── Form field properties ─────────────────────────────────────────────────
    private DocumentType _docType = DocumentType.SalesInvoice;
    public  DocumentType  DocType
    {
        get => _docType;
        set
        {
            _docType = value;
            PC(nameof(DocType));
            PC(nameof(ShowEWayBill));
            if (!IsEdit) DocNo = _docNos.Generate(value);
        }
    }

    public bool ShowEWayBill =>
        DocType is DocumentType.SalesInvoice or DocumentType.PurchaseInvoice;

    private string _docNo = "", _createdBy = "", _custRefNo = "", _eWayBill = "",
                   _placeOfSupply = "", _paymentTerms = "", _generalTerms = "",
                   _custName = "", _custGstin = "", _custStateCode = "",
                   _custBilling = "", _custShipping = "",
                   _coName = "", _coPhone = "", _coGstin = "", _coStateCode = "",
                   _coEmail = "", _coAddress = "", _coBank = "", _coAccount = "",
                   _coIfsc = "", _coSignatory = "", _coQrCode = "", _logoPath = "";

    private decimal _discount, _freight, _gstPercent = 18, _pendingAmount;
    private DateTime _docDate = DateTime.Now;
    private DocumentStatus _status = DocumentStatus.Open;

    public string DocNo         { get => _docNo;         set { _docNo         = value; PC(nameof(DocNo)); } }
    public string CreatedBy     { get => _createdBy;     set { _createdBy     = value; PC(nameof(CreatedBy)); } }
    public string CustomerRefNo { get => _custRefNo;     set { _custRefNo     = value; PC(nameof(CustomerRefNo)); } }
    public string EWayBillNo    { get => _eWayBill;      set { _eWayBill      = value; PC(nameof(EWayBillNo)); } }
    public string PlaceOfSupply { get => _placeOfSupply; set { _placeOfSupply = value; PC(nameof(PlaceOfSupply)); } }
    public string PaymentTerms  { get => _paymentTerms;  set { _paymentTerms  = value; PC(nameof(PaymentTerms)); } }
    public string GeneralTerms  { get => _generalTerms;  set { _generalTerms  = value; PC(nameof(GeneralTerms)); } }

    public string CustName      { get => _custName;      set { _custName      = value; PC(nameof(CustName)); } }
    public string CustGSTIN     { get => _custGstin;     set { _custGstin     = value; PC(nameof(CustGSTIN)); } }
    public string CustStateCode { get => _custStateCode; set { _custStateCode = value; PC(nameof(CustStateCode)); } }
    public string CustBilling   { get => _custBilling;   set { _custBilling   = value; PC(nameof(CustBilling)); } }
    public string CustShipping  { get => _custShipping;  set { _custShipping  = value; PC(nameof(CustShipping)); } }

    public string CoName      { get => _coName;      set { _coName      = value; PC(nameof(CoName)); } }
    public string CoPhone     { get => _coPhone;     set { _coPhone     = value; PC(nameof(CoPhone)); } }
    public string CoGSTIN     { get => _coGstin;     set { _coGstin     = value; PC(nameof(CoGSTIN)); } }
    public string CoStateCode { get => _coStateCode; set { _coStateCode = value; PC(nameof(CoStateCode)); } }
    public string CoEmail     { get => _coEmail;     set { _coEmail     = value; PC(nameof(CoEmail)); } }
    public string CoAddress   { get => _coAddress;   set { _coAddress   = value; PC(nameof(CoAddress)); } }
    public string CoBank      { get => _coBank;      set { _coBank      = value; PC(nameof(CoBank)); } }
    public string CoAccount   { get => _coAccount;   set { _coAccount   = value; PC(nameof(CoAccount)); } }
    public string CoIFSC      { get => _coIfsc;      set { _coIfsc      = value; PC(nameof(CoIFSC)); } }
    public string CoSignatory { get => _coSignatory; set { _coSignatory = value; PC(nameof(CoSignatory)); } }
    public string CoQRCode    { get => _coQrCode;    set { _coQrCode    = value; PC(nameof(CoQRCode)); } }
    public string LogoPath    { get => _logoPath;    set { _logoPath    = value; PC(nameof(LogoPath)); } }

    public decimal Discount
    { get => _discount; set { _discount = value; PC(nameof(Discount)); RefreshTotals(); } }
    public decimal Freight
    { get => _freight;  set { _freight  = value; PC(nameof(Freight));  RefreshTotals(); } }
    public decimal GstPercent
    { get => _gstPercent; set { _gstPercent = value; PC(nameof(GstPercent)); RefreshTotals(); } }
    public decimal PendingAmount
    { get => _pendingAmount; set { _pendingAmount = value; PC(nameof(PendingAmount)); } }

    public DateTime DocDate
    { get => _docDate; set { _docDate = value; PC(nameof(DocDate)); } }
    public DocumentStatus Status
    { get => _status; set { _status = value; PC(nameof(Status)); } }

    public GstMode GstMode
    {
        get => _gstMode;
        set { _gstMode = value; PC(nameof(GstMode)); RefreshTotals(); }
    }

    // ── Calculated totals ─────────────────────────────────────────────────────
    private decimal _subtotal, _taxAmount, _grandTotal;
    private string  _cgstLabel = "CGST", _sgstLabel = "SGST", _igstLabel = "IGST";
    private decimal _cgst, _sgst, _igst;

    public decimal Subtotal   { get => _subtotal;   private set { _subtotal   = value; PC(nameof(Subtotal)); } }
    public decimal TaxAmount  { get => _taxAmount;  private set { _taxAmount  = value; PC(nameof(TaxAmount)); } }
    public decimal GrandTotal { get => _grandTotal; private set { _grandTotal = value; PC(nameof(GrandTotal)); } }
    public decimal CGST       { get => _cgst;       private set { _cgst       = value; PC(nameof(CGST)); } }
    public decimal SGST       { get => _sgst;       private set { _sgst       = value; PC(nameof(SGST)); } }
    public decimal IGST       { get => _igst;       private set { _igst       = value; PC(nameof(IGST)); } }
    public string  CgstLabel  { get => _cgstLabel;  private set { _cgstLabel  = value; PC(nameof(CgstLabel)); } }
    public string  SgstLabel  { get => _sgstLabel;  private set { _sgstLabel  = value; PC(nameof(SgstLabel)); } }
    public string  IgstLabel  { get => _igstLabel;  private set { _igstLabel  = value; PC(nameof(IgstLabel)); } }
    public bool    ShowCgst   => GstMode == GstMode.CgstSgst;

    // ── Status bar ────────────────────────────────────────────────────────────
    private string _formStatus = "";
    public  string  FormStatus { get => _formStatus; set { _formStatus = value; PC(nameof(FormStatus)); } }

    public bool DocumentSaved { get; private set; }

    // ── Window title / save button text ───────────────────────────────────────
    public string WindowTitle  => IsEdit ? $"✏  Edit — {_editDoc?.DocumentNo}" : "✚  New Document";
    public string SaveBtnLabel => IsEdit ? "✔ Update Document  (Ctrl+S)" : "✔ Save Document  (Ctrl+S)";

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand SaveCommand       { get; }
    public ICommand AddRowCommand     { get; }
    public ICommand ExportPdfCommand  { get; }
    public ICommand ExportHtmlCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public DocumentViewModel(
        ErpDocumentDbService  db,
        ExportService         export,
        DocumentNumberService docNos,
        ErpDocument?          existing = null,
        string?               logoPath = null)
    {
        _db     = db;
        _export = export;
        _docNos = docNos;
        IsEdit  = existing != null;
        if (!string.IsNullOrEmpty(logoPath)) LogoPath = logoPath;

        SaveCommand       = new RelayCommand(Save,       () => Items.Count > 0);
        AddRowCommand     = new RelayCommand(AddEmptyRow);
        ExportPdfCommand  = new RelayCommand(() => ExportPdf());
        ExportHtmlCommand = new RelayCommand(() => ExportHtml());

        Items.CollectionChanged += (_, _) => RefreshTotals();

        if (existing != null) LoadFromDocument(existing);
        else                  SetDefaults();
    }

    // ── Load / defaults ───────────────────────────────────────────────────────
    public void LoadFromDocument(ErpDocument doc)
    {
        _editDoc   = doc;
        DocType    = doc.DocType;
        DocNo      = doc.DocumentNo;
        DocDate    = doc.Date;
        CreatedBy  = doc.CreatedBy;
        Status     = doc.Status;
        PendingAmount  = doc.PendingAmount;
        CustomerRefNo  = doc.CustomerRefNo;
        EWayBillNo     = doc.EWayBillNo;
        PlaceOfSupply  = doc.PlaceOfSupply;

        CoName      = doc.Company.Name;      CoPhone     = doc.Company.Phone;
        CoGSTIN     = doc.Company.GSTIN;     CoStateCode = doc.Company.StateCode;
        CoEmail     = doc.Company.Email;     CoAddress   = doc.Company.Address;
        CoBank      = doc.Company.Bank;      CoAccount   = doc.Company.Account;
        CoIFSC      = doc.Company.IFSC;      CoSignatory = doc.Company.Signatory;
        CoQRCode    = doc.Company.QRCodePath;
        if (!string.IsNullOrEmpty(doc.Company.LogoPath)) LogoPath = doc.Company.LogoPath;

        CustName      = doc.Customer.Name;
        CustGSTIN     = doc.Customer.GSTIN;
        CustStateCode = doc.Customer.StateCode;
        CustBilling   = doc.Customer.BillingAddress;
        CustShipping  = doc.Customer.ShippingAddress;

        GstMode    = doc.GstMode;
        GstPercent = doc.GstPercent;
        Discount   = doc.Discount;
        Freight    = doc.Freight;
        PaymentTerms = doc.PaymentTerms;
        GeneralTerms = doc.GeneralTerms;

        Items.Clear();
        foreach (var it in doc.Items)
            Items.Add(new EditableItem
            {
                ItemNumber  = it.ItemNumber, Description = it.Description,
                HSN         = it.HSN,        UOM         = it.UOM,
                Quantity    = it.Quantity,   Rate        = it.Rate,
                GSTPercent  = it.GSTPercent,
            });

        RefreshTotals();
    }

    private void SetDefaults()
    {
        DocDate = DateTime.Now;
        var cp  = _db.LoadCompany();
        CoName      = cp.Name.Length      > 0 ? cp.Name      : InvoiceSettings.CompanyName;
        CoPhone     = cp.Phone.Length     > 0 ? cp.Phone     : InvoiceSettings.CompanyPhone;
        CoGSTIN     = cp.GSTIN.Length     > 0 ? cp.GSTIN     : InvoiceSettings.CompanyGSTIN;
        CoStateCode = cp.StateCode.Length > 0 ? cp.StateCode : InvoiceSettings.CompanyStateCode;
        CoEmail     = cp.Email.Length     > 0 ? cp.Email     : InvoiceSettings.CompanyEmail;
        CoAddress   = cp.Address.Length   > 0 ? cp.Address   : InvoiceSettings.CompanyAddress;
        CoBank      = cp.Bank.Length      > 0 ? cp.Bank      : InvoiceSettings.CompanyBank;
        CoAccount   = cp.Account.Length   > 0 ? cp.Account   : InvoiceSettings.CompanyAccount;
        CoIFSC      = cp.IFSC.Length      > 0 ? cp.IFSC      : InvoiceSettings.CompanyIFSC;
        CoSignatory = cp.Signatory.Length > 0 ? cp.Signatory : InvoiceSettings.CompanySignatory;
        CoQRCode    = cp.QRCodePath.Length > 0 ? cp.QRCodePath : InvoiceSettings.QRCodePath;
        if (!string.IsNullOrEmpty(cp.LogoPath)) 
        {
            LogoPath = (!string.IsNullOrEmpty(cp.LogoPath) && File.Exists(cp.LogoPath))
            ? cp.LogoPath
            : InvoiceSettings.LogoPath;
        }

        PaymentTerms = InvoiceSettings.DefaultPaymentTerms;
        GeneralTerms = InvoiceSettings.DefaultGeneralTerms;
        GstPercent   = 18;
        DocNo        = _docNos.Generate(DocType);
    }

    // ── Load a customer into customer fields ──────────────────────────────────
    public void LoadCustomer(Customer c)
    {
        CustName      = c.Name;
        CustGSTIN     = c.GSTIN;
        CustStateCode = c.StateCode;
        CustBilling   = c.BillingAddress;
        CustShipping  = c.ShippingAddress;
    }

    // ── Add item row ──────────────────────────────────────────────────────────
    public void AddEmptyRow() => AddRow("", "", "Nos", 1, 0, 18);

    public void AddRow(string desc, string hsn, string uom,
                       decimal qty, decimal rate, decimal gst)
    {
        Items.Add(new EditableItem
        {
            ItemNumber  = Items.Count + 1,
            Description = desc, HSN = hsn, UOM = uom,
            Quantity    = qty,  Rate = rate, GSTPercent = gst,
        });
    }

    public void RemoveRow(EditableItem row)
    {
        Items.Remove(row);
        for (int i = 0; i < Items.Count; i++) Items[i].ItemNumber = i + 1;
        RefreshTotals();
    }

    // ── Totals ────────────────────────────────────────────────────────────────
    public void RefreshTotals()
    {
        decimal sub    = Items.Sum(r => r.LineTotal);
        decimal tax = Items.Sum(r => r.LineTotal * r.GSTPercent / 100);
        bool    isCgst = GstMode == GstMode.CgstSgst;

        Subtotal   = sub;
        TaxAmount  = tax;
        GrandTotal = sub - Discount + tax + Freight;

        if (isCgst)
        {
            CgstLabel = $"CGST ({GstPercent / 2:N1}%)";
            SgstLabel = $"SGST ({GstPercent / 2:N1}%)";
            CGST = tax / 2; SGST = tax / 2; IGST = 0;
        }
        else
        {
            IgstLabel = $"IGST ({GstPercent:N1}%)";
            IGST = tax; CGST = 0; SGST = 0;
        }
        PC(nameof(ShowCgst));
    }

    // ── Build domain object ───────────────────────────────────────────────────
    public ErpDocument BuildDocument()
    {
        var doc = new ErpDocument
        {
            DocType       = DocType,
            DocumentNo    = DocNo.Trim(),
            Date          = DocDate,
            CreatedBy     = CreatedBy.Trim(),
            CustomerRefNo = CustomerRefNo.Trim(),
            EWayBillNo    = EWayBillNo.Trim(),
            PlaceOfSupply = PlaceOfSupply.Trim(),
            Status        = Status,
            PendingAmount = PendingAmount,
            GstMode       = GstMode,
            GstPercent    = GstPercent,
            Discount      = Discount,
            Freight       = Freight,
            GrandTotal    = GrandTotal,
            PaymentTerms  = PaymentTerms,
            GeneralTerms  = GeneralTerms,
            Company = new CompanyProfile
            {
                Name = CoName, Phone = CoPhone, GSTIN = CoGSTIN,
                StateCode = CoStateCode, Email = CoEmail, Address = CoAddress,
                Bank = CoBank, Account = CoAccount, IFSC = CoIFSC,
                Signatory = CoSignatory, LogoPath = LogoPath, QRCodePath = CoQRCode,
            },
            Customer = new Customer
            {
                Name = CustName, GSTIN = CustGSTIN, StateCode = CustStateCode,
                BillingAddress = CustBilling, ShippingAddress = CustShipping,
            },
            Items = Items.Select((r, i) => new InvoiceItem
            {
                ItemNumber  = i + 1, Description = r.Description,
                HSN         = r.HSN, UOM         = r.UOM,
                Quantity    = r.Quantity, Rate    = r.Rate, GSTPercent = r.GSTPercent,
            }).ToList(),
        };

        if (IsEdit) doc.Id = _editDoc!.Id;
        return doc;
    }

    // ── Save ──────────────────────────────────────────────────────────────────
    public void Save()
    {
        if (Items.Count == 0)  throw new InvalidOperationException("Add at least one item.");
        if (string.IsNullOrWhiteSpace(CustName)) throw new InvalidOperationException("Customer name is required.");
        if (string.IsNullOrWhiteSpace(DocNo))    throw new InvalidOperationException("Document number is required.");

        RefreshTotals(); // ✅ make sure total is correct

        bool wasNew = !IsEdit;
        var doc = BuildDocument();

        // ✅ AUTO PENDING LOGIC
        if (wasNew)
        {
            doc.PendingAmount = doc.GrandTotal;
        }
        else if (_editDoc != null)
        {
            // calculate how much already paid
            var paidAmount = _editDoc.GrandTotal - _editDoc.PendingAmount;

            // recalculate pending based on new total
            doc.PendingAmount = doc.GrandTotal - paidAmount;

            // safety
            if (doc.PendingAmount < 0)
                doc.PendingAmount = 0;
        }

        if (doc.PendingAmount <= 0)
            doc.Status = DocumentStatus.Complete;
        else if (doc.PendingAmount < doc.GrandTotal)
            doc.Status = DocumentStatus.Pending;
        else
            doc.Status = DocumentStatus.Open;

        if (IsEdit)
        {
            _db.UpdateDocument(doc);
            _editDoc = doc;
        }
        else
        {
            _db.SaveDocument(doc);
            _editDoc = doc;
            IsEdit = true;
        }

        // ✅ update UI value
        PendingAmount = doc.PendingAmount;

        DocumentSaved = true;
        FormStatus    = $"✓ {(wasNew ? "Saved" : "Updated")} at {DateTime.Now:HH:mm:ss}" +
                        $"  |  ₹{doc.GrandTotal:N2}  |  {doc.Items.Count} items  |  {doc.Status}";

        PC(nameof(WindowTitle));
        PC(nameof(SaveBtnLabel));
    }

    // ── Export ────────────────────────────────────────────────────────────────
    public string ExportPdf()
    {
        var path = _export.ExportPdf(BuildDocument());
        FormStatus = $"📄 PDF ready: {path}";
        return path;
    }

    public string ExportHtml()
    {
        var path = _export.ExportHtml(BuildDocument());
        FormStatus = $"🌐 HTML ready: {path}";
        return path;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void PC(string n) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
