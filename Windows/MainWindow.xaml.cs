using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Ojaswat.Database;
using Ojaswat.Services;
using Ojaswat.ViewModels;

namespace Ojaswat;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        // ── Order matters ─────────────────────────────────────────────────────
        // 1. Create VM first
        // 2. Store in App.VM static locator (pages read this in their Loaded event)
        // 3. Set DataContext on Window so top-bar {Binding PageTitle} etc. work
        // 4. InitializeComponent — XAML parsed, bindings evaluated, sidebar commands bound
        // 5. Load documents — populates AllDocs → triggers stat refresh → UI updates

        Application.Current.DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show(
                $"Unhandled error:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        try { DbInitializer.Initialize(); }
        catch (Exception ex) { MessageBox.Show($"DB init error:\n{ex.Message}"); }

        // Step 1+2: create VM and register in static locator
        _vm = new MainViewModel();
        App.SetVM(_vm);

        // Step 3: set DataContext BEFORE InitializeComponent
        DataContext = _vm;

        // Step 4: parse XAML — sidebar RadioButton commands now bind correctly
        InitializeComponent();

        // Step 5: load data — stats and recent-docs will auto-update via PropertyChanged
        _vm.LoadDocuments();

        // Apply logo if one was saved previously
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.LogoPath) &&
                !string.IsNullOrEmpty(_vm.LogoPath))
                ApplyLogoToSidebar(_vm.LogoPath);
        };
        if (!string.IsNullOrEmpty(_vm.LogoPath))
            ApplyLogoToSidebar(_vm.LogoPath);
    }

    // ── Logo ──────────────────────────────────────────────────────────────────
    private void SetLogo_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select Logo Image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        _vm.ApplyLogo(dlg.FileName);
        _vm.SaveLogoSetting(dlg.FileName);
        ApplyLogoToSidebar(dlg.FileName);
    }

    private void ApplyLogoToSidebar(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource   = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            SidebarLogo.Source      = bmp;
            SidebarLogo.Visibility  = Visibility.Visible;
            SidebarBrand.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    // ── DB Export / Import ────────────────────────────────────────────────────
    private void Nav_ExportDb(object s, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title    = "Export Database",
                Filter   = "DuckDB|*.db|All|*.*",
                FileName = $"ojaswat_export_{DateTime.Now:yyyyMMdd_HHmmss}.db"
            };
            if (dlg.ShowDialog() != true) return;
            string src = DuckDbService.DbPath;
            if (!File.Exists(src)) { MessageBox.Show("No database found. Create a document first."); return; }
            File.Copy(src, dlg.FileName, true);
            MessageBox.Show($"Database exported to:\n{dlg.FileName}", "Export Complete");
        }
        catch (Exception ex) { MessageBox.Show($"Export failed:\n{ex.Message}"); }
    }

    private void Nav_ImportDb(object s, RoutedEventArgs e)
    {
        try
        {
            if (MessageBox.Show(
                "Import will REPLACE all current data. A backup is made first.\n\nContinue?",
                "Import Database", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            var dlg = new OpenFileDialog { Title = "Select Database", Filter = "DuckDB|*.db|All|*.*" };
            if (dlg.ShowDialog() != true) return;

            string dest   = DuckDbService.DbPath;
            string folder = DuckDbService.DbFolder;
            Directory.CreateDirectory(folder);

            if (File.Exists(dest))
                File.Copy(dest, Path.Combine(folder, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"), true);

            _vm.ErpDb.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            System.Threading.Thread.Sleep(400);

            File.Copy(dlg.FileName, dest, true);
            DbInitializer.Initialize();
            _vm.LoadDocuments();
            _vm.NavDashboardCommand.Execute(null);
            MessageBox.Show("Database imported and reloaded.", "Import Complete");
        }
        catch (Exception ex) { MessageBox.Show($"Import failed:\n{ex.Message}"); }
    }
}
