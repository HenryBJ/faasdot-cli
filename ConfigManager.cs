using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Serverless.CLI;

public class ConfigManager
{
    private readonly string ConfigPath;

    public Config ConfigData { get; private set; }

    public ConfigManager(bool isDevMode = false)
    {
        ConfigPath = GetConfigPath(isDevMode);
        LoadConfig();
    }

    public static string GetConfigPath(bool isDevMode = false)
    {
        // Windows: %APPDATA%\faasdot\faasdot.config.json
        //Linux: ~/.config/faasdot/faasdot.config.json
        string basePath;
        if (isDevMode)
        {
            basePath = AppContext.BaseDirectory; // Directorio del ejecutable
        }
        else 
        {
            if (OperatingSystem.IsWindows())
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "faasdot");
            else
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "faasdot");

            Directory.CreateDirectory(basePath);
        }
        return Path.Combine(basePath, "faasdot.config.json");
    }

    private void LoadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            ConfigData = JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        else
        {
            ConfigData = new Config();
            SaveConfig();
        }
    }

    public void SaveConfig()
    {
        var json = JsonSerializer.Serialize(ConfigData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public void SetBaseUrl(string url)
    {
        ConfigData.BaseUrl = url;
        SaveConfig();
    }

    public void SetToken(string token)
    {
        ConfigData.Token = token;
        SaveConfig();
    }

    public void AddProject(Guid assemblyId, string path)
    {
        ConfigData.Projects.Add(new Project { Id = assemblyId, Path = path });
        if (ConfigData.Projects.Count == 0)
        {
            ConfigData.CurrentProyectId = assemblyId;
        }
        SaveConfig();
    }

    public List<Project> ListProjects()
    {
        return ConfigData.Projects.ToList();
    }

    public void SetCurrent(Guid assemblyId)
    {
        if (ConfigData.Projects.Any(p => p.Id == assemblyId))
        {
            ConfigData.CurrentProyectId = assemblyId;
            SaveConfig();
        }
        else
        {
            throw new ArgumentException("Wrong Id.");
        }
    }

    public Project? GetCurrentProject()
    {
        return ConfigData.Projects.FirstOrDefault(p => p.Id == ConfigData.CurrentProyectId);
    }

    public Guid? GetProjectIdByPath(string path)
    {
        var project = ConfigData.Projects.FirstOrDefault(p => p.Path == path);
        return project?.Id ?? null;
    }


    public void RemoveProjectByAssemblyId(Guid assemblyId, bool isDevMode = false)
    {

        ConfigData.Projects.RemoveAll(p => p.Id == assemblyId);
        SaveConfig();
    }
}

public class Config
{
    public string AuthUrl { get; set; } = "https://auth.joseenrique.dev";
    public string BaseUrl { get; set; } = "http://localhost:5051";
    public Guid CurrentProyectId { get; set; }
    public string Token { get; set; } = "";
    public List<Project> Projects { get; set; } = new();
}

public class Project
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Path { get; set; } = "";
}
