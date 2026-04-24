using System.Linq;
using Ojaswat.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

public abstract partial class RendererBase
{
    // ── LABELS ───────────────────────────────────────────────────────────────
    protected virtual string ColItemNo       => "#";
    protected virtual string ColDescription  => "Item / Description";
    protected virtual string ColHsn          => "HSN/SAC (GST%)";
    protected virtual string ColQty          => "Qty";
    protected virtual string ColUnit         => "Unit";
    protected virtual string ColRate         => "Rate";
    protected virtual string ColGst          => "GST Amt";
    protected virtual string ColAmount       => "Amount";
    protected virtual string TotalLabel      => "TOTAL";

    // ── VISIBILITY (optional future use) ──────────────────────────────────────
    protected virtual bool ShowHsnColumn     => true;
    protected virtual bool ShowGstColumn     => true;

    // ── FORMATTING ────────────────────────────────────────────────────────────
    protected virtual string FormatCurrency(decimal val) => $"₹{val:N2}";
    protected virtual string FormatQty(decimal val)      => val.ToString("N2");

    // ── PDF ITEMS TABLE ───────────────────────────────────────────────────────
    protected virtual void ComposePdfItemsTable(IContainer container, ErpDocument doc)
    {
        const float cNo   = 20;
        const float cHsn  = 64;
        const float cQty  = 44;
        const float cUnit = 34;
        const float cRate = 68;
        const float cGst  = 66;
        const float cAmt  = 76;

        container.Table(table =>
        {
            table.ColumnsDefinition(col =>
            {
                col.ConstantColumn(cNo);
                col.RelativeColumn();

                if (ShowHsnColumn) col.ConstantColumn(cHsn);
                col.ConstantColumn(cQty);
                col.ConstantColumn(cUnit);
                col.ConstantColumn(cRate);

                if (ShowGstColumn) col.ConstantColumn(cGst);
                col.ConstantColumn(cAmt);
            });

            // ── HEADER ────────────────────────────────────────────────────────
            table.Header(hdr =>
            {
                void Hc(IContainer c, string txt, bool right = false, bool center = false)
                {
                    var bg = c.Background("#EEEEEE")
                               .BorderBottom(1f).BorderColor("#AAAAAA")
                               .BorderTop(0.5f).BorderColor("#CCCCCC")
                               .BorderLeft(0.4f).BorderColor("#CCCCCC")
                               .BorderRight(0.4f).BorderColor("#CCCCCC")
                               .PaddingVertical(6).PaddingHorizontal(5);

                    var aligned = right ? bg.AlignRight() : center ? bg.AlignCenter() : bg;

                    aligned.Text(txt)
                           .FontSize(7.5f).Bold()
                           .FontColor("#333333")
                           .LetterSpacing(0.02f);
                }

                Hc(hdr.Cell(), ColItemNo, center: true);
                Hc(hdr.Cell(), ColDescription);

                if (ShowHsnColumn)
                    Hc(hdr.Cell(), ColHsn, center: true);

                Hc(hdr.Cell(), ColQty, right: true);
                Hc(hdr.Cell(), ColUnit, center: true);
                Hc(hdr.Cell(), ColRate, right: true);

                if (ShowGstColumn)
                    Hc(hdr.Cell(), ColGst, right: true);

                Hc(hdr.Cell(), ColAmount, right: true);
            });

            // ── BODY ─────────────────────────────────────────────────────────
            bool alt = false;

            foreach (var item in doc.Items)
            {
                decimal gstAmt    = item.LineTotal * item.GSTPercent / 100;
                string hsnDisplay = $"({item.GSTPercent:0.##}%) {item.HSN}";
                string rowBg      = alt ? "#FAFAFA" : "#FFFFFF";
                alt = !alt;

                void Bc(IContainer c, string txt,
                        bool bold = false, bool right = false,
                        bool center = false, bool muted = false)
                {
                    var cell = c.Background(rowBg)
                                .BorderBottom(0.4f).BorderColor("#E0E0E0")
                                .BorderLeft(0.4f).BorderColor("#E8E8E8")
                                .BorderRight(0.4f).BorderColor("#E8E8E8")
                                .PaddingVertical(6).PaddingHorizontal(5);

                    var aligned = right ? cell.AlignRight() : center ? cell.AlignCenter() : cell;

                    var t = aligned.Text(txt).FontSize(9);

                    if (bold)  t.Bold();
                    if (muted) t.FontColor(Colors.Grey.Darken1);
                }

                Bc(table.Cell(), item.ItemNumber.ToString(), center: true, muted: true);
                Bc(table.Cell(), item.Description, bold: true);

                if (ShowHsnColumn)
                    Bc(table.Cell(), hsnDisplay, center: true, muted: true);

                Bc(table.Cell(), FormatQty(item.Quantity), right: true);
                Bc(table.Cell(), item.UOM, center: true, muted: true);
                Bc(table.Cell(), FormatCurrency(item.Rate), right: true);

                if (ShowGstColumn)
                    Bc(table.Cell(), FormatCurrency(gstAmt), right: true, muted: true);

                Bc(table.Cell(), FormatCurrency(item.LineTotal), bold: true, right: true);
            }

            // ── FOOTER TOTAL ─────────────────────────────────────────────────
            decimal qtyTotal = doc.Items.Sum(i => i.Quantity);
            decimal gstTotal = doc.Items.Sum(i => i.LineTotal * i.GSTPercent / 100);
            decimal amtTotal = doc.Items.Sum(i => i.LineTotal);

            void FcSpan(QuestPDF.Elements.Table.ITableCellContainer c, string txt, bool right, uint span)
            {
                var cell = c.ColumnSpan(span)
                    .Background("#F0F0F0")
                    .BorderTop(1f).BorderColor("#888888")
                    .BorderBottom(0.75f).BorderColor("#AAAAAA")
                    .BorderLeft(0.4f).BorderColor("#CCCCCC")
                    .BorderRight(0.4f).BorderColor("#CCCCCC")
                    .PaddingVertical(6).PaddingHorizontal(5);

                (right ? cell.AlignRight() : cell)
                    .Text(txt).FontSize(9).Bold().FontColor("#222222");
            }

            void Fc(IContainer c, string txt, bool right = false)
            {
                var cell = c
                    .Background("#F0F0F0")
                    .BorderTop(1f).BorderColor("#888888")
                    .BorderBottom(0.75f).BorderColor("#AAAAAA")
                    .BorderLeft(0.4f).BorderColor("#CCCCCC")
                    .BorderRight(0.4f).BorderColor("#CCCCCC")
                    .PaddingVertical(6).PaddingHorizontal(5);

                (right ? cell.AlignRight() : cell)
                    .Text(txt).FontSize(9).Bold().FontColor("#222222");
            }

            FcSpan(table.Cell(), TotalLabel, right: true, span: 3);
            Fc(table.Cell(), FormatQty(qtyTotal), right: true);
            Fc(table.Cell(), "");
            Fc(table.Cell(), "");

            if (ShowGstColumn)
                Fc(table.Cell(), FormatCurrency(gstTotal), right: true);

            Fc(table.Cell(), FormatCurrency(amtTotal), right: true);
        });
    }
}