using Serverless.CLI.Responses;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;

namespace Serverless.CLI;

internal class Program
{
    private static readonly HttpClient _httpClient = new();
    private static async Task Main(string[] args)
    {

        Console.ForegroundColor = ConsoleColor.Cyan;
        var versionString = (Assembly.GetEntryAssembly()?
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                            .InformationalVersion ?? "1.0.0").Split("+");

        Console.WriteLine($"\n🔥 faasdot v{versionString[0]} - The Ultimate Serverless CLI Tool 🔥");
        Console.WriteLine($"\n build:{versionString[1]}");
        Console.WriteLine("-------------------------------------------------------\n");
        Console.WriteLine("FaasDot is a Function as a Service (FaaS) system for .NET 9 with many built-in services.");
        Console.WriteLine("You can deploy solutions quickly—simplicity is our slogan.");
        Console.ResetColor();

        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var command = args[0].ToLower();

        bool isDevMode = true;//args.Contains("--dev"); // Leer si el usuario pasó el flag "--dev"
        ConfigManager configManager = new ConfigManager(isDevMode);
        
        string configPath = ConfigManager .GetConfigPath(isDevMode);
        if(isDevMode) Console.WriteLine($"📁 Config Path: {configPath}");

        switch (command)
        {
            case "login":
                await Login(args, configManager);
                break;
            case "logout":
                await Logout(args, configManager);
                break;
            case "set-current":
                await SetCurrent(args, configManager);
                break;
            case "list":
                await ListProject(args, configManager);
                break;
            case "publish":
                await PublishProject(args, configManager);
                break;
            case "update":
                await PublishProject(args, configManager, true);
                break;
            case "functions":
                await ListFunctions(args, configManager);
                break;
            case "executions":
                await ShowExecutions(args, configManager);
                break;
            case "delete":
                await DeleteProject(args, configManager);
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Unknown command: {command}");
                Console.ResetColor();
                ShowHelp();
                break;
        }
    }

