# TiXL Command Palette System (TIXL-081)

## Overview

The TiXL Command Palette System is a comprehensive, modern command palette implementation designed for the TiXL graphics editor. It provides an efficient way for users to discover, search, and execute editor commands through an intuitive fuzzy search interface.

## Features

### 🚀 Core Features
- **Fuzzy Search**: Advanced search algorithm that finds commands even with typos
- **Keyboard Shortcuts**: Ctrl+Shift+P to open, full keyboard navigation
- **Command Categories**: Organized by File, Edit, View, Tools, Navigate, Insert, Debug, Help
- **Recent Commands**: Tracks and displays recently used commands
- **Custom Commands**: Plugin system for extending command capabilities
- **Performance Optimized**: Fast search even with large command sets
- **Seamless Integration**: Built for ImGui immediate-mode GUI framework

### 🎯 User Experience
- **Instant Search**: Real-time search as you type
- **Smart Scoring**: Prioritizes exact matches, prefixes, and fuzzy matches
- **Visual Feedback**: Color-coded matches and hover states
- **Keyboard Navigation**: Arrow keys, Enter, Esc, PageUp/Down, Home/End
- **Auto-completion**: Tab for auto-completing selected commands

### 🔧 Developer Features
- **Plugin Architecture**: Easy to add custom commands via plugins
- **Extensible System**: Custom command executors and search engines
- **Service Integration**: Built on Microsoft.Extensions.DependencyInjection
- **Comprehensive Logging**: Detailed logging for debugging and monitoring
- **Strongly Typed**: Fully type-safe implementation with clear interfaces

## Architecture

### Core Components

```
TiXL.Editor.Core/
├── Models/
│   ├── CommandPaletteModels.cs      # Core data models and interfaces
│   └── ICommandPalettePlugin        # Plugin interface
├── Commands/
│   ├── CommandRegistry.cs           # Central command registry
│   └── FuzzySearchEngine.cs         # Advanced search implementation
├── UI/
│   └── CommandPalette.cs            # ImGui UI implementation
├── Integration/
│   └── CommandPaletteManager.cs     # Main manager and keyboard handling
├── Plugins/
│   └── PluginSystem.cs              # Plugin management system
└── Extensions/
    └── ServiceCollectionExtensions.cs # Dependency injection setup
```

### Key Interfaces

#### ICommandRegistry
- Manages all editor commands
- Tracks usage statistics
- Handles command execution
- Supports plugin registration

#### IFuzzySearchEngine
- Performs intelligent fuzzy search
- Calculates relevance scores
- Supports multiple match types

#### ICommandPaletteManager
- Integrates with application input system
- Handles keyboard shortcuts
- Coordinates UI and backend

#### ICommandPalettePlugin
- Interface for third-party plugins
- Enables custom command registration

## Quick Start

### 1. Basic Setup

```csharp
// Configure services
var services = new ServiceCollection();
services.AddCommandPalette();

// Get the manager
var commandPaletteManager = serviceProvider.GetRequiredService<ICommandPaletteManager>();

// Update in your main loop
while (application.IsRunning)
{
    commandPaletteManager.Update();
    // Your other rendering code
}
```

### 2. Register Custom Commands

```csharp
// Add commands to registry
var registry = serviceProvider.GetRequiredService<ICommandRegistry>();
var executor = new MyCustomExecutor();

registry.RegisterCommand(new CommandDefinition
{
    Id = "My.Custom.Command",
    Name = "Custom Command",
    Description = "Performs a custom action",
    Category = CommandCategory.Tools,
    Keywords = ImmutableArray.Create("custom", "action", "command"),
    Shortcut = "Ctrl+Alt+C",
    Priority = 90
}, executor);
```

### 3. Create a Plugin

```csharp
public class MyPlugin : ICommandPalettePlugin
{
    public string Name => "My Plugin";

    public IEnumerable<CommandDefinition> GetCommands()
    {
        return new[]
        {
            new CommandDefinition
            {
                Id = "MyPlugin.Calculate",
                Name = "Calculate",
                Description = "Open calculator",
                Category = CommandCategory.Tools,
                Keywords = ImmutableArray.Create("calc", "math", "calculator"),
                Icon = "🧮"
            }
        };
    }
}

// Register the plugin
var pluginManager = serviceProvider.GetRequiredService<IPluginManager>();
pluginManager.RegisterPlugin(new MyPlugin());
```

### 4. Handle Keyboard Input

```csharp
// The command palette automatically handles Ctrl+Shift+P
// But you can also manually control it:

commandPaletteManager.ShowCommandPalette(); // Show
commandPaletteManager.HideCommandPalette(); // Hide
commandPaletteManager.ToggleCommandPalette(); // Toggle
```

## Command Definition

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique identifier for the command |
| `Name` | `string` | Display name shown in search results |
| `Description` | `string` | Description of what the command does |
| `Category` | `CommandCategory` | Organization category |
| `Keywords` | `ImmutableArray<string>` | Search keywords |
| `Icon` | `string?` | Optional icon (UTF-8 or asset name) |
| `Shortcut` | `string?` | Keyboard shortcut display text |
| `RequiresContext` | `bool` | Whether command needs specific context |
| `IsEnabled` | `bool` | Whether command is currently available |
| `UsageCount` | `int` | Usage tracking (managed automatically) |
| `LastUsed` | `DateTime` | Last execution time (managed automatically) |
| `Priority` | `int` | Sorting priority for search results |
| `PluginName` | `string?` | Associated plugin name |

