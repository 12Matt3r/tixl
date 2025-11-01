# TiXL-082 Layered Keyboard Shortcuts - Quick Start Guide

## Overview

The Layered Keyboard Shortcut System provides a powerful, intelligent solution for managing keyboard shortcuts in TiXL. This guide will help you get started quickly with the system.

## Quick Setup (5 minutes)

### 1. Add to Your Project

```xml
<!-- Add to your .csproj file -->
<ProjectReference Include="src\Editor\Gui\Interaction\Keyboard\TiXL.KeyboardShortcutSystem.csproj" />
```

### 2. Basic Integration

```csharp
// In your main form or application class
using TiXL.Editor.Gui.Interaction.Keyboard;

public partial class MainForm : Form
{
    private ShortcutSystemManager _shortcutSystem;
    
    public MainForm()
    {
        InitializeComponent();
        
        // Initialize the shortcut system
        _shortcutSystem = new ShortcutSystemManager();
        
        // Set initial context
        _shortcutSystem.SetCurrentContext(ShortcutContext.Global);
        
        // Subscribe to events if needed
        _shortcutSystem.ShortcutExecuted += OnShortcutExecuted;
    }
    
    // Handle keyboard events
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_shortcutSystem.ProcessKeyEvent(keyData, true))
            return true; // Shortcut handled
            
        return base.ProcessCmdKey(ref msg, keyData);
    }
    
    private void OnShortcutExecuted(object sender, ShortcutExecutedEventArgs e)
    {
        // Optional: Handle shortcut execution
        Console.WriteLine($"Executed: {e.Shortcut.Name}");
    }
}
```

### 3. Add Menu Integration

```csharp
private void InitializeMenu()
{
    var toolsMenu = mainMenu.MenuItems.Add("&Tools");
    toolsMenu.MenuItems.Add(new MenuItem("&Keyboard Shortcuts...", OnShowShortcuts));
    toolsMenu.MenuItems.Add(new MenuItem("&Conflict Resolver...", OnShowConflicts));
    toolsMenu.MenuItems.Add(new MenuItem("&Keyboard Visualization", OnShowVisualization));
}

private void OnShowShortcuts(object sender, EventArgs e)
{
    _shortcutSystem.ShowShortcutEditor(this);
}

private void OnShowConflicts(object sender, EventArgs e)
{
    _shortcutSystem.ShowConflictResolver(this);
}

private void OnShowVisualization(object sender, EventArgs e)
{
    _shortcutSystem.ShowKeyboardVisualization(this);
}
```

## Common Use Cases

### Adding Custom Shortcuts

```csharp
var customShortcut = new KeyboardShortcut
{
    Id = "myapp.myAction",
    Name = "My Custom Action",
    Description = "Performs my custom function",
    Category = "Custom",
    PrimaryKey = Keys.Control | Keys.Shift | Keys.M,
    Context = ShortcutContext.GraphEditor,
    LayerPriority = 60,
    Action = (context) => {
        MyCustomFunction();
    }
};

_shortcutSystem.GetShortcutManager().RegisterShortcut(customShortcut);
```

### Context-Aware Shortcuts

```csharp
// Same key, different contexts
var globalShortcut = new KeyboardShortcut
{
    Id = "global.help",
    Name = "Show Help",
    PrimaryKey = Keys.F1,
    Context = ShortcutContext.Global,
    Action = ShowGlobalHelp
};

var graphShortcut = new KeyboardShortcut
{
    Id = "graph.zoomFit",
    Name = "Fit to View",
    PrimaryKey = Keys.F1,
    Context = ShortcutContext.GraphEditor,
    Action = FitGraphToView
};

// Both can coexist because they have different contexts
_shortcutSystem.GetShortcutManager().RegisterShortcut(globalShortcut);
_shortcutSystem.GetShortcutManager().RegisterShortcut(graphShortcut);
```

### Import/Export Configuration

```csharp
// Export shortcuts
if (_shortcutSystem.ExportShortcuts("my-shortcuts.json"))
{
    MessageBox.Show("Shortcuts exported!");
}

// Import shortcuts
if (_shortcutSystem.ImportShortcuts("imported-shortcuts.json", merge: true))
{
    MessageBox.Show("Shortcuts imported!");
}
```

## Advanced Features

### Accessibility Support

```csharp
var accessibility = _shortcutSystem.GetAccessibilitySupport();

// Enable speech output
accessibility.EnableSpeechOutput(true);

// Custom announcements
accessibility.AddAnnouncement("myapp.myAction", "Custom action has been performed");

// Set volume
accessibility.SetAnnouncementVolume(0.8f);
```

### Documentation Integration

