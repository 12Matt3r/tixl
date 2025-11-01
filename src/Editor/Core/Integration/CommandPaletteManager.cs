using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TiXL.Editor.Core.Commands;
using TiXL.Editor.Core.Models;
using TiXL.Editor.Core.UI;

namespace TiXL.Editor.Core.Integration
{
    /// <summary>
    /// Main command palette manager that integrates keyboard shortcuts and UI
    /// </summary>
    public interface ICommandPaletteManager
    {
        /// <summary>
        /// Shows the command palette
        /// </summary>
        void ShowCommandPalette();

        /// <summary>
        /// Hides the command palette
        /// </summary>
        void HideCommandPalette();

        /// <summary>
        /// Toggles the command palette visibility
        /// </summary>
        void ToggleCommandPalette();

        /// <summary>
        /// Updates the command palette (call this in your main loop)
        /// </summary>
        void Update();

        /// <summary>
        /// Gets the command registry
        /// </summary>
        ICommandRegistry CommandRegistry { get; }

        /// <summary>
        /// Gets the current command palette state
        /// </summary>
        CommandPaletteState GetCurrentState();

        /// <summary>
        /// Event fired when the command palette is shown
        /// </summary>
        event EventHandler? CommandPaletteShown;

        /// <summary>
        /// Event fired when the command palette is hidden
        /// </summary>
        event EventHandler? CommandPaletteHidden;
    }

    /// <summary>
    /// Implementation of the command palette manager
    /// </summary>
    public class CommandPaletteManager : ICommandPaletteManager
    {
        private readonly ICommandRegistry _commandRegistry;
        private readonly IFuzzySearchEngine _searchEngine;
        private readonly CommandPalette _commandPalette;
        private readonly ILogger<CommandPaletteManager> _logger;
        private readonly IDefaultCommandProvider _defaultCommandProvider;

        private bool _isCtrlDown = false;
        private bool _isShiftDown = false;
        private bool _isPaletteOpen = false;

        public event EventHandler? CommandPaletteShown;
        public event EventHandler? CommandPaletteHidden;

        public CommandPaletteManager(
            ICommandRegistry commandRegistry,
            IFuzzySearchEngine searchEngine,
            CommandPalette commandPalette,
            ILogger<CommandPaletteManager> logger,
            IDefaultCommandProvider defaultCommandProvider)
        {
            _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
            _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
            _commandPalette = commandPalette ?? throw new ArgumentNullException(nameof(commandPalette));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultCommandProvider = defaultCommandProvider ?? throw new ArgumentNullException(nameof(defaultCommandProvider));

            // Subscribe to command palette events
            _commandPalette.CommandExecuted += OnCommandExecuted;
        }

        /// <summary>
        /// Gets the command registry
        /// </summary>
        public ICommandRegistry CommandRegistry => _commandRegistry;

        public void ShowCommandPalette()
        {
            if (!_isPaletteOpen)
            {
                _commandPalette.Show();
                _isPaletteOpen = true;
                
                // Register default commands if not already registered
                _defaultCommandProvider.RegisterDefaultCommands();

                CommandPaletteShown?.Invoke(this, EventArgs.Empty);
                _logger.LogInformation("Command palette shown");
            }
        }

        public void HideCommandPalette()
        {
            if (_isPaletteOpen)
            {
                _commandPalette.Hide();
                _isPaletteOpen = false;
                CommandPaletteHidden?.Invoke(this, EventArgs.Empty);
                _logger.LogInformation("Command palette hidden");
            }
        }

        public void ToggleCommandPalette()
        {
            if (_isPaletteOpen)
            {
                HideCommandPalette();
            }
            else
            {
                ShowCommandPalette();
            }
        }

        public void Update()
        {
            HandleKeyboardShortcuts();
            _commandPalette.Update();
        }

        /// <summary>
        /// Handles global keyboard shortcuts
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            var io = ImGui.GetIO();

            // Track modifier key states
            _isCtrlDown = io.KeyCtrl;
            _isShiftDown = io.KeyShift;

