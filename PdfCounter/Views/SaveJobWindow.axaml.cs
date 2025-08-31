using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using iText.Forms.Fields.Merging;
using PdfCounter.Models;
using PdfCounter.ViewModels;

namespace PdfCounter.Views;
  
public partial class SaveJobWindow : Window
{
    private readonly MainWindowViewModel? _mainVm;
    public SaveJobWindow()
    {
        InitializeComponent();
    }

    public SaveJobWindow(MainWindowViewModel vm) : this()
    {
        _mainVm = vm;
    }

    public void SetField(object field) => Editor.DataContext = field;

    private async void OnSave(object? s, RoutedEventArgs e)
    {
        var field = Editor.DataContext as SaveJobProperty;
        if (string.IsNullOrEmpty(field?.JobName))
        {
            await ShowLocalError("Job Name is required.");
            return;
        }

        Close(field ?? new SaveJobProperty());
    }

    private async Task ShowLocalError(string msg)
    {
        var dismissButton = new Button
        {
            Content = "Dismiss",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var textBlock = new TextBlock
        {
            Text = msg,
            Foreground = Brushes.Red, 
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(12)
        };
        stack.Children.Add(textBlock);
        stack.Children.Add(dismissButton);

        var dlg = new Window
        {
            Width = 360,
            Height = 205,
            Title = "Validation Error",
            Content = stack
        };
        dismissButton.Click += (_, __) => dlg.Close();
        await dlg.ShowDialog(this); // owner = editor window
    }

    private void OnCancel(object? s, RoutedEventArgs e)
        => Close();
}
