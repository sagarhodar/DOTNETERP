using Ojaswat.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

/// <summary>
/// Purchase Order — defines how PO PDF should look.
/// Uses base layout, overrides only labels + visibility.
/// </summary>
public sealed class PurchaseOrderRenderer : DefaultRenderer
{
    // ── HEADER LABEL CUSTOMIZATION ───────────────────────────────────────────
    protected override string BillToLabel     => "Supplier";
    protected override string ShipToLabel     => "Delivery Address";
    protected override string DocumentNoLabel => "PO No.";

    protected override string DateLabel         => "PO Date";
    protected override string CustomerRefLabel  => "QTN Ref No";
    protected override string DetailsColumnLabel => "PO DETAILS";

    // ── VISIBILITY CONTROL ───────────────────────────────────────────────────
    protected override bool ShowEWayBill     => false;
    protected override bool ShowBankSection  => false;
    protected override bool ShowPreparedBy   => false;

    // Totals section control
    protected override bool ShowPaymentTerms => true;
    protected override bool ShowGeneralTerms => true;
    protected override bool ShowAmountInWords => true;

    // ── LABEL CUSTOMIZATION (TOTALS SECTION) ─────────────────────────────────


    // ── SHIPPING LOGIC ───────────────────────────────────────────────────────
    protected override string GetShippingAddress(ErpDocument doc)
    {
        var c = doc.Company;

        return string.Join("\n", new[]
        {
            c.Name,
            c.Address,
            $"GSTIN : {c.GSTIN}",
            $"State : {c.StateCode}"
        }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    // ── CONTENT LAYOUT ───────────────────────────────────────────────────────
    /// <summary>
    /// PO layout = Items + Totals + Standalone Signatory (no bank section)
    /// </summary>
    protected override void ComposePdfContent(IContainer container, ErpDocument doc)
    {
        container.Column(col =>
        {
            col.Item().Element(c => ComposePdfItemsTable(c, doc));
            col.Item().Element(c => ComposePdfTotalsAndTerms(c, doc));

            // ── Standalone Signatory Block ───────────────────────────────────
            col.Item().PaddingTop(10).AlignRight().Column(c =>
            {
                c.Item().Height(40);

                c.Item().Text($"For : {doc.Company.Name}").Bold();

                if (!string.IsNullOrEmpty(doc.Company.Signatory))
                    c.Item().Text(doc.Company.Signatory);

                c.Item().Text(SignatoryLabel);
            });
        });
    }
}