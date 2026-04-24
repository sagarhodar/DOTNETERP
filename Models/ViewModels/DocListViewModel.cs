using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Ojaswat.Models;

namespace Ojaswat.ViewModels;

/// <summary>
/// Filter + search state for DocumentListPage.
/// Works directly off MainViewModel.AllDocs; applies filters on demand.
/// </summary>
public class DocListViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _main;

    // ── Filter state ──────────────────────────────────────────────────────────
    private string         _searchText      = "";
    private string         _docTypeFilter   = "All Types";
    private string         _statusFilter    = "All Status";
    private DocumentType[] _moduleFilter    = Array.Empty<DocumentType>();
    private bool           _pendingOnly;

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; PC(nameof(SearchText)); Apply(); }
    }

    public string DocTypeFilter
    {
        get => _docTypeFilter;
        set { _docTypeFilter = value; PC(nameof(DocTypeFilter)); Apply(); }
    }

    public string StatusFilter
    {
        get => _statusFilter;
        set { _statusFilter = value; PC(nameof(StatusFilter)); Apply(); }
    }

    public DocumentType[] ModuleFilter
    {
        get => _moduleFilter;
        set { _moduleFilter = value; Apply(); }
    }

    public bool PendingOnly
    {
        get => _pendingOnly;
        set { _pendingOnly = value; Apply(); }
    }

    // ── Output ────────────────────────────────────────────────────────────────
    private ObservableCollection<DocumentListItem> _filtered = new();
    public  ObservableCollection<DocumentListItem>  Filtered
    {
        get => _filtered;
        private set { _filtered = value; PC(nameof(Filtered)); PC(nameof(FilteredCount)); }
    }

    public int FilteredCount => Filtered.Count;

    // ── Constructor ───────────────────────────────────────────────────────────
    public DocListViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.AllDocs)) Apply();
        };
        Apply();
    }

    // ── Apply all active filters ──────────────────────────────────────────────
    public void Apply()
    {
        var q = _main.AllDocs.AsEnumerable();

        // Module filter (Sales / Purchase group)
        if (_moduleFilter.Length > 0)
        {
            var allowed = _moduleFilter.Select(t => t.ToString())
                                       .ToHashSet(StringComparer.OrdinalIgnoreCase);
            q = q.Where(d => allowed.Contains(d.DocTypeLabel));
        }

        // Specific doc type
        if (!string.IsNullOrEmpty(DocTypeFilter) && DocTypeFilter != "All Types")
            q = q.Where(d => d.DocTypeLabel.Equals(DocTypeFilter, StringComparison.OrdinalIgnoreCase));

        // Status filter
        if (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "All Status" &&
            Enum.TryParse<DocumentStatus>(StatusFilter, out var st))
            q = q.Where(d => d.Status == st);

        // Pending only
        if (PendingOnly)
            q = q.Where(d => d.Status == DocumentStatus.Open || d.Status == DocumentStatus.Pending);

        // Free-text search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string s = SearchText.Trim().ToLower();
            q = q.Where(d => d.DocumentNo.ToLower().Contains(s)
                           || d.CustomerName.ToLower().Contains(s)
                           || d.CustomerRefNo.ToLower().Contains(s)
                           || d.DocTypeLabel.ToLower().Contains(s));
        }

        Filtered = new ObservableCollection<DocumentListItem>(
            q.OrderByDescending(d => d.Date));
    }

    public void Clear()
    {
        _searchText    = "";
        _docTypeFilter = "All Types";
        _statusFilter  = "All Status";
        _moduleFilter  = Array.Empty<DocumentType>();
        _pendingOnly   = false;
        Apply();
        PC(nameof(SearchText));
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void PC(string n) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
