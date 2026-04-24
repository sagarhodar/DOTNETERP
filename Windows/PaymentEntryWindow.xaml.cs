using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ojaswat.Models;
using Ojaswat.Services;

namespace Ojaswat.Windows;

public partial class PaymentEntryWindow : Window
{
    private readonly ErpDocumentDbService _db = new();
    private readonly System.Collections.Generic.List<DocumentListItem> _docs;
    public bool Saved { get; private set; }

    public PaymentEntryWindow(System.Collections.Generic.List<DocumentListItem> openDocs)
    {
        InitializeComponent();
        _docs = openDocs;
        PayDatePicker.SelectedDate = DateTime.Now;

        var items = new[] { "" }.Concat(openDocs.Select(d => d.DocumentNo)).ToList();
        DocNoCombo.ItemsSource   = items;
        DocNoCombo.SelectedIndex = 0;
    }

    public void PreSelectDoc(string docNo)
    {
        if (string.IsNullOrEmpty(docNo)) return;
        int idx = DocNoCombo.Items.IndexOf(docNo);
        if (idx >= 0) DocNoCombo.SelectedIndex = idx;
        else          DocNoCombo.Text = docNo;
        DocNoCombo_Changed(DocNoCombo, null!);
    }

    private void DocNoCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        string docNo = DocNoCombo.SelectedItem as string ?? DocNoCombo.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(docNo)) return;

        var doc = _docs.FirstOrDefault(d => d.DocumentNo == docNo);
        if (doc != null)
        {
            PartyNameBox.Text = doc.CustomerName;
            StatusText.Text = doc.PendingAmount > 0
                ? $"Pending: ₹{doc.PendingAmount:N2}  |  Grand Total: ₹{doc.GrandTotal:N2}"
                : "No pending amount on this document.";
            if (doc.PendingAmount > 0) AmountBox.Text = doc.PendingAmount.ToString("F2");
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
            { MessageBox.Show("Enter a valid amount greater than zero.", "Validation"); return; }
            if (PayDatePicker.SelectedDate == null)
            { MessageBox.Show("Select a payment date.", "Validation"); return; }

            string linkedDocNo = DocNoCombo.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(linkedDocNo))
            {
                var linked = _docs.FirstOrDefault(d => d.DocumentNo == linkedDocNo);
                if (linked != null && linked.PendingAmount > 0 && amount > linked.PendingAmount)
                {
                    var res = MessageBox.Show(
                        $"Payment ₹{amount:N2} exceeds pending ₹{linked.PendingAmount:N2}.\n\nContinue anyway?",
                        "Confirm Overpayment", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res != MessageBoxResult.Yes) return;
                }
            }

            var modeStr = (ModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
            Enum.TryParse<PaymentMode>(modeStr, out var mode);

            _db.SavePayment(new PaymentEntry
            {
                DocumentNo = DocNoCombo.Text?.Trim() ?? "",
                PartyName  = PartyNameBox.Text.Trim(),
                Date       = PayDatePicker.SelectedDate.Value,
                Amount     = amount,
                Mode       = mode,
                Reference  = ReferenceBox.Text.Trim(),
                Notes      = NotesBox.Text.Trim(),
            });

            Saved = true;
            StatusText.Text = $"✓ Payment saved — ₹{amount:N2} via {modeStr}";
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error saving payment:\n{ex.Message}", "Error"); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
