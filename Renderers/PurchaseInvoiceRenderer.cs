using Ojaswat.Models;

namespace Ojaswat.Renderers;

/// <summary>
/// Purchase Invoice — vendor-centric labels, E-Way Bill displayed when available.
/// Bank section visible (default) since payment terms apply.
/// </summary>
public sealed class PurchaseInvoiceRenderer : DefaultRenderer
{
    protected override string BillToLabel => "Vendor";
    protected override string ShipToLabel => "Deliver To";
    protected override bool   ShowEWayBill => true;
}
