using System.Windows;
using System.Windows.Controls;

namespace Ojaswat;

/// <summary>
/// Maps CurrentPageKey string → DataTemplate declared in MainWindow.Resources.
///
/// IMPORTANT: The ContentControl.Content is the string key (e.g. "dash").
/// That means the DataTemplate's root element (the UserControl page) will
/// have DataContext = "dash" — a string, not MainViewModel.
///
/// Fix: after SelectTemplate returns, WPF calls PrepareContainerForItem which
/// sets the content. We cannot override that here. Instead, each page reads
/// App.VM directly in its Loaded event — bypassing DataContext entirely.
/// </summary>
public class PageTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not string key || string.IsNullOrEmpty(key))
            return null;

        if (container is FrameworkElement fe)
            return fe.TryFindResource($"DT_{key}") as DataTemplate;

        return null;
    }
}
