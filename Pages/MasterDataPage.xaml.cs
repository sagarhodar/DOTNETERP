using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Ojaswat.Models;
using Ojaswat.Services;
using Ojaswat.ViewModels;
using Windows = Ojaswat.Windows;

namespace Ojaswat.Pages;

public partial class MasterDataPage : UserControl
{
    private static MainViewModel VM => App.VM;
    private MasterDataViewModel? _masterVm;
    private readonly CsvService  _csv = new();

    public MasterDataPage() { InitializeComponent(); Loaded += OnLoaded; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _masterVm = new MasterDataViewModel(VM.ErpDb);
        CustomerGrid.ItemsSource = _masterVm.Customers;
        ItemGrid.ItemsSource     = _masterVm.Items;
        CustomerStatus.Text      = _masterVm.CustomerStatus;
        ItemStatus.Text          = _masterVm.ItemStatus;
        _masterVm.PropertyChanged += (_, ev) =>
        {
            if (ev.PropertyName == nameof(MasterDataViewModel.Customers))
            { CustomerGrid.ItemsSource = _masterVm.Customers; CustomerStatus.Text = _masterVm.CustomerStatus; }
            if (ev.PropertyName == nameof(MasterDataViewModel.Items))
            { ItemGrid.ItemsSource = _masterVm.Items; ItemStatus.Text = _masterVm.ItemStatus; }
        };
    }

