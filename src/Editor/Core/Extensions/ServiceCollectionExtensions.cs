using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TiXL.Editor.Core.Commands;
using TiXL.Editor.Core.Integration;
using TiXL.Editor.Core.Plugins;
using TiXL.Editor.Core.UI;

namespace TiXL.Editor.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering command palette services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the command palette system to the service collection
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddCommandPalette(this IServiceCollection services)
        {
            // Core services
            services.AddSingleton<ICommandRegistry, CommandRegistry>();
            services.AddSingleton<IFuzzySearchEngine>(provider => 
                AdvancedFuzzySearchEngine.Create());
            services.AddSingleton<CommandPalette>();
            
            // Management services
            services.AddSingleton<ICommandPaletteManager, CommandPaletteManager>();
            services.AddSingleton<IDefaultCommandProvider, DefaultCommandProvider>();
            services.AddSingleton<IPluginManager, PluginManager>();

            // Logging
            services.AddTransient<ILogger, Logger>();
            services.AddTransient<ILogger<CommandRegistry>, Logger<CommandRegistry>>();
            services.AddTransient<ILogger<AdvancedFuzzySearchEngine>, Logger<AdvancedFuzzySearchEngine>>();
            services.AddTransient<ILogger<CommandPalette>, Logger<CommandPalette>>();
            services.AddTransient<ILogger<CommandPaletteManager>, Logger<CommandPaletteManager>>();
            services.AddTransient<ILogger<DefaultCommandProvider>, Logger<DefaultCommandProvider>>();
            services.AddTransient<ILogger<PluginManager>, Logger<PluginManager>>();

            return services;
        }

        /// <summary>
        /// Adds command palette services with custom configuration
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configureOptions">Configuration options</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddCommandPalette(this IServiceCollection services, 
            Action<CommandPaletteOptions> configureOptions)
        {
            var options = new CommandPaletteOptions();
            configureOptions(options);

            services.AddSingleton(options);
            
            return services.AddCommandPalette();
        }
    }

    /// <summary>
    /// Configuration options for the command palette system
    /// </summary>
    public class CommandPaletteOptions
    {
        /// <summary>
        /// Maximum number of search results to return
        /// </summary>
        public int MaxSearchResults { get; set; } = 50;

        /// <summary>
        /// Maximum number of recent commands to track
        /// </summary>
        public int MaxRecentCommands { get; set; } = 50;

        /// <summary>
        /// Minimum score threshold for search results
        /// </summary>
        public float MinSearchScore { get; set; } = 0.1f;

        /// <summary>
        /// Whether to automatically load plugins on startup
        /// </summary>
        public bool AutoLoadPlugins { get; set; } = true;

        /// <summary>
        /// Whether to register default commands automatically
        /// </summary>
        public bool RegisterDefaultCommands { get; set; } = true;

        /// <summary>
        /// Whether to enable keyboard shortcuts
        /// </summary>
        public bool EnableKeyboardShortcuts { get; set; } = true;

        /// <summary>
        /// The keyboard shortcut to open the command palette
        /// </summary>
        public string CommandPaletteShortcut { get; set; } = "Ctrl+Shift+P";

        /// <summary>
        /// Whether to show debug information
        /// </summary>
        public bool EnableDebugMode { get; set; } = false;
    }

    /// <summary>
    /// Extension methods for the command palette system
    /// </summary>
    public static class CommandPaletteExtensions
    {
        /// <summary>
        /// Adds a custom command to the registry
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="command">Command to add</param>
        /// <param name="executor">Command executor</param>
        /// <returns>The registry for chaining</returns>
        public static ICommandRegistry AddCommand(this ICommandRegistry registry, 
            CommandDefinition command, ICommandExecutor executor)
        {
            registry.RegisterCommand(command, executor);
            return registry;
        }

        /// <summary>
        /// Adds multiple commands to the registry
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="commands">Commands to add</param>
        /// <param name="executor">Command executor</param>
        /// <returns>The registry for chaining</returns>
        public static ICommandRegistry AddCommands(this ICommandRegistry registry, 
            IEnumerable<CommandDefinition> commands, ICommandExecutor executor)
        {
            registry.RegisterCommands(commands, executor);
            return registry;
        }

        /// <summary>
        /// Creates a builder for registering commands
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="executor">Default executor for registered commands</param>
        /// <returns>A command builder</returns>
        public static CommandBuilder CreateCommands(this ICommandRegistry registry, ICommandExecutor executor)
        {
            return new CommandBuilder(registry, executor);
        }

        /// <summary>
        /// Gets the count of commands in the registry
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <returns>Total number of commands</returns>
        public static int GetCommandCount(this ICommandRegistry registry)
        {
            return registry.GetAllCommands().Length;
        }

        /// <summary>
        /// Gets commands by multiple categories
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="categories">Categories to filter by</param>
        /// <returns>Commands matching any of the specified categories</returns>
        public static ImmutableArray<CommandDefinition> GetCommandsByCategories(this ICommandRegistry registry, 
            params CommandCategory[] categories)
        {
            return registry.GetAllCommands()
                .Where(c => categories.Contains(c.Category))
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets a command by exact name match
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="name">Exact command name</param>
        /// <returns>The command definition or null if not found</returns>
        public static CommandDefinition? GetCommandByName(this ICommandRegistry registry, string name)
        {
            return registry.GetAllCommands()
                .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Searches for commands by name or description
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="query">Search query</param>
        /// <returns>Matching commands</returns>
        public static ImmutableArray<CommandDefinition> SearchCommands(this ICommandRegistry registry, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return registry.GetAllCommands();

            var queryLower = query.ToLowerInvariant();
            return registry.GetAllCommands()
                .Where(c => 
                    c.Name.ToLowerInvariant().Contains(queryLower) ||
                    c.Description.ToLowerInvariant().Contains(queryLower) ||
                    c.Keywords.Any(k => k.ToLowerInvariant().Contains(queryLower)))
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets the most frequently used commands
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="count">Number of commands to return</param>
        /// <returns>Most frequently used commands</returns>
        public static ImmutableArray<CommandDefinition> GetMostUsedCommands(this ICommandRegistry registry, int count = 10)
        {
            return registry.GetAllCommands()
                .OrderByDescending(c => c.UsageCount)
                .ThenBy(c => c.Name)
                .Take(count)
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets commands by plugin name
        /// </summary>
        /// <param name="registry">Command registry</param>
        /// <param name="pluginName">Plugin name</param>
        /// <returns>Commands from the specified plugin</returns>
        public static ImmutableArray<CommandDefinition> GetCommandsByPlugin(this ICommandRegistry registry, string pluginName)
        {
            return registry.GetAllCommands()
                .Where(c => string.Equals(c.PluginName, pluginName, StringComparison.OrdinalIgnoreCase))
                .ToImmutableArray();
        }
    }

    /// <summary>
    /// Builder for registering commands fluently
    /// </summary>
    public class CommandBuilder
    {
        private readonly ICommandRegistry _registry;
        private readonly ICommandExecutor _executor;

        internal CommandBuilder(ICommandRegistry registry, ICommandExecutor executor)
        {
            _registry = registry;
            _executor = executor;
        }

        /// <summary>
        /// Adds a command to the builder
        /// </summary>
        /// <param name="command">Command to add</param>
        /// <returns>This builder for chaining</returns>
        public CommandBuilder Add(CommandDefinition command)
        {
            _registry.RegisterCommand(command, _executor);
            return this;
        }

        /// <summary>
        /// Adds multiple commands
        /// </summary>
        /// <param name="commands">Commands to add</param>
        /// <returns>This builder for chaining</returns>
        public CommandBuilder AddRange(IEnumerable<CommandDefinition> commands)
        {
            _registry.RegisterCommands(commands, _executor);
            return this;
        }

        /// <summary>
        /// Completes the command registration
        /// </summary>
        /// <returns>The command registry</returns>
        public ICommandRegistry Build()
        {
            return _registry;
        }
    }
}
