using System.Xml.Linq;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

string folderPath = @"C:\Users\tzerb\source\repos\ConsoleAppDotNet8"; // Change this to your target folder

Dictionary<string, ProjectInfo> projectReferences = GetAllDotNetProjects(folderPath);

DisplayProjectInfo(projectReferences);

DisplayLegend();

static void DisplayLegend()
{
    Console.WriteLine();
    Console.WriteLine("Legend:");
    WriteWithColor("  Exe - Console Application", ConsoleColor.Green);
    WriteWithColor("  Library - Class Library", ConsoleColor.Cyan);
    WriteWithColor("  WinExe - Windows Application", ConsoleColor.Magenta);
    WriteWithColor("  Web - Web Application", ConsoleColor.Yellow);
    WriteWithColor("  .csproj (Unknown) - C# Project", ConsoleColor.Blue);
    WriteWithColor("  .vbproj (Unknown) - VB.NET Project", ConsoleColor.DarkYellow);
    WriteWithColor("  .fsproj (Unknown) - F# Project", ConsoleColor.DarkCyan);
}

static void DisplayProjectInfo(Dictionary<string, ProjectInfo> projectReferences)
{
    Console.WriteLine($"Found {projectReferences.Count} .NET projects:");
    foreach (var project in projectReferences)
    {
        var info = project.Value;
        WriteWithColor($"{project.Key} [{info.ProjectType}] - {info.References.Count} references", GetProjectColor(project.Key, info.ProjectType));
        
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
        WriteWithColor($"{Path.GetFileName(project.Key)} [{project.Value.ProjectType}] - {project.Value.References.Count} references", 
                     GetProjectColor(project.Key, project.Value.ProjectType));
    }

    return projects;
}

static ProjectInfo GetProjectInfo(string projectPath)
{
    List<string> references = [];
    string projectType = "Unknown";

    try
    {
        var doc = XDocument.Load(projectPath);
        
        // Get project references
        var projectRefs = doc.Descendants("ProjectReference")
                             .Select(pr => pr.Attribute("Include")?.Value)
                             .Where(val => val != null)
                             .Cast<string>();
        references.AddRange(projectRefs.Select(Path.GetFileName)!);

        // Determine project type
        var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading {projectPath}: {ex.Message}");
    }

    return new ProjectInfo(references, projectType);
}

record ProjectInfo(List<string> References, string ProjectType);
