using System;
using System.Threading.Tasks;

class Program
{
    // A small generator that 1) announces capabilities to the coordinator via a single-line JSON handshake,
    // 2) creates a small C# project folder in the current workspace, and
    // 3) periodically adds timestamped files into that generated project and emits single-line JSON events
    // so the coordinator can forward structured events to the extension output.

    public static async Task Main(string[] args)
    {
        try
        {
            var cwd = Directory.GetCurrentDirectory();
            Console.WriteLine("Sample Generator starting up..."); 

            // Emit a single-line JSON handshake (generator -> coordinator)
            var hello = new
            {
                method = "generator/hello",
                @params = new
                {
                    capabilities = new
                    {
                        publishDiagnostics = false,
                        watchMode = false, // let coordinator handle rebuilds for this simple prototype
                        watchGlobs = new[] { "**/*" },
                        watchDebounceMs = 500
                    }
                }
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(hello));

            // Create a generated project folder OUTSIDE the generator project
            // Place generated output next to the generator project's parent folder (a sibling directory).
            // e.g. if generator is at <repo>/test-workspace, generated projects will be placed in <repo>/gengora-output
            var workspaceRoot = Directory.GetParent(cwd)?.FullName ?? cwd;
            var outputRoot = Path.Combine(workspaceRoot, "gengora-output");
            Directory.CreateDirectory(outputRoot);
            
            var baseName = "GeneratedProject";
            var unique = DateTime.UtcNow.ToString("yyyyMMddHH");
            var genFolderName = baseName + "-" + unique;
            var genPath = Path.Combine(outputRoot, genFolderName);
            Directory.CreateDirectory(genPath);

            // Minimal csproj 
            var csprojText =
                "<Project Sdk=\"Microsoft.NET.Sdk\">" + Environment.NewLine +
                "  <PropertyGroup>" + Environment.NewLine +
                "    <OutputType>Exe</OutputType>" + Environment.NewLine +
                "    <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "</Project>";
            File.WriteAllText(Path.Combine(genPath, genFolderName + ".csproj"), csprojText);

            // Minimal Program.cs 
            var programText =
                "using System;" + Environment.NewLine +
                "class GeneratedApp { static void Main() { Console.WriteLine(\"Hello from GeneratedProject\"); } }";
            File.WriteAllText(Path.Combine(genPath, "Program.cs"), programText);

            // Create initial timestamp file
            var createdFiles = new List<string>();
            var tsFile = Path.Combine(genPath, $"generated-{DateTime.UtcNow:yyyy.MM.dd.HH-mm-ss}.txt6"); 
            File.WriteAllText(tsFile, DateTime.UtcNow.ToString("o") + "\n");
            createdFiles.Add(tsFile);
 
            // Emit an event describing what we created
            var createdMsg = new
            {
                method = "generator/generated",
                @params = new
                {
                    project = genPath,
                    created = createdFiles
                }
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(createdMsg)); 
        }
        catch (Exception ex)
        {
            var err = new { method = "generator/error", @params = new { message = ex.Message, stack = ex.StackTrace } };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(err));
            Console.Error.WriteLine("Generator fatal: " + ex.ToString());
            Environment.Exit(1);
        }
    }
}// Test change 23:02:59
