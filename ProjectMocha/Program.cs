using System.Xml.Linq;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

string folderPath = @"C:\Users\tzerb\source\repos\ConsoleAppDotNet8"; // Change this to your target folder

Dictionary<string, List<string>> projectReferences = GetAllDotNetProjects(folderPath);

Console.WriteLine($"Found {projectReferences.Count} .NET projects:");
foreach (var project in projectReferences)
{
    Console.WriteLine($"{project.Key} - {project.Value.Count} references");
    foreach (var reference in project.Value)
    {
        Console.WriteLine($"    -> {reference}");
    }
}

static Dictionary<string, List<string>> GetAllDotNetProjects(string rootFolder)
{
    Dictionary<string, List<string>> projects = [];

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
                var references = GetProjectReferences(file);
                projects[file] = references;
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
        Console.WriteLine($"{Path.GetFileName(project.Key)} - {project.Value.Count} references");
    }

    return projects;
}

static List<string> GetProjectReferences(string projectPath)
{
    List<string> references = [];

    try
    {
        var doc = XDocument.Load(projectPath);
        var projectRefs = doc.Descendants("ProjectReference")
                             .Select(pr => pr.Attribute("Include")?.Value)
                             .Where(val => val != null)
                             .Cast<string>();

        references.AddRange(projectRefs.Select(Path.GetFileName)!);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading {projectPath}: {ex.Message}");
    }

    return references;
}