### Categories

- **File**: File operations (New, Open, Save, Close, etc.)
- **Edit**: Text editing (Undo, Redo, Cut, Copy, Paste, Find, Replace)
- **View**: Visual controls (Zoom, Fullscreen, Panels, etc.)
- **Tools**: Tools and utilities (Preferences, Calculator, etc.)
- **Navigate**: Navigation and jumping (Go to line, Recent files)
- **Insert**: Insert content (Text, Image, etc.)
- **Debug**: Debugging and development tools
- **Help**: Help and documentation
- **Custom**: Custom/extension commands

## Search Algorithm

The command palette uses a sophisticated fuzzy search algorithm:

### Scoring System

1. **Exact Match**: Perfect match on command name (Score: 1.0 × 2.0)
2. **Prefix Match**: Command name starts with query (Score: 0.8 × 1.5)
3. **Substring Match**: Query found within command name (Score: 0.6)
4. **Keyword Match**: Query matches keywords (Score: 0.7)
5. **Description Match**: Query found in description (Score: 0.7 × 0.7)
6. **Usage Bonus**: Popular commands get priority (Score: log10(usage) × 0.1)
7. **Priority Bonus**: Manually set priority (Score: priority × 0.05)

### Match Types

- **Exact**: Perfect character-by-character match
- **Fuzzy**: Allows for character substitutions and transpositions
- **Partial**: Substring matches with position weighting

## Keyboard Navigation

### Default Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+P` | Open command palette |
| `Escape` | Close command palette |
| `Enter` / `KeypadEnter` | Execute selected command |
| `Arrow Up/Down` | Navigate results |
| `Page Up/Down` | Jump 5 results at a time |
| `Home/End` | Jump to first/last result |
| `Tab` | Auto-complete to selected command |
| `Ctrl+Shift+Tab` | Cycle through results backwards |

### Search Tips

1. **Partial Matching**: Type partial words to find commands
2. **Category Filtering**: Include category names in your search
3. **Keyword Search**: Use descriptive keywords
4. **Shortcut Display**: Commands show their shortcuts for quick reference

## Performance Considerations

### Optimization Features

- **Lazy Loading**: Commands loaded on demand
- **Search Result Caching**: Results cached during typing
- **Concurrent Updates**: Search operations don't block UI
- **Memory Efficient**: Immutable collections for thread safety

### Scalability

- **Large Command Sets**: Tested with 1000+ commands
- **Fast Search**: Sub-millisecond response times
- **Memory Usage**: Minimal memory overhead per command

## Plugin Development

### Plugin Structure

```csharp
public class MyCustomPlugin : ICommandPalettePlugin
{
    public string Name => "My Plugin";

    public IEnumerable<CommandDefinition> GetCommands()
    {
        // Return your commands here
        yield return CreateMyCommand();
    }

    private CommandDefinition CreateMyCommand()
    {
        return new CommandDefinition
        {
            Id = $"MyPlugin.{Guid.NewGuid()}",
            Name = "My Custom Command",
            Description = "A custom command from my plugin",
            Category = CommandCategory.Custom,
            Keywords = ImmutableArray.Create("custom", "plugin"),
            // ... other properties
        };
    }
}
```

### Plugin Best Practices

1. **Unique IDs**: Use namespaces for command IDs
2. **Meaningful Names**: Use clear, descriptive command names
3. **Good Keywords**: Include relevant keywords for searchability
4. **Appropriate Categories**: Use the right category for organization
5. **Error Handling**: Handle execution errors gracefully
6. **Logging**: Log important actions and errors

## Integration Examples

### With Existing TiXL Editor

```csharp
// In your main application setup
public class TiXLEditorApplication
{
    private readonly ICommandPaletteManager _commandPaletteManager;

    public void Initialize()
    {
        // Set up command palette with editor commands
        var registry = _commandPaletteManager.CommandRegistry;
        registry.CreateCommands(new EditorCommandExecutor(this))
            .Add(CreateEditorCommand("New Project", "project.new", CommandCategory.File))
            .Add(CreateEditorCommand("Open Project", "project.open", CommandCategory.File))
            .Add(CreateEditorCommand("Save Project", "project.save", CommandCategory.File));
    }

    public void Update()
    {
        _commandPaletteManager.Update();
        
        // Continue with your normal update logic
    }
}
```

### With Custom UI Framework

```csharp
public class CustomEditorUI
{
    private readonly CommandPalette _commandPalette;

    public void HandleInput()
    {
        // Handle your input processing
        
        // Let command palette handle its own keyboard shortcuts
        _commandPalette.Update();
    }

    public void Render()
    {
        // Render your UI
        
        // Command palette will render automatically when visible
    }
}
```

## Testing

### Unit Tests

