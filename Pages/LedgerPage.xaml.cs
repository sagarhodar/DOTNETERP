using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Ojaswat.Models;
using Ojaswat.ViewModels;

namespace Ojaswat.Pages;

public partial class LedgerPage : UserControl
{
    private static MainViewModel VM => App.VM;
    private string? _currentPartyFilter = null;
    private string? _currentTypeFilter  = null;

    public LedgerPage()
    {
        InitializeComponent();
        
        // Wire up event handlers programmatically
        PartyTypeCombo.SelectionChanged += PartyTypeFilter_Changed;
        PartyFilterCombo.SelectionChanged += PartyFilter_Changed;
        
        // Initialize on page load
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Temporarily unsubscribe from ComboBox events to prevent circular firing
            PartyTypeCombo.SelectionChanged -= PartyTypeFilter_Changed;
            PartyFilterCombo.SelectionChanged -= PartyFilter_Changed;
            
            // Initialize data
            LoadPartyCombo();
            _currentTypeFilter = null;
            _currentPartyFilter = null;
            
            // Now subscribe back and refresh
            PartyTypeCombo.SelectionChanged += PartyTypeFilter_Changed;
            PartyFilterCombo.SelectionChanged += PartyFilter_Changed;
            
            RefreshSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading ledger:\n{ex.Message}\n\n{ex.InnerException?.Message}",
                           "Ledger Load Error");
        }
    }

    private void LoadPartyCombo()
    {
        try
        {
            var parties = VM.ErpDb.Inventory.GetPartySummary()
                .Select(p => p.PartyName).OrderBy(n => n).ToList();
            parties.Insert(0, "All Parties");
            PartyFilterCombo.ItemsSource   = parties;
            PartyFilterCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading party combo:\n{ex.Message}", "Load Party Combo Error");
        }
    }

    private void RefreshSummary()
    {
        try
        {
            var data = VM.ErpDb.Inventory.GetPartySummary();
            if (!string.IsNullOrEmpty(_currentTypeFilter))
                data = data.Where(p => p.PartyType == _currentTypeFilter).ToList();
            if (!string.IsNullOrEmpty(_currentPartyFilter))
                data = data.Where(p => p.PartyName == _currentPartyFilter).ToList();
            SummaryGrid.ItemsSource = data;
            decimal totalDr = data.Sum(p => p.TotalDebit);
            decimal totalCr = data.Sum(p => p.TotalCredit);
            StatusText.Text = $"{data.Count} parties  |  Total Dr: ₹{totalDr:N2}  |  Total Cr: ₹{totalCr:N2}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing summary:\n{ex.Message}", "Refresh Summary Error");
        }
    }

    private void RefreshDetail()
    {
        try
        {
            var data = VM.ErpDb.Inventory.GetPartyLedger(_currentPartyFilter);
            if (!string.IsNullOrEmpty(_currentTypeFilter))
                data = data.Where(p => p.PartyType == _currentTypeFilter).ToList();
            DetailGrid.ItemsSource = data;
            StatusText.Text = $"{data.Count} entries" +
                (_currentPartyFilter != null ? $"  for  {_currentPartyFilter}" : "");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing detail:\n{ex.Message}", "Refresh Detail Error");
        }
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void Tab_Changed(object s, RoutedEventArgs e)
    {
        try
        {
            if (SummaryPanel == null || TabSummary == null) return;
            
            bool isSummary = TabSummary.IsChecked == true;
            SummaryPanel.Visibility = isSummary ? Visibility.Visible : Visibility.Collapsed;
            DetailPanel.Visibility  = isSummary ? Visibility.Collapsed : Visibility.Visible;
            
            if (isSummary) RefreshSummary(); else RefreshDetail();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error in tab change:\n{ex.Message}", "Tab Change Error");
        }
    }

    private void Refresh_Click(object s, RoutedEventArgs e)
    {
        try
        {
            LoadPartyCombo();
            if (TabSummary.IsChecked == true) RefreshSummary(); else RefreshDetail();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing:\n{ex.Message}", "Refresh Error");
        }
    }

    private void PartyTypeFilter_Changed(object s, SelectionChangedEventArgs e)
    {
        try
        {
            var item = PartyTypeCombo.SelectedItem;
            if (item == null) return;
            
            string sel = (item as ComboBoxItem)?.Content?.ToString() ?? (item as string) ?? "";
            _currentTypeFilter = (sel == "All Parties" || sel == "") ? null : sel;
            
            if (TabSummary.IsChecked == true) RefreshSummary(); else RefreshDetail();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error changing party type filter:\n{ex.Message}", "Filter Error");
        }
    }

    private void PartyFilter_Changed(object s, SelectionChangedEventArgs e)
    {
        try
        {
            var item = PartyFilterCombo.SelectedItem;
            if (item == null) return;
            
            string sel = (item as string) ?? "";
            _currentPartyFilter = (sel == "All Parties" || sel == "") ? null : sel;
            
            if (TabSummary.IsChecked == true) RefreshSummary(); else RefreshDetail();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error changing party filter:\n{ex.Message}", "Filter Error");
        }
    }

    private void ClearPartyFilter_Click(object s, RoutedEventArgs e)
    {
        try
        {
            // Unsubscribe to prevent event fire during reset
            PartyFilterCombo.SelectionChanged -= PartyFilter_Changed;
            PartyFilterCombo.SelectedIndex = 0;
            PartyFilterCombo.SelectionChanged += PartyFilter_Changed;
            
            _currentPartyFilter = null;
            if (TabSummary.IsChecked == true) RefreshSummary(); else RefreshDetail();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error clearing filter:\n{ex.Message}", "Clear Filter Error");
        }
    }

    // Double-click summary row → drill into that party's ledger
    private void SummaryGrid_DoubleClick(object s, MouseButtonEventArgs e)
    {
        try
        {
            if (SummaryGrid.SelectedItem is PartySummary p)
            {
                _currentPartyFilter = p.PartyName;
                // Select that party in combo without triggering event
                PartyFilterCombo.SelectionChanged -= PartyFilter_Changed;
                var idx = (PartyFilterCombo.ItemsSource as System.Collections.Generic.List<string>)?.IndexOf(p.PartyName) ?? -1;
                if (idx >= 0) PartyFilterCombo.SelectedIndex = idx;
                PartyFilterCombo.SelectionChanged += PartyFilter_Changed;
                
                TabDetail.IsChecked = true; // triggers Tab_Changed
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error selecting party:\n{ex.Message}", "Selection Error");
        }
    }

    private void ExportCsv_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title    = "Export Ledger CSV",
                Filter   = "CSV|*.csv",
                FileName = $"party_ledger_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var data = VM.ErpDb.Inventory.GetPartyLedger(_currentPartyFilter);
            if (!string.IsNullOrEmpty(_currentTypeFilter))
                data = data.Where(p => p.PartyType == _currentTypeFilter).ToList();

            using var w = new System.IO.StreamWriter(dlg.FileName);
            w.WriteLine("Date,Party,Type,Entry,Debit,Credit,Balance,Document,DocType,Narration");
            foreach (var r in data)
                w.WriteLine($"{r.DateLabel},{r.PartyName},{r.PartyType},{r.EntryType},{r.Debit:F2},{r.Credit:F2},{r.Balance:F2},{r.DocumentNo},{r.DocType},\"{r.Narration}\"");
            MessageBox.Show($"Exported {data.Count} entries.", "Done");
        }
        catch (Exception ex) { MessageBox.Show($"Export error:\n{ex.Message}"); }
    }

    //-----DELETE LEDGER ENTRY -----
    private void DeleteLedger_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var confirm = MessageBox.Show("Delete this ledger entry?",
                    "Confirm", MessageBoxButton.YesNo);

                if (confirm != MessageBoxResult.Yes) return;

                VM.ErpDb.DeleteLedgerEntry(id);

                RefreshDetail(); // refresh grid
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete error:\n{ex.Message}");
        }
    }
}
