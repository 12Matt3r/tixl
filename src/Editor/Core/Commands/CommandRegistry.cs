using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace TiXL.Editor.Core.Commands
{
    /// <summary>
    /// Central registry for all editor commands
    /// </summary>
    public interface ICommandRegistry
    {
        /// <summary>
        /// Registers a command with the registry
        /// </summary>
        /// <param name="command">The command to register</param>
        /// <param name="executor">The executor for this command</param>
        void RegisterCommand(CommandDefinition command, ICommandExecutor executor);

        /// <summary>
        /// Registers multiple commands
        /// </summary>
        /// <param name="commands">Commands to register</param>
        /// <param name="executor">The executor for these commands</param>
        void RegisterCommands(IEnumerable<CommandDefinition> commands, ICommandExecutor executor);

        /// <summary>
        /// Unregisters a command
        /// </summary>
        /// <param name="commandId">ID of the command to unregister</param>
        void UnregisterCommand(string commandId);

        /// <summary>
        /// Gets all registered commands
        /// </summary>
        /// <returns>All commands</returns>
        ImmutableArray<CommandDefinition> GetAllCommands();

        /// <summary>
        /// Gets commands by category
        /// </summary>
        /// <param name="category">Category to filter by</param>
        /// <returns>Commands in the specified category</returns>
        ImmutableArray<CommandDefinition> GetCommandsByCategory(CommandCategory category);

        /// <summary>
        /// Gets a command by ID
        /// </summary>
        /// <param name="commandId">Command ID</param>
        /// <returns>The command definition or null if not found</returns>
        CommandDefinition? GetCommand(string commandId);

        /// <summary>
        /// Gets the executor for a command
        /// </summary>
        /// <param name="commandId">Command ID</param>
        /// <returns>The executor or null if not found</returns>
        ICommandExecutor? GetExecutor(string commandId);

        /// <summary>
        /// Executes a command by ID
        /// </summary>
        /// <param name="commandId">Command ID</param>
        /// <param name="context">Additional context</param>
        /// <returns>Execution result</returns>
        CommandResult ExecuteCommand(string commandId, object? context = null);

        /// <summary>
        /// Records command usage for tracking recent commands
        /// </summary>
        /// <param name="commandId">Command ID</param>
        void RecordCommandUsage(string commandId);

        /// <summary>
        /// Gets recently used commands
        /// </summary>
        /// <param name="count">Number of recent commands to return</param>
        /// <returns>Recent commands</returns>
        ImmutableArray<CommandDefinition> GetRecentCommands(int count = 10);

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
        /// Event fired when a command is registered
        /// </summary>
        event EventHandler<CommandRegisteredEventArgs>? CommandRegistered;

        /// <summary>
        /// Event fired when a command is unregistered
        /// </summary>
        event EventHandler<CommandUnregisteredEventArgs>? CommandUnregistered;
    }

    /// <summary>
    /// Event arguments for command registration events
    /// </summary>
    public class CommandRegisteredEventArgs : EventArgs
    {
        public CommandDefinition Command { get; }
        public ICommandExecutor Executor { get; }

        public CommandRegisteredEventArgs(CommandDefinition command, ICommandExecutor executor)
        {
            Command = command;
            Executor = executor;
        }
    }

    /// <summary>
    /// Event arguments for command unregistration events
    /// </summary>
    public class CommandUnregisteredEventArgs : EventArgs
    {
        public string CommandId { get; }

        public CommandUnregisteredEventArgs(string commandId)
        {
            CommandId = commandId;
        }
    }

    /// <summary>
    /// Implementation of the command registry
    /// </summary>
    public class CommandRegistry : ICommandRegistry
    {
        private readonly ILogger<CommandRegistry> _logger;
        private readonly ConcurrentDictionary<string, RegisteredCommand> _commands = new();
        private readonly List<CommandDefinition> _recentCommands = new();
        private readonly object _recentCommandsLock = new object();

        /// <summary>
        /// Maximum number of recent commands to track
        /// </summary>
        private const int MaxRecentCommands = 50;

        public event EventHandler<CommandRegisteredEventArgs>? CommandRegistered;
        public event EventHandler<CommandUnregisteredEventArgs>? CommandUnregistered;

        public CommandRegistry(ILogger<CommandRegistry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterCommand(CommandDefinition command, ICommandExecutor executor)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (string.IsNullOrWhiteSpace(command.Id))
                throw new ArgumentException("Command ID cannot be null or empty", nameof(command));

            if (_commands.TryAdd(command.Id, new RegisteredCommand(command, executor)))
            {
                _logger.LogInformation("Registered command: {CommandId} - {CommandName}", command.Id, command.Name);
                CommandRegistered?.Invoke(this, new CommandRegisteredEventArgs(command, executor));
            }
            else
            {
                _logger.LogWarning("Command already registered: {CommandId}", command.Id);
            }
        }

        public void RegisterCommands(IEnumerable<CommandDefinition> commands, ICommandExecutor executor)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            foreach (var command in commands)
            {
                RegisterCommand(command, executor);
            }
        }

        public void UnregisterCommand(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID cannot be null or empty", nameof(commandId));

            if (_commands.TryRemove(commandId, out _))
            {
                _logger.LogInformation("Unregistered command: {CommandId}", commandId);
                CommandUnregistered?.Invoke(this, new CommandUnregisteredEventArgs(commandId));
            }
        }

        public ImmutableArray<CommandDefinition> GetAllCommands()
        {
            return _commands.Values.Select(v => v.Definition).ToImmutableArray();
        }

        public ImmutableArray<CommandDefinition> GetCommandsByCategory(CommandCategory category)
        {
            return _commands.Values
                .Where(v => v.Definition.Category == category)
                .Select(v => v.Definition)
                .ToImmutableArray();
        }

        public CommandDefinition? GetCommand(string commandId)
        {
            return _commands.TryGetValue(commandId, out var registered) ? registered.Definition : null;
        }

        public ICommandExecutor? GetExecutor(string commandId)
        {
            return _commands.TryGetValue(commandId, out var registered) ? registered.Executor : null;
        }

        public CommandResult ExecuteCommand(string commandId, object? context = null)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                return new CommandResult
                {
                    Success = false,
                    ErrorMessage = "Command ID cannot be null or empty"
                };

            if (!_commands.TryGetValue(commandId, out var registered))
            {
                return new CommandResult
                {
                    Success = false,
                    ErrorMessage = $"Command not found: {commandId}"
                };
            }

            try
            {
                var startTime = DateTime.UtcNow;
                var result = registered.Executor.Execute(registered.Definition, context);
                result.ExecutionTime = DateTime.UtcNow - startTime;
                
                if (result.Success)
                {
                    RecordCommandUsage(commandId);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {CommandId}", commandId);
                return new CommandResult
                {
                    Success = false,
                    ErrorMessage = $"Execution failed: {ex.Message}"
                };
            }
        }

        public void RecordCommandUsage(string commandId)
        {
            if (!_commands.TryGetValue(commandId, out var registered))
                return;

            lock (_recentCommandsLock)
            {
                // Update usage count and last used time
                registered.Definition.UsageCount++;
                registered.Definition.LastUsed = DateTime.UtcNow;

                // Remove from recent commands list if it exists
                _recentCommands.RemoveAll(c => c.Id == commandId);

                // Add to beginning of recent commands
                _recentCommands.Insert(0, registered.Definition);

                // Trim to maximum size
                while (_recentCommands.Count > MaxRecentCommands)
                {
                    _recentCommands.RemoveAt(_recentCommands.Count - 1);
                }
            }

            _logger.LogDebug("Recorded usage for command: {CommandId}", commandId);
        }

        public ImmutableArray<CommandDefinition> GetRecentCommands(int count = 10)
        {
            lock (_recentCommandsLock)
            {
                return _recentCommands.Take(count).ToImmutableArray();
            }
        }

        public void RegisterPlugin(ICommandPalettePlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));

            var commands = plugin.GetCommands().ToList();
            if (commands.Count == 0)
            {
                _logger.LogWarning("Plugin {PluginName} has no commands to register", plugin.Name);
                return;
            }

            foreach (var command in commands)
            {
                command.PluginName = plugin.Name;
                RegisterCommand(command, new PluginCommandExecutor(plugin));
            }

            _logger.LogInformation("Registered {Count} commands from plugin: {PluginName}", commands.Count, plugin.Name);
        }

        public void UnregisterPlugin(ICommandPalettePlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));

            // Find all commands from this plugin
            var pluginCommands = _commands.Values
                .Where(v => v.Definition.PluginName == plugin.Name)
                .ToList();

            foreach (var registeredCommand in pluginCommands)
            {
                UnregisterCommand(registeredCommand.Definition.Id);
            }

            _logger.LogInformation("Unregistered {Count} commands from plugin: {PluginName}", 
                pluginCommands.Count, plugin.Name);
        }

        /// <summary>
        /// Internal class to hold registered command data
        /// </summary>
        private class RegisteredCommand
        {
            public CommandDefinition Definition { get; }
            public ICommandExecutor Executor { get; }

            public RegisteredCommand(CommandDefinition definition, ICommandExecutor executor)
            {
                Definition = definition;
                Executor = executor;
            }
        }

        /// <summary>
        /// Executor that delegates to plugin methods
        /// </summary>
        private class PluginCommandExecutor : ICommandExecutor
        {
            private readonly ICommandPalettePlugin _plugin;

            public PluginCommandExecutor(ICommandPalettePlugin plugin)
            {
                _plugin = plugin;
            }

            public CommandResult Execute(CommandDefinition command, object? context = null)
            {
                // Plugins can override this method to provide custom execution
                return new CommandResult
                {
                    Success = true,
                    Result = null,
                    ErrorMessage = null
                };
            }
        }
    }
}
