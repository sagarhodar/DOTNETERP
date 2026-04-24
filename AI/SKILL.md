# SKILL.md — InvoiceApp_V33 (Ojaswat ERP)
> Read this before writing any code for this project.
> It documents conventions, file locations, and exact patterns to follow.

---

## 1. Project Map (quick reference)

```
InvoiceApp_V33/
├── Database/
│   ├── DbInitializer.cs          ← runs migrations in order
│   └── Migrations/
│       ├── DbMigration_001.cs    ← baseline schema
│       ├── DbMigration_002.cs    ← placeholder (empty)
│       └── DbMigration_003.cs    ← Inventory + PartyLedger columns
├── Models/
│   ├── Models.cs                 ← ALL domain enums + classes
│   └── ViewModels/
│       ├── MainViewModel.cs      ← nav commands, AllDocs, stats
│       ├── DocListViewModel.cs   ← filter/search for document list
│       ├── DocumentViewModel.cs  ← form logic for CreateDocumentWindow
│       ├── FinanceViewModel.cs   ← payment list
│       ├── MasterDataViewModel.cs← customer + item CRUD wrappers
│       └── RelayCommand.cs       ← ICommand implementation
├── Services/
│   ├── DuckDbService.cs          ← connection factory only
│   ├── ErpDocumentDbService.cs   ← all DB CRUD; calls InventoryService
│   ├── InventoryService.cs       ← StockLedger + PartyLedger logic
│   ├── DocumentNumberService.cs  ← doc-number generation
│   ├── ExportService.cs          ← PDF + HTML export via renderers
│   └── CsvService.cs             ← CSV import/export
├── Pages/                        ← UserControls (one per module)
├── Windows/                      ← modal/top-level windows
├── Renderers/                    ← IDocRenderer + per-type impls
├── Resources/
│   ├── Colors.xaml
│   └── Styles.xaml               ← BtnPrimary, NavRadioBtn, StatCard…
├── App.xaml / App.xaml.cs        ← App.VM static locator
├── InvoiceSettings.cs            ← compile-time defaults
└── PageTemplateSelector.cs       ← routes CurrentPageKey → DataTemplate
```

---

## 2. Core Conventions

### 2.1 DataContext / VM access
Pages live inside a `ContentControl` whose `Content` is a **string key**
(e.g. `"dash"`), so `DataContext` inside a page is that string — NOT the VM.

**Rule:** every Page/UserControl reads the VM via the static locator:
```csharp
private static MainViewModel VM => App.VM;
```
Never use `DataContext` inside pages.

### 2.2 Navigation
All navigation lives in `MainViewModel`. Pattern:
```csharp
// Add command field
public ICommand NavXyzCommand { get; }

// Wire in constructor
NavXyzCommand = NavCmd("xyzkey", "Page Title");

// Register DataTemplate in MainWindow.xaml
<DataTemplate x:Key="DT_xyzkey"> <pages:XyzPage/> </DataTemplate>
```
`NavCmd` clears pending filters then sets `CurrentPageKey`.

### 2.3 Migrations
Add each schema change as a **new** migration class. Never edit existing ones.
```
DbMigration_004.cs  ← next available number
```
Register it in `DbInitializer.cs`:
```csharp
RunMigration(conn, 4, DbMigration_004.Up);
```
Use `TryAlter` for `ALTER TABLE` (DuckDB has no `IF NOT EXISTS` on ALTER).

### 2.4 CRUD pattern in ErpDocumentDbService
```csharp
// Load
using var conn = _db.GetConnection(); conn.Open();
using var r = Cmd(conn, "SELECT … FROM …").ExecuteReader();

// Save (insert or update)
if (entity.Id == 0) { entity.Id = GetNextId(conn, "TableName"); /* INSERT */ }
else { /* UPDATE */ }
```
Always use parameterised `Cmd(conn, sql, params object[] parms)` — never string interpolation.

### 2.5 Button / Style reuse
Styles are defined in `Resources/Styles.xaml` and available app-wide:
- `BtnPrimary` — blue
- `BtnGhost`   — light grey
- `BtnGreen`   — green
- `BtnRed`     — red
- `NavRadioBtn` / `SubNavRadioBtn` — sidebar radio buttons
- `StatCard`, `CardBorder`, `SectionBorder` — layout containers

