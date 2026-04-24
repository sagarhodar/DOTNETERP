using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ojaswat.Models;
using Ojaswat.ViewModels;
using Windows = Ojaswat.Windows;

namespace Ojaswat.Pages;

public partial class DashboardPage : UserControl
{
    // Read VM from static locator — 100% reliable regardless of DataContext
    private static MainViewModel VM => App.VM;

    public DashboardPage()
    {
        InitializeComponent();
        DashDateText.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy");

        // Bind stat TextBlocks manually since DataContext = "dash" string, not VM
        // We subscribe to PropertyChanged on the VM directly
        VM.PropertyChanged += OnVmPropertyChanged;

        // Populate immediately with current data
        RefreshStats();
        RefreshRecentGrid();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Use Dispatcher in case this fires from background thread
        Dispatcher.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.StatTotal):
                case nameof(MainViewModel.StatSales):
                case nameof(MainViewModel.StatPurchase):
                case nameof(MainViewModel.StatPending):
                case nameof(MainViewModel.StatPayments):
                    RefreshStats();
                    break;
                case nameof(MainViewModel.AllDocs):
                    RefreshRecentGrid();
                    break;
            }
        });
    }

    private void RefreshStats()
    {
        StatTotal.Text    = VM.StatTotal.ToString();
        StatSales.Text    = VM.StatSales.ToString();
        StatPurchase.Text = VM.StatPurchase.ToString();
        StatPending.Text  = $"₹{VM.StatPending:N0}";
        StatPayments.Text = $"₹{VM.StatPayments:N0}";
    }

    private void RefreshRecentGrid()
    {
        RecentGrid.ItemsSource = VM.AllDocs.Take(15).ToList();
    }

    // ── Quick actions ─────────────────────────────────────────────────────────

    private void Quick_NewSalesInvoice(object s, RoutedEventArgs e) =>
        OpenCreateWindow(DocumentType.SalesInvoice);

    private void Quick_NewPurchaseOrder(object s, RoutedEventArgs e) =>
        OpenCreateWindow(DocumentType.PurchaseOrder);

    private void ViewAll_Click(object s, RoutedEventArgs e) =>
        VM.NavReportAllCommand.Execute(null);

    private void RecentGrid_DoubleClick(object s, MouseButtonEventArgs e)
    {
        if (RecentGrid.SelectedItem is DocumentListItem d)
            OpenEditWindow(d.Id);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void OpenCreateWindow(DocumentType preset)
    {
        var win = new Windows.CreateDocumentWindow(
            existing: null, vm: VM, presetType: preset)
        { Owner = Window.GetWindow(this) };
        win.Closed += (_, _) =>
        {
            if (win.DocumentSaved) { VM.LoadDocuments(); RefreshRecentGrid(); }
        };
        win.Show();
    }

    private void OpenEditWindow(int id)
    {
        var doc = VM.ErpDb.LoadDocument(id);
        if (doc == null) return;
        var win = new Windows.CreateDocumentWindow(existing: doc, vm: VM)
        { Owner = Window.GetWindow(this) };
        win.Closed += (_, _) =>
        {
            if (win.DocumentSaved) { VM.LoadDocuments(); RefreshRecentGrid(); }
        };
        win.Show();
    }
}
