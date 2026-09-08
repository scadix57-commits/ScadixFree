using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.IO;
using System.Reflection;
using Scadix.AxamlDesigner;
using System;
using System.Linq;

namespace Scadix.Designer;

public partial class App : Application
{
    public static string[]? Args;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Args = desktop.Args;

            // Load persisted recent projects
            RecentProjectsStore.Load();

            // Register Assembly Resolver BEFORE loading extensions
            AppDomain.CurrentDomain.AssemblyResolve += AppDomain_CurrentDomain_AssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += AppDomain_CurrentDomain_UnhandledException;
            DragDropExceptionHandler.UnhandledException += DragDropExceptionHandler_UnhandledException;

            
            // Save on exit
            desktop.Exit += (_, _) => RecentProjectsStore.Save();

            // Show the welcome screen on startup (unless a file/project was passed as argument)
            if (Args is { Length: > 0 })
            {
                var arg = Args[0];
                var ext = Path.GetExtension(arg).ToLowerInvariant();
                var isProject = ext is ".sln" or ".slnx" or ".csproj";
                var isFolder  = Directory.Exists(arg) && !isProject;
                var isXaml    = ext is ".xaml" or ".axaml";

                if (isProject || isFolder || isXaml)
                {
                    var main = new MainWindow();
                    desktop.MainWindow = main;
                    main.Loaded += (_, _) =>
                    {
                        if (isXaml)
                            MainWindowViewModel.Instance.Open(arg);
                        else if (isFolder)
                            MainWindowViewModel.Instance.OpenFolder(arg);
                        else
                            MainWindowViewModel.Instance.OpenSolution(arg);
                    };
                }
                else
                {
                    var welcome = new WelcomeScreen();
                    desktop.MainWindow = welcome;
                }
            }
            else
            {
                var welcome = new WelcomeScreen();
                desktop.MainWindow = welcome;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool internalLoad = false;
    private static string? lastRequesting = null;

    private Assembly? AppDomain_CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var shortName = args.Name.Split(',')[0].Trim();

        // Skip satellite resource assemblies — they are never redirected
        if (shortName.EndsWith(".resources")) return null;

        var assList = AppDomain.CurrentDomain.GetAssemblies();

        // 1. Exact full-name match (version + culture + token all match)
        var loaded = assList.FirstOrDefault(x => x.FullName == args.Name);
        if (loaded != null) return loaded;

        // 2. Version-tolerant match: same short name already loaded → redirect to it.
        //    This handles the common case where the host loads v10.0.0.0 of a shared
        //    Microsoft.Extensions.* assembly and an extension was compiled against a
        //    different version of the same package.
        var byShortName = assList.FirstOrDefault(x =>
            x.GetName().Name?.Equals(shortName, StringComparison.OrdinalIgnoreCase) == true);
        if (byShortName != null) return byShortName;

        if (internalLoad) return null;

        internalLoad = true;
        Assembly? ass = null;

        // 3. Try the default probing (GAC / TPA list)
        try { ass = Assembly.Load(args.Name); } catch { }

        // 4. Probe the requesting assembly's own directory
        if (ass == null && args.RequestingAssembly != null)
        {
            lastRequesting = args.RequestingAssembly.Location;
            var dir  = Path.GetDirectoryName(args.RequestingAssembly.Location);
            var file = shortName + ".dll";
            try { if (dir != null) ass = Assembly.LoadFrom(Path.Combine(dir, file)); } catch { }
        }

        // 5. Probe the Extensions output folder (flat layout)
        if (ass == null)
        {
            var extDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");
            if (Directory.Exists(extDir))
            {
                var fullPath = Path.Combine(extDir, shortName + ".dll");
                if (File.Exists(fullPath))
                    try { ass = Assembly.LoadFrom(fullPath); } catch { }

                // Also search one level deep (e.g. Extensions/Maui/Scadix.MauiDesignerExtension/)
                if (ass == null)
                {
                    foreach (var subDir in Directory.GetDirectories(extDir, "*", SearchOption.AllDirectories))
                    {
                        var candidate = Path.Combine(subDir, shortName + ".dll");
                        if (File.Exists(candidate))
                        {
                            try { ass = Assembly.LoadFrom(candidate); break; } catch { }
                        }
                    }
                }
            }
        }

        // 6. Fall back to the last known requesting-assembly directory
        if (ass == null && lastRequesting != null)
        {
            var dir  = Path.GetDirectoryName(lastRequesting);
            var file = shortName + ".dll";
            try { if (dir != null) ass = Assembly.LoadFrom(Path.Combine(dir, file)); } catch { }
        }

        internalLoad = false;
        return ass;
    }

    private void DragDropExceptionHandler_UnhandledException(object sender, System.Threading.ThreadExceptionEventArgs e)
        => MainWindowViewModel.ReportException(e.Exception);

    private void AppDomain_CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        => MainWindowViewModel.ReportException(e.ExceptionObject as Exception);
}