---

## 3. Feature A — Separate Customers and Suppliers Tables

### 3.1 Goal
Replace the single `Customers` table (with a `PartyType` column) with two
dedicated tables: **`Customers`** and **`Suppliers`**.

- `Customers` → used on Sales documents (Quotation, SalesOrder, SalesInvoice, CreditNote)
- `Suppliers` → used on Purchase documents (PurchaseOrder, PurchaseInvoice, GRN, DebitNote)
- Both tables share the same shape (same columns as current `Customers` minus `PartyType`)
- `PartyLedger` keeps a `PartyType` TEXT column (`'Customer'` or `'Supplier'`)

### 3.2 Migration (DbMigration_004.cs)
```csharp
public static class DbMigration_004
{
    public static void Up(DuckDBConnection conn)
    {
        // 1. Create Suppliers table (mirrors Customers)
        Exec(conn, @"CREATE TABLE IF NOT EXISTS Suppliers (
            Id                 INTEGER PRIMARY KEY,
            Name               TEXT    NOT NULL DEFAULT '',
            GSTIN              TEXT    DEFAULT '',
            BillingAddress     TEXT    DEFAULT '',
            ShippingAddress    TEXT    DEFAULT '',
            StateCode          TEXT    DEFAULT '',
            OpeningBalance     DECIMAL(18,2) DEFAULT 0,
            OpeningBalanceDate TEXT    DEFAULT '');");

        // 2. Migrate existing Vendor rows from Customers → Suppliers
        Exec(conn, @"INSERT INTO Suppliers (Id,Name,GSTIN,BillingAddress,ShippingAddress,StateCode,OpeningBalance,OpeningBalanceDate)
            SELECT Id,Name,GSTIN,BillingAddress,ShippingAddress,StateCode,
                   COALESCE(OpeningBalance,0), COALESCE(OpeningBalanceDate,'')
            FROM Customers
            WHERE COALESCE(PartyType,'Customer') IN ('Vendor','Supplier')
            ON CONFLICT DO NOTHING;");

        // 3. Remove migrated rows from Customers
        TryExec(conn, "DELETE FROM Customers WHERE COALESCE(PartyType,'Customer') IN ('Vendor','Supplier');");

        // 4. Drop PartyType column from Customers (best-effort — DuckDB may not support DROP COLUMN)
        TryExec(conn, "ALTER TABLE Customers DROP COLUMN PartyType;");
    }

    private static void Exec(DuckDBConnection conn, string sql)
    { var c = conn.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); }

    private static void TryExec(DuckDBConnection conn, string sql)
    { try { Exec(conn, sql); } catch { } }
}
```

### 3.3 New Model class — Supplier
Add to `Models/Models.cs` alongside `Customer`:
```csharp
public class Supplier
{
    public int     Id                  { get; set; }
    public string  Name                { get; set; } = "";
    public string  GSTIN               { get; set; } = "";
    public string  BillingAddress      { get; set; } = "";
    public string  ShippingAddress     { get; set; } = "";
    public string  StateCode           { get; set; } = "";
    public decimal OpeningBalance      { get; set; } = 0;
    public string  OpeningBalanceDate  { get; set; } = "";
}
```

Remove `PartyType` and `OpeningBalance` from `Customer` (they move to Supplier;
`Customer` keeps `OpeningBalance` for receivables tracking).

### 3.4 ErpDocumentDbService — new Supplier CRUD methods
Add four methods mirroring the Customer ones:
```csharp
public List<Supplier> LoadSuppliers() { … }
public void SaveSupplier(Supplier s) { … }
public void DeleteSupplier(int id)   { … }
public void BulkUpsertSuppliers(IEnumerable<Supplier> list) { … }
```

