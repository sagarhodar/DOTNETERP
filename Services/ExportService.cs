using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ojaswat.Models;
using Ojaswat.Renderers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Ojaswat.Services;

public class ExportService
{
    private static readonly Dictionary<DocumentType, IDocRenderer> _renderers = new()
    {
        [DocumentType.SalesInvoice]    = new SalesInvoiceRenderer(),
        [DocumentType.Quotation]       = new QuotationRenderer(),
        [DocumentType.SalesOrder]      = new SalesOrderRenderer(),
        [DocumentType.PurchaseOrder]   = new PurchaseOrderRenderer(),
        [DocumentType.PurchaseInvoice] = new PurchaseInvoiceRenderer(),
        [DocumentType.CreditNote]      = new CreditNoteRenderer(),
        [DocumentType.DebitNote]       = new DebitNoteRenderer(),
        [DocumentType.GRN]             = new GrnRenderer(),
    };

    private static readonly DefaultRenderer _fallback = new();

    static ExportService()
    {
        // Required by QuestPDF v202X+
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static IDocRenderer GetRenderer(DocumentType t) =>
        _renderers.GetValueOrDefault(t, _fallback);

    public string ExportHtml(ErpDocument doc, string? outputPath = null, string? customHtml = null)
    {
        string html = ResolveHtml(doc, customHtml);
        string path = outputPath ?? DefaultHtmlPath(doc.DocumentNo);
        EnsureDir(path);
        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }

    public string ExportPdf(ErpDocument doc, string? pdfPath = null, string? customHtml = null)
    {
        string outPdf = pdfPath ?? DefaultPdfPath(doc.DocumentNo);
        EnsureDir(outPdf);

        // If custom HTML is provided, we can't use QuestPDF for it. 
        // In a real scenario, you'd phase custom HTML out. 
        // For now, we always use the robust native QuestPDF renderer.
        var pdfDoc = GetRenderer(doc.DocType).BuildPdf(doc);
        pdfDoc.GeneratePdf(outPdf);

        return outPdf;
    }

    private static string ResolveHtml(ErpDocument doc, string? customHtml)
    {
        if (!string.IsNullOrEmpty(customHtml) &&
            customHtml.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            return customHtml;
        return GetRenderer(doc.DocType).BuildHtml(doc);
    }

    private static string ExportFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Ojaswat", "Exports");

    public static string SanitiseName(string s) =>
        string.Concat(s.Split(Path.GetInvalidFileNameChars()));

    private static string DefaultHtmlPath(string docNo) =>
        Path.Combine(ExportFolder, $"{SanitiseName(docNo)}.html");

    private static string DefaultPdfPath(string docNo) =>
        Path.Combine(ExportFolder, $"{SanitiseName(docNo)}.pdf");

    private static void EnsureDir(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            try { Directory.CreateDirectory(dir); } catch { /* ignore */ }
    }
}