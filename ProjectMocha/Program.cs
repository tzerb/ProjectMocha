using System.Xml.Linq;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

string folderPath = @"C:\Users\tzerb\source\repos\ConsoleAppDotNet8"; // Change this to your target folder

Dictionary<string, ProjectInfo> projectReferences = GetAllDotNetProjects(folderPath);

DisplayProjectInfo(projectReferences);

Console.WriteLine();
DisplayHierarchy(projectReferences);

DisplayLegend();

static void DisplayHierarchy(Dictionary<string, ProjectInfo> projects)
{
    // Build a lookup from filename to full path
    var fileNameToPath = projects.ToDictionary(p => Path.GetFileName(p.Key), p => p.Key);
    
    // Build adjacency list using file names
    var graph = projects.ToDictionary(
        p => Path.GetFileName(p.Key),
        p => p.Value.References.ToHashSet());
    
    // Compute transitive closure for each node
    var transitiveClosure = new Dictionary<string, HashSet<string>>();
    foreach (var node in graph.Keys)
    {
        transitiveClosure[node] = GetAllReachable(node, graph);
    }
    
    // Compute minimum references (transitive reduction)
    var minimalRefs = new Dictionary<string, HashSet<string>>();
    foreach (var (node, refs) in graph)
    {
        var minimal = new HashSet<string>(refs);
        foreach (var directRef in refs)
        {
            // Remove any reference that can be reached through another direct reference
            foreach (var otherRef in refs)
            {
                if (otherRef != directRef && transitiveClosure.GetValueOrDefault(otherRef, []).Contains(directRef))
                {
                    minimal.Remove(directRef);
                    break;
                }
            }
        }
        minimalRefs[node] = minimal;
    }
    
    // Find root projects (not referenced by anyone)
    var allReferenced = graph.Values.SelectMany(r => r).ToHashSet();
    var roots = graph.Keys.Where(k => !allReferenced.Contains(k)).ToList();
    
    // Display hierarchy
    Console.WriteLine("Project Hierarchy (Minimum References):");
    Console.WriteLine("========================================");
    
    var visited = new HashSet<string>();
    foreach (var root in roots.OrderBy(r => r))
    {
        DisplayNode(root, minimalRefs, projects, fileNameToPath, 0, visited);
    }
    
    // Display any orphaned nodes not reachable from roots
    foreach (var node in graph.Keys.OrderBy(n => n))
    {
        if (!visited.Contains(node))
        {
            DisplayNode(node, minimalRefs, projects, fileNameToPath, 0, visited);
        }
    }
}

static HashSet<string> GetAllReachable(string start, Dictionary<string, HashSet<string>> graph)
{
    var reachable = new HashSet<string>();
    var queue = new Queue<string>(graph.GetValueOrDefault(start, []));
    
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (reachable.Add(current))
        {
            foreach (var next in graph.GetValueOrDefault(current, []))
            {
                queue.Enqueue(next);
            }
        }
    }
    
    return reachable;
}

static void DisplayNode(string node, Dictionary<string, HashSet<string>> minimalRefs, 
    Dictionary<string, ProjectInfo> projects, Dictionary<string, string> fileNameToPath,
    int indent, HashSet<string> visited)
{
    if (!visited.Add(node)) return;
    
    var indentStr = new string(' ', indent * 2);
    var fullPath = fileNameToPath.GetValueOrDefault(node);
    var info = fullPath != null ? projects.GetValueOrDefault(fullPath) : null;
    
    if (info != null && fullPath != null)
    {
        var typeColor = GetProjectColor(fullPath, info.ProjectType);
        var versionColor = GetVersionColor(info.TargetFramework);
        
        Console.Write(indentStr);
        Console.ForegroundColor = typeColor;
        Console.Write($"{node} [{info.ProjectType}]");
        Console.ForegroundColor = versionColor;
        Console.WriteLine($" ({info.TargetFramework})");
        Console.ResetColor();
    }
    else
    {
        Console.WriteLine($"{indentStr}{node}");
    }
    
    foreach (var child in minimalRefs.GetValueOrDefault(node, []).OrderBy(c => c))
    {
        DisplayNode(child, minimalRefs, projects, fileNameToPath, indent + 1, visited);
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
