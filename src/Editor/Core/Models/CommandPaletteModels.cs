using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace TiXL.Editor.Core.Models
{
    /// <summary>
    /// Represents the category of a command for organization and filtering
    /// </summary>
    public enum CommandCategory
    {
        File,
        Edit,
        View,
        Tools,
        Navigate,
        Insert,
        Debug,
        Help,
        Custom
    }

    /// <summary>
    /// Represents a command that can be executed via the command palette
    /// </summary>
    public class CommandDefinition
    {
        /// <summary>
        /// Unique identifier for the command
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the command
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the command does
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Category for organization and filtering
        /// </summary>
        public CommandCategory Category { get; set; } = CommandCategory.Custom;

        /// <summary>
        /// Keywords for fuzzy search
        /// </summary>
        public ImmutableArray<string> Keywords { get; set; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Icon identifier (optional)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Keyboard shortcut (optional)
        /// </summary>
        public string? Shortcut { get; set; }

        /// <summary>
        /// Whether the command requires a context to be available
        /// </summary>
        public bool RequiresContext { get; set; }

        /// <summary>
        /// Whether the command is currently available
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Usage count for recent commands tracking
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Last time the command was used
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Priority for sorting in search results (higher = more relevant)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Associated plugin or extension name
        /// </summary>
        public string? PluginName { get; set; }
    }

    /// <summary>
    /// Represents a command execution result
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// Whether the command execution was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Return value from command execution
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Execution duration
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }
    }

    /// <summary>
    /// Interface for command executors
    /// </summary>
    public interface ICommandExecutor
    {
        /// <summary>
        /// Executes a command
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <param name="context">Additional context for execution</param>
        /// <returns>Execution result</returns>
        CommandResult Execute(CommandDefinition command, object? context = null);
    }

    /// <summary>
    /// Search result for command palette queries
    /// </summary>
    public class CommandSearchResult
    {
        /// <summary>
        /// The matched command
        /// </summary>
        public CommandDefinition Command { get; set; } = null!;

        /// <summary>
        /// Match score (0.0 to 1.0, higher is better)
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Matched portions of the command name
        /// </summary>
        public ImmutableArray<MatchRange> NameMatches { get; set; } = ImmutableArray<MatchRange>.Empty;

        /// <summary>
        /// Matched portions of the description
        /// </summary>
        public ImmutableArray<MatchRange> DescriptionMatches { get; set; } = ImmutableArray<MatchRange>.Empty;

        /// <summary>
        /// Matched keywords
        /// </summary>
        public ImmutableArray<KeywordMatch> KeywordMatches { get; set; } = ImmutableArray<KeywordMatch>.Empty;
    }

    /// <summary>
    /// Represents a text match range
    /// </summary>
    public class MatchRange
    {
        /// <summary>
        /// Start index of the match
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Length of the match
        /// </summary>
        public int Length { get; set; }
    }

    /// <summary>
    /// Represents a keyword match
    /// </summary>
    public class KeywordMatch
    {
        /// <summary>
        /// The matched keyword
        /// </summary>
        public string Keyword { get; set; } = string.Empty;

        /// <summary>
        /// Match score for this keyword
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Match range in the keyword
        /// </summary>
        public MatchRange Range { get; set; } = new();
    }

    /// <summary>
    /// Command palette state
    /// </summary>
    public class CommandPaletteState
    {
        /// <summary>
        /// Whether the command palette is visible
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Current search query
        /// </summary>
        public string SearchQuery { get; set; } = string.Empty;

        /// <summary>
        /// Currently selected command index
        /// </summary>
        public int SelectedIndex { get; set; }

        /// <summary>
        /// Filter category (null for all)
        /// </summary>
        public CommandCategory? FilterCategory { get; set; }

        /// <summary>
        /// Search results
        /// </summary>
        public ImmutableArray<CommandSearchResult> SearchResults { get; set; } = ImmutableArray<CommandSearchResult>.Empty;

        /// <summary>
        /// Recently used commands
        /// </summary>
        public ImmutableArray<CommandDefinition> RecentCommands { get; set; } = ImmutableArray<CommandDefinition>.Empty;
    }

    /// <summary>
    /// Interface for plugins to register commands
    /// </summary>
    public interface ICommandPalettePlugin
    {
        /// <summary>
        /// Plugin name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets commands provided by this plugin
        /// </summary>
        /// <returns>Collection of commands</returns>
        IEnumerable<CommandDefinition> GetCommands();
    }
}
