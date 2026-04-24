using Ojaswat.Models;

namespace Ojaswat.Renderers;

/// <summary>
/// Sales Order — no E-Way Bill, no bank / signatory section.
/// Title is driven by DocLabel("SalesOrder") → "Sales Order".
/// </summary>
public sealed class SalesOrderRenderer : DefaultRenderer
{
    protected override bool ShowEWayBill    => false;
    protected override bool ShowBankSection => false;
}
