using System.Collections.Frozen;
using System.Text.Json;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Information about a discovered project
/// </summary>
/// <param name="FolderName">Folder name in projects/ directory</param>
/// <param name="Path">Full path to the project directory</param>
/// <param name="DisplayName">Project name from project.json, or folder name if not set</param>
internal sealed record ProjectInfo(string FolderName, string Path, string DisplayName);

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
    /// Gets all project folders in the projects/ directory
    /// </summary>
    /// <remarks>
    /// Returns all subdirectories, regardless of whether they contain a project.json.
    /// Folders without project.json are shown as "not configured".
    /// </remarks>
    /// <returns>List of discovered project folders, empty if none found</returns>
    public static IReadOnlyList<ProjectInfo> GetProjectFolders()
    {
        var projectsDir = ConfigPathResolver.ProjectsDirectory;

        if (!Directory.Exists(projectsDir))
        {
            return [];
        }

        var folders = new List<ProjectInfo>();

        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var folderName = Path.GetFileName(dir);
            var projectFile = Path.Combine(dir, "project.json");

            if (File.Exists(projectFile))
            {
                // Configured project - use project name or folder name
                var displayName = ReadProjectName(projectFile) ?? folderName;
                folders.Add(new ProjectInfo(folderName, dir, displayName));
            }
            else
            {
                // Empty folder - show as not configured
                var displayName = $"{folderName} [dim](not configured)[/]";
                folders.Add(new ProjectInfo(folderName, dir, displayName));
            }
        }

        return [.. folders.OrderBy(p => p.FolderName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Shows interactive project folder selection menu
    /// </summary>
    /// <param name="folders">Available project folders to choose from</param>
    /// <returns>Selected folder path, or null if user chose to exit</returns>
    public static string? SelectProjectInteractively(IReadOnlyList<ProjectInfo> folders)
    {
        ConsoleUI.ClearAndShowLogo();
        ConsoleUI.ShowWelcomePanel();
        AnsiConsole.WriteLine();

        // Build choices - DisplayName already contains markup for unconfigured folders
        const string projectsHeader = "Projects";
        const string setupHeader = "Setup";
        const string newFolderChoice = "Create new project folder";
        const string exitChoice = "Exit";

        var folderChoices = folders
            .Select(p => p.DisplayName)
            .ToList();

        var prompt = new SelectionPrompt<string>()
            .Title("[cyan]Select a project folder:[/]")
            .PageSize(20)
            .WrapAround()
            .Mode(SelectionMode.Leaf)
            .HighlightStyle(ConsoleUI.PromptBoldHighlightStyle)
            .AddChoiceGroup(projectsHeader, folderChoices)
            .AddChoiceGroup(setupHeader, newFolderChoice)
            .AddChoices(exitChoice);

        prompt.DisabledStyle = ConsoleUI.GroupHeaderStyle;

        var selection = AnsiConsole.Prompt(prompt);

        if (selection == "Exit")
        {
            return null;
        }

        if (selection == newFolderChoice)
        {
            return CreateNewProjectFolder();
        }

        // Find folder by matching the choice text
        var selectedIndex = folderChoices.IndexOf(selection);
        return selectedIndex >= 0 ? folders[selectedIndex].Path : null;
    }

    /// <summary>
    /// Tries to match a --project/-p argument at the given index.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="index">Current index in the args array.</param>
    /// <param name="value">The project name value, if matched.</param>
    /// <param name="consumed">Number of args consumed (1 for =style, 2 for space-separated).</param>
    /// <returns>True if a project argument was matched at this index.</returns>
    private static bool TryMatchProjectArg(string[] args, int index, out string? value, out int consumed)
    {
        var arg = args[index];

        // --project=Name or -p=Name
        if (arg.StartsWith("--project=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg["--project=".Length..];
            consumed = 1;
            return true;
        }

        if (arg.StartsWith("-p=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg["-p=".Length..];
            consumed = 1;
            return true;
        }

        // --project Name or -p Name
        if ((arg.Equals("--project", StringComparison.OrdinalIgnoreCase) ||
             arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) &&
            index + 1 < args.Length &&
            !args[index + 1].StartsWith('-'))
        {
            value = args[index + 1];
            consumed = 2;
            return true;
        }

        value = null;
        consumed = 0;
        return false;
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
            if (TryMatchProjectArg(args, i, out var value, out _))
            {
                return value;
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
            if (TryMatchProjectArg(args, i, out _, out var consumed))
            {
                i += consumed - 1; // -1 because for-loop increments
                continue;
            }

            result.Add(args[i]);
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
        AnsiConsole.MarkupLine($"[dim]Path: {Markup.Escape(cwd)}[/]");
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
    private static readonly FrozenSet<string> ProjectIndependentCommands = FrozenSet.ToFrozenSet(
    [
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
    ], StringComparer.OrdinalIgnoreCase);

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
        // First-run check: If no revela.json exists, skip project selection entirely.
        // The InteractiveMenuService.HandleFirstRunAsync() will show the Setup Wizard.
        // This ensures: Setup Wizard → Project Selection (not the other way around!)
        if (!GlobalConfigManager.ConfigFileExists())
        {
            // Return null project path - let InteractiveMenuService handle first-run
            return (null, filteredArgs, false);
        }

        var projectName = ParseProjectArgument(args);

        // --project specified
        if (projectName is not null)
        {
            var projectPath = Path.Combine(ConfigPathResolver.ProjectsDirectory, projectName);

            if (!Directory.Exists(projectPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Project '[yellow]{Markup.Escape(projectName)}[/]' not found.");
                AnsiConsole.MarkupLine($"[dim]Path: {Markup.Escape(projectPath)}[/]");
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
            AnsiConsole.MarkupLine("[blue]Available project folders:[/]");

            var folders = GetProjectFolders();
            foreach (var f in folders)
            {
                AnsiConsole.MarkupLine($"  • {f.DisplayName}");
            }

            return (null, filteredArgs, true);
        }

        // Interactive mode
        var projectFolders = GetProjectFolders();

        // No project folders - offer to create one
        if (projectFolders.Count == 0)
        {
            var newFolderPath = PromptCreateFirstProjectFolder();
            if (newFolderPath is null)
            {
                return (null, filteredArgs, true);
            }

            return (newFolderPath, filteredArgs, false);
        }

        // Single folder - auto-select
        if (projectFolders.Count == 1)
        {
            return (projectFolders[0].Path, filteredArgs, false);
        }

        // Multiple folders - show selection
        var selectedPath = SelectProjectInteractively(projectFolders);
        if (selectedPath is null)
        {
            return (null, filteredArgs, true);
        }

        return (selectedPath, filteredArgs, false);
    }

    /// <summary>
    /// Reads project.name from project.json
    /// </summary>
    /// <param name="projectFilePath">Path to project.json</param>
    /// <returns>Project name or null if not found/readable</returns>
    private static string? ReadProjectName(string projectFilePath)
    {
        try
        {
            var json = File.ReadAllText(projectFilePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("project", out var projectSection) &&
                projectSection.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                var name = nameElement.GetString();
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
        }
        catch (Exception)
        {
            // JSON parse or I/O error — fall back to folder name
        }

        return null;
    }

    /// <summary>
    /// Creates a new project folder interactively
    /// </summary>
    /// <returns>Path to the new project folder, or null if cancelled</returns>
    internal static string? CreateNewProjectFolder()
    {
        AnsiConsole.WriteLine();

        var folderName = AnsiConsole.Prompt(
            new TextPrompt<string>("[blue]Folder name:[/]")
                .Validate(name =>
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        return ValidationResult.Error("[red]Name cannot be empty[/]");
                    }

                    // Check for invalid path characters
                    if (name.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        return ValidationResult.Error("[red]Name contains invalid characters[/]");
                    }

                    // Check if folder already exists
                    var targetPath = Path.Combine(ConfigPathResolver.ProjectsDirectory, name);
                    if (Directory.Exists(targetPath))
                    {
                        return ValidationResult.Error($"[red]Folder '{Markup.Escape(name)}' already exists[/]");
                    }

                    return ValidationResult.Success();
                }));

        var projectPath = Path.Combine(ConfigPathResolver.ProjectsDirectory, folderName);

        // Create the folder structure
        Directory.CreateDirectory(projectPath);

        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Project folder created: [cyan]{Markup.Escape(folderName)}[/]");
        AnsiConsole.WriteLine();

        return projectPath;
    }

    /// <summary>
    /// Prompts user to create the first project folder when none exist
    /// </summary>
    /// <returns>Path to the new project folder, or null if user chose to exit</returns>
    private static string? PromptCreateFirstProjectFolder()
    {
        ConsoleUI.ClearAndShowLogo();
        ConsoleUI.ShowWelcomePanel();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]No project folders found.[/]");
        AnsiConsole.WriteLine();

        var prompt = new SelectionPrompt<string>()
            .Title("[cyan]Select an option:[/]")
            .PageSize(20)
            .WrapAround()
            .Mode(SelectionMode.Leaf)
            .HighlightStyle(ConsoleUI.PromptBoldHighlightStyle)
            .AddChoiceGroup("Setup", "Create new project folder")
            .AddChoices("Exit");

        prompt.DisabledStyle = ConsoleUI.GroupHeaderStyle;

        var choice = AnsiConsole.Prompt(prompt);

        if (choice == "Exit")
        {
            return null;
        }

        return CreateNewProjectFolder();
    }
}
