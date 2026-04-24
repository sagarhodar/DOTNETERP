using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ojaswat.Windows;

public partial class QuickEditWindow : Window
{
    private readonly List<TextBox> _boxes = new();
    public string[] Values => _boxes.Select(b => b.Text.Trim()).ToArray();

    public QuickEditWindow(string entityName, (string Label, string Value)[] fields)
    {
        InitializeComponent();
        Title = $"Edit {entityName}";

        // Insert label+textbox pairs before the button row
        var btnRow = (StackPanel)FieldsPanel.Children[^1];

        foreach (var (label, value) in fields)
        {
            var lbl = new TextBlock
            {
                Text       = label,
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x47, 0x55, 0x69)),
                Margin     = new Thickness(0, 8, 0, 3)
            };
            var box = new TextBox
            {
                Text = value,
                Height = 36,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 12,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCB, 0xD5, 0xE1)),
                BorderThickness = new Thickness(1.5),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            int idx = FieldsPanel.Children.IndexOf(btnRow);
            FieldsPanel.Children.Insert(idx, lbl);
            FieldsPanel.Children.Insert(idx + 1, box);
            _boxes.Add(box);
        }
    }

    private void Save_Click(object s, RoutedEventArgs e) => DialogResult = true;
}
