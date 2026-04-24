using Ojaswat.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

/// <summary>
/// Concrete default renderer used as the base for all document-specific renderers.
/// Wires QuestPDF page structure; all section content is delegated to RendererBase partials.
/// </summary>
public class DefaultRenderer : RendererBase
{
    public override IDocument BuildPdf(ErpDocument doc)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                page.PageColor(Colors.White);

                page.DefaultTextStyle(x =>
                    x.FontSize(10).FontFamily(Fonts.Arial).FontColor(Colors.Black));

                // 1. REPEATING HEADER — letterhead + address grid (repeats on every page)
                page.Header().Element(c => ComposePdfHeader(c, doc));

                // 2. MAIN CONTENT — items table, totals, bank & signatory
                page.Content().Element(c => ComposePdfContent(c, doc));

                // 3. FOOTER — timestamp | doc label | page number
                page.Footer().Element(c => ComposePdfFooter(c, doc));
            });
        });
    }
}
