using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ojaswat.Services;
using Ojaswat.ViewModels;

namespace Ojaswat.Pages;

public partial class GstReportPage : UserControl
{
    private static MainViewModel VM => App.VM;

    public GstReportPage() { InitializeComponent(); Loaded += (_, _) => Load(); }

    private void Load()
    {
        try
        {
            var rows = VM.ErpDb.GetGstReport();
            GstGrid.ItemsSource = rows;
            StatusText.Text = rows.Count > 0
                ? $"{rows.Count} rows  |  Total GST: ₹{rows.Sum(r => r.TotalTax):N2}"
                : "No data — save some documents first";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; MessageBox.Show($"GST Report error:\n{ex.Message}"); }
    }

    private void Refresh_Click(object s, RoutedEventArgs e) => Load();

    private void ExportCsv_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var rows = VM.ErpDb.GetGstReport();
            var dlg  = new SaveFileDialog { Title = "Export GST CSV", Filter = "CSV|*.csv", FileName = $"gst_report_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            new CsvService().ExportGstReport(rows, dlg.FileName);
            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Done");
        }
        catch (Exception ex) { MessageBox.Show($"Export error:\n{ex.Message}"); }
    }
}
