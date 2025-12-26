using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Information about a discovered project
/// </summary>
/// <param name="Name">Display name (folder name)</param>
/// <param name="Path">Full path to the project directory</param>
internal sealed record ProjectInfo(string Name, string Path);

/// <summary>
/// Resolves and selects projects in standalone multi-project mode
/// </summary>
/// <remarks>
/// <para>
/// In standalone mode (portable exe with projects/ folder), this class handles:
/// </para>
/// <list type="bullet">
/// <item>Discovering available projects in the projects/ directory</item>
/// <item>Interactive project selection via Spectre.Console</item>
/// <item>Parsing --project/-p command line arguments</item>
/// </list>
/// <para>
/// This runs BEFORE the Host is built, so it cannot use DI services.
/// </para>
/// </remarks>
internal static class ProjectResolver
{
    /// <summary>
    /// Gets all available projects in the projects/ directory
    /// </summary>
    /// <returns>List of discovered projects, empty if none found</returns>
    public static IReadOnlyList<ProjectInfo> GetAvailableProjects()
    {
        var projectsDir = ConfigPathResolver.ProjectsDirectory;

        if (!Directory.Exists(projectsDir))
        {
            return [];
        }

        var projects = new List<ProjectInfo>();

        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var projectFile = Path.Combine(dir, "project.json");
            if (File.Exists(projectFile))
            {
                var name = Path.GetFileName(dir);
                projects.Add(new ProjectInfo(name, dir));
            }
        }

        return [.. projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Shows interactive project selection menu
    /// </summary>
    /// <param name="projects">Available projects to choose from</param>
    /// <returns>Selected project path, or null if user chose to exit</returns>
    public static string? SelectProjectInteractively(IReadOnlyList<ProjectInfo> projects)
    {
        AnsiConsole.Clear();

        // ASCII logo
        var logoLines = new[]
        {
            @"   ____                _       ",
            @"  |  _ \ _____   _____| | __ _ ",
            @"  | |_) / _ \ \ / / _ \ |/ _` |",
            @"  |  _ <  __/\ V /  __/ | (_| |",
            @"  |_| \_\___| \_/ \___|_|\__,_|",
        };

        foreach (var line in logoLines)
        {
            AnsiConsole.MarkupLine("[cyan1]" + line + "[/]");
        }

        AnsiConsole.WriteLine();

        // Build choices
        var choices = projects
            .Select(p => p.Name)
            .Concat(["[dim]Exit[/]"])
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[blue]Select a project:[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                .AddChoices(choices));

        if (selection == "[dim]Exit[/]")
        {
            return null;
        }

        var project = projects.FirstOrDefault(p => p.Name == selection);
        return project?.Path;
    }

    /// <summary>
    /// Shows first-run menu when no projects exist (standalone mode)
    /// </summary>
    /// <returns>True if user wants to create a project, false to exit</returns>
    public static bool ShowFirstRunMenu()
    {
        return ShowNoProjectMenu(
            "No projects found in the projects/ folder.",
            "Create your first project");
    }

    /// <summary>
    /// Shows menu when current directory is not a project (tool mode)
    /// </summary>
    /// <returns>True if user wants to initialize this folder, false to exit</returns>
    public static bool ShowNoProjectInCwdMenu()
    {
        return ShowNoProjectMenu(
            "This directory doesn't contain a project.",
            "Initialize this folder as a project");
    }

    /// <summary>
    /// Shows the no-project menu with customizable message
    /// </summary>
    private static bool ShowNoProjectMenu(string message, string createOption)
    {
        AnsiConsole.Clear();

        // ASCII logo
        var logoLines = new[]
        {
            @"   ____                _       ",
            @"  |  _ \ _____   _____| | __ _ ",
            @"  | |_) / _ \ \ / / _ \ |/ _` |",
            @"  |  _ <  __/\ V /  __/ | (_| |",
            @"  |_| \_\___| \_/ \___|_|\__,_|",
        };

        foreach (var line in logoLines)
        {
            AnsiConsole.MarkupLine("[cyan1]" + line + "[/]");
        }

        AnsiConsole.WriteLine();

        var panel = new Panel(
            new Markup(
                "[bold]Welcome to Revela![/]\n\n" +
                $"[dim]{message}[/]\n\n" +
                "Let's get started."))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                .AddChoices([createOption, "[dim]Exit[/]"]));

        return selection != "[dim]Exit[/]";
    }

    /// <summary>
    /// Parses --project or -p argument from command line
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Project name if specified, null otherwise</returns>
    public static string? ParseProjectArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // --project=Name or -p=Name
            if (arg.StartsWith("--project=", StringComparison.OrdinalIgnoreCase))
            {
                return arg["--project=".Length..];
            }

            if (arg.StartsWith("-p=", StringComparison.OrdinalIgnoreCase))
            {
                return arg["-p=".Length..];
            }

