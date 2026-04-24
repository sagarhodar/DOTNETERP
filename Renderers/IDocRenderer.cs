using Ojaswat.Models;
using QuestPDF.Infrastructure;

namespace Ojaswat.Renderers;

public interface IDocRenderer
{
    string    BuildHtml(ErpDocument doc);
    IDocument BuildPdf(ErpDocument doc);
}