### 3.5 CreateDocumentWindow — party field labelling
```csharp
// Determine which list to show in CustomerLoadCombo based on DocType
bool isPurchase = dt is DocumentType.PurchaseOrder
    or DocumentType.PurchaseInvoice
    or DocumentType.GRN
    or DocumentType.DebitNote;

if (isPurchase)
{
    // populate combo from Suppliers
    _suppliers = _mainVm.ErpDb.LoadSuppliers();
    CustomerLoadCombo.ItemsSource = new[] { new Supplier { Name = "— Select supplier —" } }
        .Concat(_suppliers).Cast<object>().ToList();
    // also update label
    CustNameLabel.Text = "Supplier Name";
}
else
{
    // populate combo from Customers (existing logic)
    …
    CustNameLabel.Text = "Customer Name";
}
```
Also update `DocTypeCombo_Changed` to call this refresh.

### 3.6 MasterDataPage — two separate tabs
Split the single Customer card into two cards (or two TabItems):
- **Customers tab** — existing grid + buttons (no PartyType column)
- **Suppliers tab** — identical layout, bound to `MasterDataViewModel.Suppliers`

Add to `MasterDataViewModel`:
```csharp
private ObservableCollection<Supplier> _suppliers = new();
public  ObservableCollection<Supplier>  Suppliers { … }

public void RefreshSuppliers() { … }
public void SaveSupplier(Supplier s)   { _db.SaveSupplier(s);   RefreshSuppliers(); }
public void DeleteSupplier(int id)     { _db.DeleteSupplier(id); RefreshSuppliers(); }
public void BulkUpsertSuppliers(…)    { … }
```

### 3.7 InventoryService — party type label
In `PostPartyLedger` replace:
```csharp
// OLD
string partyType = ModuleDocTypes.Sales.Contains(doc.DocType) ? "Customer" : "Vendor";
// NEW
string partyType = ModuleDocTypes.Sales.Contains(doc.DocType) ? "Customer" : "Supplier";
```

In `ErpDocumentDbService.SavePayment`:
```csharp
// OLD
if (dt is "PurchaseInvoice" or "PurchaseOrder" or "GRN" or "DebitNote")
    partyType = "Vendor";
// NEW
    partyType = "Supplier";
```

### 3.8 LedgerPage filter
Update `PartyTypeCombo` items:
```xaml
<ComboBoxItem Content="All Parties" IsSelected="True"/>
<ComboBoxItem Content="Customer"/>
<ComboBoxItem Content="Supplier"/>   <!-- was Vendor -->
```

---

## 4. Feature B — Export / Import Menu

### 4.1 Goal
Move the two sidebar plain-Button entries (`Export DB` / `Import DB`) into a
proper popup **Menu** control so they don't clutter the navigation rail.

### 4.2 Approach — MenuItem in Sidebar footer
Replace the two `<Button>` elements in `MainWindow.xaml` sidebar footer with:
```xaml
<Menu Background="Transparent" Foreground="#94A3B8"
      Margin="6,1" HorizontalAlignment="Left">
    <MenuItem Header="  ⇅  Database" Foreground="#94A3B8"
              FontSize="12" FontFamily="Segoe UI"
              Background="#0F172A">
        <MenuItem Header="⇪  Export Database…"
                  Click="Nav_ExportDb" Foreground="#0F172A"/>
        <MenuItem Header="⇩  Import Database…"
                  Click="Nav_ImportDb" Foreground="#0F172A"/>
        <Separator/>
        <MenuItem Header="📂  Open DB Folder"
                  Click="Nav_OpenDbFolder" Foreground="#0F172A"/>
    </MenuItem>
</Menu>
```

### 4.3 Style the Menu to match sidebar
Add to `MainWindow.Resources` (or `Styles.xaml`):
```xaml
<Style TargetType="MenuItem" x:Key="SidebarMenuItem">
    <Setter Property="Background"   Value="#0F172A"/>
    <Setter Property="Foreground"   Value="#94A3B8"/>
    <Setter Property="FontSize"     Value="12"/>
    <Setter Property="FontFamily"   Value="Segoe UI"/>
    <Setter Property="Height"       Value="36"/>
    <Setter Property="Padding"      Value="14,0"/>
</Style>
```

### 4.4 New handler — Open DB Folder
Add to `MainWindow.xaml.cs`:
```csharp
private void Nav_OpenDbFolder(object s, RoutedEventArgs e)
{
    string folder = DuckDbService.DbFolder;
    if (Directory.Exists(folder))
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
    else
        MessageBox.Show("Database folder not found. Create a document first.");
}
```

