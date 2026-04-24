// ═══════════════════════════════════════════════════════════════════════════
//  InvoiceSettings.cs  —  Edit this file to customise invoice layout & defaults.
//  Rebuild after any change.
// ═══════════════════════════════════════════════════════════════════════════
using System;
using System.IO; 
namespace Ojaswat;

public static class InvoiceSettings
{
    // ── COMPANY DEFAULTS ─────────────────────────────────────────────────────
    public const string CompanyName      = "OJASWAT ENGINEERING";
    public const string CompanyGSTIN     = "24AAIFO4045M1Z2";
    public const string CompanyStateCode = "24-Gujarat";
    public const string CompanyPhone     = "+91 97731 18112";
    public const string CompanyEmail     = "info@ojaswat.com";
    public const string CompanyAddress   = "SURVEY NO 172, ATLAS PARK, BEHIND MALDHARI FATAK, KOTHARIYA, RAJKOT";
    public const string CompanyBank      = "HDFC BANK, MAVDI ROAD";
    public const string CompanyAccount   = "50200081814289";
    public const string CompanyIFSC      = "HDFC0005875";
    public const string CompanySignatory = "Authorised Signatory";

    // ── LOGO ──────────────────────────────────────────────────────────────────
    public static string LogoPath { get; set; } = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "Resources", 
        "Ojaswat.png");
    // public const string LogoPath     = @"   // absolute path to logo image
    public const int    LogoHeight   = 120;
    public const int    LogoWidth    = 120;
    public const string LogoPosition = "left";

    // ── QR CODE ───────────────────────────────────────────────────────────────
    public static string QRCodePath { get; set; } = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Resources", 
            "qr.png");
    // public const string QRCodePath   = @"   // // absolute path to QR code image
    public const int    QRCodeSize = 90;

    // ── DOCUMENT COLOURS ─────────────────────────────────────────────────────
    public const string ColorAccent     = "#494949";
    public const string ColorAccent2    = "#373737";
    public const string ColorTextDark   = "#333333";
    public const string ColorTextMuted  = "#c9c9c9";
    public const string ColorBorder     = "#FFFFFF";
    public const string ColorRowAlt     = "#FFFBF5";
    public const string ColorTableHdr1  = "#dddddd";
    public const string ColorTableHdr2  = "#dddddd";
    public const string ColorGrandBg1   = "#3D1F00";
    public const string ColorGrandBg2   = "#1C0A00";
    public const string ColorGrandText  = "#FFF8EE";
    public const string ColorGrandValue = "#FCD34D";

    // ── TYPOGRAPHY ────────────────────────────────────────────────────────────
    public const string FontFamily   = "'Arial', Arial, sans-serif";
    public const int    FontSizeBody  = 13;
    public const int    FontSizeSmall = 11;
    public const int    FontSizeMeta  = 10;

    // ── PAGE ─────────────────────────────────────────────────────────────────
    public const string PageBackground = "#ffffff";
    public const string PageBorder     = "#FDDCB5";

    // ── ACCENT STRIPE ─────────────────────────────────────────────────────────
    public const string StripeGradient = "linear-gradient(90deg,#E8650A,#FCD34D,#F59E0B,#E8650A)";
    public const int    StripeHeight   = 4;

    // ── TERMS DEFAULTS ────────────────────────────────────────────────────────
    public const string DefaultPaymentTerms = "Payment due within 30 days.";
    public const string DefaultGeneralTerms =
        "Goods once sold will not be taken back.\nSubject to local jurisdiction.";
}
