using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ojaswat.Services;
using Ojaswat.ViewModels;

namespace Ojaswat.Pages;

public partial class ReportItemsPage : UserControl
{
    private static MainViewModel VM => App.VM;

    public ReportItemsPage() { InitializeComponent(); Loaded += (_, _) => Load(); }

    private void Load()
    {
        try
        {
            var rows = new List<object>();
            foreach (var d in VM.AllDocs)
            {
                var doc = VM.ErpDb.LoadDocument(d.Id);
                if (doc == null) continue;
                foreach (var it in doc.Items)
                    rows.Add(new { d.DocumentNo, d.DocTypeLabel, d.CustomerName, d.DateLabel, ItemDesc = it.Description, it.HSN, Qty = it.Quantity, it.Rate, Amount = it.LineTotal });
            }
            ItemReportGrid.ItemsSource = rows;
            StatusText.Text            = $"{rows.Count} item rows";
        }
        catch (Exception ex) { MessageBox.Show($"Report error:\n{ex.Message}"); }
    }

    private void Refresh_Click(object s, RoutedEventArgs e) => Load();

    private void ExportCsv_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog { Title = "Export Item Report CSV", Filter = "CSV|*.csv", FileName = $"item_report_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            new CsvService().ExportItemReport(VM.AllDocs, VM.ErpDb, dlg.FileName);
            MessageBox.Show("Exported.", "Done");
        }
        catch (Exception ex) { MessageBox.Show($"Export error:\n{ex.Message}"); }
    }
}