### 4.5 Exact lines to remove from MainWindow.xaml
Delete these two `<Button>` blocks (they will be replaced by the Menu above):
```xaml
<!-- DELETE these two -->
<Button Content="  ⇪  Export DB" … Click="Nav_ExportDb"/>
<Button Content="  ⇩  Import DB" … Click="Nav_ImportDb"/>
```
The `Nav_ExportDb` and `Nav_ImportDb` handlers in `MainWindow.xaml.cs` stay unchanged.

---

## 5. Testing Checklist

### Feature A
- [ ] App starts without exception after migration
- [ ] MasterData page shows two separate tabs: Customers / Suppliers
- [ ] Creating a Sales Invoice shows Customer dropdown, not Supplier
- [ ] Creating a Purchase Invoice shows Supplier dropdown, not Customer
- [ ] PartyLedger shows `Customer` and `Supplier` type labels (no "Vendor")
- [ ] CSV import/export works independently for Customers and Suppliers
- [ ] Opening balance posts correctly to PartyLedger for both types

### Feature B
- [ ] Sidebar shows `⇅ Database` menu item (not two separate buttons)
- [ ] Clicking opens dropdown with Export / Import / Open Folder
- [ ] Export DB saves `.db` file as before
- [ ] Import DB replaces database and reloads as before
- [ ] Open DB Folder opens Windows Explorer at `%APPDATA%\Ojaswat`

---

## 6. File Change Summary

| File | Change |
|---|---|
| `Database/DbInitializer.cs` | Add `RunMigration(conn, 4, DbMigration_004.Up)` |
| `Database/Migrations/DbMigration_004.cs` | **NEW** — create Suppliers table |
| `Models/Models.cs` | Add `Supplier` class; remove `PartyType` from `Customer` |
| `Services/ErpDocumentDbService.cs` | Add Supplier CRUD; fix partyType string |
| `Services/InventoryService.cs` | Change `"Vendor"` → `"Supplier"` |
| `Models/ViewModels/MasterDataViewModel.cs` | Add Supplier collection + methods |
| `Pages/MasterDataPage.xaml` | Add Suppliers card/tab |
| `Pages/MasterDataPage.xaml.cs` | Wire Supplier CRUD handlers |
| `Pages/LedgerPage.xaml` | Change ComboBoxItem `"Vendor"` → `"Supplier"` |
| `Windows/CreateDocumentWindow.xaml.cs` | Dynamic Customer/Supplier combo |
| `Windows/MainWindow.xaml` | Replace 2 buttons with `<Menu>` |
| `Windows/MainWindow.xaml.cs` | Add `Nav_OpenDbFolder` handler |
| `Services/CsvService.cs` | Add `ImportSuppliers` / `ExportSuppliers` |







---
name: MD of old ojaswat-erp (OLD V25 Version)
description(OLD V25 Version): >
  Knowledge of the Ojaswat ERP codebase — a .NET 8 WPF desktop ERP for Indian SME invoicing.
  Use this skill whenever the user asks to build, explain, modify, or extend anything in the
  Ojaswat ERP project. Trigger on any mention of: ERP, InvoiceApp, Ojaswat, DuckDB, Renderers,
  XAML pages, document types (SalesInvoice, PurchaseOrder, GRN, CreditNote etc.), GST logic,
  payment ledger, or master data in this app context.
---

# Ojaswat ERP — Project Skill

## Stack
- **Framework**: .NET 8 + WPF (Windows desktop)
- **Database**: DuckDB (embedded, file-based, sync driver)
- **PDF**: Headless Chrome via CLI
- **CSV**: CsvHelper
- **Namespace**: `Ojaswat`

---

## Folder Map

```
InvoiceApp_V18/
├── App.xaml                    ← Global styles & design tokens
├── InvoiceSettings.cs          ← Company info cache, app paths
├── Database/
│   ├── DbInitializer.cs        ← Runs all migrations on startup
│   └── Migrations/             ← DbMigration_001.cs, 002.cs … (idempotent)
├── Models/
│   ├── Models.cs               ← All data classes + DocType constants
│   └── ViewModels/             ← RelayCommand, DocumentViewModel, DocListViewModel …
├── Pages/                      ← One UserControl per sidebar page
├── Renderers/                  ← IDocRenderer + RendererBase + one file per DocType
├── Services/
│   ├── DuckDbService.cs        ← All DB CRUD (raw SQL, $param style)
│   ├── ExportService.cs        ← Registry: DocType string → IDocRenderer
│   ├── DocumentNumberService.cs← Auto-numbering: INV-2024-001, PO-2024-001 …
│   └── CsvService.cs           ← Master data import/export
└── Windows/                    ← MainWindow, CreateDocumentWindow, modals
```

