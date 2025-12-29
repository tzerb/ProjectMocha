using System.Xml.Linq;
using System.Diagnostics;

string folderPath = args.Length > 0 
    ? args[0] 
    : @"C:\Users\tzerb\source\repos\ConsoleAppDotNet8";

if (args.Length == 0)
{
    Console.WriteLine($"Usage: ProjectMocha <folderPath>");
    Console.WriteLine($"Using default path: {folderPath}");
    Console.WriteLine();
}

Dictionary<string, ProjectInfo> projectReferences = GetAllDotNetProjects(folderPath);

DisplayProjectInfo(projectReferences);

Console.WriteLine();
DisplayHierarchy(projectReferences);
DisplayLegend();
DisplayHierarchyMenu(projectReferences, folderPath);

static void DisplayHierarchyMenu(Dictionary<string, ProjectInfo> projects, string rootFolder)
{
    // Build a lookup from filename to full path
    var fileNameToPath = projects.ToDictionary(p => Path.GetFileName(p.Key), p => p.Key);
    
    // Sort all projects by number of references ascending
    var sortedProjects = projects
        .OrderBy(p => p.Value.References.Count)
        .ThenBy(p => Path.GetFileName(p.Key))
        .ToList();

    if (sortedProjects.Count == 0)
    {
        Console.WriteLine("No projects found.");
        return;
    }

    int selectedIndex = 0;
    bool running = true;

    while (running)
    {
        Console.Clear();
        Console.WriteLine("All Projects (sorted by reference count ascending):");
        Console.WriteLine("===================================================");
        Console.WriteLine("Use ↑/↓ to navigate, 'S' to create solution, 'Q' to quit");
        Console.WriteLine();

        for (int i = 0; i < sortedProjects.Count; i++)
        {
            var project = sortedProjects[i];
            var info = project.Value;
            var fileName = Path.GetFileName(project.Key);
            var typeColor = GetProjectColor(project.Key, info.ProjectType);
            var versionColor = GetVersionColor(info.TargetFramework);

            if (i == selectedIndex)
            {
                Console.BackgroundColor = ConsoleColor.DarkGray;
                Console.Write("► ");
            }
            else
            {
                Console.Write("  ");
            }

            Console.ForegroundColor = typeColor;
            Console.Write($"{fileName} [{info.ProjectType}]");
            Console.ForegroundColor = versionColor;
            Console.Write($" ({info.TargetFramework})");
            Console.ResetColor();
            
            if (i == selectedIndex)
            {
                Console.BackgroundColor = ConsoleColor.DarkGray;
            }
            Console.WriteLine($" - {info.References.Count} references");
            Console.ResetColor();

            foreach (var reference in info.References.OrderBy(r => r))
            {
                Console.WriteLine($"      -> {reference}");
            }
        }

        var key = Console.ReadKey(true);
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                selectedIndex = (selectedIndex - 1 + sortedProjects.Count) % sortedProjects.Count;
                break;
            case ConsoleKey.DownArrow:
                selectedIndex = (selectedIndex + 1) % sortedProjects.Count;
                break;
            case ConsoleKey.S:
                CreateSolutionForProject(sortedProjects[selectedIndex], projects, fileNameToPath, rootFolder);
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey(true);
                break;
            case ConsoleKey.Q:
                running = false;
                break;
        }
    }
}

static void CreateSolutionForProject(KeyValuePair<string, ProjectInfo> selectedProject, 
    Dictionary<string, ProjectInfo> allProjects,
    Dictionary<string, string> fileNameToPath,
    string rootFolder)
{
    var projectMochaFolder = Path.Combine(rootFolder, ".ProjectMocha");
    
    // Create folder if it doesn't exist
    if (!Directory.Exists(projectMochaFolder))
    {
        Directory.CreateDirectory(projectMochaFolder);
        Console.WriteLine($"Created folder: {projectMochaFolder}");
    }

    var selectedFileName = Path.GetFileName(selectedProject.Key);
    var solutionName = Path.GetFileNameWithoutExtension(selectedFileName) + ".sln";
    var solutionPath = Path.Combine(projectMochaFolder, solutionName);

    // Get all referenced projects (transitive)
    var allReferencedProjects = GetAllReferencedProjects(selectedFileName, allProjects, fileNameToPath);
    allReferencedProjects.Add(selectedProject.Key); // Include the selected project itself

    // Delete existing solution if it exists
    if (File.Exists(solutionPath))
    {
        File.Delete(solutionPath);
    }

    // Create solution using dotnet CLI
    Console.WriteLine($"\nCreating solution: {solutionPath}");
    RunDotNetCommand($"new sln -n {Path.GetFileNameWithoutExtension(solutionName)} -o \"{projectMochaFolder}\"");

    // Add each project to the solution
    Console.WriteLine($"Adding {allReferencedProjects.Count} project(s):");
    foreach (var proj in allReferencedProjects.OrderBy(p => p))
    {
        Console.WriteLine($"  - {Path.GetFileName(proj)}");
        RunDotNetCommand($"sln \"{solutionPath}\" add \"{proj}\"");
    }
    
    Console.WriteLine($"\nSolution created successfully: {solutionPath}");
}

