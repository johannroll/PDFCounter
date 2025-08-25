using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using PdfCounter.ViewModels;
using PdfCounter.Views;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PdfCounter;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static void Main(string[] args)
    {
        // app services
        var services = new ServiceCollection();
        services.AddSingleton<IPdfExtractorService, PdfExtractorService>();

        // view models
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        Console.WriteLine("UNHANDLED: " + e.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Console.WriteLine("UNOBSERVED: " + e.Exception);
            e.SetObserved();
        };

        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (s, e) =>
        {
            Console.WriteLine("UI EXCEPTION: " + e.Exception);
            e.Handled = true; // s
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"UNHANDLED: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine($"UNOBSERVED: {e.Exception}");
            e.SetObserved();
        };

        Console.WriteLine("BaseDir: " + AppContext.BaseDirectory);
        Console.WriteLine("Has runtimes?: " + Directory.Exists(Path.Combine(AppContext.BaseDirectory, "runtimes")));
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