The system includes comprehensive unit tests covering:

- Command registration and unregistration
- Search algorithm accuracy
- UI component behavior
- Plugin system functionality
- Performance benchmarks

### Integration Testing

Test the complete integration with your application:

```csharp
[Test]
public void CommandPalette_Integration_Test()
{
    // Setup
    var services = new ServiceCollection();
    services.AddCommandPalette();
    var provider = services.BuildServiceProvider();
    
    var manager = provider.GetRequiredService<ICommandPaletteManager>();
    
    // Test keyboard shortcut
    manager.ToggleCommandPalette();
    Assert.IsTrue(manager.GetCurrentState().IsVisible);
    
    // Test command execution
    var registry = manager.CommandRegistry;
    var result = registry.ExecuteCommand("Tools.CommandPalette");
    Assert.IsTrue(result.Success);
}
```

## Configuration Options

### CommandPaletteOptions

```csharp
services.AddCommandPalette(options =>
{
    options.MaxSearchResults = 50;          // Max search results to return
    options.MaxRecentCommands = 20;         // Max recent commands to track
    options.MinSearchScore = 0.1f;          // Minimum score for results
    options.AutoLoadPlugins = true;         // Auto-load plugins on startup
    options.RegisterDefaultCommands = true; // Register built-in commands
    options.EnableKeyboardShortcuts = true; // Enable keyboard shortcuts
    options.CommandPaletteShortcut = "Ctrl+Shift+P"; // Open shortcut
    options.EnableDebugMode = false;        // Show debug information
});
```

## Logging

The system provides comprehensive logging through Microsoft.Extensions.Logging:

### Log Categories

- `TiXL.Editor.Core.Commands.CommandRegistry`: Command management
- `TiXL.Editor.Core.Commands.FuzzySearchEngine`: Search operations
- `TiXL.Editor.Core.UI.CommandPalette`: UI interactions
- `TiXL.Editor.Core.Integration.CommandPaletteManager`: Keyboard handling
- `TiXL.Editor.Core.Plugins.PluginManager`: Plugin operations

### Log Levels

- **Information**: Major operations (command execution, palette visibility)
- **Warning**: Non-critical issues (unknown commands, plugin failures)
- **Error**: Serious errors (execution failures, critical system issues)
- **Debug**: Detailed operation information (for development)

## Troubleshooting

### Common Issues

#### Command Not Found
```csharp
// Ensure commands are registered before searching
var registry = serviceProvider.GetRequiredService<ICommandRegistry>();
var command = registry.GetCommand("Your.Command.Id");
if (command == null)
{
    // Command not registered
}
```

#### Keyboard Shortcuts Not Working
```csharp
// Ensure Update() is called in your main loop
commandPaletteManager.Update();

// Check if keyboard shortcuts are enabled
// options.EnableKeyboardShortcuts = true;
```

#### Search Results Not Appearing
```csharp
// Verify search is being called
var state = commandPaletteManager.GetCurrentState();
Console.WriteLine($"Search query: '{state.SearchQuery}'");
Console.WriteLine($"Results count: {state.SearchResults.Length}");
```

#### Plugin Commands Not Showing
```csharp
// Check if plugins are loaded
var pluginManager = serviceProvider.GetRequiredService<IPluginManager>();
var plugins = pluginManager.GetLoadedPlugins();
foreach (var plugin in plugins)
{
    var commands = plugin.GetCommands();
    Console.WriteLine($"Plugin {plugin.Name}: {commands.Count()} commands");
}
```

### Debug Mode

Enable debug mode to get additional logging:

```csharp
services.AddCommandPalette(options =>
{
    options.EnableDebugMode = true;
});
```

## Future Enhancements

### Planned Features

1. **Command History**: More sophisticated recent commands tracking
2. **Command Groups**: Hierarchical command organization
3. **Dynamic Filtering**: Filter by application context
4. **Custom Themes**: UI theme customization
5. **Command Shortcuts**: Quick access to frequently used commands
6. **Search Analytics**: Usage statistics and optimization
7. **Voice Search**: Voice-activated command search
8. **Smart Suggestions**: AI-powered command recommendations

### Extension Points

- Custom search algorithms
- Alternative UI implementations
- Additional input modalities
- Extended plugin capabilities
- Custom command providers

## Contributing

### Development Setup

1. Clone the repository
2. Restore dependencies: `dotnet restore`
3. Build the solution: `dotnet build`
4. Run tests: `dotnet test`
5. Run examples: `dotnet run --project src/Editor/Examples`

### Code Style

- Follow .NET naming conventions
- Use XML documentation comments
- Write unit tests for new features
- Update this documentation for new functionality

### Submitting Changes

1. Create a feature branch
2. Implement your changes
3. Add tests
4. Update documentation
5. Submit a pull request

## License

This project is part of the TiXL editor system and follows the same licensing terms.

## Support

For questions, issues, or contributions:

1. Check the troubleshooting section
2. Review the code examples
3. Submit issues for bugs or feature requests
4. Join the development discussions

---

**TiXL Command Palette System v1.0.0**  
*Improving developer productivity through intelligent command discovery*