static void RunDotNetCommand(string arguments)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };
    
    process.Start();
    process.WaitForExit();
    
    if (process.ExitCode != 0)
    {
        var error = process.StandardError.ReadToEnd();
        Console.WriteLine($"Error: {error}");
    }
}

static HashSet<string> GetAllReferencedProjects(string projectFileName, 
    Dictionary<string, ProjectInfo> allProjects,
    Dictionary<string, string> fileNameToPath)
{
    var result = new HashSet<string>();
    var queue = new Queue<string>();
    
    // Get the full path and its references
    if (fileNameToPath.TryGetValue(projectFileName, out var fullPath))
    {
        var info = allProjects[fullPath];
        foreach (var reference in info.References)
        {
            queue.Enqueue(reference);
        }
    }

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (fileNameToPath.TryGetValue(current, out var currentFullPath))
        {
            if (result.Add(currentFullPath))
            {
                var info = allProjects[currentFullPath];
                foreach (var reference in info.References)
                {
                    queue.Enqueue(reference);
                }
            }
        }
    }

    return result;
}

static void DisplayHierarchy(Dictionary<string, ProjectInfo> projects)
{
    // Build a lookup from filename to full path
    var fileNameToPath = projects.ToDictionary(p => Path.GetFileName(p.Key), p => p.Key);
    
    // Sort all projects by number of references ascending
    var sortedProjects = projects
        .OrderBy(p => p.Value.References.Count)
        .ThenBy(p => Path.GetFileName(p.Key))
        .ToList();
    
    Console.WriteLine("All Projects (sorted by reference count ascending):");
    Console.WriteLine("===================================================");
    
    foreach (var project in sortedProjects)
    {
        var info = project.Value;
        var fileName = Path.GetFileName(project.Key);
        var typeColor = GetProjectColor(project.Key, info.ProjectType);
        var versionColor = GetVersionColor(info.TargetFramework);
        
        Console.ForegroundColor = typeColor;
        Console.Write($"{fileName} [{info.ProjectType}]");
        Console.ForegroundColor = versionColor;
        Console.Write($" ({info.TargetFramework})");
        Console.ResetColor();
        Console.WriteLine($" - {info.References.Count} references");
        
        foreach (var reference in info.References.OrderBy(r => r))
        {
            Console.WriteLine($"    -> {reference}");
        }
    }
}

static void DisplayLegend()
{
    Console.WriteLine();
    Console.WriteLine("Legend:");
    Console.WriteLine("Project Types:");
    WriteWithColor("  Exe - Console Application", ConsoleColor.Green);
    WriteWithColor("  Library - Class Library", ConsoleColor.Cyan);
    WriteWithColor("  WinExe - Windows Application", ConsoleColor.Magenta);
    WriteWithColor("  Web - Web Application", ConsoleColor.Yellow);
    WriteWithColor("  .csproj (Unknown) - C# Project", ConsoleColor.Blue);
    WriteWithColor("  .vbproj (Unknown) - VB.NET Project", ConsoleColor.DarkYellow);
    WriteWithColor("  .fsproj (Unknown) - F# Project", ConsoleColor.DarkCyan);
    Console.WriteLine(".NET Versions:");
    WriteWithColor("  .NET Framework - Legacy Framework", ConsoleColor.DarkRed);
    WriteWithColor("  .NET Standard - Cross-platform Library", ConsoleColor.DarkMagenta);
    WriteWithColor("  .NET Core / .NET 5+ - Modern .NET", ConsoleColor.White);
}

static void DisplayProjectInfo(Dictionary<string, ProjectInfo> projectReferences)
{
    Console.WriteLine($"Found {projectReferences.Count} .NET projects:");
    foreach (var project in projectReferences)
    {
        var info = project.Value;
        var typeColor = GetProjectColor(project.Key, info.ProjectType);
        var versionColor = GetVersionColor(info.TargetFramework);
        
        Console.ForegroundColor = typeColor;
        Console.Write($"{project.Key} [{info.ProjectType}]");
        Console.ForegroundColor = versionColor;
        Console.Write($" ({info.TargetFramework})");
        Console.ResetColor();
        Console.WriteLine($" - {info.References.Count} references");
        
        foreach (var reference in info.References)
        {
            Console.WriteLine($"    -> {reference}");
        }
    }
}

