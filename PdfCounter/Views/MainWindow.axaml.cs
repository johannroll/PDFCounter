using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfCounter.Models;
using PdfCounter.ViewModels;

namespace PdfCounter.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Register interaction handler
        this.DataContextChanged += (_, __) => Wire();
        Wire();
    }
    public PdfCounter.ViewModels.MainWindowViewModel ViewModel
    => (PdfCounter.ViewModels.MainWindowViewModel)DataContext!;
    private void Wire()
    {
        if (DataContext is not MainWindowViewModel vm || Grid is null) return;

        // Rebuild columns whenever ColumnNames changes
        vm.ColumnNames.CollectionChanged += (_, __) => RebuildColumns(vm);
        RebuildColumns(vm);
    }

    private void RebuildColumns(MainWindowViewModel vm)
    {
        Grid.Columns.Clear();

        foreach (var name in vm.ColumnNames)
        {
            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = name,
                Binding = new Avalonia.Data.Binding(".")
                {
                    Converter = new DictKeyConverter(name) // from the converter class we made
                }
            });
        }
    }
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.PickPdf.RegisterHandler(async ctx =>
            {
                var options = new FilePickerOpenOptions
                {
                    Title = "Select a PDF file",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("PDF Files")
                        {
                            Patterns = new[] { "*.pdf" },
                            MimeTypes = new[] { "application/pdf" }
                        }
                    }
                };

                var files = await StorageProvider.OpenFilePickerAsync(options);
                ctx.SetOutput(files?.FirstOrDefault());
            });

            vm.ShowError.RegisterHandler(async ctx =>
            {
                var msg = ctx.Input;

                var dismissButton = new Button
                {
                    Content = "Dismiss",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var textBlock = new TextBlock
                {
                    Text = msg,
                    Foreground = Brushes.Red, // change text color
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
                    Height = 105,
                    Title = "Validation Error",
                    Content = stack,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                dismissButton.Click += (_, __) => dlg.Close();

                await dlg.ShowDialog(this);

                ctx.SetOutput(Unit.Default);
            });

            vm.ShowInfo.RegisterHandler(async ctx =>
            {
                var msg = ctx.Input;

                var dismissButton = new Button
                {
                    Content = "Dismiss",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var textBlock = new TextBlock
                {
                    Text = msg,
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
                    Height = 145,
                    Title = "Info",
                    Content = stack,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                dismissButton.Click += (_, __) => dlg.Close();

                await dlg.ShowDialog(this);

                ctx.SetOutput(Unit.Default);
            });

            vm.EditField.RegisterHandler(async ctx =>
            {
                var dlg = new SaveJobWindow()
                {
                    DataContext = vm
                };
                dlg.SetField(ctx.Input); // the ExtractField (temp or working copy)

                var outcome = await dlg.ShowDialog<FieldEditorOutcome>(this);
                ctx.SetOutput(outcome);
            });

            vm.LoadJob.RegisterHandler(async ctx =>
            {
                var paths = ListJobFiles(); // your helper
                var items = new ObservableCollection<string>(
                    paths.Select(GetJobNameFromPath)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

                var root = new StackPanel { Margin = new Thickness(12) };

                var dlg = new Window
                {
                    Title = "Load Job",
                    Width = 600,
                    Height = 600,
                    Content = root,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var list = new ListBox
                {
                    ItemsSource = items,
                    Margin = new Thickness(0, 0, 0, 8),
                    Height = 525,
                    Background = Brushes.Transparent,
                };


                // ItemTemplate: Job name + Delete button (aligned right)
                list.ItemTemplate = new FuncDataTemplate<string>((jobName, _) =>
                {   
                    var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0,2,0,2) };
                    
                    var deleteBtn = new Button
                    {
                        Content = "Delete",
                        Margin = new Thickness(8, 0, 0, 0),
                        MinWidth = 72,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                    };

                    // Dock the button to the right
                    DockPanel.SetDock(deleteBtn, Dock.Right);

                    var nameBlock = new TextBlock
                    {
                        Text = jobName,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.NoWrap,
                    };

                    var scroller = new ScrollViewer
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Content = nameBlock
                    };

                    deleteBtn.PointerPressed += (_, pe) => pe.Handled = true;
                    deleteBtn.Click += async (s, e) =>
                    {
                        // prevent bubbling from also triggering ListBox DoubleTapped etc.
                        e.Handled = true;

                        var ok = await ConfirmAsync($"Delete job \"{jobName}\"?", dlg); // pass owner here
                        if (!ok) return;

                        var filePath = Path.Combine(GetJobsFolder(), jobName + ".json");
                        try
                        {
                            if (File.Exists(filePath))
                                File.Delete(filePath);

                            items.Remove(jobName);
                            if (Equals(list.SelectedItem as string, jobName))
                                list.SelectedItem = null;
                        }
                        catch (Exception ex)
                        {
                            await InfoAsync($"Couldn't delete:\n{ex.Message}", dlg); // same owner trick
                        }
                    };

                    row.Children.Add(scroller);
                    row.Children.Add(deleteBtn);
                    return row;
                });

                var ok = new Button { Content = "Load", IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
                var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(8, 0, 0, 0), MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };

                var btns = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                btns.Children.Add(ok);
                btns.Children.Add(cancel);

                // root.Children.Add(new TextBlock { Text = "Select a job to load:", Margin = new Thickness(0, 0, 0, 8) });

                var scrollerWindow = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = list
                };

                root.Children.Add(scrollerWindow);
                root.Children.Add(btns);

                string? selected = null;

                ok.Click += (_, __) =>
                {
                    selected = list.SelectedItem as string;
                    dlg.Close();
                };

                // list.DoubleTapped += (sender, de) =>
                // {
                //     if (de.Source is Control c && c is Button) return;        // ignore
                //     if (de.Source is Control c2 && c2.FindAncestorOfType<Button>() != null) return;

                //     selected = list.SelectedItem as string;
                //     dlg.Close();
                // };

                cancel.Click += (_, __) =>
                {
                    selected = null;
                    dlg.Close();
                };

                await dlg.ShowDialog(this);
                ctx.SetOutput(selected ?? "");
            });

            vm.SaveJob.RegisterHandler(async ctx =>
            {
                // Preload existing job names (case-insensitive)
                var existing = new HashSet<string>(
                    ListJobFiles().Select(GetJobNameFromPath),
                    StringComparer.OrdinalIgnoreCase);

                // Seed dialog with the input (or a new model)
                var input = ctx.Input ?? new SaveJobProperty();
                var dlg = new SaveJobWindow
                {
                    DataContext = input,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // Show dialog; user may cancel => result can be null
                var outcome = await dlg.ShowDialog<SaveJobProperty?>(this);

                // User cancelled? return null through the interaction
                if (outcome is null)
                {
                    ctx.SetOutput(null);
                    return;
                }

                // Validate name
                var name = (outcome.JobName ?? "").Trim();
                if (string.IsNullOrEmpty(name))
                {
                    await InfoAsync("Job name is required.", dlg);
                    ctx.SetOutput(null);
                    return;
                }

                // Duplicate check (case-insensitive)
                if (existing.Contains(name))
                {
                    var overwrite = await ConfirmReplaceAsync($"Job \"{name}\" already exists. Replace the saved file?", dlg);
                    if (!overwrite)
                    {
                        ctx.SetOutput(null);
                        return;
                    }
                }

                // All good
                ctx.SetOutput(outcome);
            });
        }
    }

    private bool _handling;

    private async void PropertyPicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {

        if (_handling) return;
        _handling = true;
        try
        {
            if (DataContext is not MainWindowViewModel vm) return;
            if (e.AddedItems.Count == 0) return;

            var combo = (ComboBox)sender!;
            var selected = e.AddedItems[0];

            // reset selection immediately so same item can be picked again
            Dispatcher.UIThread.Post(() => combo.SelectedIndex = -1, DispatcherPriority.Background);

            FieldEditorOutcome outcome;

            if (selected is AddPropertyItem) // "➕ Add property…"
            {
                var temp = new ExtractField();
                var dlg = new SaveJobWindow(vm) { DataContext = vm };
                dlg.SetField(temp);
                outcome = await dlg.ShowDialog<FieldEditorOutcome>(this);

                if (outcome.Result == FieldEditorResult.Save && outcome.Edited is { } added)
                    vm.Fields.Add(added);
                return;
            }

            if (selected is ExtractField existing)
            {
                var working = new ExtractField
                {
                    Name = existing.Name,
                    X = existing.X,
                    Y = existing.Y,
                    Width = existing.Width,
                    Height = existing.Height,
                    MatchValues = existing.MatchValues,
                    IsFirstPageIdentifier = existing.IsFirstPageIdentifier,
                    IsInlineValue = existing.IsInlineValue,
                };

                var dlg = new SaveJobWindow {  };
                dlg.SetField(working);
                outcome = await dlg.ShowDialog<FieldEditorOutcome>(this);

                switch (outcome.Result)
                {
                    case FieldEditorResult.Save when outcome.Edited is { } e2:
                        existing.Name = e2.Name;
                        existing.X = e2.X; existing.Y = e2.Y;
                        existing.Width = e2.Width; existing.Height = e2.Height;
                        existing.MatchValues = e2.MatchValues;
                        existing.IsFirstPageIdentifier = e2.IsFirstPageIdentifier;
                        existing.IsInlineValue = e2.IsInlineValue;
                        break;
                    case FieldEditorResult.Remove:
                        vm.Fields.Remove(existing);
                        break;
                }
            }
        }
        finally { _handling = false; }
    }

    private static string GetJobsFolder()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(root, "PdfCounter", "Jobs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string[] ListJobFiles()
    {
        var dir = GetJobsFolder();
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
    }

    private static string GetJobNameFromPath(string path)
        => Path.GetFileNameWithoutExtension(path);


    private void OnTabHeaderClicked(object sender, TappedEventArgs e)
    {
        ViewModel.CurrentTab = 0;
    }

    private void OnSwitchThemeClicked(object sender, RoutedEventArgs e)
    {
        if (Application.Current is null)
        {
            return;
        }

        var currentTheme = Application.Current.RequestedThemeVariant;
        if (currentTheme != null && currentTheme.Equals(ThemeVariant.Dark))
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        }
        else
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }
    
    // overload that takes the owner window
    private async Task<bool> ConfirmAsync(string message, Window owner)
    {
        var msg = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        var ok = new Button { Content = "Yes", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "No", IsCancel = true, MinWidth = 80, Margin = new Thickness(8, 0, 0, 0) };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(msg);
        root.Children.Add(buttons);

        var w = new Window
        {
            Title = "Confirm",
            Width = 420,
            Height = 140,
            Content = root,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        bool result = false;
        ok.Click += (_, __) => { result = true; w.Close(); };
        cancel.Click += (_, __) => { result = false; w.Close(); };

        await w.ShowDialog(owner);   // <<--- key: use the picker dialog as owner
        return result;
    }
    private async Task<bool> ConfirmReplaceAsync(string message, Window owner)
    {
        var msg = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) };
        var ok = new Button { Content = "Yes", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "No", IsCancel = true, MinWidth = 80, Margin = new Thickness(8,0,0,0) };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(msg);
        root.Children.Add(buttons);

        var w = new Window
        {
            Title = "Confirm",
            Width = 400,
            Height = 140,
            Content = root,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        bool result = false;
        ok.Click += (_, __) => { result = true; w.Close(); };
        cancel.Click += (_, __) => { result = false; w.Close(); };

        await w.ShowDialog(this);   // <<--- key: use the picker dialog as owner
        return result;
    }

    private async Task InfoAsync(string message, Window owner)
    {
        var msg = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) };
        var close = new Button { Content = "OK", IsDefault = true, MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right };

        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(msg);
        root.Children.Add(close);

        var w = new Window
        {
            Title = "Info",
            Width = 420,
            Height = 120,
            Content = root,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        w.SizeToContent.CompareTo(root);

        close.Click += (_, __) => w.Close();
        await w.ShowDialog(owner);  // <<--- child of picker
    }

}