    private void NewCustomer_Click(object s, RoutedEventArgs e)
    {
        if (_masterVm == null) return;
        var win = new Windows.QuickEditWindow("Customer", new[] { ("Name",""),("GSTIN",""),("State Code",""),("Billing Address",""),("Shipping Address","") }) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() != true) return;
        var v = win.Values;
        _masterVm.SaveCustomer(new Customer { Name=v[0], GSTIN=v[1], StateCode=v[2], BillingAddress=v[3], ShippingAddress=v[4] });
    }

    private void CustomerGrid_DoubleClick(object s, MouseButtonEventArgs e) { if ((s as DataGrid)?.SelectedItem is Customer c) OpenCustomerEditor(c); }
    private void CustomerEdit_Click(object s, RoutedEventArgs e) { if (s is Button { Tag: Customer c }) OpenCustomerEditor(c); }

    private void OpenCustomerEditor(Customer c)
    {
        if (_masterVm == null) return;
        var win = new Windows.QuickEditWindow("Customer", new[] { ("Name",c.Name),("GSTIN",c.GSTIN),("State Code",c.StateCode),("Billing Address",c.BillingAddress),("Shipping Address",c.ShippingAddress) }) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() != true) return;
        var v = win.Values; c.Name=v[0]; c.GSTIN=v[1]; c.StateCode=v[2]; c.BillingAddress=v[3]; c.ShippingAddress=v[4];
        _masterVm.SaveCustomer(c);
    }

    private void CustomerDelete_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: Customer c } || _masterVm == null) return;
        if (MessageBox.Show($"Delete '{c.Name}'?", "Delete", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { _masterVm.DeleteCustomer(c.Id); } catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}"); }
    }

    private void RefreshCustomers_Click(object s, RoutedEventArgs e) => _masterVm?.RefreshCustomers();

    private void ImportCustomers_Click(object s, RoutedEventArgs e)
    {
        if (_masterVm == null) return;
        var path = PickCsv("customers"); if (path == null) return;
        try { var list = _csv.ImportCustomers(path); _masterVm.BulkUpsertCustomers(list); CustomerStatus.Text = $"✓ {list.Count} customers loaded"; }
        catch (Exception ex) { MessageBox.Show($"Import error:\n{ex.Message}"); }
    }

    private void ExportCustomers_Click(object s, RoutedEventArgs e)
    {
        if (_masterVm == null) return;
        try { var dlg = new SaveFileDialog { Filter="CSV|*.csv", FileName=$"customers_{DateTime.Now:yyyyMMdd}.csv" }; if (dlg.ShowDialog()!=true) return; _csv.ExportCustomers(_masterVm.Customers,dlg.FileName); MessageBox.Show($"Exported {_masterVm.Customers.Count} customers.","Done"); }
        catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}"); }
    }

    private void ClearCustomers_Click(object s, RoutedEventArgs e)
    { if (MessageBox.Show("Clear customer list from memory?","Confirm",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return; CustomerGrid.ItemsSource=null; CustomerStatus.Text="Cleared."; }

    private void NewItem_Click(object s, RoutedEventArgs e)
    {
        if (_masterVm == null) return;
        var win = new Windows.QuickEditWindow("Item", new[] { ("Name",""),("HSN/SAC",""),("Unit (UOM)","Nos"),("Default Rate","0"),("GST %","18") }) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() != true) return;
        var v = win.Values; decimal.TryParse(v[3],out var rate); decimal.TryParse(v[4],out var gst);
        _masterVm.SaveItem(new ItemMaster { Name=v[0], HSN=v[1], UOM=v[2], DefaultRate=rate, GSTPercent=gst });
    }

    private void ItemGrid_DoubleClick(object s, MouseButtonEventArgs e) { if ((s as DataGrid)?.SelectedItem is ItemMaster m) OpenItemEditor(m); }
    private void ItemEdit_Click(object s, RoutedEventArgs e) { if (s is Button { Tag: ItemMaster m }) OpenItemEditor(m); }

    private void OpenItemEditor(ItemMaster m)
    {
        if (_masterVm == null) return;
        var win = new Windows.QuickEditWindow("Item", new[] { ("Name",m.Name),("HSN/SAC",m.HSN),("Unit (UOM)",m.UOM),("Default Rate",m.DefaultRate.ToString("F2")),("GST %",m.GSTPercent.ToString("F0")) }) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() != true) return;
        var v = win.Values; m.Name=v[0]; m.HSN=v[1]; m.UOM=v[2]; decimal.TryParse(v[3],out var rate); m.DefaultRate=rate; decimal.TryParse(v[4],out var gst); m.GSTPercent=gst;
        _masterVm.SaveItem(m);
    }

    private void ItemDelete_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: ItemMaster m } || _masterVm == null) return;
        if (MessageBox.Show($"Delete '{m.Name}'?","Delete",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
        try { _masterVm.DeleteItem(m.Id); } catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}"); }
    }

    private void RefreshItems_Click(object s, RoutedEventArgs e) => _masterVm?.RefreshItems();

    private void ImportItems_Click(object s, RoutedEventArgs e)
    {
        if (_masterVm == null) return;
        var path = PickCsv("items"); if (path == null) return;
        try { var list = _csv.ImportItems(path); _masterVm.BulkUpsertItems(list); ItemStatus.Text = $"✓ {list.Count} items loaded"; }
        catch (Exception ex) { MessageBox.Show($"Import error:\n{ex.Message}"); }
    }

    private void ExportItems_Click(object s, RoutedEventArgs e)
    {
        if (_masterVm == null) return;
        try { var dlg = new SaveFileDialog { Filter="CSV|*.csv", FileName=$"items_{DateTime.Now:yyyyMMdd}.csv" }; if (dlg.ShowDialog()!=true) return; _csv.ExportItems(_masterVm.Items,dlg.FileName); MessageBox.Show($"Exported {_masterVm.Items.Count} items.","Done"); }
        catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}"); }
    }

    private void ClearItems_Click(object s, RoutedEventArgs e)
    { if (MessageBox.Show("Clear item list from memory?","Confirm",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return; ItemGrid.ItemsSource=null; ItemStatus.Text="Cleared."; }

    private static string? PickCsv(string hint)
    { var dlg = new OpenFileDialog { Title=$"Select {hint} CSV", Filter="CSV|*.csv|All|*.*" }; return dlg.ShowDialog()==true ? dlg.FileName : null; }
}