    private static async Task Login(string[] args, ConfigManager configManager)
    {
        string authUrl = $"{configManager.ConfigData.AuthUrl}?app=faasdot&webhook=local";
        
        // Abre la URL en el navegador
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = authUrl,
            UseShellExecute = true
        });

        // Configura el HttpListener
        HttpListener listener = new HttpListener();
        string listenUrl = "http://localhost:5000/";
        listener.Prefixes.Add(listenUrl);
        listener.Start();

        Console.WriteLine("🔒 Waiting for authentication...");

        // Espera por una solicitud HTTP
        HttpListenerContext context = await listener.GetContextAsync();
        HttpListenerRequest request = context.Request;

        // Verifica que la solicitud sea un POST
        if (request.HttpMethod == "POST")
        {
            using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestBody = await reader.ReadToEndAsync();
                var formData = System.Web.HttpUtility.ParseQueryString(requestBody);

                string token = formData["token"];

                if (!string.IsNullOrEmpty(token))
                {
                    // Guarda el token en el ConfigManager
                    configManager.SetToken(token);
                    Console.WriteLine("✅ Token received and saved.");
                }
                else
                {
                    Console.WriteLine("❌ No token received.");
                }
            }
        }
        else
        {
            Console.WriteLine("❌ Invalid request method. Only POST is allowed.");
        }
        // Cierra el listener
        listener.Stop();
    }

    private static async Task Logout(string[] args, ConfigManager configManager)
    {
        configManager.SetToken(string.Empty);
        Console.WriteLine($"✅ Logged");
    }

    private static async Task SetCurrent(string[] args, ConfigManager configManager)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("❌ Must specified project Id");
            return;
        }
        try
        {
            configManager.SetCurrent(Guid.Parse(args[1]));
        }
        catch (Exception)
        {
            Console.WriteLine("❌ Invalid project Id");
            return;
        }
    }

    private static async Task ListProject(string[] args, ConfigManager configManager)
    {
        var projects = configManager.ListProjects();

        if (projects.Count == 0)
        {
            Console.WriteLine("⚠️ No projects found.");
            return;
        }

        // Column headers
        string[] headers = { "ID", "Path" };
        int[] columnWidths = { 40, 50 };

        Console.WriteLine("\n📌 Projects:");
        Console.WriteLine(string.Join("", headers.Select((h, i) => h.PadRight(columnWidths[i]))));

        foreach (var project in projects)
        {
            string id = project.Id.ToString().PadRight(columnWidths[0]);
            string path = project.Path.PadRight(columnWidths[1]);

            Console.WriteLine($"{id}{path}");
        }
    }

    private static void ShowHelp()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Usage:");
        Console.WriteLine("  faasdot <command> [options]\n");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Commands:");
        Console.ResetColor();

        Console.WriteLine("  🔑 login          Authenticate with FaaSDot.NET. (not implemented)");
        Console.WriteLine("  🚪 logout         Log out of the current session. (not implemented)");
        Console.WriteLine("  📜 list           Show deployed projects with their IDs.");
        Console.WriteLine("  🚀 publish        Deploy project.");
        Console.WriteLine("  🔄 update         Update an existing project.");
        Console.WriteLine("  ⚙️ functions      Show available functions.");
        Console.WriteLine("  🎯 set-current    Set current project (arg: projectId:GUID)");
        Console.WriteLine("  📜 executions     Show execution logs.");
        Console.WriteLine("  🗑️ delete         Delete the project (removes all functions).");
    }

    private static async Task ListFunctions(string[] args, ConfigManager _config)
    {
        var project = _config.GetCurrentProject();
        if (project == null)
        {
            Console.WriteLine("❌ No current project selected.");
            return;
        }

        var response = await _httpClient.GetAsync($"{_config.ConfigData.BaseUrl}/api/functions/{project.Id}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Error: Unable to fetch functions. Status: {response.StatusCode}");
            return;
        }

        var functionsList = await response.Content.ReadFromJsonAsync<FunctionList>();

        if (functionsList == null || functionsList.Items.Count == 0)
        {
            Console.WriteLine("⚠️ No functions found.");
            return;
        }

        // Column headers
        string[] headers = { "ID", "Name", "Route", "Method", "Cron Expression", "Last Execution", "Next Execution" };
        int[] columnWidths = { 40, 15, 15, 10, 20, 30, 30 };

        Console.WriteLine("\n📌 Functions:");
        Console.WriteLine(string.Join("", headers.Select((h, i) => h.PadRight(columnWidths[i]))));

        foreach (var func in functionsList.Items)
        {
            string id = func.Id.ToString().PadRight(columnWidths[0]);
            string name = func.Name.PadRight(columnWidths[1]);
            string route = (func.Route ?? "N/A").PadRight(columnWidths[2]);
            string method = (func.Method == 0 ? "GET" : "POST").PadRight(columnWidths[3]);
            string cron = (func.CronExpression ?? "N/A").PadRight(columnWidths[4]);
            string lastExec = (func.LastExecution?.ToLocalTime().ToString() ?? "N/A").PadRight(columnWidths[5]);
            string nextExec = (func.NextExecution?.ToLocalTime().ToString() ?? "N/A").PadRight(columnWidths[6]);

            Console.WriteLine($"{id}{name}{route}{method}{cron}{lastExec}{nextExec}");
        }
    }

    private static async Task PublishProject(string[] args, ConfigManager _config, bool isUpdating = false)
    {
        var csprojFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 0)
        {
            Console.WriteLine("❌ No .csproj file found in the current directory.");
            return;
        }
        if (csprojFiles.Length > 1)
        {
            Console.WriteLine("❌ Multiple .csproj files found in the current directory. Please specify one.");
            return;
        }
        var projectPath = csprojFiles[0];

        string publishPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "publish");

        
        Console.WriteLine("🚀 Building project...");
        var publishProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{projectPath}\" -c Release -o \"{publishPath}\" --self-contained false -r linux-x64 -p:DebugType=none ",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        publishProcess.Start();
        await publishProcess.WaitForExitAsync();

        string errorOutput = await publishProcess.StandardError.ReadToEndAsync();
        if (publishProcess.ExitCode != 0)
        {
            Console.WriteLine($"❌ Compilation failed: {errorOutput}");
            Cleanups(publishPath, string.Empty);
            return;
        }
        // ---------------- ZIPPING ----------------------
        Console.WriteLine("📦 Zipping publish output...");
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(projectPath)}.zip");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        try
        {
            ZipFile.CreateFromDirectory(publishPath, zipPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error zipping: {ex.Message}");
            Cleanups(publishPath, null);
            return;
        }
        

        // ---------------- END ZIPPING -------------------

        Console.WriteLine("📤 Uploading ZIP package...");

        using var httpClient = new HttpClient();
        using var zipContent = new ByteArrayContent(await File.ReadAllBytesAsync(zipPath));
        zipContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        string endpoint = isUpdating ? 
            $"{_config.ConfigData.BaseUrl}/api/update/{_config.ConfigData.CurrentProyectId}?name={Path.GetFileNameWithoutExtension(projectPath)}.dll" 
            : $"{_config.ConfigData.BaseUrl}/api/upload?name={Path.GetFileNameWithoutExtension(projectPath)}.dll";


        HttpResponseMessage? response = null;
        if (isUpdating)
        {
            response = await httpClient.PutAsync(endpoint, zipContent);
        }
        else
        {
            response = await httpClient.PostAsync(endpoint, zipContent);
        }
        

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Upload failed. Status: {response.StatusCode}");
            Cleanups(publishPath, zipPath);
            return;
        }

        if (isUpdating)
        {
            Console.WriteLine("✅ Update successful");
        }
        else
        {
            IdResponse? resp = null;
            try
            {
                resp = await response.Content.ReadFromJsonAsync<IdResponse>();
            }
            catch (Exception)
            {
                Console.WriteLine("❌ Error: Invalid response from server.");
                Cleanups(publishPath, zipPath);
                return;
            }
            // Add project record to ConfigManager
            _config.AddProject(resp!.Id, projectPath);
            _config.SetCurrent(resp!.Id);
            Console.WriteLine($"✅ Upload successful, project id: {resp?.Id}");
        }
        Cleanups(publishPath, zipPath);
    }

    private static void Cleanups(string publishPath, string zipPath)
    {
        if(!string.IsNullOrEmpty(zipPath))File.Delete(zipPath);
        Directory.Delete(publishPath, true);
        Console.WriteLine("🧹 Cleanup completed.");
    }

    
    private static async Task ShowExecutions(string[] args, ConfigManager _config)
    {
        var project = _config.GetCurrentProject();
        if (project == null)
        {
            Console.WriteLine("❌ No current project selected.");
            return;
        }

        var response = await _httpClient.GetAsync($"{_config.ConfigData.BaseUrl}/api/results/{project.Id}?page=1&pageSize=500");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Error: Unable to fetch executions. Status: {response.StatusCode}");
            return;
        }

        var executionHistory = await response.Content.ReadFromJsonAsync<ExecutionList>();

        if (executionHistory == null || executionHistory.Items.Count == 0)
        {
            Console.WriteLine("⚠️ No executions found.");
            return;
        }

        // Column headers
        string[] headers = { "Function Name", "Date", "Next Execution", "Execution Time", "Loading Time", "Status", "Assembly ID" };
        int[] columnWidths = { 20, 30, 30, 15, 15, 10, 40 };

        Console.WriteLine("\n📌 Execution History:");
        Console.WriteLine(string.Join("", headers.Select((h, i) => h.PadRight(columnWidths[i]))));

        foreach (var exec in executionHistory.Items)
        {
            string name = exec.FunctionName.PadRight(columnWidths[0]);
            string date = exec.Date.ToString().PadRight(columnWidths[1]);
            string nextExec = exec.NextExecutionDate.ToLocalTime().ToString().PadRight(columnWidths[2]);
            string execTime = (exec.ExecutionTime?.ToString() ?? "N/A").PadRight(columnWidths[3]);
            string loadTime = (exec.LoadingTime?.ToString() ?? "N/A").PadRight(columnWidths[4]);
            string status = exec.Status.PadRight(columnWidths[5]);
            string assemblyId = exec.AssemblyId.PadRight(columnWidths[6]);

            Console.WriteLine($"{name}{date}{nextExec}{execTime}{loadTime}{status}{assemblyId}");
        }
    }

    private static async Task DeleteProject(string[] args, ConfigManager _config)
    {
        var project = _config.GetCurrentProject();
        if (project == null)
        {
            Console.WriteLine("❌ No current project selected.");
            return;
        }

        Console.WriteLine($"⚠️ Are you sure you want to delete the project with ID {project.Id}? (yes/no)");
        string? confirmation = Console.ReadLine()?.Trim().ToLower();

        if (confirmation != "yes")
        {
            Console.WriteLine("❌ Project deletion cancelled.");
            return;
        }

        var response = await _httpClient.DeleteAsync($"{_config.ConfigData.BaseUrl}/api/delete/{project.Id}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Error: Unable to delete execution. Status: {response.StatusCode}");
            return;
        }
        _config.RemoveProjectByAssemblyId(project.Id);
        Console.WriteLine($"✅ Project with ID {project.Id} remove from config successfully.");
        Console.WriteLine("✅ Project deleted successfully.");
    }
}
