using System;
using System.Collections.Generic;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TiXL.Editor.Core;
using TiXL.Editor.Core.Commands;
using TiXL.Editor.Core.Extensions;
using TiXL.Editor.Core.Integration;
using TiXL.Editor.Core.Models;
using TiXL.Editor.Core.Plugins;
using TiXL.Editor.Core.UI;

namespace TiXL.Editor.Examples
{
    /// <summary>
    /// Example application demonstrating the command palette system integration
    /// </summary>
    public class CommandPaletteDemo : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICommandPaletteManager _commandPaletteManager;
        private readonly ICommandRegistry _commandRegistry;
        private readonly ILogger<CommandPaletteDemo> _logger;
        
        private bool _isRunning = true;

        public CommandPaletteDemo()
        {
            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            
            _serviceProvider = services.BuildServiceProvider();
            
            // Get services
            _commandPaletteManager = _serviceProvider.GetRequiredService<ICommandPaletteManager>();
            _commandRegistry = _serviceProvider.GetRequiredService<ICommandRegistry>();
            _logger = _serviceProvider.GetRequiredService<ILogger<CommandPaletteDemo>>();

            // Register event handlers
            _commandPaletteManager.CommandPaletteShown += OnCommandPaletteShown;
            _commandPaletteManager.CommandPaletteHidden += OnCommandPaletteHidden;

            _logger.LogInformation("Command palette demo initialized");
        }

        /// <summary>
        /// Configures the service collection with all required dependencies
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add command palette system
            services.AddCommandPalette(options =>
            {
                options.MaxSearchResults = 50;
                options.MaxRecentCommands = 20;
                options.EnableKeyboardShortcuts = true;
                options.AutoLoadPlugins = true;
                options.EnableDebugMode = false;
            });

