using Ojaswat.Models;

namespace Ojaswat.Renderers;

/// <summary>
/// Resolves the correct <see cref="IDocRenderer"/> for a given <see cref="DocumentType"/>.
///
/// Usage:
/// <code>
///     IDocRenderer renderer = RendererFactory.For(doc.DocType);
///     IDocument    pdf      = renderer.BuildPdf(doc);
///     string       html     = renderer.BuildHtml(doc);
/// </code>
///
/// To register a new document type:
///   1. Create a class that extends <see cref="DefaultRenderer"/>.
///   2. Add a case for its <see cref="DocumentType"/> value below.
/// </summary>
public static class RendererFactory
{
    /// <summary>
    /// Returns a fresh renderer instance for the given document type.
    /// Renderers are stateless so a new instance per call is safe and cheap.
    /// </summary>
    public static IDocRenderer For(DocumentType docType) => docType switch
    {
        DocumentType.SalesInvoice    => new SalesInvoiceRenderer(),
        DocumentType.SalesOrder      => new SalesOrderRenderer(),
        DocumentType.Quotation       => new QuotationRenderer(),
        DocumentType.PurchaseOrder   => new PurchaseOrderRenderer(),
        DocumentType.PurchaseInvoice => new PurchaseInvoiceRenderer(),
        DocumentType.CreditNote      => new CreditNoteRenderer(),
        DocumentType.DebitNote       => new DebitNoteRenderer(),
        DocumentType.GRN             => new GrnRenderer(),
        _                            => new DefaultRenderer(),
    };

    /// <summary>
    /// Convenience overload — resolves the renderer from the document itself.
    /// </summary>
    public static IDocRenderer For(ErpDocument doc) => For(doc.DocType);
}
