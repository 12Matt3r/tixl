using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TiXL.Editor.Core.Models;

namespace TiXL.Editor.Core.Plugins
{
    /// <summary>
    /// Example plugin that provides sample custom commands
    /// </summary>
    public class SamplePlugin : ICommandPalettePlugin
    {
        public string Name => "Sample Plugin";

        public IEnumerable<CommandDefinition> GetCommands()
        {
            return new[]
            {
                new CommandDefinition
                {
                    Id = "Sample.Calculator",
                    Name = "Calculator",
                    Description = "Open the built-in calculator",
                    Category = CommandCategory.Tools,
                    Keywords = ImmutableArray.Create("calculator", "math", "compute"),
                    Priority = 50,
                    Icon = "🧮"
                },
                new CommandDefinition
                {
                    Id = "Sample.ColorPicker",
                    Name = "Color Picker",
                    Description = "Open the color picker dialog",
                    Category = CommandCategory.Tools,
                    Keywords = ImmutableArray.Create("color", "picker", "palette"),
                    Priority = 50,
                    Icon = "🎨"
                },
                new CommandDefinition
                {
                    Id = "Sample.FileAnalyzer",
                    Name = "Analyze File",
                    Description = "Analyze the current file for issues",
                    Category = CommandCategory.Tools,
                    Keywords = ImmutableArray.Create("analyze", "file", "scan", "check"),
                    Priority = 60,
                    Icon = "📊"
                },
                new CommandDefinition
                {
                    Id = "Sample.BackupProject",
                    Name = "Backup Project",
                    Description = "Create a backup of the current project",
                    Category = CommandCategory.File,
                    Keywords = ImmutableArray.Create("backup", "project", "save", "copy"),
                    Priority = 70,
                    Icon = "💾"
                },
                new CommandDefinition
                {
                    Id = "Sample.ExportSettings",
                    Name = "Export Settings",
                    Description = "Export application settings to a file",
                    Category = CommandCategory.Tools,
                    Keywords = ImmutableArray.Create("export", "settings", "configuration"),
                    Priority = 40,
                    Icon = "📤"
                }
            };
        }
    }

    /// <summary>
    /// Plugin manager for handling plugin lifecycle and command registration
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// Loads and registers all plugins
        /// </summary>
        void LoadPlugins();

        /// <summary>
        /// Registers a plugin
        /// </summary>
        /// <param name="plugin">Plugin to register</param>
        void RegisterPlugin(ICommandPalettePlugin plugin);

        /// <summary>
        /// Unregisters a plugin
        /// </summary>
        /// <param name="plugin">Plugin to unregister</param>
        void UnregisterPlugin(ICommandPalettePlugin plugin);

        /// <summary>
        /// Gets all loaded plugins
        /// </summary>
        /// <returns>Collection of loaded plugins</returns>
        IReadOnlyList<ICommandPalettePlugin> GetLoadedPlugins();

        /// <summary>
        /// Event fired when a plugin is registered
        /// </summary>
        event EventHandler<PluginEventArgs>? PluginRegistered;

        /// <summary>
        /// Event fired when a plugin is unregistered
        /// </summary>
        event EventHandler<PluginEventArgs>? PluginUnregistered;
    }

    /// <summary>
    /// Event arguments for plugin events
    /// </summary>
    public class PluginEventArgs : EventArgs
    {
        public ICommandPalettePlugin Plugin { get; }

        public PluginEventArgs(ICommandPalettePlugin plugin)
        {
            Plugin = plugin;
        }
    }

    /// <summary>
    /// Implementation of the plugin manager
    /// </summary>
    public class PluginManager : IPluginManager
    {
        private readonly ICommandRegistry _commandRegistry;
        private readonly ILogger<PluginManager> _logger;
        private readonly List<ICommandPalettePlugin> _loadedPlugins = new();

        public event EventHandler<PluginEventArgs>? PluginRegistered;
        public event EventHandler<PluginEventArgs>? PluginUnregistered;

        public PluginManager(
            ICommandRegistry commandRegistry,
            ILogger<PluginManager> logger)
        {
            _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LoadPlugins()
        {
            // In a real implementation, this would:
            // 1. Scan for plugin assemblies in a plugins directory
            // 2. Load them dynamically using reflection
            // 3. Instantiate plugin classes
            // 4. Register them with the command registry

            _logger.LogInformation("Loading plugins...");

            // For demo purposes, we'll load the sample plugin
            var samplePlugin = new SamplePlugin();
            RegisterPlugin(samplePlugin);

            _logger.LogInformation("Plugin loading completed. Loaded {Count} plugin(s).", _loadedPlugins.Count);
        }

        public void RegisterPlugin(ICommandPalettePlugin plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            try
            {
                _commandRegistry.RegisterPlugin(plugin);
                _loadedPlugins.Add(plugin);

                var commandCount = plugin.GetCommands().Count();
                _logger.LogInformation("Registered plugin '{PluginName}' with {CommandCount} commands", 
                    plugin.Name, commandCount);

                PluginRegistered?.Invoke(this, new PluginEventArgs(plugin));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register plugin '{PluginName}'", plugin.Name);
                throw;
            }
        }

        public void UnregisterPlugin(ICommandPalettePlugin plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            try
            {
                _commandRegistry.UnregisterPlugin(plugin);
                _loadedPlugins.Remove(plugin);

                _logger.LogInformation("Unregistered plugin '{PluginName}'", plugin.Name);

                PluginUnregistered?.Invoke(this, new PluginEventArgs(plugin));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister plugin '{PluginName}'", plugin.Name);
                throw;
            }
        }

        public IReadOnlyList<ICommandPalettePlugin> GetLoadedPlugins()
        {
            return _loadedPlugins.AsReadOnly();
        }
    }
}
