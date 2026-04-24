using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Ojaswat.Models;
using Ojaswat.Services;
using Ojaswat.ViewModels;
using Windows = Ojaswat.Windows;

namespace Ojaswat.Pages;

public partial class TandCPage : UserControl
{
    private static MainViewModel VM => App.VM;
    private readonly CsvService _csv = new();

    public TandCPage() { InitializeComponent(); Loaded += (_, _) => Load(); }

    private void Load()
    {
        try { var list = VM.ErpDb.LoadAllTandC(); TandCGrid.ItemsSource = new ObservableCollection<TandCMaster>(list); StatusText.Text = $"{list.Count} templates"; }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }

    private void New_Click(object s, RoutedEventArgs e) => OpenEditor(null);
    private void Refresh_Click(object s, RoutedEventArgs e) => Load();
    private void Grid_DoubleClick(object s, MouseButtonEventArgs e) { if ((s as DataGrid)?.SelectedItem is TandCMaster t) OpenEditor(t); }
    private void Edit_Click(object s, RoutedEventArgs e) { if (s is Button { Tag: TandCMaster t }) OpenEditor(t); }

    private void Delete_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: TandCMaster t }) return;
        if (MessageBox.Show($"Delete '{t.Label}'?", "Delete", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { VM.ErpDb.DeleteTandC(t.Id); Load(); } catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}"); }
    }

    private void OpenEditor(TandCMaster? existing)
    {
        var win = new Windows.TandCEditorWindow(existing) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() == true && win.Result != null)
            try { VM.ErpDb.SaveTandC(win.Result); Load(); } catch (Exception ex) { MessageBox.Show($"Save error:\n{ex.Message}"); }
    }

    private void Import_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Import T&C CSV", Filter = "CSV|*.csv|All|*.*" };
        if (dlg.ShowDialog() != true) return;
        try { var list = _csv.ImportTandC(dlg.FileName); int c = 0; foreach (var rec in list) { rec.Id = 0; VM.ErpDb.SaveTandC(rec); c++; } Load(); StatusText.Text = $"✓ Imported {c} templates"; }
        catch (Exception ex) { MessageBox.Show($"Import error:\n{ex.Message}"); }
    }

    private void Export_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var list = VM.ErpDb.LoadAllTandC();
            var dlg  = new SaveFileDialog { Title = "Export T&C CSV", Filter = "CSV|*.csv", FileName = $"tandc_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            _csv.ExportTandC(list, dlg.FileName);
            MessageBox.Show($"Exported {list.Count} templates.", "Done");
        }
        catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}"); }
    }
}