            // Handle Ctrl+Shift+P to open command palette
            if (_isCtrlDown && _isShiftDown && ImGui.IsKeyPressed(ImGuiKey.P))
            {
                ToggleCommandPalette();
                return;
            }

            // Handle Escape to close command palette (handled in CommandPalette.Update)
        }

        /// <summary>
        /// Handles command execution events
        /// </summary>
        private void OnCommandExecuted(object? sender, CommandExecutedEventArgs e)
        {
            _logger.LogInformation("Command executed: {CommandName} ({CommandId})", 
                e.Command.Name, e.Command.Id);

            // You could implement additional features here like:
            // - Notification system
            // - Undo/redo tracking
            // - Command execution history
            // - Performance monitoring
        }

        /// <summary>
        /// Gets the current command palette state
        /// </summary>
        public CommandPaletteState GetCurrentState()
        {
            return _commandPalette.GetState();
        }
    }

    /// <summary>
    /// Interface for providing default commands
    /// </summary>
    public interface IDefaultCommandProvider
    {
        /// <summary>
        /// Registers default commands
        /// </summary>
        void RegisterDefaultCommands();
    }

    /// <summary>
    /// Provides default editor commands
    /// </summary>
    public class DefaultCommandProvider : IDefaultCommandProvider
    {
        private readonly ICommandRegistry _commandRegistry;
        private readonly ILogger<DefaultCommandProvider> _logger;
        private bool _isRegistered = false;

        public DefaultCommandProvider(
            ICommandRegistry commandRegistry,
            ILogger<DefaultCommandProvider> logger)
        {
            _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterDefaultCommands()
        {
            if (_isRegistered)
                return;

            var commands = CreateDefaultCommands();
            _commandRegistry.RegisterCommands(commands, new DefaultCommandExecutor());

            _isRegistered = true;
            _logger.LogInformation("Registered {Count} default commands", commands.Count);
        }

        /// <summary>
        /// Creates the default editor commands
        /// </summary>
        private List<CommandDefinition> CreateDefaultCommands()
        {
            return new List<CommandDefinition>
            {
                // File Commands
                new CommandDefinition
                {
                    Id = "File.New",
                    Name = "New File",
                    Description = "Create a new file",
                    Category = CommandCategory.File,
                    Keywords = ImmutableArray.Create("new", "create", "file", "document"),
                    Shortcut = "Ctrl+N",
                    Priority = 100,
                    Icon = "📄"
                },
                new CommandDefinition
                {
                    Id = "File.Open",
                    Name = "Open File",
                    Description = "Open an existing file",
                    Category = CommandCategory.File,
                    Keywords = ImmutableArray.Create("open", "load", "file", "document"),
                    Shortcut = "Ctrl+O",
                    Priority = 100,
                    Icon = "📁"
                },
                new CommandDefinition
                {
                    Id = "File.Save",
                    Name = "Save File",
                    Description = "Save the current file",
                    Category = CommandCategory.File,
                    Keywords = ImmutableArray.Create("save", "file", "document"),
                    Shortcut = "Ctrl+S",
                    Priority = 100,
                    Icon = "💾"
                },
                new CommandDefinition
                {
                    Id = "File.SaveAs",
                    Name = "Save As...",
                    Description = "Save the current file with a new name",
                    Category = CommandCategory.File,
                    Keywords = ImmutableArray.Create("save", "as", "file", "document"),
                    Shortcut = "Ctrl+Shift+S",
                    Priority = 90,
                    Icon = "💾"
                },
                new CommandDefinition
                {
                    Id = "File.Close",
                    Name = "Close File",
                    Description = "Close the current file",
                    Category = CommandCategory.File,
                    Keywords = ImmutableArray.Create("close", "file", "document"),
                    Shortcut = "Ctrl+W",
                    Priority = 90,
                    Icon = "❌"
                },
                new CommandDefinition
                {
                    Id = "File.Exit",
                    Name = "Exit",
                    Description = "Exit the application",
                    Category = CommandCategory.File,
                    Keywords = ImmutableArray.Create("exit", "quit", "close", "application"),
                    Shortcut = "Alt+F4",
                    Priority = 80,
                    Icon = "🚪"
                },

                // Edit Commands
                new CommandDefinition
                {
                    Id = "Edit.Undo",
                    Name = "Undo",
                    Description = "Undo the last action",
                    Category = CommandCategory.Edit,
                    Keywords = ImmutableArray.Create("undo", "reverse", "cancel"),
                    Shortcut = "Ctrl+Z",
                    Priority = 100,
                    Icon = "↶"
                },
                new CommandDefinition
                {
                    Id = "Edit.Redo",
                    Name = "Redo",
                    Description = "Redo the last undone action",
                    Category = CommandCategory.Edit,
                    Keywords = ImmutableArray.Create("redo", "repeat", "restore"),
                    Shortcut = "Ctrl+Y",
                    Priority = 100,
                    Icon = "↷"
                },
                new CommandDefinition
                {
                    Id = "Edit.Cut",
                    Name = "Cut",
                    Description = "Cut the selected text or items",
                    Category = CommandCategory.Edit,
                    Keywords = ImmutableArray.Create("cut", "clip", "move"),
                    Shortcut = "Ctrl+X",
                    Priority = 100,
                    Icon = "✂️"
                },
                new CommandDefinition
                {
                    Id = "Edit.Copy",
                    Name = "Copy",
                    Description = "Copy the selected text or items",
                    Category = CommandCategory.Edit,
                    Keywords = ImmutableArray.Create("copy", "duplicate", "clone"),
                    Shortcut = "Ctrl+C",
                    Priority = 100,
                    Icon = "📋"
                },
                new CommandDefinition
                {
                    Id = "Edit.Paste",
                    Name = "Paste",
                    Description = "Paste from the clipboard",
                    Category = CommandCategory.Edit,
                    Keywords = ImmutableArray.Create("paste", "insert", "clipboard"),
                    Shortcut = "Ctrl+V",
                    Priority = 100,
                    Icon = "📋"
                },
                new CommandDefinition
                {
                    Id = "Edit.Find",
                    Name = "Find",
                    Description = "Find text or items",
                    Category = CommandCategory.Edit,
                    Keywords = ImmutableArray.Create("find", "search", "locate"),
                    Shortcut = "Ctrl+F",
                    Priority = 100,
                    Icon = "🔍"
                },
                new CommandDefinition
                {
                    Id = "Edit.Replace",
                    Name = "Replace",
                    Description = "Find and replace text or items",
                    Category = CommandCategory.Edit,
                    Keywords = ImmutableArray.Create("replace", "substitute", "change"),
                    Shortcut = "Ctrl+H",
                    Priority = 90,
                    Icon = "🔄"
                },

                // View Commands
                new CommandDefinition
                {
                    Id = "View.ZoomIn",
                    Name = "Zoom In",
                    Description = "Zoom in to get a closer view",
                    Category = CommandCategory.View,
                    Keywords = ImmutableArray.Create("zoom", "in", "closer", "bigger"),
                    Shortcut = "Ctrl+Plus",
                    Priority = 90,
                    Icon = "🔍+"
                },
                new CommandDefinition
                {
                    Id = "View.ZoomOut",
                    Name = "Zoom Out",
                    Description = "Zoom out to see more of the canvas",
                    Category = CommandCategory.View,
                    Keywords = ImmutableArray.Create("zoom", "out", "farther", "smaller"),
                    Shortcut = "Ctrl+-",
                    Priority = 90,
                    Icon = "🔍-"
                },
                new CommandDefinition
                {
                    Id = "View.ZoomReset",
                    Name = "Reset Zoom",
                    Description = "Reset zoom to default level",
                    Category = CommandCategory.View,
                    Keywords = ImmutableArray.Create("zoom", "reset", "default", "normal"),
                    Shortcut = "Ctrl+0",
                    Priority = 80,
                    Icon = "🔍0"
                },
                new CommandDefinition
                {
                    Id = "View.Fullscreen",
                    Name = "Toggle Fullscreen",
                    Description = "Toggle fullscreen mode",
                    Category = CommandCategory.View,
                    Keywords = ImmutableArray.Create("fullscreen", "maximize", "window"),
                    Shortcut = "F11",
                    Priority = 80,
                    Icon = "⛶"
                },

                // Tools Commands
                new CommandDefinition
                {
                    Id = "Tools.Preferences",
                    Name = "Preferences",
                    Description = "Open application preferences",
                    Category = CommandCategory.Tools,
                    Keywords = ImmutableArray.Create("preferences", "settings", "options", "configure"),
                    Shortcut = "Ctrl+,",
                    Priority = 90,
                    Icon = "⚙️"
                },
                new CommandDefinition
                {
                    Id = "Tools.CommandPalette",
                    Name = "Command Palette",
                    Description = "Open the command palette",
                    Category = CommandCategory.Tools,
                    Keywords = ImmutableArray.Create("command", "palette", "search", "find"),
                    Shortcut = "Ctrl+Shift+P",
                    Priority = 100,
                    Icon = "🎯"
                },
                new CommandDefinition
                {
                    Id = "Tools.ToggleTheme",
                    Name = "Toggle Theme",
                    Description = "Switch between light and dark themes",
                    Category = CommandCategory.Tools,
                    Keywords = ImmutableArray.Create("theme", "dark", "light", "color", "appearance"),
                    Priority = 70,
                    Icon = "🌓"
                },

                // Navigate Commands
                new CommandDefinition
                {
                    Id = "Navigate.GotoLine",
                    Name = "Go to Line...",
                    Description = "Navigate to a specific line number",
                    Category = CommandCategory.Navigate,
                    Keywords = ImmutableArray.Create("goto", "line", "jump", "navigate"),
                    Shortcut = "Ctrl+G",
                    Priority = 90,
                    Icon = "📍"
                },
                new CommandDefinition
                {
                    Id = "Navigate.RecentFiles",
                    Name = "Recent Files",
                    Description = "View and open recent files",
                    Category = CommandCategory.Navigate,
                    Keywords = ImmutableArray.Create("recent", "files", "history", "open"),
                    Priority = 80,
                    Icon = "📚"
                },

                // Insert Commands
                new CommandDefinition
                {
                    Id = "Insert.Text",
                    Name = "Insert Text",
                    Description = "Insert text at cursor position",
                    Category = CommandCategory.Insert,
                    Keywords = ImmutableArray.Create("insert", "text", "add", "type"),
                    Priority = 70,
                    Icon = "📝"
                },
                new CommandDefinition
                {
                    Id = "Insert.Image",
                    Name = "Insert Image",
                    Description = "Insert an image file",
                    Category = CommandCategory.Insert,
                    Keywords = ImmutableArray.Create("insert", "image", "picture", "photo"),
                    Priority = 70,
                    Icon = "🖼️"
                },

                // Help Commands
                new CommandDefinition
                {
                    Id = "Help.About",
                    Name = "About",
                    Description = "Show information about the application",
                    Category = CommandCategory.Help,
                    Keywords = ImmutableArray.Create("about", "information", "version", "help"),
                    Priority = 60,
                    Icon = "ℹ️"
                },
                new CommandDefinition
                {
                    Id = "Help.Documentation",
                    Name = "Documentation",
                    Description = "Open documentation in browser",
                    Category = CommandCategory.Help,
                    Keywords = ImmutableArray.Create("documentation", "help", "guide", "manual"),
                    Priority = 70,
                    Icon = "📖"
                },
                new CommandDefinition
                {
                    Id = "Help.Shortcuts",
                    Name = "Keyboard Shortcuts",
                    Description = "View all keyboard shortcuts",
                    Category = CommandCategory.Help,
                    Keywords = ImmutableArray.Create("shortcuts", "keyboard", "help", "keys"),
                    Priority = 80,
                    Icon = "⌨️"
                }
            };
        }

        /// <summary>
        /// Default command executor for built-in commands
        /// </summary>
        private class DefaultCommandExecutor : ICommandExecutor
        {
            public CommandResult Execute(CommandDefinition command, object? context = null)
            {
                // For demo purposes, we'll return success for all default commands
                // In a real implementation, you would perform the actual actions
                return new CommandResult
                {
                    Success = true,
                    Result = null,
                    ErrorMessage = null,
                    ExecutionTime = TimeSpan.Zero
                };
            }
        }
    }
}
