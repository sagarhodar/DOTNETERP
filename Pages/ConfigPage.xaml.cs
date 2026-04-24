using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ojaswat.Models;
using Ojaswat.ViewModels;

namespace Ojaswat.Pages;

public partial class ConfigPage : UserControl
{
    private static MainViewModel VM => App.VM;

    public ConfigPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadFromDb();
    }

    private void LoadFromDb()
    {
        try
        {
            var p = VM.ErpDb.LoadCompany();

            CfgName.Text      = p.Name.Length      > 0 ? p.Name      : InvoiceSettings.CompanyName;
            CfgGST.Text       = p.GSTIN.Length     > 0 ? p.GSTIN     : InvoiceSettings.CompanyGSTIN;
            CfgStateCode.Text = p.StateCode.Length > 0 ? p.StateCode : InvoiceSettings.CompanyStateCode;
            CfgPhone.Text     = p.Phone.Length     > 0 ? p.Phone     : InvoiceSettings.CompanyPhone;
            CfgEmail.Text     = p.Email.Length     > 0 ? p.Email     : InvoiceSettings.CompanyEmail;
            CfgAddress.Text   = p.Address.Length   > 0 ? p.Address   : InvoiceSettings.CompanyAddress;
            CfgBank.Text      = p.Bank.Length      > 0 ? p.Bank      : InvoiceSettings.CompanyBank;
            CfgAccount.Text   = p.Account.Length   > 0 ? p.Account   : InvoiceSettings.CompanyAccount;
            CfgIFSC.Text      = p.IFSC.Length      > 0 ? p.IFSC      : InvoiceSettings.CompanyIFSC;
            CfgSignatory.Text = p.Signatory.Length > 0 ? p.Signatory : InvoiceSettings.CompanySignatory;

            // ✅ Resolve logo safely
            var logoPath = (!string.IsNullOrEmpty(p.LogoPath) && File.Exists(p.LogoPath))
                ? p.LogoPath
                : InvoiceSettings.LogoPath;

            CfgLogo.Text = logoPath;

            // ✅ Resolve QR safely (same pattern)
            var qrPath = (!string.IsNullOrEmpty(p.QRCodePath) && File.Exists(p.QRCodePath))
                ? p.QRCodePath
                : InvoiceSettings.QRCodePath;

            CfgQRCode.Text = qrPath;

            // ✅ Always apply resolved logo (NOT raw DB value)
            VM.ApplyLogo(logoPath);
        }
        catch
        {
            // ✅ fallback defaults (including logo now)
            CfgName.Text      = InvoiceSettings.CompanyName;
            CfgGST.Text       = InvoiceSettings.CompanyGSTIN;
            CfgStateCode.Text = InvoiceSettings.CompanyStateCode;
            CfgPhone.Text     = InvoiceSettings.CompanyPhone;
            CfgEmail.Text     = InvoiceSettings.CompanyEmail;
            CfgAddress.Text   = InvoiceSettings.CompanyAddress;
            CfgBank.Text      = InvoiceSettings.CompanyBank;
            CfgAccount.Text   = InvoiceSettings.CompanyAccount;
            CfgIFSC.Text      = InvoiceSettings.CompanyIFSC;
            CfgSignatory.Text = InvoiceSettings.CompanySignatory;

            CfgLogo.Text   = InvoiceSettings.LogoPath;
            CfgQRCode.Text = InvoiceSettings.QRCodePath;

            VM.ApplyLogo(InvoiceSettings.LogoPath);
        }
    }

    private void Save_Click(object s, RoutedEventArgs e)
    {
        try
        {
            VM.ErpDb.SaveCompany(new CompanyProfile
            {
                Name = CfgName.Text, GSTIN = CfgGST.Text, StateCode = CfgStateCode.Text,
                Phone = CfgPhone.Text, Email = CfgEmail.Text, Address = CfgAddress.Text,
                Bank = CfgBank.Text, Account = CfgAccount.Text, IFSC = CfgIFSC.Text,
                Signatory = CfgSignatory.Text, LogoPath = CfgLogo.Text, QRCodePath = CfgQRCode.Text,
            });
            if (!string.IsNullOrEmpty(CfgLogo.Text) && File.Exists(CfgLogo.Text))
            { VM.ApplyLogo(CfgLogo.Text); VM.SaveLogoSetting(CfgLogo.Text); }
            SaveStatus.Text = $"✓ Saved at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { SaveStatus.Text = $"Error: {ex.Message}"; }
    }

    private void BrowseLogo_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Select Logo Image", Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All|*.*" };
        if (dlg.ShowDialog() == true) CfgLogo.Text = dlg.FileName;
    }

    private void BrowseQRCode_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Select QR Code Image", Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All|*.*" };
        if (dlg.ShowDialog() == true) CfgQRCode.Text = dlg.FileName;
    }
}