static void WriteWithColor(string text, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ResetColor();
}

static ConsoleColor GetProjectColor(string path, string type) => type.ToLower() switch
{
    "exe" => ConsoleColor.Green,
    "library" => ConsoleColor.Cyan,
    "winexe" => ConsoleColor.Magenta,
    "web" => ConsoleColor.Yellow,
    _ => Path.GetExtension(path).ToLower() switch
    {
        ".csproj" => ConsoleColor.Blue,
        ".vbproj" => ConsoleColor.DarkYellow,
        ".fsproj" => ConsoleColor.DarkCyan,
        _ => ConsoleColor.White
    }
};

static ConsoleColor GetVersionColor(string targetFramework) => targetFramework.ToLower() switch
{
    var tf when tf.StartsWith("net4") || tf.StartsWith("v4") || tf.StartsWith("v3") || tf.StartsWith("v2") => ConsoleColor.DarkRed,
    var tf when tf.StartsWith("netstandard") => ConsoleColor.DarkMagenta,
    var tf when tf.StartsWith("netcoreapp") || tf.StartsWith("net5") || tf.StartsWith("net6") || 
                tf.StartsWith("net7") || tf.StartsWith("net8") || tf.StartsWith("net9") || tf.StartsWith("net10") => ConsoleColor.White,
    _ => ConsoleColor.Gray
};

static Dictionary<string, ProjectInfo> GetAllDotNetProjects(string rootFolder)
{
    Dictionary<string, ProjectInfo> projects = [];

    if (!Directory.Exists(rootFolder))
    {
        Console.WriteLine($"Directory not found: {rootFolder}");
        return projects;
    }

    string[] projectExtensions = ["*.csproj", "*.vbproj", "*.fsproj"];

    foreach (var extension in projectExtensions)
    {
        try
        {
            var files = Directory.EnumerateFiles(rootFolder, extension, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var info = GetProjectInfo(file);
                projects[file] = info;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.WriteLine($"Directory not found: {ex.Message}");
        }
    }

    foreach (var project in projects)
    {
        var typeColor = GetProjectColor(project.Key, project.Value.ProjectType);
        var versionColor = GetVersionColor(project.Value.TargetFramework);
        
        Console.ForegroundColor = typeColor;
        Console.Write($"{Path.GetFileName(project.Key)} [{project.Value.ProjectType}]");
        Console.ForegroundColor = versionColor;
        Console.Write($" ({project.Value.TargetFramework})");
        Console.ResetColor();
        Console.WriteLine($" - {project.Value.References.Count} references");
    }

    return projects;
}

static ProjectInfo GetProjectInfo(string projectPath)
{
    List<string> references = [];
    string projectType = "Unknown";
    string targetFramework = "Unknown";

    try
    {
        var doc = XDocument.Load(projectPath);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        
        // Get project references (check both with and without namespace)
        var projectRefs = doc.Descendants(ns + "ProjectReference")
                             .Concat(doc.Descendants("ProjectReference"))
                             .Select(pr => pr.Attribute("Include")?.Value)
                             .Where(val => val != null)
                             .Distinct()
                             .Cast<string>();
        references.AddRange(projectRefs.Select(Path.GetFileName)!);

        // Determine project type (check both with and without namespace)
        var outputType = doc.Descendants(ns + "OutputType").FirstOrDefault()?.Value
                      ?? doc.Descendants("OutputType").FirstOrDefault()?.Value;
        var sdk = doc.Root?.Attribute("Sdk")?.Value;

        if (!string.IsNullOrEmpty(outputType))
        {
            projectType = outputType;
        }
        else if (sdk != null)
        {
            // SDK-style projects default based on SDK
            projectType = sdk switch
            {
                "Microsoft.NET.Sdk.Web" => "Web",
                "Microsoft.NET.Sdk.Worker" => "Exe",
                "Microsoft.NET.Sdk.BlazorWebAssembly" => "Web",
                _ => "Library" // Default for SDK-style without OutputType
            };
        }

        // Determine target framework (check both SDK-style and legacy formats)
        targetFramework = doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                       ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').FirstOrDefault()
                       ?? doc.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value
                       ?? doc.Descendants("TargetFrameworkVersion").FirstOrDefault()?.Value
                       ?? "Unknown";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading {projectPath}: {ex.Message}");
    }

    return new ProjectInfo(references, projectType, targetFramework);
}

record ProjectInfo(List<string> References, string ProjectType, string TargetFramework);
