using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Ojaswat.Models;
using Ojaswat.Services;
using Ojaswat.ViewModels;

namespace Ojaswat.Windows;

/// <summary>
/// Thin code-behind — only file pickers and DataGrid commit remain here.
/// All form logic (totals, build, save, export) lives in DocumentViewModel.
/// </summary>
public partial class CreateDocumentWindow : Window
{
    public  bool             DocumentSaved => _docVm.DocumentSaved;
    private DocumentViewModel _docVm;

    private readonly MainViewModel _mainVm;
    private readonly CsvService    _csv = new();
    private List<Customer>         _customers = new();
    private List<ItemMaster>       _items     = new();
    private List<TandCMaster>      _tandcList = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public CreateDocumentWindow(
        ErpDocument?   existing  = null,
        MainViewModel? vm        = null,
        DocumentType?  presetType = null)
    {
        

        _mainVm = vm ?? new MainViewModel();
        _docVm  = new DocumentViewModel(
            _mainVm.ErpDb,
            _mainVm.Export,
            _mainVm.DocNumbers,
            existing,
            _mainVm.LogoPath);

        InitializeComponent();

        // Wire ViewModel → UI (manual, since we keep named TextBoxes for UX convenience)
        _docVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentViewModel.FormStatus))
                FormStatusText.Text = _docVm.FormStatus;
            if (e.PropertyName == nameof(DocumentViewModel.WindowTitle))
                WindowTitleText.Text = _docVm.WindowTitle;
            if (e.PropertyName == nameof(DocumentViewModel.SaveBtnLabel))
                SaveBtn.Content = _docVm.SaveBtnLabel;
        };

        _customers  = _mainVm.ErpDb.LoadCustomers();
        _items      = _mainVm.ErpDb.LoadItems();
        _tandcList  = _mainVm.ErpDb.LoadAllTandC();

        InitDocTypes();
        StatusCombo.SelectedIndex = 0;
        RefreshCustomerDropdown();
        RefreshItemDropdown();
        RefreshTandCDropdown();

        // Apply preset type or existing doc type
        if (presetType.HasValue && existing == null)
            SelectDocType(presetType.Value);

        // Bind grid
        ItemsGrid.ItemsSource = _docVm.Items;
        _docVm.Items.CollectionChanged += (_, _) => RefreshTotalsDisplay();

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => Save_Click(s, e)));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Close()));

        if (existing != null)
            PopulateFromViewModel();
        else
            PopulateFromViewModel(); // also sets defaults via VM

        WindowTitleText.Text = _docVm.WindowTitle;
        SaveBtn.Content      = _docVm.SaveBtnLabel;
    }

    // ── Doc types ─────────────────────────────────────────────────────────────

    private void InitDocTypes()
    {
        DocTypeCombo.ItemsSource = new[]
        {
            new { V = DocumentType.Quotation,       L = "Quotation"                },
            new { V = DocumentType.SalesOrder,      L = "Sales Order"              },
            new { V = DocumentType.SalesInvoice,    L = "Sales Invoice"            },
            new { V = DocumentType.CreditNote,      L = "Credit Note"              },
            new { V = DocumentType.PurchaseOrder,   L = "Purchase Order"           },
            new { V = DocumentType.PurchaseInvoice, L = "Purchase Invoice"         },
            new { V = DocumentType.GRN,             L = "GRN (Goods Receipt Note)" },
            new { V = DocumentType.DebitNote,       L = "Debit Note"               },
        };
        DocTypeCombo.DisplayMemberPath = "L";
        DocTypeCombo.SelectedIndex     = 2; // default: Sales Invoice
    }

    private void SelectDocType(DocumentType dt)
    {
        var arr = DocTypeCombo.ItemsSource as Array;
        for (int i = 0; arr != null && i < arr.Length; i++)
        {
            dynamic item = arr.GetValue(i)!;
            if ((DocumentType)item.V == dt) { DocTypeCombo.SelectedIndex = i; break; }
        }
    }

    private DocumentType SelectedDocType()
    {
        if (DocTypeCombo.SelectedItem == null) return DocumentType.SalesInvoice;
        return (DocumentType)((dynamic)DocTypeCombo.SelectedItem).V;
    }

    // ── Populate UI from ViewModel ────────────────────────────────────────────

    private void PopulateFromViewModel()
    {
        DocNoBox.Text         = _docVm.DocNo;
        DocDatePicker.SelectedDate = _docVm.DocDate;
        CreatedByBox.Text     = _docVm.CreatedBy;
        CustomerRefNoBox.Text = _docVm.CustomerRefNo;
        EWayBillNoBox.Text    = _docVm.EWayBillNo;
        PlaceOfSupplyBox.Text = _docVm.PlaceOfSupply;
        PendingAmountBox.Text = _docVm.PendingAmount.ToString("F2");

        StatusCombo.SelectedIndex = _docVm.Status switch
        {
            DocumentStatus.Open     => 0, DocumentStatus.Pending  => 1,
            DocumentStatus.Complete => 2, DocumentStatus.Canceled => 3,
            _ => 0
        };

        CompanyNameBox.Text      = _docVm.CoName;
        CompanyPhoneBox.Text     = _docVm.CoPhone;
        CompanyGSTBox.Text       = _docVm.CoGSTIN;
        CompanyStateCodeBox.Text = _docVm.CoStateCode;
        CompanyEmailBox.Text     = _docVm.CoEmail;
        CompanyAddressBox.Text   = _docVm.CoAddress;
        CompanyBankBox.Text      = _docVm.CoBank;
        CompanyAccountBox.Text   = _docVm.CoAccount;
        CompanyIFSCBox.Text      = _docVm.CoIFSC;
        CompanySignatureBox.Text = _docVm.CoSignatory;
        CompanyQRCodeBox.Text    = _docVm.CoQRCode;

        CustNameBox.Text      = _docVm.CustName;
        CustGSTBox.Text       = _docVm.CustGSTIN;
        CustStateCodeBox.Text = _docVm.CustStateCode;
        CustBillingBox.Text   = _docVm.CustBilling;
        CustShippingBox.Text  = _docVm.CustShipping;

        FreightBox.Text      = _docVm.Freight.ToString("F2");
        DiscountBox.Text     = _docVm.Discount.ToString("F2");
        GstPercentBox.Text   = _docVm.GstPercent.ToString();
        PaymentTermsBox.Text = _docVm.PaymentTerms;
        GeneralTermsBox.Text = _docVm.GeneralTerms;

        if (_docVm.GstMode == GstMode.Igst) RbIgst.IsChecked = true;
        else RbCgst.IsChecked = true;

        SelectDocType(_docVm.DocType);
        UpdateEWayVisibility(_docVm.DocType);
        RefreshTotalsDisplay();
    }

    private void PushFormToViewModel()
    {
        _docVm.DocNo          = DocNoBox.Text.Trim();
        _docVm.DocDate        = DocDatePicker.SelectedDate ?? DateTime.Now;
        _docVm.CreatedBy      = CreatedByBox.Text.Trim();
        _docVm.CustomerRefNo  = CustomerRefNoBox.Text.Trim();
        _docVm.EWayBillNo     = EWayBillNoBox.Text.Trim();
        _docVm.PlaceOfSupply  = PlaceOfSupplyBox.Text.Trim();
        if (_docVm.IsEdit && decimal.TryParse(PendingAmountBox.Text, out var pend))
        {
            _docVm.PendingAmount = pend;
        }

        _docVm.Status = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Pending"  => DocumentStatus.Pending,
            "Complete" => DocumentStatus.Complete,
            "Canceled" => DocumentStatus.Canceled,
            _          => DocumentStatus.Open
        };

        _docVm.CoName      = CompanyNameBox.Text;
        _docVm.CoPhone     = CompanyPhoneBox.Text;
        _docVm.CoGSTIN     = CompanyGSTBox.Text;
        _docVm.CoStateCode = CompanyStateCodeBox.Text;
        _docVm.CoEmail     = CompanyEmailBox.Text;
        _docVm.CoAddress   = CompanyAddressBox.Text;
        _docVm.CoBank      = CompanyBankBox.Text;
        _docVm.CoAccount   = CompanyAccountBox.Text;
        _docVm.CoIFSC      = CompanyIFSCBox.Text;
        _docVm.CoSignatory = CompanySignatureBox.Text;
        _docVm.CoQRCode    = CompanyQRCodeBox.Text;

        _docVm.CustName      = CustNameBox.Text;
        _docVm.CustGSTIN     = CustGSTBox.Text;
        _docVm.CustStateCode = CustStateCodeBox.Text;
        _docVm.CustBilling   = CustBillingBox.Text;
        _docVm.CustShipping  = CustShippingBox.Text;

        decimal.TryParse(FreightBox.Text,    out var frgt);  _docVm.Freight    = frgt;
        decimal.TryParse(DiscountBox.Text,   out var disc);  _docVm.Discount   = disc;
        decimal.TryParse(GstPercentBox.Text, out var gstPct); _docVm.GstPercent = gstPct;

        _docVm.PaymentTerms = PaymentTermsBox.Text;
        _docVm.GeneralTerms = GeneralTermsBox.Text;
        _docVm.DocType      = SelectedDocType();
        _docVm.GstMode      = RbIgst.IsChecked == true ? GstMode.Igst : GstMode.CgstSgst;
    }

    // ── Dropdowns ─────────────────────────────────────────────────────────────

    private void RefreshCustomerDropdown()
    {
        var list = new[] { new Customer { Name = "— Select customer —" } }
                   .Concat(_customers).ToList();
        CustomerLoadCombo.ItemsSource       = list;
        CustomerLoadCombo.DisplayMemberPath = "Name";
        CustomerLoadCombo.SelectedIndex     = 0;
    }

    private void RefreshItemDropdown()
    {
        var list = new[] { new ItemMaster { Name = "— Select item to add —" } }
                   .Concat(_items).ToList();
        ItemLoadCombo.ItemsSource       = list;
        ItemLoadCombo.DisplayMemberPath = "Name";
        ItemLoadCombo.SelectedIndex     = 0;
    }

    private void RefreshTandCDropdown()
    {
        if (TandCLoadCombo == null) return;
        string currentType = SelectedDocType().ToString();
        var filtered = _tandcList
            .Where(t => t.DocType == "All" || t.DocType == currentType).ToList();
        var list = new[] { new TandCMaster { Label = "— Load T&C template —" } }
                   .Concat(filtered).ToList();
        TandCLoadCombo.ItemsSource       = list;
        TandCLoadCombo.DisplayMemberPath = "Label";
        TandCLoadCombo.SelectedIndex     = 0;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void DocTypeCombo_Changed(object s, SelectionChangedEventArgs e)
    {
        if (DocTypeCombo.SelectedItem == null) return;
        var dt = SelectedDocType();
        UpdateEWayVisibility(dt);
        if (!_docVm.IsEdit) DocNoBox.Text = _mainVm.DocNumbers.Generate(dt);
        RefreshTandCDropdown();
    }

    private void UpdateEWayVisibility(DocumentType dt)
    {
        bool show = dt is DocumentType.SalesInvoice or DocumentType.PurchaseInvoice;
        EWayBillPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CustomerCombo_Changed(object s, SelectionChangedEventArgs e)
    {
        if (CustomerLoadCombo.SelectedItem is not Customer c || c.Id == 0) return;
        CustNameBox.Text      = c.Name;      CustGSTBox.Text       = c.GSTIN;
        CustStateCodeBox.Text = c.StateCode; CustBillingBox.Text   = c.BillingAddress;
        CustShippingBox.Text  = c.ShippingAddress;
        CustomerLoadCombo.SelectedIndex = 0;
    }

    private void ItemCombo_Changed(object s, SelectionChangedEventArgs e)
    {
        if (ItemLoadCombo.SelectedItem is not ItemMaster m || m.Id == 0) return;
        _docVm.AddRow(m.Name, m.HSN, m.UOM, 1, m.DefaultRate, m.GSTPercent);
        RefreshTotalsDisplay();
        ItemLoadCombo.SelectedIndex = 0;
        ScrollItemsToBottom();
    }

    private void AddRow_Click(object s, RoutedEventArgs e)
    {
        _docVm.AddEmptyRow();
        RefreshTotalsDisplay();
        ScrollItemsToBottom();
    }

    private void DeleteRow_Click(object s, RoutedEventArgs e)
    {
        if (s is Button { Tag: EditableItem row })
        {
            _docVm.RemoveRow(row);
            RefreshTotalsDisplay();
        }
    }

    private void ItemsGrid_CellEditEnding(object s, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit)
            Dispatcher.BeginInvoke(new Action(RefreshTotalsDisplay),
                System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ItemsGrid_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Insert) { _docVm.AddEmptyRow(); RefreshTotalsDisplay(); }
        if ((e.Key == Key.Delete || e.Key == Key.Back) &&
            e.KeyboardDevice.Modifiers == ModifierKeys.Shift &&
            ItemsGrid.SelectedItem is EditableItem row)
        {
            _docVm.RemoveRow(row);
            RefreshTotalsDisplay();
        }
    }

    private void GstMode_Changed(object s, RoutedEventArgs e)
    {
        if (_docVm == null) return;
        _docVm.GstMode = (RbIgst?.IsChecked == true) ? GstMode.Igst : GstMode.CgstSgst;
        UpdateGstVisibility(_docVm.GstMode);
        RefreshTotalsDisplay();
    }

    private void Total_Changed(object s, TextChangedEventArgs e) => RefreshTotalsDisplay();

    private void TandCCombo_Changed(object s, SelectionChangedEventArgs e)
    {
        if (TandCLoadCombo?.SelectedItem is not TandCMaster t || t.Id == 0) return;
        if (!string.IsNullOrEmpty(t.PaymentTerms)) PaymentTermsBox.Text = t.PaymentTerms;
        if (!string.IsNullOrEmpty(t.GeneralTerms)) GeneralTermsBox.Text = t.GeneralTerms;
        TandCLoadCombo.SelectedIndex = 0;
    }

    // ── Totals display ────────────────────────────────────────────────────────

    private void RefreshTotalsDisplay()
    {
        if (SubtotalText == null || _docVm == null) return; 

        decimal.TryParse(DiscountBox?.Text,    out var disc);
        decimal.TryParse(FreightBox?.Text,     out var frgt);
        decimal.TryParse(GstPercentBox?.Text,  out var gstPct);
        bool isCgst = RbIgst?.IsChecked != true;

        // Push current text values into VM so it can recalculate
        _docVm.Discount   = disc;
        _docVm.Freight    = frgt;
        _docVm.GstPercent = gstPct == 0 ? 18 : gstPct;
        _docVm.GstMode    = isCgst ? GstMode.CgstSgst : GstMode.Igst;
        _docVm.RefreshTotals();

        SubtotalText.Text   = $"Rs {_docVm.Subtotal:N2}";
        DiscountText.Text   = $"-Rs {_docVm.Discount:N2}";
        FreightText.Text    = $"Rs {_docVm.Freight:N2}";
        GrandTotalText.Text = $"Rs {_docVm.GrandTotal:N2}";

        if (isCgst)
        {
            CgstLabel.Text = _docVm.CgstLabel;
            SgstLabel.Text = _docVm.SgstLabel;
            CgstText.Text  = $"Rs {_docVm.CGST:N2}";
            SgstText.Text  = $"Rs {_docVm.SGST:N2}";
        }
        else
        {
            IgstLabel.Text = _docVm.IgstLabel;
            IgstText.Text  = $"Rs {_docVm.IGST:N2}";
        }

        UpdateGstVisibility(isCgst ? GstMode.CgstSgst : GstMode.Igst);
    }

    private void UpdateGstVisibility(GstMode mode)
    {
        bool isCgst = mode == GstMode.CgstSgst;
        if (CgstRow != null) CgstRow.Visibility = isCgst ? Visibility.Visible : Visibility.Collapsed;
        if (SgstRow != null) SgstRow.Visibility = isCgst ? Visibility.Visible : Visibility.Collapsed;
        if (IgstRow != null) IgstRow.Visibility = isCgst ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Save / Preview ────────────────────────────────────────────────────────

    private void Save_Click(object s, RoutedEventArgs e)
    {
        try
        {
            ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            PushFormToViewModel();
            _docVm.Save();
            FormStatusText.Text  = _docVm.FormStatus;
            WindowTitleText.Text = _docVm.WindowTitle;
            SaveBtn.Content      = _docVm.SaveBtnLabel;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving:\n{ex.Message}", "Save Error");
        }
    }

    private void OpenPdf_Click(object s, RoutedEventArgs e)
    {
        try
        {
            ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            PushFormToViewModel();
            OpenFile(_docVm.ExportPdf());
        }
        catch (Exception ex) { MessageBox.Show($"PDF error:\n{ex.Message}", "PDF"); }
    }

    private void OpenHtml_Click(object s, RoutedEventArgs e)
    {
        try
        {
            ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            PushFormToViewModel();
            OpenFile(_docVm.ExportHtml());
        }
        catch (Exception ex) { MessageBox.Show($"HTML error:\n{ex.Message}", "HTML"); }
    }

    private void Cancel_Click(object s, RoutedEventArgs e) => Close();

    // ── CSV loaders ───────────────────────────────────────────────────────────

    private void LoadCustomersCsv_Click(object s, RoutedEventArgs e)
    {
        string? path = PickCsv("customers"); if (path == null) return;
        _customers = _csv.ImportCustomers(path);
        RefreshCustomerDropdown();
        FormStatusText.Text = $"Loaded {_customers.Count} customers";
    }

    private void LoadItemsCsv_Click(object s, RoutedEventArgs e)
    {
        string? path = PickCsv("items"); if (path == null) return;
        _items = _csv.ImportItems(path);
        RefreshItemDropdown();
        FormStatusText.Text = $"Loaded {_items.Count} items";
    }

    private void BrowseQRCode_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select QR Code Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
        };
        if (dlg.ShowDialog() == true) CompanyQRCodeBox.Text = dlg.FileName;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? PickCsv(string hint)
    {
        var dlg = new OpenFileDialog
        {
            Title  = $"Select {hint} CSV",
            Filter = "CSV files (*.csv)|*.csv|All files|*.*"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private static void OpenFile(string path)
    {
        if (!System.IO.File.Exists(path)) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ScrollItemsToBottom()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ItemsGrid.Items.Count > 0)
            {
                ItemsGrid.ScrollIntoView(ItemsGrid.Items[^1]);
                ItemsGrid.SelectedIndex = ItemsGrid.Items.Count - 1;
            }
        }));
    }
}
