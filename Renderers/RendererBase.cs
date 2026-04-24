using System;
using System.IO;
using Ojaswat.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

/// <summary>
/// Abstract base for all ERP document renderers.
/// Split into partial files:
///   RendererBase.cs          — properties, utilities, PDF composition orchestration
///   RendererBase.Header.cs   — PDF header (letterhead + address grid)
///   RendererBase.Table.cs    — PDF items table
///   RendererBase.Totals.cs   — PDF totals, terms, bank &amp; signatory
///   RendererBase.Html.cs     — full HTML generation
/// </summary>
public abstract partial class RendererBase : IDocRenderer
{
    // ── Abstract entry points ─────────────────────────────────────────────────
    public abstract IDocument BuildPdf(ErpDocument doc);

    // ── Label overrides ───────────────────────────────────────────────────────
    protected virtual string BillToLabel        => "Bill To";
    protected virtual string ShipToLabel        => "Ship To";
    protected virtual string DocumentNoLabel    => "Invoice No.";
    protected virtual string TotalsLabel        => "Total";
    protected virtual string DetailsColumnLabel => "INVOICE DETAILS";
    protected virtual string SignatoryLabel     => "Authorised Signatory";

    // ── Section visibility toggles ────────────────────────────────────────────
    protected virtual bool ShowEWayBill      => false;
    protected virtual bool ShowBankSection   => true;
    protected virtual bool ShowShipToSection => true;
    protected virtual bool ShowAmountInWords => true;
    protected virtual bool ShowGeneralTerms  => true;
    protected virtual bool ShowPaymentTerms  => true;
    protected virtual bool ShowSignatory     => true;

    // ── Color / font shortcuts (from InvoiceSettings) ─────────────────────────
    protected static string CA   => InvoiceSettings.ColorAccent;
    protected static string CTD  => InvoiceSettings.ColorTextDark;
    protected static string CBR  => InvoiceSettings.ColorBorder;
    protected static string CTH1 => InvoiceSettings.ColorTableHdr1;
    protected static string CTH2 => InvoiceSettings.ColorTableHdr2;
    protected static int    FsS  => InvoiceSettings.FontSizeSmall;
    protected static int    FsM  => InvoiceSettings.FontSizeMeta;

    // ── HTML escape ───────────────────────────────────────────────────────────
    protected static string H(string s) =>
        s?.Replace("&", "&amp;").Replace("<", "&lt;")
          .Replace(">", "&gt;").Replace("\"", "&quot;") ?? "";

    // ── Document number cleaner ───────────────────────────────────────────────
    protected static string CleanDocNo(string docNo)
    {
        if (string.IsNullOrWhiteSpace(docNo)) return docNo;
        int idx = docNo.IndexOf("-Rev", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? docNo[..idx] : docNo;
    }

    // ── Document type label ───────────────────────────────────────────────────
    protected static string DocLabel(DocumentType t) => t switch
    {
        DocumentType.SalesInvoice    => "Tax Invoice",
        DocumentType.Quotation       => "Quotation",
        DocumentType.SalesOrder      => "Sales Order",
        DocumentType.PurchaseOrder   => "Purchase Order",
        DocumentType.PurchaseInvoice => "Purchase Invoice",
        DocumentType.CreditNote      => "Credit Note",
        DocumentType.DebitNote       => "Debit Note",
        DocumentType.GRN             => "Goods Receipt Note",
        _                            => t.ToString(),
    };

    // ── Amount in words (Indian numeral system) ───────────────────────────────
    protected static string AmountInWords(decimal amount)
    {
        try
        {
            long r = (long)Math.Floor(amount);
            int  p = (int)Math.Round((amount - r) * 100);
            string w = ToWords(r) + " Rupees";
            if (p > 0) w += " and " + ToWords(p) + " Paise";
            return w + " only";
        }
        catch { return ""; }
    }

    private static string ToWords(long n)
    {
        if (n == 0) return "Zero";
        if (n < 0)  return "Minus " + ToWords(-n);
        string[] ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight",
            "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen",
            "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
        string w = "";
        if (n >= 10000000) { w += ToWords(n / 10000000) + " Crore ";    n %= 10000000; }
        if (n >= 100000)   { w += ToWords(n / 100000)   + " Lakh ";     n %= 100000;   }
        if (n >= 1000)     { w += ToWords(n / 1000)     + " Thousand "; n %= 1000;     }
        if (n >= 100)      { w += ones[n / 100] + " Hundred ";          n %= 100;      }
        if (n >= 20)       { w += tens[n / 10]; n %= 10; if (n > 0) w += "-" + ones[n]; }
        else if (n > 0)    { w += ones[n]; }
        return w.Trim();
    }

    // ── Logo / QR base-64 helpers (used by HTML renderer) ────────────────────
    protected static string BuildLogoHtml(string logoPath, int maxH, int maxW)
    {
        if (string.IsNullOrEmpty(logoPath) || !File.Exists(logoPath)) return "";
        try
        {
            string ext  = Path.GetExtension(logoPath).ToLower().TrimStart('.');
            string mime = ext is "jpg" or "jpeg" ? "image/jpeg" : "image/png";
            string b64  = Convert.ToBase64String(File.ReadAllBytes(logoPath));
            return $"<img src=\"data:{mime};base64,{b64}\" " +
                   $"style=\"max-height:{maxH}px;max-width:{maxW}px;object-fit:contain;display:block\" alt=\"\"/>";
        }
        catch { return ""; }
    }

    protected static string BuildQRHtml(string qrPath, int size)
    {
        if (string.IsNullOrEmpty(qrPath) || !File.Exists(qrPath)) return "";
        try
        {
            string ext  = Path.GetExtension(qrPath).ToLower().TrimStart('.');
            string mime = ext is "jpg" or "jpeg" ? "image/jpeg" : "image/png";
            string b64  = Convert.ToBase64String(File.ReadAllBytes(qrPath));
            return $"<img src=\"data:{mime};base64,{b64}\" " +
                   $"style=\"width:{size}px;height:{size}px;object-fit:contain;display:block\" alt=\"QR\"/>";
        }
        catch { return ""; }
    }

    // ── Styled banner block (PDF) ─────────────────────────────────────────────
    protected void StyledBlock(IContainer c, string text)
    {
        c.Background("#F5F5F5")
         .BorderTop(0.75f).BorderBottom(0.75f)
         .BorderColor(Colors.Grey.Darken1)
         .PaddingVertical(5).PaddingHorizontal(6)
         .AlignCenter()
         .Text(text)
         .FontSize(9).Bold()
         .FontColor(Colors.Grey.Darken3)
         .LetterSpacing(0.05f);
    }

    // ── Shipping address resolution (override per doc type if needed) ─────────
    protected virtual string GetShippingAddress(ErpDocument doc) =>
        string.IsNullOrWhiteSpace(doc.Customer.ShippingAddress)
            ? doc.Customer.BillingAddress
            : doc.Customer.ShippingAddress;

    // ── PDF content pipeline — override to restructure content sections ───────
    protected virtual void ComposePdfContent(IContainer container, ErpDocument doc)
    {
        container.Column(col =>
        {
            col.Item().Element(c => ComposePdfItemsTable(c, doc));
            col.Item().Element(c => ComposePdfTotalsAndTerms(c, doc));
            if (ShowBankSection)
                col.Item().Element(c => ComposePdfBankAndSign(c, doc));
        });
    }
}
