using System.Windows;
using System.Windows.Controls;

namespace v2rayN.Base;

/// <summary>
/// Attached properties that extend any <see cref="DataGridColumn"/> subclass,
/// including <see cref="DataGridTemplateColumn"/> which does not inherit from
/// <see cref="FrameworkElement"/> and therefore has no Tag/Name DP of its own.
/// </summary>
public static class DataGridColumnEx
{
    public static readonly DependencyProperty ExNameProperty =
        DependencyProperty.RegisterAttached(
            "ExName",
            typeof(string),
            typeof(DataGridColumnEx),
            new PropertyMetadata(string.Empty));

    public static string GetExName(DependencyObject obj) =>
        (string)obj.GetValue(ExNameProperty);

    public static void SetExName(DependencyObject obj, string value) =>
        obj.SetValue(ExNameProperty, value);
}