            // --project Name or -p Name
            if ((arg.Equals("--project", StringComparison.OrdinalIgnoreCase) ||
                 arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length &&
                !args[i + 1].StartsWith('-'))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Removes --project/-p arguments from args array
    /// </summary>
    /// <param name="args">Original command line arguments</param>
    /// <returns>Arguments without project specification</returns>
    public static string[] RemoveProjectArguments(string[] args)
    {
        var result = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // Skip --project=Name or -p=Name
            if (arg.StartsWith("--project=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-p=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip --project Name or -p Name (both parts)
            if ((arg.Equals("--project", StringComparison.OrdinalIgnoreCase) ||
                 arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length &&
                !args[i + 1].StartsWith('-'))
            {
                i++; // Skip the value too
                continue;
            }

            result.Add(arg);
        }

        return [.. result];
    }

    /// <summary>
    /// Resolves the project path based on installation mode
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>
    /// Tuple of (projectPath, filteredArgs, shouldExit).
    /// projectPath is null if using CWD directly or user chose to exit.
    /// shouldExit is true if the application should terminate.
    /// </returns>
    public static (string? ProjectPath, string[] FilteredArgs, bool ShouldExit) ResolveProject(string[] args)
    {
        var filteredArgs = RemoveProjectArguments(args);
        var hasCommands = filteredArgs.Length > 0;

        // Tool-Mode: use current directory
        if (!ConfigPathResolver.IsStandaloneMode)
        {
            return ResolveToolMode(filteredArgs, hasCommands);
        }

        // Standalone-Mode: use projects/ directory
        return ResolveStandaloneMode(args, filteredArgs, hasCommands);
    }

    /// <summary>
    /// Resolves project in Tool-Mode (global dotnet tool)
    /// </summary>
    private static (string? ProjectPath, string[] FilteredArgs, bool ShouldExit) ResolveToolMode(
        string[] filteredArgs,
        bool hasCommands)
    {
        var cwd = Directory.GetCurrentDirectory();
        var projectFile = Path.Combine(cwd, "project.json");

        // CWD has project.json - use it
        if (File.Exists(projectFile))
        {
            return (null, filteredArgs, false);
        }

        // No project.json in CWD
        if (hasCommands)
        {
            // Check if this is a project-independent command
            if (IsProjectIndependentCommand(filteredArgs))
            {
                // Allow the command to run without project
                return (null, filteredArgs, false);
            }

            // User ran a project-requiring command but not in a project directory
            ShowNotInProjectError(cwd);
            return (null, filteredArgs, true);
        }

        // Interactive mode without project - let the menu handle it
        // The menu will automatically filter to show only project-independent commands
        // (config project, plugin list, theme list, etc.)
        return (null, filteredArgs, false);
    }

    /// <summary>
    /// Shows error when user runs a command outside a project directory
    /// </summary>
    private static void ShowNotInProjectError(string cwd)
    {
        AnsiConsole.MarkupLine("[red]Error:[/] Not in a Revela project directory.");
        AnsiConsole.MarkupLine($"[dim]Path: {cwd}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Options:[/]");
        AnsiConsole.MarkupLine("  • Run [cyan]revela[/] (no arguments) to initialize this folder");
        AnsiConsole.MarkupLine("  • Run [cyan]cd path/to/project[/] to switch to an existing project");
    }

    /// <summary>
    /// Commands that work without a project context.
    /// </summary>
    /// <remarks>
    /// These commands bypass the "not in project directory" error.
    /// Note: Not all subcommands may work (e.g., 'config site' requires project).
    /// The interactive menu uses CommandDescriptor.RequiresProject for finer control.
    /// </remarks>
    private static readonly HashSet<string> ProjectIndependentCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Setup commands
        "init",
        "config",

        // Global management commands
        "plugin",
        "theme",
        "packages",

        // Help commands
        "help",
        "--help",
        "-h",
        "-?",
        "--version"
    };

    /// <summary>
    /// Checks if the command args start with a project-independent command
    /// </summary>
    private static bool IsProjectIndependentCommand(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        return ProjectIndependentCommands.Contains(args[0]);
    }

    /// <summary>
    /// Resolves project in Standalone-Mode (portable exe)
    /// </summary>
    private static (string? ProjectPath, string[] FilteredArgs, bool ShouldExit) ResolveStandaloneMode(
        string[] args,
        string[] filteredArgs,
        bool hasCommands)
    {
        var projectName = ParseProjectArgument(args);

        // --project specified
        if (projectName is not null)
        {
            var projectPath = Path.Combine(ConfigPathResolver.ProjectsDirectory, projectName);

            if (!Directory.Exists(projectPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Project '[yellow]{projectName}[/]' not found.");
                AnsiConsole.MarkupLine($"[dim]Path: {projectPath}[/]");
                return (null, filteredArgs, true);
            }

            return (projectPath, filteredArgs, false);
        }

        // Project-independent commands (create, help, --version) - run without project
        if (IsProjectIndependentCommand(filteredArgs))
        {
            return (null, filteredArgs, false);
        }

        // No --project, but has commands = error
        if (hasCommands)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No project specified.");
            AnsiConsole.MarkupLine("[dim]Use --project <name> or run without arguments for interactive mode.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Available projects:[/]");

            var projects = GetAvailableProjects();
            foreach (var p in projects)
            {
                AnsiConsole.MarkupLine($"  • {p.Name}");
            }

            return (null, filteredArgs, true);
        }

        // Interactive mode
        var availableProjects = GetAvailableProjects();

        // No projects - show first run
        if (availableProjects.Count == 0)
        {
            var wantsCreate = ShowFirstRunMenu();
            if (!wantsCreate)
            {
                return (null, filteredArgs, true);
            }

            // User wants to create - we'll handle this by returning a special marker
            // The create command will be triggered in Program.cs
            return (ConfigPathResolver.ProjectsDirectory, ["create", "project"], false);
        }

        // Single project - auto-select
        if (availableProjects.Count == 1)
        {
            return (availableProjects[0].Path, filteredArgs, false);
        }

        // Multiple projects - show selection
        var selectedPath = SelectProjectInteractively(availableProjects);
        if (selectedPath is null)
        {
            return (null, filteredArgs, true);
        }

        return (selectedPath, filteredArgs, false);
    }
}
