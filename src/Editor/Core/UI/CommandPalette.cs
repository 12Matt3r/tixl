using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using TiXL.Editor.Core.Commands;
using TiXL.Editor.Core.Models;

namespace TiXL.Editor.Core.UI
{
    /// <summary>
    /// Command palette UI component for ImGui
    /// </summary>
    public class CommandPalette
    {
        private readonly ICommandRegistry _commandRegistry;
        private readonly IFuzzySearchEngine _searchEngine;
        private readonly ILogger<CommandPalette> _logger;

        private readonly CommandPaletteState _state = new();
        private readonly Dictionary<ImGuiKey, bool> _keyState = new();

        // UI configuration
        private const float WindowWidth = 600f;
        private const float WindowHeight = 400f;
        private const float ItemHeight = 30f;
        private const int MaxVisibleItems = 10;
        private const float ScrollbarWidth = 16f;

        // Appearance
        private readonly Vector4 BackgroundColor = new(0.1f, 0.1f, 0.1f, 0.95f);
        private readonly Vector4 BorderColor = new(0.3f, 0.3f, 0.3f, 1f);
        private readonly Vector4 SelectedColor = new(0.2f, 0.4f, 0.8f, 1f);
        private readonly Vector4 HoverColor = new(0.2f, 0.2f, 0.2f, 1f);
        private readonly Vector4 TextColor = new(1f, 1f, 1f, 1f);
        private readonly Vector4 DescriptionColor = new(0.7f, 0.7f, 0.7f, 1f);
        private readonly Vector4 CategoryColor = new(0.5f, 0.5f, 1f, 1f);

        public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;

        public CommandPalette(
            ICommandRegistry commandRegistry,
            IFuzzySearchEngine searchEngine,
            ILogger<CommandPalette> logger)
        {
            _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
            _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load recent commands on startup
            LoadRecentCommands();
        }

        /// <summary>
        /// Shows the command palette
        /// </summary>
        public void Show()
        {
            _state.IsVisible = true;
            _state.SelectedIndex = 0;
            // Focus input for immediate typing
            ImGui.SetNextWindowFocus();
        }

        /// <summary>
        /// Hides the command palette
        /// </summary>
        public void Hide()
        {
            _state.IsVisible = false;
        }

        /// <summary>
        /// Toggles the visibility of the command palette
        /// </summary>
        public void Toggle()
        {
            if (_state.IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>
        /// Updates the command palette (call this in your main loop)
        /// </summary>
        public void Update()
        {
            if (!_state.IsVisible)
                return;

            HandleKeyboardInput();

            DrawWindow();
        }

        /// <summary>
        /// Processes keyboard input for the command palette
        /// </summary>
        private void HandleKeyboardInput()
        {
            var io = ImGui.GetIO();

            // Handle escape to close
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                Hide();
                return;
            }

            // Handle Enter to execute selected command
            if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
            {
                ExecuteSelectedCommand();
                return;
            }

            // Handle navigation keys
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            {
                _state.SelectedIndex = Math.Max(0, _state.SelectedIndex - 1);
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            {
                _state.SelectedIndex = Math.Min(_state.SearchResults.Length - 1, _state.SelectedIndex + 1);
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.PageUp))
            {
                _state.SelectedIndex = Math.Max(0, _state.SelectedIndex - 5);
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.PageDown))
            {
                _state.SelectedIndex = Math.Min(_state.SearchResults.Length - 1, _state.SelectedIndex + 5);
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.Home))
            {
                _state.SelectedIndex = 0;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.End))
            {
                _state.SelectedIndex = Math.Max(0, _state.SearchResults.Length - 1);
            }

            // Handle tab for autocomplete (basic implementation)
            if (ImGui.IsKeyPressed(ImGuiKey.Tab) && _state.SearchResults.Length > 0 && _state.SearchQuery.Length == 0)
            {
                _state.SearchQuery = _state.SearchResults[0].Command.Name;
            }

