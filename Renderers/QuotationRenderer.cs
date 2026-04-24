using Ojaswat.Models;

namespace Ojaswat.Renderers;

/// <summary>
/// Quotation — no bank / payment section, totals labelled as "Quotation Total".
/// </summary>
public sealed class QuotationRenderer : DefaultRenderer
{
    protected override bool   ShowBankSection => false;
    protected override string TotalsLabel     => "Quotation Total";
}
