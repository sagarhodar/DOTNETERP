using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ojaswat.Models;
using Ojaswat.Services;
using Ojaswat.ViewModels;
using Windows = Ojaswat.Windows;

namespace Ojaswat.Pages;

public partial class FinancePage : UserControl
{
    private static MainViewModel VM => App.VM;
    private FinanceViewModel? _finVm;

    public FinancePage()
    {
        InitializeComponent();
        Loaded += (_, _) => { _finVm = new FinanceViewModel(VM.ErpDb); Refresh(); };
    }

    private void Refresh()
    {
        if (_finVm == null) return;
        _finVm.Load();
        PaymentGrid.ItemsSource = _finVm.Payments;
        StatusText.Text         = _finVm.StatusText;
    }

    private void NewPayment_Click(object s, RoutedEventArgs e)
    {
        var win = new Windows.PaymentEntryWindow(VM.AllDocs.ToList()) { Owner = Window.GetWindow(this) };
        win.ShowDialog();
        if (win.Saved) { VM.LoadDocuments(); Refresh(); }
    }

    private void Refresh_Click(object s, RoutedEventArgs e) => Refresh();

    private void PaymentDelete_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: PaymentListItem p } || _finVm == null) return;
        if (MessageBox.Show($"Delete payment of ₹{p.Amount:N2}?", "Delete", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { _finVm.DeletePayment(p.Id); VM.LoadDocuments(); Refresh(); }
        catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}"); }
    }

    private void ExportPayments_Click(object s, RoutedEventArgs e)
    {
        if (_finVm == null) return;
        try
        {
            var dlg = new SaveFileDialog { Title = "Export Payments CSV", Filter = "CSV|*.csv", FileName = $"payments_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            new CsvService().ExportPayments(_finVm.Payments, dlg.FileName);
            MessageBox.Show($"Exported {_finVm.Payments.Count} payments.", "Done");
        }
        catch (Exception ex) { MessageBox.Show($"Export error:\n{ex.Message}"); }
    }
}
