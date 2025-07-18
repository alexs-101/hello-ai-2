using System.Reflection;
using System.Runtime.Loader;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PluginFramework;

public class PluginManager : IPluginManager, IDisposable
{
    private readonly ILogger<PluginManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<IProtocolPlugin> _loadedPlugins = new();
    private readonly List<PluginLoadContext> _pluginContexts = new();
    private readonly string _pluginDirectory;
    private FileSystemWatcher? _watcher;
    private bool _disposed = false;

    public PluginManager(ILogger<PluginManager> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _pluginDirectory = configuration["PluginSettings:Directory"] ?? "./plugins";
        
        if (!Directory.Exists(_pluginDirectory))
        {
            Directory.CreateDirectory(_pluginDirectory);
        }
    }

    public async Task<IEnumerable<IProtocolPlugin>> LoadPluginsAsync()
    {
        _logger.LogInformation("Loading plugins from {Directory}", _pluginDirectory);
        
        var pluginFiles = Directory.GetFiles(_pluginDirectory, "*.dll");
        
        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                await LoadPluginAsync(pluginFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin {PluginFile}", pluginFile);
            }
        }

        SetupHotReload();
        return _loadedPlugins;
    }

    private async Task LoadPluginAsync(string pluginPath)
    {
        _logger.LogDebug("Loading plugin from {PluginPath}", pluginPath);
        
        var loadContext = new PluginLoadContext(pluginPath);
        _pluginContexts.Add(loadContext);
        
        var assembly = loadContext.LoadFromAssemblyPath(pluginPath);
        var pluginTypes = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IProtocolPlugin).IsAssignableFrom(type))
            .ToList();

        foreach (var pluginType in pluginTypes)
        {
            var plugin = Activator.CreateInstance(pluginType) as IProtocolPlugin;
            if (plugin != null)
            {
                await plugin.InitializeAsync(_configuration);
                _loadedPlugins.Add(plugin);
                
                _logger.LogInformation("Loaded plugin {PluginName} v{Version} for protocol {Protocol}", 
                    plugin.Name, plugin.Version, plugin.SupportedProtocol);
            }
        }
    }

    private void SetupHotReload()
    {
        var enableHotReload = _configuration.GetValue<bool>("PluginSettings:EnableHotReload", false);
        if (!enableHotReload)
        {
            return;
        }

        _watcher = new FileSystemWatcher(_pluginDirectory, "*.dll");
        _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
        _watcher.Changed += OnPluginFileChanged;
        _watcher.Created += OnPluginFileChanged;
        _watcher.EnableRaisingEvents = true;
        
        _logger.LogInformation("Plugin hot-reload enabled for {Directory}", _pluginDirectory);
    }

    private async void OnPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            await Task.Delay(100); // Small delay to ensure file is fully written
            await ReloadPluginAsync(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hot-reload plugin {PluginPath}", e.FullPath);
        }
    }

    private async Task ReloadPluginAsync(string pluginPath)
    {
        _logger.LogInformation("Hot-reloading plugin {PluginPath}", pluginPath);
        
        // Find and unload existing plugin
        var existingPlugin = _loadedPlugins.FirstOrDefault(p => p.GetType().Assembly.Location == pluginPath);
        if (existingPlugin != null)
        {
            await existingPlugin.CleanupAsync();
            _loadedPlugins.Remove(existingPlugin);
        }

        // Load new version
        await LoadPluginAsync(pluginPath);
    }

    public IProtocolPlugin? GetPlugin(ProtocolType protocolType)
    {
        return _loadedPlugins.FirstOrDefault(p => p.SupportedProtocol == protocolType);
    }

    public async Task<IProtocolPlugin?> GetPluginForDataAsync(byte[] data)
    {
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                if (await plugin.CanHandleAsync(data))
                {
                    return plugin;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if plugin {PluginName} can handle data", plugin.Name);
            }
        }
        return null;
    }

    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down plugin manager");
        
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                await plugin.CleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up plugin {PluginName}", plugin.Name);
            }
        }
        
        _loadedPlugins.Clear();
        
        foreach (var context in _pluginContexts)
        {
            try
            {
                context.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading plugin context");
            }
        }
        
        _pluginContexts.Clear();
        
        _watcher?.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Task.Run(async () => await ShutdownAsync()).Wait();
            _disposed = true;
        }
    }
}