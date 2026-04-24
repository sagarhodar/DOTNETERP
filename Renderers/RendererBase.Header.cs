using System.IO;
using Ojaswat.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

public abstract partial class RendererBase
{
    // ── LABELS ───────────────────────────────────────────────────────────────
    protected virtual string DateLabel => "Date";
    protected virtual string PlaceOfSupplyLabel => "Place of Supply";
    protected virtual string CustomerRefLabel => "PO Ref No";
    protected virtual string EWayBillLabel => "E-Way Bill No";
    protected virtual string PreparedByLabel => "Prepared by";

    // ── VISIBILITY ───────────────────────────────────────────────────────────
    protected virtual bool ShowCustomerRef => true;
    protected virtual bool ShowPreparedBy => true;

    // ── PDF HEADER ───────────────────────────────────────────────────────────
    protected virtual void ComposePdfHeader(IContainer container, ErpDocument doc)
    {
        container.Column(col =>
        {
            // ── Letterhead row ───────────────────────────────────────────────
            col.Item().PaddingBottom(6).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(doc.Company.Name)
                        .FontSize(12).Bold().LetterSpacing(0.03f);

                    if (!string.IsNullOrWhiteSpace(doc.Company.Address))
                        c.Item().PaddingTop(3).Text(doc.Company.Address)
                            .FontSize(7).FontColor(Colors.Grey.Darken1).LineHeight(1.5f);

                    c.Item().PaddingTop(3).Column(meta =>
                    {
                        void M(string t) =>
                            meta.Item().Text(t).FontSize(7)
                                .FontColor(Colors.Grey.Darken1).LineHeight(1.4f);

                        if (!string.IsNullOrWhiteSpace(doc.Company.GSTIN))
                            M($"GSTIN : {doc.Company.GSTIN}");

                        string stateLine = "";
                        if (!string.IsNullOrWhiteSpace(doc.Company.StateCode))
                            stateLine += $"State : {doc.Company.StateCode}";
                        if (!string.IsNullOrWhiteSpace(doc.Company.Phone))
                            stateLine += (stateLine.Length > 0 ? "    |    " : "") + $"Ph : {doc.Company.Phone}";
                        if (stateLine.Length > 0) M(stateLine);

                        if (!string.IsNullOrWhiteSpace(doc.Company.Email))
                            M($"Email : {doc.Company.Email}");
                    });
                });

                if (!string.IsNullOrEmpty(doc.Company.LogoPath) && File.Exists(doc.Company.LogoPath))
                {
                    try
                    {
                        byte[] lb = File.ReadAllBytes(doc.Company.LogoPath);
                        row.ConstantItem(90).AlignRight().AlignMiddle()
                           .Height(65).Image(lb).FitArea();
                    }
                    catch { }
                }
            });

            // ── Doc-type banner ──────────────────────────────────────────────
            col.Item().Element(c => StyledBlock(c, DocLabel(doc.DocType)));

            // ── 3-column layout ──────────────────────────────────────────────
            col.Item().PaddingTop(6).PaddingBottom(4)
               .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
               .Row(row =>
               {
                   ComposeHeaderBillTo(row, doc);
                   if (ShowShipToSection) ComposeHeaderShipTo(row, doc);
                   ComposeHeaderDetails(row, doc);
               });

            // col.Item().LineHorizontal(0.75f).LineColor(Colors.Grey.Darken1);
        });
    }

    // ── Bill-To ──────────────────────────────────────────────────────────────
    protected virtual void ComposeHeaderBillTo(RowDescriptor row, ErpDocument doc)
    {
        row.RelativeItem()
           .BorderRight(0.5f).BorderColor(Colors.Grey.Lighten2)
           .Padding(8).Column(c =>
           {
               c.Item().PaddingBottom(3)
                .Text(BillToLabel.ToUpper())
                .FontSize(7).Bold().FontColor(Colors.Grey.Medium).LetterSpacing(0.08f);

               c.Item().Text(doc.Customer.Name)
                .FontSize(10).Bold();

               c.Item().PaddingTop(2).Text(doc.Customer.BillingAddress)
                .FontSize(8.5f).FontColor(Colors.Grey.Darken1).LineHeight(1.5f);

               if (!string.IsNullOrEmpty(doc.Customer.GSTIN))
                   c.Item().PaddingTop(3).Text($"GSTIN : {doc.Customer.GSTIN}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);

               if (!string.IsNullOrEmpty(doc.Customer.StateCode))
                   c.Item().Text($"State : {doc.Customer.StateCode}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
           });
    }

    // ── Ship-To ──────────────────────────────────────────────────────────────
    protected virtual void ComposeHeaderShipTo(RowDescriptor row, ErpDocument doc)
    {
        string shipAddress = GetShippingAddress(doc);

        // 👉 fallback to Bill-To if Ship-To is empty
        if (string.IsNullOrWhiteSpace(shipAddress))
            shipAddress = doc.Customer.BillingAddress;

        row.RelativeItem()
        .BorderRight(0.5f).BorderColor(Colors.Grey.Lighten2)
        .Padding(8).Column(c =>
        {
            c.Item().PaddingBottom(3)
                .Text(ShipToLabel.ToUpper())
                .FontSize(7).Bold().FontColor(Colors.Grey.Medium).LetterSpacing(0.08f);

            c.Item().Text(shipAddress)
                .FontSize(6.5f).FontColor(Colors.Grey.Darken1).LineHeight(1.5f);
        });
    }

    // ── Details column ───────────────────────────────────────────────────────
    protected virtual void ComposeHeaderDetails(RowDescriptor row, ErpDocument doc)
    {
        row.RelativeItem().Padding(8).Column(c =>
        {
            c.Item().PaddingBottom(3)
             .Text(DetailsColumnLabel)
             .FontSize(7).Bold().FontColor(Colors.Grey.Medium).LetterSpacing(0.08f);

            void AddRow(string lbl, string val) =>
                c.Item().PaddingBottom(2)
                 .BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2)
                 .Row(r =>
                 {
                     r.RelativeItem().Text(lbl).FontSize(8).FontColor(Colors.Grey.Darken1);
                     r.RelativeItem().AlignRight().Text(val).FontSize(8).Bold();
                 });

            AddRow(DocumentNoLabel, CleanDocNo(doc.DocumentNo));
            AddRow(DateLabel, doc.Date.ToString("dd-MM-yyyy"));

            if (!string.IsNullOrEmpty(doc.PlaceOfSupply))
                AddRow(PlaceOfSupplyLabel, doc.PlaceOfSupply);

            if (ShowCustomerRef && !string.IsNullOrEmpty(doc.CustomerRefNo))
                AddRow(CustomerRefLabel, doc.CustomerRefNo);

            if (ShowEWayBill && !string.IsNullOrEmpty(doc.EWayBillNo))
                AddRow(EWayBillLabel, doc.EWayBillNo);

            if (ShowPreparedBy && !string.IsNullOrEmpty(doc.CreatedBy))
                AddRow(PreparedByLabel, doc.CreatedBy);
        });
    }
}