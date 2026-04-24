using Ojaswat.Models;

namespace Ojaswat.Renderers;

/// <summary>
/// Tax Invoice — shows E-Way Bill field when populated.
/// Everything else uses DefaultRenderer / RendererBase defaults.
/// </summary>
public sealed class SalesInvoiceRenderer : DefaultRenderer
{
    protected override bool ShowEWayBill => true;
    
}