---

## Document Types

`Quotation` · `SalesOrder` · `SalesInvoice` · `CreditNote`
`PurchaseOrder` · `PurchaseInvoice` · `GRN` · `DebitNote`

`PurchaseInvoice` and `GRN` expose an extra **E-Way Bill No.** field.

---

## Core Models (Models.cs)

**ErpDocument** — one row in `documents` table
- Identity: `Id` (GUID), `DocumentNo`, `DocType`, `Date`, `Status`, `CreatedBy`
- Company snapshot: `CompanyName`, `CompanyGST`, `CompanyBank`, `CompanyIFSC` … (snapshotted at save time)
- Customer: `CustomerName`, `CustomerGST`, `CustomerStateCode`, `CustomerBilling`, `CustomerShipping`
- Financials: `Subtotal`, `TotalCGST`, `TotalSGST`, `TotalIGST`, `Freight`, `Discount`, `GrandTotal`, `PendingAmount`, `IsIGST`
- Terms: `PaymentTerms`, `GeneralTerms`
- Navigation: `List<LineItem> Items`

**LineItem** — `DocumentId` FK · `ItemNumber` · `Description` · `HSN` · `UOM` · `Quantity` · `Rate` · `GSTPercent` · `LineTotal`

**Payment** — `DocumentNo` · `PartyName` · `Date` · `Amount` · `Mode` · `Reference` · `Notes`

**Master data** — `Customer`, `Item`, `TandC` (simple flat models)

---

## GST Logic

```
LineTotal    = Qty × Rate
GST per line = LineTotal × (GSTPercent / 100)

Intra-state (IsIGST=false):  CGST = SGST = totalGST / 2
Inter-state  (IsIGST=true):  IGST = totalGST

GrandTotal = Subtotal + CGST + SGST + IGST + Freight − Discount
```

---

## Key Patterns

**Renderer pattern** — `ExportService` maps DocType → `IDocRenderer`. To add a type: create `Renderers/XxxRenderer.cs` inheriting `RendererBase`, register in `ExportService`.

**Navigation** — `MainWindow` hosts a WPF `Frame`. Each nav button calls `MainFrame.Navigate(new XxxPage())`.

**DB access** — always raw SQL through `DuckDbService`, `$param` placeholders, sync calls only.

**Migrations** — every schema change is a new `DbMigration_00N.cs` with `IF NOT EXISTS` guards, registered in `DbInitializer`.

---

## XAML / Style Rules

Global style keys (defined in `App.xaml`):
`BtnPrimary` · `BtnGhost` · `BtnSuccess` · `BtnDanger` · `BtnIcon` · `BtnInline`
`FieldLabel` · `SectionHeading` · `Card` · `TextBoxMulti`

Color tokens: `Accent` #2563EB · `Green` #059669 · `Red` #DC2626 · `TextPri` #0F172A · `Sidebar` #0F172A

Rules: corner radius 12px cards / 8px buttons+inputs · no business logic in `.xaml.cs` · `x:Name` suffix matches control type (e.g. `CustNameBox`, `DocTypeCombo`)

---

## Change Cheatsheet

| Goal | Touch these files |
|------|-------------------|
| New document type | `Models.cs` → new `Renderers/XxxRenderer.cs` → `ExportService.cs` → `CreateDocumentWindow.xaml` |
| New DB column | `DbMigration_00N.cs` → `Models.cs` → `DuckDbService.cs` |
| New sidebar page | `Pages/XxxPage.xaml/.cs` → `MainWindow.xaml` (button) → `MainWindow.xaml.cs` (handler) |
| Change PDF layout | `Renderers/XxxRenderer.cs` — edit `RenderHtml()` |
| Change global style | `App.xaml` |
