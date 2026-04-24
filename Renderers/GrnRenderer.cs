using Ojaswat.Models;

namespace Ojaswat.Renderers;

/// <summary>
/// Goods Receipt Note — supplier / warehouse labels, no bank section.
/// </summary>
public sealed class GrnRenderer : DefaultRenderer
{
    protected override string BillToLabel     => "Supplier";
    protected override string ShipToLabel     => "Received At";
    protected override string DocumentNoLabel => "GRN No. :";
    protected override bool   ShowBankSection => false;
}
