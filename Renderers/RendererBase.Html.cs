using System;
using System.Linq;
using System.Text;
using Ojaswat.Models;

namespace Ojaswat.Renderers;

public abstract partial class RendererBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  HTML GENERATION
    //  Each visual section is a protected virtual method so subclasses can
    //  swap out individual blocks without rewriting the full pipeline.
    // ══════════════════════════════════════════════════════════════════════════

    public virtual string BuildHtml(ErpDocument doc)
    {
        decimal sub     = doc.Items.Sum(i => i.LineTotal);
        decimal taxable = sub - doc.Discount;
        decimal tax     = taxable * doc.GstPercent / 100;
        decimal grand   = taxable + tax + doc.Freight;

        string logo        = BuildLogoHtml(doc.Company.LogoPath, InvoiceSettings.LogoHeight, InvoiceSettings.LogoWidth);
        string stripe      = BuildStripe();
        string letterhead  = BuildLetterhead(doc, logo);
        string titleBar    = BuildTitleBar(doc);
        string headerGrid  = BuildHeaderGrid(doc);
        string itemsTable  = BuildItemsTable(doc);
        string totalsBlock = BuildTotalsBlock(doc);
        string bankSection = ShowBankSection ? BuildBankSection(doc) : "";
        string wordsTerms  = BuildTermsAndWords(doc, grand);
        string signatory   = ShowSignatory ? BuildSignatory(doc) : "";
        string css         = BuildPageCss();
        string dLabel      = DocLabel(doc.DocType);

        return $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<title>{dLabel} — {H(CleanDocNo(doc.DocumentNo))}</title>
{css}
</head>
<body>
<div class='page'>
{stripe}{letterhead}{titleBar}{headerGrid}{itemsTable}
<div style='display:grid;grid-template-columns:1fr 220px;border-top:1px solid #DDD;border-bottom:1px solid #DDD'>
  <div style='padding:12px 16px;border-right:1px solid #DDD'>{wordsTerms}</div>
  <div style='padding:12px 16px'>{totalsBlock}</div>
</div>
{bankSection}{signatory}
<div style='border-top:1px solid #E0E0E0;padding:6px 24px;display:flex;justify-content:space-between;font-size:9px;color:#999;font-family:Arial,sans-serif'>
  <span>Generated: {DateTime.Now:dd MMM yyyy, HH:mm}</span>
  <span style='font-weight:600;color:{CA}'>{dLabel} · {H(CleanDocNo(doc.DocumentNo))}</span>
  <span>Thank you for your business</span>
</div>
</div>
</body>
</html>";
    }

    // ── Accent stripe ─────────────────────────────────────────────────────────
    protected virtual string BuildStripe() =>
        $"<div style='height:{InvoiceSettings.StripeHeight}px;background:{InvoiceSettings.StripeGradient};" +
        $"print-color-adjust:exact;-webkit-print-color-adjust:exact'></div>";

    // ── Company letterhead ────────────────────────────────────────────────────
    protected virtual string BuildLetterhead(ErpDocument doc, string logoHtml)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='display:flex;align-items:flex-start;justify-content:space-between;" +
                  "padding:16px 20px 10px;font-family:Arial,sans-serif'>");
        sb.Append("<div style='flex:1'>");
        sb.Append($"<div style='font-size:17px;font-weight:800;letter-spacing:.4px;color:{CTD};" +
                  $"margin-bottom:2px'>{H(doc.Company.Name)}</div>");
        sb.Append($"<div style='font-size:9px;color:#555;line-height:1.7;margin-bottom:2px'>" +
                  $"{H(doc.Company.Address)}</div>");
        sb.Append($"<div style='font-size:9px;color:#555'>GSTIN : {H(doc.Company.GSTIN)}");
        if (!string.IsNullOrEmpty(doc.Company.StateCode))
            sb.Append($" &nbsp;|&nbsp; State : {H(doc.Company.StateCode)}");
        if (!string.IsNullOrEmpty(doc.Company.Phone))
            sb.Append($" &nbsp;|&nbsp; Ph : {H(doc.Company.Phone)}");
        sb.Append("</div>");
        if (!string.IsNullOrEmpty(doc.Company.Email))
            sb.Append($"<div style='font-size:9px;color:#555'>Email : {H(doc.Company.Email)}</div>");
        sb.Append("</div>");
        sb.Append($"<div style='flex-shrink:0;margin-left:20px'>{logoHtml}</div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    // ── Document type title bar ───────────────────────────────────────────────
    protected virtual string BuildTitleBar(ErpDocument doc) =>
        $"<div style='text-align:center;padding:6px 20px;background:#F5F5F5;" +
        $"border-top:1px solid #C8C8C8;border-bottom:1px solid #C8C8C8'>" +
        $"<span style='font-size:11px;font-weight:700;letter-spacing:1.5px;color:{CTD};" +
        $"text-transform:uppercase'>{DocLabel(doc.DocType)}</span></div>";

    // ── 3-column Bill-To / Ship-To / Details grid ─────────────────────────────
    protected virtual string BuildHeaderGrid(ErpDocument doc)
    {
        var sb = new StringBuilder();
        string labelStyle = "font-size:7px;font-weight:700;color:#999;text-transform:uppercase;" +
                            "letter-spacing:.9px;margin-bottom:4px";
        string nameStyle  = $"font-size:11px;font-weight:700;color:{CTD};margin-bottom:2px";
        string addrStyle  = "font-size:9px;color:#555;line-height:1.65";

        // Compute column count so borders render correctly when ShipTo is hidden
        int cols = ShowShipToSection ? 3 : 2;
        sb.Append($"<div style='display:grid;grid-template-columns:repeat({cols},1fr);" +
                  $"border-bottom:1px solid #DDD;font-family:Arial,sans-serif'>");

        // Bill To
        sb.Append("<div style='padding:10px 14px;border-right:1px solid #DDD'>");
        sb.Append($"<div style='{labelStyle}'>{H(BillToLabel)}</div>");
        sb.Append($"<div style='{nameStyle}'>{H(doc.Customer.Name)}</div>");
        sb.Append($"<div style='{addrStyle}'>{H(doc.Customer.BillingAddress).Replace("\n", "<br/>")}</div>");
        if (!string.IsNullOrWhiteSpace(doc.Customer.GSTIN))
            sb.Append($"<div style='font-size:8.5px;color:{CTD};margin-top:3px;font-family:monospace'>" +
                      $"GSTIN : {H(doc.Customer.GSTIN)}</div>");
        if (!string.IsNullOrWhiteSpace(doc.Customer.StateCode))
            sb.Append($"<div style='font-size:8.5px;color:#555'>State : {H(doc.Customer.StateCode)}</div>");
        sb.Append("</div>");

        // Ship To (optional)
        if (ShowShipToSection)
        {
            string shipAddr = GetShippingAddress(doc);
            sb.Append("<div style='padding:10px 14px;border-right:1px solid #DDD'>");
            sb.Append($"<div style='{labelStyle}'>{H(ShipToLabel)}</div>");
            sb.Append($"<div style='{addrStyle}'>{H(shipAddr).Replace("\n", "<br/>")}</div>");
            sb.Append("</div>");
        }

        // Document details
        sb.Append("<div style='padding:10px 14px'>");
        sb.Append($"<div style='{labelStyle}'>Details</div>");
        sb.Append(DetRow(DocumentNoLabel, H(CleanDocNo(doc.DocumentNo))));
        sb.Append(DetRow("Date", doc.Date.ToString("dd-MM-yyyy")));
        if (!string.IsNullOrWhiteSpace(doc.PlaceOfSupply))
            sb.Append(DetRow("Place of Supply", H(doc.PlaceOfSupply)));
        if (!string.IsNullOrWhiteSpace(doc.CustomerRefNo))
            sb.Append(DetRow("PO Ref No", H(doc.CustomerRefNo)));
        if (ShowEWayBill && !string.IsNullOrWhiteSpace(doc.EWayBillNo))
            sb.Append(DetRow("E-Way Bill No", H(doc.EWayBillNo)));
        sb.Append("</div></div>");
        return sb.ToString();
    }

    // ── HTML items table ──────────────────────────────────────────────────────
    protected virtual string BuildItemsTable(ErpDocument doc)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='border-bottom:1px solid #DDD'>");
        sb.Append("<table style='width:100%;border-collapse:collapse;font-family:Arial,sans-serif'>");
        sb.Append("<thead><tr style='background:#F5F5F5;border-top:1px solid #DDD;border-bottom:1px solid #BBB'>");
        foreach (var (label, align) in new[] { ("#", "left"), ("Description", "left"), ("Qty", "right"), ("Rate", "right"), ("Amount", "right") })
            sb.Append($"<th style='padding:7px 9px;text-align:{align};font-size:8px;font-weight:700;" +
                      $"color:#555;letter-spacing:.5px'>{label}</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var item in doc.Items)
        {
            sb.Append("<tr style='border-bottom:1px solid #F0F0F0'>");
            sb.Append($"<td style='padding:5px 9px;font-size:9px;color:#888'>{item.ItemNumber}</td>");
            sb.Append($"<td style='padding:5px 9px;font-size:9.5px;font-weight:600'>{H(item.Description)}</td>");
            sb.Append($"<td style='padding:5px 9px;text-align:right;font-size:9px'>{item.Quantity:N2}</td>");
            sb.Append($"<td style='padding:5px 9px;text-align:right;font-size:9px'>₹{item.Rate:N2}</td>");
            sb.Append($"<td style='padding:5px 9px;text-align:right;font-size:9.5px;font-weight:700'>₹{item.LineTotal:N2}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    // ── HTML totals block (right column of bottom grid) ───────────────────────
    protected virtual string BuildTotalsBlock(ErpDocument doc)
    {
        decimal sub = doc.Items.Sum(i => i.LineTotal);
        decimal totalCgst = 0, totalSgst = 0, totalIgst = 0;

        foreach (var item in doc.Items)
        {
            decimal itemTax = item.LineTotal * item.GSTPercent / 100;
            if (doc.GstMode == GstMode.CgstSgst) { totalCgst += itemTax / 2; totalSgst += itemTax / 2; }
            else totalIgst += itemTax;
        }

        decimal totalTax = totalCgst + totalSgst + totalIgst;
        decimal grand    = sub + totalTax + doc.Freight - doc.Discount;

        var sb = new StringBuilder();
        sb.Append(TotRow("Sub Total", $"₹{sub:N2}", false));
        if (doc.Discount > 0) sb.Append(TotRow("Discount", $"−₹{doc.Discount:N2}", true));
        sb.Append(TotRow("GST", $"₹{totalTax:N2}", false));
        if (doc.Freight > 0) sb.Append(TotRow("Freight", $"₹{doc.Freight:N2}", false));

        sb.Append($"<div style='display:flex;justify-content:space-between;font-size:13px;font-weight:700;" +
                  $"padding:6px 0;border-top:1.5px solid {CTD};margin-top:3px;font-family:Arial,sans-serif'>" +
                  $"<span>{TotalsLabel}</span><span>₹{grand:N2}</span></div>");
        return sb.ToString();
    }

    // ── HTML bank section (override to provide bank HTML) ─────────────────────
    // Returns empty by default; ShowBankSection gates whether this is included.
    protected virtual string BuildBankSection(ErpDocument doc) => "";

    // ── Amount in words + payment terms (left column of bottom grid) ──────────
    protected virtual string BuildTermsAndWords(ErpDocument doc, decimal grand)
    {
        var sb = new StringBuilder();
        if (ShowAmountInWords)
        {
            sb.Append($"<div style='font-size:8px;font-weight:700;color:#777;letter-spacing:.7px;" +
                      $"text-transform:uppercase;font-family:Arial,sans-serif'>Amount in Words</div>");
            sb.Append($"<div style='font-size:9px;font-style:italic;color:#444;margin-top:3px;" +
                      $"font-family:Arial,sans-serif'>{AmountInWords(grand)}</div>");
        }
        if (!string.IsNullOrWhiteSpace(doc.PaymentTerms))
            sb.Append($"<div style='font-size:8.5px;color:#555;margin-top:8px;" +
                      $"font-family:Arial,sans-serif'>{H(doc.PaymentTerms)}</div>");
        return sb.ToString();
    }

    // ── HTML signatory block ──────────────────────────────────────────────────
    protected virtual string BuildSignatory(ErpDocument doc) => "";

    // ── Page-level CSS ────────────────────────────────────────────────────────
    protected virtual string BuildPageCss() =>
        "<style>*{box-sizing:border-box}body{font-family:Arial,sans-serif;font-size:9.5px;margin:0;color:#222}" +
        ".page{max-width:800px;margin:0 auto;border:1px solid #E0E0E0}" +
        "@media print{body{margin:0}.page{border:none;max-width:none}}</style>";

    // ── Reusable HTML row helpers ─────────────────────────────────────────────

    /// <summary>A label/value row used inside the document details column.</summary>
    protected string DetRow(string lbl, string val) =>
        $"<div style='display:flex;justify-content:space-between;font-size:9px;padding:2px 0;" +
        $"border-bottom:1px dotted #EEE;font-family:Arial,sans-serif'>" +
        $"<span style='color:#777'>{lbl}</span>" +
        $"<span style='font-weight:600;color:{CTD}'>{val}</span></div>";

    /// <summary>A label/value row used inside the totals column.</summary>
    protected string TotRow(string lbl, string val, bool highlight) =>
        $"<div style='display:flex;justify-content:space-between;font-size:9px;padding:3px 0;" +
        $"border-bottom:1px dotted #EEE;font-family:Arial,sans-serif'>" +
        $"<span style='color:{(highlight ? "#059669" : "#555")}'>{lbl}</span>" +
        $"<span style='font-weight:600;color:{(highlight ? "#059669" : CTD)};font-family:monospace'>{val}</span></div>";
}