            // Auto-complete search results based on selected command
            if (io.KeyShift && ImGui.IsKeyPressed(ImGuiKey.Tab) && _state.SearchResults.Length > 0)
            {
                // Ctrl+Shift+Tab to cycle through results backwards
                _state.SearchQuery = _state.SearchResults[_state.SelectedIndex].Command.Name;
            }
        }

        /// <summary>
        /// Draws the command palette window
        /// </summary>
        private void DrawWindow()
        {
            var viewport = ImGui.GetMainViewport();
            var center = viewport.Size / 2f;

            ImGui.SetNextWindowSize(new Vector2(WindowWidth, WindowHeight), ImGuiCond.Always);
            ImGui.SetNextWindowPos(center - new Vector2(WindowWidth / 2f, WindowHeight / 2f), ImGuiCond.Always);

            // Window flags for modal behavior
            var flags = ImGuiWindowFlags.NoCollapse |
                       ImGuiWindowFlags.NoSavedSettings |
                       ImGuiWindowFlags.NoTitleBar |
                       ImGuiWindowFlags.NoResize |
                       ImGuiWindowFlags.NoMove;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 12f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, BackgroundColor);
            ImGui.PushStyleColor(ImGuiCol.Border, BorderColor);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.15f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, HoverColor);

            if (ImGui.Begin("##CommandPalette", flags))
            {
                DrawHeader();
                DrawSearchInput();
                DrawResults();
            }

            ImGui.End();

            // Pop style variables
            ImGui.PopStyleVar(1);
            ImGui.PopStyleColor(4);
        }

        /// <summary>
        /// Draws the command palette header
        /// </summary>
        private void DrawHeader()
        {
            ImGui.Text("Command Palette");
            ImGui.Separator();
            ImGui.Spacing();
        }

        /// <summary>
        /// Draws the search input field
        /// </summary>
        private void DrawSearchInput()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 6f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 4f));

            var buffer = new byte[512];
            var encoding = System.Text.Encoding.UTF8;
            var queryBytes = encoding.GetBytes(_state.SearchQuery);
            Array.Copy(queryBytes, buffer, Math.Min(queryBytes.Length, buffer.Length - 1));

            var inputFlags = ImGuiInputTextFlags.CallbackCharFilter |
                           ImGuiInputTextFlags.CallbackResize |
                           ImGuiInputTextFlags.EnterReturnsTrue;

            ImGui.SetKeyboardFocusHere();
            if (ImGui.InputText("##SearchInput", buffer, (uint)buffer.Length, inputFlags, (data) =>
            {
                // Filter input - allow all printable characters
                var key = ImGui.KeyEventToIndex(data.EventKey, ImGui.GetKeyMods());
                return 0; // Accept all input
            }))
            {
                _state.SearchQuery = encoding.GetString(buffer).TrimEnd('\0');
                PerformSearch();
            }

            // Draw placeholder text
            if (string.IsNullOrEmpty(_state.SearchQuery))
            {
                var pos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(pos + new Vector2(8f, 0f));
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Type to search commands...");
                ImGui.SetCursorPos(pos);
            }

            ImGui.PopStyleVar(2);
        }

        /// <summary>
        /// Draws the search results list
        /// </summary>
        private void DrawResults()
        {
            ImGui.Spacing();

            if (_state.SearchResults.Length == 0)
            {
                if (string.IsNullOrEmpty(_state.SearchQuery))
                {
                    DrawEmptyState("No commands available", "Press Ctrl+Shift+P to open palette");
                }
                else
                {
                    DrawEmptyState("No results", $"No commands match '{_state.SearchQuery}'");
                }
                return;
            }

            var visibleHeight = Math.Min(WindowHeight - 120f, _state.SearchResults.Length * ItemHeight);
            
            if (ImGui.BeginChild("##Results", new Vector2(0, visibleHeight), false, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                for (int i = 0; i < _state.SearchResults.Length; i++)
                {
                    if (i >= MaxVisibleItems) break; // Limit visible items

                    var result = _state.SearchResults[i];
                    var isSelected = i == _state.SelectedIndex;

                    DrawCommandItem(result, isSelected, i);
                }
            }
            ImGui.EndChild();

            // Draw result count
            ImGui.Spacing(0.5f);
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), 
                $"{_state.SearchResults.Length} result(s) • ↑↓ to navigate, Enter to execute, Esc to close");
        }

        /// <summary>
        /// Draws a single command item in the results list
        /// </summary>
        private void DrawCommandItem(CommandSearchResult result, bool isSelected, int index)
        {
            var command = result.Command;

            // Draw selection background
            if (isSelected)
            {
                ImGui.GetWindowDrawList().AddRectFilled(
                    ImGui.GetCursorScreenPos(),
                    ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X, ItemHeight),
                    ImGui.GetColorU32(SelectedColor));
            }

            ImGui.PushID(index);

            // Command icon/name
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f);
            
            var color = isSelected ? ImGui.GetStyleColorVec4(ImGuiCol.Text) : TextColor;
            
            if (!string.IsNullOrEmpty(command.Icon))
            {
                // Draw icon (simplified - you'd have your own icon system)
                ImGui.TextColored(color, $"[{command.Icon}] {command.Name}");
            }
            else
            {
                ImGui.TextColored(color, command.Name);
            }

            // Command category
            var categoryStr = GetCategoryDisplayName(command.Category);
            var categoryPos = ImGui.GetCursorPos();
            ImGui.SetCursorPos(categoryPos + new Vector2(0f, -ItemHeight/2f + 3f));
            ImGui.TextColored(CategoryColor, categoryStr);

            // Command description
            if (!string.IsNullOrEmpty(command.Description))
            {
                var descPos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(descPos + new Vector2(0f, -ItemHeight/2f + 8f));
                ImGui.TextColored(DescriptionColor, command.Description);
            }

            // Command shortcut
            if (!string.IsNullOrEmpty(command.Shortcut))
            {
                var shortcutPos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(new Vector2(ImGui.GetContentRegionAvail().X - 60f, shortcutPos.Y - ItemHeight/2f + 2f));
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), command.Shortcut);
            }

            ImGui.Spacing(0.5f);
            ImGui.PopID();

            // Handle item click
            if (ImGui.IsItemClicked())
            {
                ExecuteCommand(result.Command);
            }

            // Handle item hover
            if (ImGui.IsItemHovered())
            {
                _state.SelectedIndex = index;
            }
        }

        /// <summary>
        /// Draws an empty state message
        /// </summary>
        private void DrawEmptyState(string title, string subtitle)
        {
            var center = new Vector2(
                ImGui.GetContentRegionAvail().X / 2f,
                ImGui.GetContentRegionAvail().Y / 2f);

            ImGui.SetCursorPos(center + new Vector2(-100f, -20f));
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), title);

            ImGui.SetCursorPos(center + new Vector2(-120f, 0f));
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), subtitle);
        }

        /// <summary>
        /// Executes the currently selected command
        /// </summary>
        private void ExecuteSelectedCommand()
        {
            if (_state.SelectedIndex >= 0 && _state.SelectedIndex < _state.SearchResults.Length)
            {
                var selectedResult = _state.SearchResults[_state.SelectedIndex];
                ExecuteCommand(selectedResult.Command);
            }
        }

        /// <summary>
        /// Executes a command
        /// </summary>
        private void ExecuteCommand(CommandDefinition command)
        {
            try
            {
                var result = _commandRegistry.ExecuteCommand(command.Id);
                
                if (result.Success)
                {
                    _logger.LogInformation("Executed command: {CommandName}", command.Name);
                    CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, result));
                    Hide();
                }
                else
                {
                    _logger.LogWarning("Failed to execute command {CommandId}: {Error}", command.Id, result.ErrorMessage);
                    // You could show a temporary error message here
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {CommandId}", command.Id);
            }
        }

        /// <summary>
        /// Performs search based on current query
        /// </summary>
        private void PerformSearch()
        {
            var allCommands = _commandRegistry.GetAllCommands();
            
            if (string.IsNullOrWhiteSpace(_state.SearchQuery))
            {
                // Show recent commands when no query
                var recentCommands = _commandRegistry.GetRecentCommands(20);
                _state.SearchResults = recentCommands
                    .Select(c => new CommandSearchResult
                    {
                        Command = c,
                        Score = 1.0f,
                        NameMatches = ImmutableArray<MatchRange>.Empty,
                        DescriptionMatches = ImmutableArray<MatchRange>.Empty,
                        KeywordMatches = ImmutableArray<KeywordMatch>.Empty
                    })
                    .ToImmutableArray();
            }
            else
            {
                // Perform fuzzy search
                _state.SearchResults = _searchEngine.Search(_state.SearchQuery, allCommands, 50);
            }

            // Reset selection to first item
            _state.SelectedIndex = 0;
        }

        /// <summary>
        /// Loads recent commands from the registry
        /// </summary>
        private void LoadRecentCommands()
        {
            // This would typically load from a persistent store
            // For now, we'll get them from the registry
            _state.RecentCommands = _commandRegistry.GetRecentCommands(10);
        }

        /// <summary>
        /// Gets the display name for a command category
        /// </summary>
        private string GetCategoryDisplayName(CommandCategory category)
        {
            return category switch
            {
                CommandCategory.File => "File",
                CommandCategory.Edit => "Edit",
                CommandCategory.View => "View",
                CommandCategory.Tools => "Tools",
                CommandCategory.Navigate => "Navigate",
                CommandCategory.Insert => "Insert",
                CommandCategory.Debug => "Debug",
                CommandCategory.Help => "Help",
                CommandCategory.Custom => "Custom",
                _ => "Other"
            };
        }

        /// <summary>
        /// Gets the current state of the command palette
        /// </summary>
        public CommandPaletteState GetState()
        {
            return _state;
        }
    }

    /// <summary>
    /// Event arguments for command execution
    /// </summary>
    public class CommandExecutedEventArgs : EventArgs
    {
        public CommandDefinition Command { get; }
        public CommandResult Result { get; }

        public CommandExecutedEventArgs(CommandDefinition command, CommandResult result)
        {
            Command = command;
            Result = result;
        }
    }
}
