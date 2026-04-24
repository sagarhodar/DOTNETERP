using System.Windows;
using System.Windows.Controls;
using Ojaswat.Models;

namespace Ojaswat.Windows;

public partial class TandCEditorWindow : Window
{
    public TandCMaster? Result { get; private set; }
    private readonly int _existingId;

    public TandCEditorWindow(TandCMaster? existing = null)
    {
        InitializeComponent();
        if (existing != null)
        {
            _existingId    = existing.Id;
            TitleText.Text = "Edit T&C Template";
            LabelBox.Text     = existing.Label;
            PaymentBox.Text   = existing.PaymentTerms;
            GeneralBox.Text   = existing.GeneralTerms;
            for (int i = 0; i < DocTypeCombo.Items.Count; i++)
                if ((DocTypeCombo.Items[i] as ComboBoxItem)?.Content?.ToString() == existing.DocType)
                { DocTypeCombo.SelectedIndex = i; break; }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LabelBox.Text))
        { MessageBox.Show("Enter a label.", "Validation"); return; }
        Result = new TandCMaster
        {
            Id           = _existingId,
            Label        = LabelBox.Text.Trim(),
            DocType      = (DocTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All",
            PaymentTerms = PaymentBox.Text.Trim(),
            GeneralTerms = GeneralBox.Text.Trim(),
        };
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
