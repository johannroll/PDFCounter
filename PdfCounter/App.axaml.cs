using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using PdfCounter.ViewModels;
using PdfCounter.Views;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Styling;

namespace PdfCounter;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var sp = Program.Services;
            var window = sp.GetRequiredService<MainWindow>();
            window.DataContext ??= sp.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = window;

            DisableAvaloniaDataAnnotationValidation();
            #if DEBUG
                this.AttachDevTools();
            #endif
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}