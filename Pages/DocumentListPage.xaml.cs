using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ojaswat.Models;
using Ojaswat.Services;
using Ojaswat.ViewModels;
using Windows = Ojaswat.Windows;

namespace Ojaswat.Pages;

public partial class DocumentListPage : UserControl
{
    private static MainViewModel VM => App.VM;
    private DocListViewModel? _listVm;

    public DocumentListPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _listVm = new DocListViewModel(VM);

        // Apply navigation filter set by NavCommand
        if (VM.PendingModuleFilter  != null && VM.PendingModuleFilter.Length > 0)
            _listVm.ModuleFilter = VM.PendingModuleFilter;
        if (VM.PendingDocTypeFilter != null)
            _listVm.DocTypeFilter = VM.PendingDocTypeFilter.Value.ToString();
        if (VM.PendingPendingOnly)
            _listVm.PendingOnly = true;

        _listVm.PropertyChanged += (_, ev) =>
        {
            if (ev.PropertyName == nameof(DocListViewModel.Filtered))
                UpdateGrid();
        };
        UpdateGrid();
    }

    private void UpdateGrid()
    {
        if (_listVm == null) return;
        DocListGrid.ItemsSource = _listVm.Filtered;
        CountText.Text          = $"— {_listVm.FilteredCount} records";
    }

    private void Search_Changed(object s, TextChangedEventArgs e)
    {
        if (_listVm == null) return;
        ClearSearchBtn.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        _listVm.SearchText = SearchBox.Text;
    }

    private void ClearSearch_Click(object s, RoutedEventArgs e)
    { SearchBox.Clear(); if (_listVm != null) _listVm.SearchText = ""; }

    private void Filter_Changed(object s, SelectionChangedEventArgs e)
    {
        if (_listVm == null) return;
        _listVm.DocTypeFilter = (DocTypeFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Types";
        _listVm.StatusFilter  = (StatusFilterCombo.SelectedItem  as ComboBoxItem)?.Content?.ToString() ?? "All Status";
    }

    private void Refresh_Click(object s, RoutedEventArgs e)
    { VM.LoadDocuments(); _listVm?.Apply(); }

    private void New_Click(object s, RoutedEventArgs e)
    {
        var win = new Windows.CreateDocumentWindow(vm: VM) { Owner = Window.GetWindow(this) };
        win.Closed += (_, _) => { if (win.DocumentSaved) { VM.LoadDocuments(); _listVm?.Apply(); } };
        win.Show();
    }

    private void DocGrid_DoubleClick(object s, MouseButtonEventArgs e)
    { if (DocListGrid.SelectedItem is DocumentListItem d) OpenEditWindow(d.Id); }

    private void DocGrid_KeyDown(object s, KeyEventArgs e)
    {
        if ((e.Key == Key.Enter || e.Key == Key.F2) && DocListGrid.SelectedItem is DocumentListItem ed)
            OpenEditWindow(ed.Id);
        if ((e.Key == Key.Delete || e.Key == Key.Back) &&
            e.KeyboardDevice.Modifiers == ModifierKeys.Shift &&
            DocListGrid.SelectedItem is DocumentListItem del)
            DeleteDocument(del);
    }

    private void DocEdit_Click(object s, RoutedEventArgs e)
    { if (s is Button { Tag: DocumentListItem d }) OpenEditWindow(d.Id); }

    private void DocDelete_Click(object s, RoutedEventArgs e)
    { if (s is Button { Tag: DocumentListItem d }) DeleteDocument(d); }

    private void DocPdf_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: DocumentListItem item }) return;
        try
        {
            var doc = LoadDoc(item.Id); if (doc == null) return;
            var tpl = VM.ErpDb.LoadTemplate(doc.DocType.ToString());
            OpenFile(VM.Export.ExportPdf(doc, customHtml: tpl?.PdfTemplate));
        }
        catch (Exception ex) { MessageBox.Show($"PDF error:\n{ex.Message}"); }
    }

    private void DocHtml_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: DocumentListItem item }) return;
        try
        {
            var doc = LoadDoc(item.Id); if (doc == null) return;
            var tpl = VM.ErpDb.LoadTemplate(doc.DocType.ToString());
            OpenFile(VM.Export.ExportHtml(doc, customHtml: tpl?.HtmlTemplate));
        }
        catch (Exception ex) { MessageBox.Show($"HTML error:\n{ex.Message}"); }
    }

    private void DocAddPayment_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: DocumentListItem item }) return;
        var win = new Windows.PaymentEntryWindow(VM.AllDocs.ToList()) { Owner = Window.GetWindow(this) };
        win.PreSelectDoc(item.DocumentNo);
        win.ShowDialog();
        if (win.Saved) { VM.LoadDocuments(); _listVm?.Apply(); }
    }

    private void OpenEditWindow(int id)
    {
        var doc = LoadDoc(id); if (doc == null) return;
        var win = new Windows.CreateDocumentWindow(existing: doc, vm: VM) { Owner = Window.GetWindow(this) };
        win.Closed += (_, _) => { if (win.DocumentSaved) { VM.LoadDocuments(); _listVm?.Apply(); } };
        win.Show();
    }

    private void DeleteDocument(DocumentListItem item)
    {
        if (MessageBox.Show($"Delete {item.DocumentNo}?\nThis cannot be undone.",
            "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { VM.ErpDb.DeleteDocument(item.Id); VM.LoadDocuments(); _listVm?.Apply(); }
        catch (Exception ex) { MessageBox.Show($"Delete failed:\n{ex.Message}"); }
    }

    private static ErpDocument? LoadDoc(int id)
    {
        var doc = VM.ErpDb.LoadDocument(id);
        if (doc != null && !string.IsNullOrEmpty(VM.LogoPath))
            doc.Company.LogoPath = VM.LogoPath;
        return doc;
    }

    private static void OpenFile(string path)
    {
        if (!System.IO.File.Exists(path)) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }
}
