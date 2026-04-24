using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Ojaswat.Models;
using Ojaswat.Services;

namespace Ojaswat.ViewModels;

public class FinanceViewModel : INotifyPropertyChanged
{
    private readonly ErpDocumentDbService _db;

    private ObservableCollection<PaymentListItem> _payments = new();
    public  ObservableCollection<PaymentListItem>  Payments
    {
        get => _payments;
        private set { _payments = value; PC(nameof(Payments)); PC(nameof(StatusText)); }
    }

    public string StatusText =>
        $"{Payments.Count} entries  |  Total: ₹{Payments.Sum(p => p.Amount):N2}";

    public FinanceViewModel(ErpDocumentDbService db)
    {
        _db = db;
        Load();
    }

    public void Load()
    {
        try
        {
            var list = _db.LoadAllPayments();
            Payments = new ObservableCollection<PaymentListItem>(list);
        }
        catch { Payments = new(); }
    }

    public void DeletePayment(int id)
    {
        _db.DeletePayment(id);
        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void PC(string n) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
