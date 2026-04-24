using System;
using Ojaswat.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

public abstract partial class RendererBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  PDF FOOTER
    //  Registered via page.Footer() in DefaultRenderer.BuildPdf().
    //  Override ComposePdfFooter to replace the entire footer row.
    //  Override individual Build*FooterCell methods for targeted changes.
    // ══════════════════════════════════════════════════════════════════════════

    protected virtual void ComposePdfFooter(IContainer container, ErpDocument doc)
    {
        container.PaddingTop(5).Row(row =>
        {
            row.RelativeItem()
               .Element(c => ComposePdfFooterLeft(c, doc));

            row.RelativeItem().AlignCenter()
               .Element(c => ComposePdfFooterCenter(c, doc));

            row.RelativeItem().AlignRight()
               .Element(c => ComposePdfFooterRight(c, doc));
        });
    }

    // ── Left cell: generated timestamp ───────────────────────────────────────
    protected virtual void ComposePdfFooterLeft(IContainer container, ErpDocument doc)
    {
        container
            .Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
            .FontSize(8).FontColor(Colors.Grey.Medium);
    }

    // ── Centre cell: document type + document number ──────────────────────────
    protected virtual void ComposePdfFooterCenter(IContainer container, ErpDocument doc)
    {
        container
            .Text($"{DocLabel(doc.DocType)} · {CleanDocNo(doc.DocumentNo)}")
            .FontSize(8).Bold();
    }

    // ── Right cell: page counter ──────────────────────────────────────────────
    protected virtual void ComposePdfFooterRight(IContainer container, ErpDocument doc)
    {
        container.Text(text =>
        {
            text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }
}
