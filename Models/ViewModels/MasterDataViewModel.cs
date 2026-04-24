using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Ojaswat.Models;
using Ojaswat.Services;

namespace Ojaswat.ViewModels;

public class MasterDataViewModel : INotifyPropertyChanged
{
    private readonly ErpDocumentDbService _db;

    private ObservableCollection<Customer>   _customers = new();
    private ObservableCollection<ItemMaster> _items     = new();

    public ObservableCollection<Customer>   Customers
    {
        get => _customers;
        private set { _customers = value; PC(nameof(Customers)); }
    }

    public ObservableCollection<ItemMaster> Items
    {
        get => _items;
        private set { _items = value; PC(nameof(Items)); }
    }

    private string _customerStatus = "", _itemStatus = "";
    public string CustomerStatus { get => _customerStatus; set { _customerStatus = value; PC(nameof(CustomerStatus)); } }
    public string ItemStatus     { get => _itemStatus;     set { _itemStatus     = value; PC(nameof(ItemStatus)); } }

    public MasterDataViewModel(ErpDocumentDbService db)
    {
        _db = db;
        RefreshCustomers();
        RefreshItems();
    }

    public void RefreshCustomers()
    {
        var list = _db.LoadCustomers();
        Customers = new ObservableCollection<Customer>(list);
        CustomerStatus = $"✓ {list.Count} customers";
    }

    public void RefreshItems()
    {
        var list = _db.LoadItems();
        Items = new ObservableCollection<ItemMaster>(list);
        ItemStatus = $"✓ {list.Count} items";
    }

    public void SaveCustomer(Customer c)   { _db.SaveCustomer(c);   RefreshCustomers(); }
    public void DeleteCustomer(int id)     { _db.DeleteCustomer(id); RefreshCustomers(); }
    public void BulkUpsertCustomers(List<Customer> list)
    { _db.BulkUpsertCustomers(list); RefreshCustomers(); }

    public void SaveItem(ItemMaster m)     { _db.SaveItem(m);   RefreshItems(); }
    public void DeleteItem(int id)         { _db.DeleteItem(id); RefreshItems(); }
    public void BulkUpsertItems(List<ItemMaster> list)
    { _db.BulkUpsertItems(list); RefreshItems(); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void PC(string n) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
