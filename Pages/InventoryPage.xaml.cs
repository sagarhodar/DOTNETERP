using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ojaswat.Models;
using Ojaswat.Services;
using Ojaswat.ViewModels;

namespace Ojaswat.Pages;

public partial class InventoryPage : UserControl
{
    private static MainViewModel VM => App.VM;
    private string? _currentItemFilter = null;

    public InventoryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadItemFilterCombo();
        RefreshSummary();
    }

    private void LoadItemFilterCombo()
    {
        var items = VM.ErpDb.LoadItems().Select(i => i.Name).ToList();
        items.Insert(0, "All Items");
        ItemFilterCombo.ItemsSource   = items;
        ItemFilterCombo.SelectedIndex = 0;
    }

    private void RefreshSummary()
    {
        var data = VM.ErpDb.Inventory.GetStockSummary();
        SummaryGrid.ItemsSource = data;
        int low  = data.Count(s => s.StockStatus == "Low Stock");
        int zero = data.Count(s => s.StockStatus == "Out of Stock");
        StatusText.Text = $"{data.Count} items  |  {low} low stock  |  {zero} out of stock";
    }

    private void RefreshLedger()
    {
        var data = VM.ErpDb.Inventory.GetStockLedger(_currentItemFilter);
        LedgerGrid.ItemsSource = data;
        StatusText.Text = $"{data.Count} entries" +
            (_currentItemFilter != null ? $"  for  {_currentItemFilter}" : "");
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void Tab_Changed(object s, RoutedEventArgs e)
    {
        if (SummaryPanel == null) return;
        bool isSummary = TabSummary.IsChecked == true;
        SummaryPanel.Visibility = isSummary ? Visibility.Visible : Visibility.Collapsed;
        LedgerPanel.Visibility  = isSummary ? Visibility.Collapsed : Visibility.Visible;
        if (isSummary) RefreshSummary(); else RefreshLedger();
    }

    private void Refresh_Click(object s, RoutedEventArgs e)
    {
        if (TabSummary.IsChecked == true) RefreshSummary(); else RefreshLedger();
    }

    private void ItemFilter_Changed(object s, SelectionChangedEventArgs e)
    {
        var sel = ItemFilterCombo.SelectedItem as string;
        _currentItemFilter = (sel == "All Items" || sel == null) ? null : sel;
        if (TabLedger.IsChecked == true) RefreshLedger();
    }

    private void ClearFilter_Click(object s, RoutedEventArgs e)
    {
        ItemFilterCombo.SelectedIndex = 0;
        _currentItemFilter = null;
        if (TabLedger.IsChecked == true) RefreshLedger();
    }

    private void Adjust_Click(object s, RoutedEventArgs e)
    {
        var items = VM.ErpDb.LoadItems();
        if (!items.Any()) { MessageBox.Show("Add items in Master Data first.", "No Items"); return; }

        var dlg = new Windows.StockAdjustmentWindow(items) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        try
        {
            VM.ErpDb.Inventory.PostManualAdjustment(
                dlg.SelectedItemName, dlg.SelectedHSN, dlg.SelectedUOM,
                dlg.Quantity, dlg.MovementType, dlg.Narration);
            RefreshSummary();
            VM.LoadDocuments(); // refresh stats
            MessageBox.Show($"Stock adjusted: {dlg.MovementType} {dlg.Quantity:N2} {dlg.SelectedUOM} for {dlg.SelectedItemName}", "Done");
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
    }

    private void ExportCsv_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title    = "Export Stock Ledger CSV",
                Filter   = "CSV|*.csv",
                FileName = $"stock_ledger_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;
            var data = VM.ErpDb.Inventory.GetStockLedger(_currentItemFilter);
            using var w = new System.IO.StreamWriter(dlg.FileName);
            w.WriteLine("Date,Item,HSN,UOM,Type,Qty,Rate,Value,Document,Party,Narration");
            foreach (var r in data)
                w.WriteLine($"{r.DateLabel},{r.ItemName},{r.HSN},{r.UOM},{r.MovementType},{r.Quantity:F2},{r.Rate:F2},{r.Value:F2},{r.DocumentNo},{r.PartyName},\"{r.Narration}\"");
            MessageBox.Show($"Exported {data.Count} entries.", "Done");
        }
        catch (Exception ex) { MessageBox.Show($"Export error:\n{ex.Message}"); }
    }

    //---Inventory ledger entry deletion----
    private void DeleteStock_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var confirm = MessageBox.Show("Delete this stock entry?",
                    "Confirm", MessageBoxButton.YesNo);

                if (confirm != MessageBoxResult.Yes) return;

                VM.ErpDb.DeleteStockLedgerEntry(id);

                RefreshLedger();
                RefreshSummary();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete error:\n{ex.Message}");
        }
    }
}