```csharp
var helpSystem = _shortcutSystem.GetHelpSystem();

// Add documentation for custom shortcuts
helpSystem.AddDocumentation("myapp.myAction", "My Custom Action", 
    "This shortcut performs a custom function specific to my application");

// Export documentation
_shortcutSystem.ExportDocumentation("shortcuts-guide.md", DocumentationFormat.Markdown);
```

### Conflict Resolution

```csharp
// The system automatically detects conflicts
_shortcutSystem.ShortcutConflictDetected += (sender, e) => {
    var conflictCount = e.Conflicts.Count;
    if (conflictCount > 0)
    {
        // Show conflict resolver
        _shortcutSystem.ShowConflictResolver(this);
    }
};

// Manual conflict detection
var conflicts = _shortcutSystem.GetShortcutManager()
    .GetAllShortcuts()
    .Where(s => s.Name.Contains("conflict"))
    .ToList();
```

## UI Integration Examples

### Toolbar Buttons

```csharp
// Create toolbar with shortcut buttons
var toolbar = new ToolBar();
var saveButton = new ToolBarButton { Text = "Save", ImageIndex = 0 };
saveButton.Click += (s, e) => {
    // Execute the save shortcut directly
    var saveShortcuts = _shortcutSystem.SearchShortcuts("save");
    if (saveShortcuts.Any())
    {
        _shortcutSystem.GetShortcutManager()
            .ExecuteShortcut(saveShortcuts[0].PrimaryKey, ShortcutContext.Global);
    }
};
toolbar.Buttons.Add(saveButton);
```

### Status Bar Display

```csharp
private void OnShortcutExecuted(object sender, ShortcutExecutedEventArgs e)
{
    statusLabel.Text = $"Executed: {e.Shortcut.Name} ({e.Shortcut.GetKeyDisplayString()})";
    
    // Fade out after 3 seconds
    var timer = new Timer { Interval = 3000 };
    timer.Tick += (s, args) => {
        statusLabel.Text = "Ready";
        timer.Stop();
        timer.Dispose();
    };
    timer.Start();
}
```

## Configuration Tips

### Recommended Settings

```csharp
// Enable accessibility by default
_shortcutSystem.GetAccessibilitySupport().EnableSpeechOutput(true);

// Set context based on active window
_shortcutSystem.SetCurrentContext(GetCurrentContext());

// Load user preferences
LoadShortcutPreferences();

// Auto-save on changes
_shortcutSystem.ShortcutChanged += (s, e) => {
    SaveShortcutPreferences();
};
```

### Performance Optimization

```csharp
// For applications with many shortcuts, consider:
// 1. Lazy loading of documentation
// 2. Caching of conflict detection results
// 3. Debouncing of rapid key presses (already implemented)
// 4. Context-based shortcut filtering
```

## Troubleshooting

### Common Issues

1. **Shortcuts not working**
   - Check that `ProcessCmdKey` is properly overridden
   - Verify the current context matches the shortcut context
   - Ensure shortcuts are registered before processing key events

2. **Conflicts not detected**
   - Conflicts are only detected when registering new shortcuts
   - Manually trigger conflict detection if needed

3. **UI not updating**
   - Ensure the visual editor is properly refreshing
   - Check that event handlers are properly subscribed

### Debug Logging

```csharp
// Enable detailed logging
LogManager.GetLogger("ShortcutSystem").LogLevel = LogLevel.Debug;

// Monitor specific events
_shortcutSystem.ShortcutExecuted += (s, e) => {
    LogManager.GetLogger("Debug").Debug($"Shortcut: {e.Shortcut.Name}");
};
```

## Best Practices

### 1. Shortcut Naming
- Use descriptive, action-based names
- Include context in the ID (e.g., "graph.selectAll", "timeline.play")
- Group related shortcuts with common prefixes

### 2. Key Selection
- Avoid overriding standard shortcuts (Ctrl+C, Ctrl+V, etc.)
- Use consistent modifier combinations
- Consider accessibility when choosing key combinations

### 3. Context Usage
- Use context to avoid conflicts between views
- Set context appropriately for your UI layout
- Provide fallbacks for context-less operations

### 4. User Experience
- Provide visual feedback for executed shortcuts
- Include comprehensive help documentation
- Offer customization options for power users

## Next Steps

1. **Run the Example**: Use the provided `UsageExample.cs` to see the system in action
2. **Customize Shortcuts**: Use the visual editor to customize key bindings
3. **Add Documentation**: Create help documentation for your custom shortcuts
4. **Test Accessibility**: Verify the system works with screen readers and other assistive technologies
5. **Export Configuration**: Save and share shortcut configurations with your team

## Support

For additional help:
- Review the comprehensive `TIXL-082_Implementation_Summary.md`
- Check the inline XML documentation
- Examine the example implementation
- Use the built-in help system

The Layered Keyboard Shortcut System is designed to be intuitive, powerful, and accessible. Start with the basics and gradually explore the advanced features as needed!