using System.IO;
using System.Linq;
using Ojaswat.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

public abstract partial class RendererBase
{
    // ── LABELS ───────────────────────────────────────────────────────────────
    protected virtual string SubtotalLabel => "SUB TOTAL";
    protected virtual string CgstLabel => "CGST";
    protected virtual string SgstLabel => "SGST";
    protected virtual string IgstLabel => "IGST";
    protected virtual string FreightLabel => "Freight";
    protected virtual string DiscountLabel => "Discount";
    protected virtual string TotalAmountLabel => "Total";

    protected virtual string AmountInWordsLabel => "AMOUNT IN WORDS";
    protected virtual string PaymentTermsLabel => "PAYMENT TERMS";
    protected virtual string TermsLabel => "TERMS & CONDITIONS";

    protected virtual string BankDetailsLabel => "BANK DETAILS";
    protected virtual string BankNameLabel => "Bank Name";
    protected virtual string AccountNoLabel => "Account No.";
    protected virtual string IfscLabel => "IFSC Code";
    protected virtual string AccountHolderLabel => "Account Holder";

    // ── TOTALS + TERMS ────────────────────────────────────────────────────────
    protected virtual void ComposePdfTotalsAndTerms(IContainer container, ErpDocument doc)
    {
        decimal sub       = doc.Items.Sum(i => i.LineTotal);
        decimal totalCgst = 0, totalSgst = 0, totalIgst = 0;

        foreach (var item in doc.Items)
        {
            decimal tax = item.LineTotal * item.GSTPercent / 100;

            if (doc.GstMode == GstMode.CgstSgst)
            {
                totalCgst += tax / 2;
                totalSgst += tax / 2;
            }
            else totalIgst += tax;
        }

        decimal totalTax = totalCgst + totalSgst + totalIgst;
        decimal grand    = sub + totalTax + doc.Freight - doc.Discount;

        container
            .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Row(row =>
            {
                // ── LEFT SIDE ────────────────────────────────────────────────
                row.RelativeItem()
                   .BorderRight(0.75f).BorderColor(Colors.Grey.Lighten2)
                   .Padding(10).Column(col =>
                {
                    if (ShowAmountInWords)
                    {
                        col.Item().PaddingBottom(4)
                           .Text(AmountInWordsLabel).FontSize(8).Bold();

                        col.Item().Text(AmountInWords(grand))
                           .FontSize(9).Italic();
                    }

                    // ✅ Payment Terms (your addition)
                    if (ShowPaymentTerms && !string.IsNullOrWhiteSpace(doc.PaymentTerms))
                    {
                        col.Item().PaddingTop(10).PaddingBottom(4)
                           .Text(PaymentTermsLabel).FontSize(8).Bold();

                        col.Item().Text(doc.PaymentTerms).FontSize(9);
                    }

                    if (ShowGeneralTerms && !string.IsNullOrWhiteSpace(doc.GeneralTerms))
                    {
                        col.Item().PaddingTop(10).PaddingBottom(4)
                           .Text(TermsLabel).FontSize(8).Bold();

                        col.Item().Text(doc.GeneralTerms).FontSize(9);
                    }
                });

                // ── RIGHT SIDE ───────────────────────────────────────────────
                row.ConstantItem(210).PaddingVertical(10).Column(c =>
                {
                    void Line(string label, decimal value, bool highlight = false)
                    {
                        c.Item().PaddingVertical(2).PaddingHorizontal(5)
                         .BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2)
                         .Row(r =>
                         {
                             r.RelativeItem().Text(label).FontSize(9)
                              .FontColor(highlight ? Colors.Green.Darken2 : Colors.Grey.Darken2);

                             r.RelativeItem().AlignRight()
                              .Text($"₹{value:N2}")
                              .FontSize(9).Bold()
                              .FontColor(highlight ? Colors.Green.Darken2 : Colors.Black);
                         });
                    }

                    Line(SubtotalLabel, sub);

                    if (doc.GstMode == GstMode.CgstSgst)
                    {
                        Line(CgstLabel, totalCgst);
                        Line(SgstLabel, totalSgst);
                    }
                    else
                    {
                        Line(IgstLabel, totalIgst);
                    }

                    if (doc.Freight > 0)
                        Line(FreightLabel, doc.Freight);

                    if (doc.Discount > 0)
                        Line(DiscountLabel, doc.Discount, highlight: true);

                    c.Item().PaddingTop(6)
                     .Background("#F5F5F5")
                     .Border(0.75f).BorderColor(Colors.Grey.Lighten2)
                     .PaddingVertical(7).PaddingHorizontal(5)
                     .Row(r =>  
                     {
                         r.RelativeItem()
                          .Text(TotalAmountLabel.ToUpper())
                          .FontSize(10).Bold();

                         r.RelativeItem().AlignRight()
                          .Text($"₹{grand:N2}")
                          .FontSize(10).Bold();
                     });
                });
            });
    }

    // ── BANK + SIGNATORY ─────────────────────────────────────────────────────
    protected virtual void ComposePdfBankAndSign(IContainer container, ErpDocument doc)
    {
        bool hasBank = !string.IsNullOrWhiteSpace(doc.Company.Bank);
        bool hasQr   = !string.IsNullOrEmpty(doc.Company.QRCodePath) && File.Exists(doc.Company.QRCodePath);

        container
            .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Row(row =>
        {
            // ── LEFT: QR + BANK ─────────────────────────────────────────────
            row.RelativeItem()
            .BorderRight(0.75f).BorderColor(Colors.Grey.Lighten2)
            .Padding(10).Row(inner =>
            {
                if (hasQr)
                {
                    try
                    {
                        byte[] qrBytes = File.ReadAllBytes(doc.Company.QRCodePath);
                        inner.ConstantItem(70)
                             .Height(65).Width(65).Image(qrBytes).FitArea();
                    }
                    catch { }
                }

                if (hasBank)
                {
                    inner.RelativeItem().PaddingLeft(hasQr ? 10 : 0).Column(c =>
                    {
                        c.Item().PaddingBottom(4)
                         .Text(BankDetailsLabel)
                         .FontSize(7).Bold().FontColor(Colors.Grey.Medium).LetterSpacing(0.07f);

                        void BL(string lbl, string val) =>
                            c.Item().PaddingTop(2)
                             .BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2)
                             .Row(r =>
                             {
                                 r.ConstantItem(95).Text(lbl)
                                  .FontSize(8.5f).FontColor(Colors.Grey.Darken1);

                                 r.RelativeItem().Text(val)
                                  .FontSize(8.5f).Bold();
                             });

                        BL(BankNameLabel, doc.Company.Bank);
                        BL(AccountNoLabel, doc.Company.Account);
                        BL(IfscLabel, doc.Company.IFSC);
                        BL(AccountHolderLabel, doc.Company.Name);
                    });
                }
            });

            // ── RIGHT: SIGNATORY ─────────────────────────────────────────────
            if (ShowSignatory)
            {
                row.ConstantItem(210)
                   .Padding(10)
                   .AlignRight().Column(c =>
                {
                    c.Item().Height(45);

                    c.Item().PaddingTop(6).AlignRight()
                     .Text($"For : {doc.Company.Name}").FontSize(9).Bold();

                    if (!string.IsNullOrEmpty(doc.Company.Signatory))
                        c.Item().AlignRight()
                         .Text(doc.Company.Signatory)
                         .FontSize(8).FontColor(Colors.Grey.Darken1);

                    c.Item().AlignRight()
                     .Text(SignatoryLabel)
                     .FontSize(7.5f).FontColor(Colors.Grey.Medium);
                });
            }
        });
    }
}