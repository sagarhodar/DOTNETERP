using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Ojaswat.Models;

namespace Ojaswat.Windows;

public partial class StockAdjustmentWindow : Window
{
    private readonly List<ItemMaster> _items;

    // Results read by caller
    public string        SelectedItemName { get; private set; } = "";
    public string        SelectedHSN      { get; private set; } = "";
    public string        SelectedUOM      { get; private set; } = "";
    public decimal       Quantity         { get; private set; }
    public StockMovement MovementType     { get; private set; } = StockMovement.ADJUST_IN;
    public string        Narration        { get; private set; } = "";

    public StockAdjustmentWindow(List<ItemMaster> items)
    {
        InitializeComponent();
        _items = items;
        ItemCombo.ItemsSource       = items;
        ItemCombo.DisplayMemberPath = "Name";
        if (items.Count > 0) ItemCombo.SelectedIndex = 0;
    }

    private void ItemCombo_Changed(object s, SelectionChangedEventArgs e)
    {
        if (ItemCombo.SelectedItem is ItemMaster m)
        {
            UOMBox.Text          = m.UOM;
            CurrentStockBox.Text = m.CurrentStock.ToString("N2") + " " + m.UOM;
        }
    }

    private void Save_Click(object s, RoutedEventArgs e)
    {
        ErrText.Text = "";
        if (ItemCombo.SelectedItem is not ItemMaster item)
        { ErrText.Text = "Select an item."; return; }
        if (!decimal.TryParse(QtyBox.Text, out var qty) || qty <= 0)
        { ErrText.Text = "Enter a valid quantity greater than zero."; return; }

        var typeTag = ((TypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ADJUST_IN");
        System.Enum.TryParse<StockMovement>(typeTag, out var movType);

        SelectedItemName = item.Name;
        SelectedHSN      = item.HSN;
        SelectedUOM      = UOMBox.Text.Trim().Length > 0 ? UOMBox.Text.Trim() : item.UOM;
        Quantity         = qty;
        MovementType     = movType;
        Narration        = NarrationBox.Text.Trim().Length > 0
            ? NarrationBox.Text.Trim()
            : $"Manual {typeTag.Replace("_", " ").ToLower()}";

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object s, RoutedEventArgs e) => Close();
}
