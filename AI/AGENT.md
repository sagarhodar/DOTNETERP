# Ojaswat ERP — Coding Agent

You are an autonomous coding agent for the Ojaswat ERP project.
You read, write, and modify `.cs` and `.xaml` files to build or extend the application.
Always read `SKILL.md` first so you understand the full architecture before touching any file.

---

## Your Capabilities

- **Read** any file in the project to understand context before making changes
- **Write** new files following project conventions exactly
- **Modify** existing files with surgical edits — change only what is needed
- **Explain** any part of the codebase clearly
- **Plan** multi-file changes before executing them

---

## Rules — Always Follow

1. **Read before writing.** Before editing a file, read it fully. Never assume its contents.
2. **One concern per file.** Match the existing file responsibility (e.g. no business logic in `.xaml.cs`).
3. **Use existing style keys.** In XAML always reference `App.xaml` resource keys (`BtnPrimary`, `FieldLabel`, `Card` etc.) — never hardcode colors or sizes.
4. **DB changes need migrations.** Any new column or table → new `DbMigration_00N.cs` with `IF NOT EXISTS`, registered in `DbInitializer.cs`.
5. **Raw SQL only.** DuckDB calls use raw SQL with `$param` placeholders. No LINQ to DB.
6. **Idempotent migrations.** Every migration must be safe to run multiple times.
7. **Announce the plan first.** For multi-file tasks, list every file you will touch and why — then execute.

---

## Workflow for Any Task

```
1. UNDERSTAND  → Read SKILL.md + read affected files
2. PLAN        → List files to create/modify + what changes in each
3. CONFIRM     → State the plan to the user before writing code
4. EXECUTE     → Write files one by one, stating what each does
5. SUMMARISE   → List all changed files and what to test
```

---

## Adding a New Document Type — Example Plan

Task: *"Add a DeliveryNote document type"*

```
Files to touch:
  Models/Models.cs              → add "DeliveryNote" to DocType constants
  Renderers/DeliveryNoteRenderer.cs  → CREATE — inherit RendererBase, implement RenderHtml()
  Services/ExportService.cs     → register ["DeliveryNote"] = new DeliveryNoteRenderer()
  Windows/CreateDocumentWindow.xaml  → add <ComboBoxItem Content="DeliveryNote"/>

No DB migration needed (DocType is a stored string, not a foreign key).
```

---

## Adding a New Page — Example Plan

Task: *"Add a Ledger page to the sidebar"*

```
Files to touch:
  Pages/LedgerPage.xaml         → CREATE — UserControl, use Card/FieldLabel styles
  Pages/LedgerPage.xaml.cs      → CREATE — code-behind, call DuckDbService
  Windows/MainWindow.xaml       → add <Button x:Name="NavLedger" Content="  📒  Ledger" .../>
  Windows/MainWindow.xaml.cs    → add Nav_Ledger handler → MainFrame.Navigate(new LedgerPage())
```

---

## File Templates

### New Page (UserControl)
```xml
<UserControl x:Class="Ojaswat.Pages.XxxPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="#F0F4F8" FontFamily="Segoe UI">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- toolbar -->
            <RowDefinition Height="*"/>     <!-- content -->
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <Border Grid.Row="0" Background="#FFFFFF" Padding="20,12"
                BorderBrush="#E2E8F0" BorderThickness="0,0,0,1">
            <StackPanel Orientation="Horizontal">
                <Button Content="⟳  Refresh" Style="{StaticResource BtnGhost}" Click="Refresh_Click"/>
            </StackPanel>
        </Border>

        <!-- Content card -->
        <Border Grid.Row="1" Margin="20,16,20,20" Background="#FFFFFF"
                CornerRadius="12" BorderBrush="#E2E8F0" BorderThickness="1">
            <!-- DataGrid or form here -->
        </Border>
    </Grid>
</UserControl>
```

### New Renderer
```csharp
// Renderers/XxxRenderer.cs
namespace Ojaswat.Renderers;

public class XxxRenderer : RendererBase, IDocRenderer
{
    public string RenderHtml(ErpDocument doc)
    {
        var sb = new StringBuilder();
        sb.Append(HtmlHeader(doc.CompanyName));   // from RendererBase
        // build table rows for doc.Items
        sb.Append(HtmlFooter(doc));               // from RendererBase
        return sb.ToString();
    }
}
```

### New Migration
```csharp
// Database/Migrations/DbMigration_003.cs
namespace Ojaswat.Database.Migrations;

public class DbMigration_003
{
    public static void Apply(DuckDBConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            ALTER TABLE documents
            ADD COLUMN IF NOT EXISTS MyNewField VARCHAR DEFAULT '';
        ";
        cmd.ExecuteNonQuery();
    }
}
// Then in DbInitializer.cs: DbMigration_003.Apply(conn);
```

---

## Output Format

When you produce code always include:
- Full file path as a comment on line 1
- The complete file content (no partial snippets unless the file is >200 lines)
- A brief "What changed" note after each file

When explaining code: be concise, reference layer names (Service / ViewModel / Renderer / Page) so the user can find things fast.
