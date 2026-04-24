namespace Ojaswat.Renderers;

/// <summary>
/// Central store for all design tokens used by the renderer system.
/// Change a value here and every renderer picks it up — no hunting through templates.
///
/// Colours are expressed as CSS hex strings so they work in both QuestPDF and HTML paths.
/// Numeric sizes use the unit appropriate for the consumer (px for HTML, pt/float for PDF).
/// </summary>
public static class InvoiceSettings
{
    // ── Brand colours ─────────────────────────────────────────────────────────

    /// <summary>Primary accent colour used for totals highlights and the HTML footer label.</summary>
    public static string ColorAccent   { get; set; } = "#1D4ED8";   // Indigo-700

    /// <summary>Dark text colour used for headings and monetary values.</summary>
    public static string ColorTextDark { get; set; } = "#111827";   // Gray-900

    /// <summary>Default border colour for subtle dividers.</summary>
    public static string ColorBorder   { get; set; } = "#D1D5DB";   // Gray-300

    /// <summary>Table header primary background (first row).</summary>
    public static string ColorTableHdr1 { get; set; } = "#EEEEEE";

    /// <summary>Table header secondary / alternating background.</summary>
    public static string ColorTableHdr2 { get; set; } = "#F5F5F5";

    // ── Typography ────────────────────────────────────────────────────────────

    /// <summary>Small label font size (pt / px depending on context).</summary>
    public static int FontSizeSmall { get; set; } = 7;

    /// <summary>Meta / secondary information font size.</summary>
    public static int FontSizeMeta  { get; set; } = 9;

    // ── Top accent stripe (HTML only) ─────────────────────────────────────────

    /// <summary>Height of the top colour stripe in pixels.</summary>
    public static int StripeHeight { get; set; } = 5;

    /// <summary>CSS gradient value for the top accent stripe.</summary>
    public static string StripeGradient { get; set; } =
        "linear-gradient(90deg,#1D4ED8 0%,#3B82F6 50%,#1D4ED8 100%)";

    // ── Company logo (HTML only) ──────────────────────────────────────────────

    /// <summary>Maximum height of the embedded logo image in pixels.</summary>
    public static int LogoHeight { get; set; } = 60;

    /// <summary>Maximum width of the embedded logo image in pixels.</summary>
    public static int LogoWidth  { get; set; } = 160;
}