            // Add demo-specific services
            services.AddSingleton<ITestEditor, TestEditor>();
            services.AddSingleton<EditorCommandExecutor>();
        }

        /// <summary>
        /// Runs the demo application
        /// </summary>
        public void Run()
        {
            _logger.LogInformation("Starting command palette demo...");

            // Initialize ImGui (this would normally be done by your main application)
            InitializeImGui();

            // Main application loop
            while (_isRunning)
            {
                Update();
                Render();
            }

            Cleanup();
        }

        /// <summary>
        /// Initializes ImGui (mock implementation for demo)
        /// </summary>
        private void InitializeImGui()
        {
            _logger.LogInformation("Initializing ImGui...");
            // In a real application, this would initialize ImGui with your window handle
        }

        /// <summary>
        /// Updates the application state
        /// </summary>
        private void Update()
        {
            // Update input and handle events
            ProcessInput();
            
            // Update the command palette
            _commandPaletteManager.Update();

            // Check for exit conditions
            if (ShouldExit())
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Renders the application UI
        /// </summary>
        private void Render()
        {
            // In a real application, this would call ImGui.Render()
            
            // For demo purposes, we'll simulate frame updates
            SimulateFrameRendering();
        }

        /// <summary>
        /// Processes input events
        /// </summary>
        private void ProcessInput()
        {
            // Handle global application shortcuts (outside of command palette)
            var io = ImGui.GetIO();
            
            // Check for Ctrl+Q to quit
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Q))
            {
                _logger.LogInformation("Quit shortcut pressed");
                _isRunning = false;
            }
        }

        /// <summary>
        /// Simulates frame rendering for demo purposes
        /// </summary>
        private void SimulateFrameRendering()
        {
            // This would normally be your main application frame rendering
            
            // Demo: Show some frame statistics
            if (Environment.TickCount % 1000 == 0) // Every second
            {
                var state = _commandPaletteManager.GetCurrentState();
                _logger.LogDebug("Frame stats - Visible: {IsVisible}, Results: {ResultCount}", 
                    state.IsVisible, state.SearchResults.Length);
            }
        }

        /// <summary>
        /// Determines if the application should exit
        /// </summary>
        private bool ShouldExit()
        {
            // Check various exit conditions
            return !_isRunning;
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        private void Cleanup()
        {
            _logger.LogInformation("Cleaning up resources...");
            
            // Dispose services
            _serviceProvider.Dispose();
        }

        /// <summary>
        /// Event handler for when the command palette is shown
        /// </summary>
        private void OnCommandPaletteShown(object? sender, EventArgs e)
        {
            _logger.LogInformation("Command palette shown - Press Esc to close, Ctrl+Shift+P to toggle");
        }

        /// <summary>
        /// Event handler for when the command palette is hidden
        /// </summary>
        private void OnCommandPaletteHidden(object? sender, EventArgs e)
        {
            _logger.LogInformation("Command palette hidden");
        }

        public void Dispose()
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Test editor interface for demo purposes
    /// </summary>
    public interface ITestEditor
    {
        void PerformAction(string actionName);
        string GetCurrentFileName();
        bool HasUnsavedChanges();
    }

    /// <summary>
    /// Mock test editor implementation
    /// </summary>
    public class TestEditor : ITestEditor
    {
        private readonly ILogger<TestEditor> _logger;
        private string _currentFile = "untitled.txt";
        private bool _hasUnsavedChanges = false;

        public TestEditor(ILogger<TestEditor> logger)
        {
            _logger = logger;
        }

        public void PerformAction(string actionName)
        {
            _logger.LogInformation("Editor performing action: {ActionName}", actionName);
            
            switch (actionName.ToLowerInvariant())
            {
                case "new file":
                    _currentFile = "untitled.txt";
                    _hasUnsavedChanges = false;
                    break;
                case "save file":
                    _hasUnsavedChanges = false;
                    break;
                case "open file":
                    _hasUnsavedChanges = false;
                    break;
                default:
                    _hasUnsavedChanges = true;
                    break;
            }
        }

        public string GetCurrentFileName() => _currentFile;
        public bool HasUnsavedChanges() => _hasUnsavedChanges;
    }

    /// <summary>
    /// Command executor that integrates with the test editor
    /// </summary>
    public class EditorCommandExecutor : ICommandExecutor
    {
        private readonly ITestEditor _editor;
        private readonly ILogger<EditorCommandExecutor> _logger;

        public EditorCommandExecutor(ITestEditor editor, ILogger<EditorCommandExecutor> logger)
        {
            _editor = editor;
            _logger = logger;
        }

        public CommandResult Execute(CommandDefinition command, object? context = null)
        {
            try
            {
                switch (command.Id)
                {
                    case "File.New":
                        _editor.PerformAction("new file");
                        return new CommandResult { Success = true, Result = "New file created" };
                        
                    case "File.Open":
                        _editor.PerformAction("open file");
                        return new CommandResult { Success = true, Result = "File opened" };
                        
                    case "File.Save":
                        _editor.PerformAction("save file");
                        return new CommandResult { Success = true, Result = "File saved" };
                        
                    case "File.SaveAs":
                        _editor.PerformAction("save as");
                        return new CommandResult { Success = true, Result = "File saved as" };
                        
                    case "File.Close":
                        _editor.PerformAction("close file");
                        return new CommandResult { Success = true, Result = "File closed" };
                        
                    case "Edit.Undo":
                        _editor.PerformAction("undo");
                        return new CommandResult { Success = true, Result = "Action undone" };
                        
                    case "Edit.Redo":
                        _editor.PerformAction("redo");
                        return new CommandResult { Success = true, Result = "Action redone" };
                        
                    case "Tools.CommandPalette":
                        // Command palette will handle this internally
                        return new CommandResult { Success = true, Result = "Command palette opened" };
                        
                    case "View.ZoomIn":
                        _editor.PerformAction("zoom in");
                        return new CommandResult { Success = true, Result = "Zoomed in" };
                        
                    case "View.ZoomOut":
                        _editor.PerformAction("zoom out");
                        return new CommandResult { Success = true, Result = "Zoomed out" };
                        
                    case "View.Fullscreen":
                        _editor.PerformAction("toggle fullscreen");
                        return new CommandResult { Success = true, Result = "Fullscreen toggled" };
                        
                    default:
                        _logger.LogWarning("Unknown command executed: {CommandId}", command.Id);
                        return new CommandResult 
                        { 
                            Success = false, 
                            ErrorMessage = $"Unknown command: {command.Id}" 
                        };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {CommandId}", command.Id);
                return new CommandResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Execution error: {ex.Message}" 
                };
            }
        }
    }

    /// <summary>
    /// Main program entry point
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("TiXL Command Palette Demo");
                Console.WriteLine("==========================");
                Console.WriteLine();
                Console.WriteLine("Instructions:");
                Console.WriteLine("- Press Ctrl+Shift+P to open the command palette");
                Console.WriteLine("- Type to search for commands");
                Console.WriteLine("- Use arrow keys to navigate");
                Console.WriteLine("- Press Enter to execute a command");
                Console.WriteLine("- Press Esc to close the command palette");
                Console.WriteLine("- Press Ctrl+Q to quit the demo");
                Console.WriteLine();
                Console.WriteLine("Demo starting...");

                using var demo = new CommandPaletteDemo();
                demo.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running demo: {ex.Message}");
            }
        }
    }
}
