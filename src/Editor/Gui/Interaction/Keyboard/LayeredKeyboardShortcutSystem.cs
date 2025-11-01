// Layered Keyboard Shortcut System for TiXL
// Implements comprehensive shortcut management with conflict detection,
// context awareness, and visual editing capabilities.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms; // For Keys enum
using TiXL.Core.Logging;
using TiXL.Editor.Gui.Styling;

namespace TiXL.Editor.Gui.Interaction.Keyboard
{
    /// <summary>
    /// Represents a keyboard shortcut binding with metadata
    /// </summary>
    public class KeyboardShortcut
    {
        /// <summary>
        /// Unique identifier for the shortcut
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Display name for the shortcut
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of what the shortcut does
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Category for organization (File, Edit, View, Tools, etc.)
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// Primary key combination
        /// </summary>
        public Keys PrimaryKey { get; set; }
        
        /// <summary>
        /// Alternative key combination
        /// </summary>
        public Keys? AlternativeKey { get; set; }
        
        /// <summary>
        /// Context where this shortcut is active
        /// </summary>
        public ShortcutContext Context { get; set; }
        
        /// <summary>
        /// Layer priority (higher numbers override lower)
        /// </summary>
        public int LayerPriority { get; set; }
        
        /// <summary>
        /// Whether the shortcut is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Whether this is a system shortcut (protected)
        /// </summary>
        public bool IsSystem { get; set; }
        
        /// <summary>
        /// Tags for filtering and searching
        /// </summary>
        public List<string> Tags { get; set; } = new();
        
        /// <summary>
        /// Icon for UI display
        /// </summary>
        public string Icon { get; set; } = string.Empty;
        
        /// <summary>
        /// Action to execute when shortcut is triggered
        /// </summary>
        [JsonIgnore]
        public Action<ShortcutExecutionContext> Action { get; set; }
        
        /// <summary>
        /// Check if this shortcut matches the given key combination
        /// </summary>
        public bool MatchesKey(Keys key)
        {
            return key == PrimaryKey || (AlternativeKey.HasValue && key == AlternativeKey.Value);
        }
        
        /// <summary>
        /// Get formatted key display string
        /// </summary>
        public string GetKeyDisplayString()
        {
            var result = FormatKey(PrimaryKey);
            if (AlternativeKey.HasValue)
                result += " / " + FormatKey(AlternativeKey.Value);
            return result;
        }
        
        private string FormatKey(Keys key)
        {
            var parts = new List<string>();
            
            if (key.HasFlag(Keys.Control)) parts.Add("Ctrl");
            if (key.HasFlag(Keys.Alt)) parts.Add("Alt");
            if (key.HasFlag(Keys.Shift)) parts.Add("Shift");
            if (key.HasFlag(Keys.LWin) || key.HasFlag(Keys.RWin)) parts.Add("Win");
            
            var keyCode = key & Keys.KeyCode;
            if (keyCode != Keys.None)
            {
                if (keyCode >= Keys.F1 && keyCode <= Keys.F12)
                    parts.Add($"F{keyCode - Keys.F1 + 1}");
                else
                    parts.Add(keyCode.ToString());
            }
            
            return string.Join("+", parts);
        }
    }
    
    /// <summary>
    /// Execution context for shortcut actions
    /// </summary>
    public class ShortcutExecutionContext
    {
        public ShortcutContext Context { get; set; }
        public KeyboardShortcut Shortcut { get; set; }
        public Keys TriggeredKey { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool SuppressDefault { get; set; }
    }
    
    /// <summary>
    /// Context where shortcuts can be active
    /// </summary>
    [Flags]
    public enum ShortcutContext
    {
        None = 0,
        Global = 1,
        GraphEditor = 2,
        Timeline = 4,
        Properties = 8,
        AssetBrowser = 16,
        Scripting = 32,
        Performance = 64,
        Rendering = 128
    }
    
    /// <summary>
    /// Manager for layered keyboard shortcuts
    /// </summary>
    public class LayeredKeyboardShortcutManager
    {
        private readonly Dictionary<string, KeyboardShortcut> _shortcuts = new();
        private readonly List<ShortcutLayer> _layers = new();
        private readonly ShortcutConflictDetector _conflictDetector;
        private readonly ShortcutLogger _logger = new();
        
        public event EventHandler<ShortcutConflictEventArgs> ShortcutConflictDetected;
        public event EventHandler<ShortcutExecutedEventArgs> ShortcutExecuted;
        public event EventHandler<ShortcutChangedEventArgs> ShortcutChanged;
        
        public LayeredKeyboardShortcutManager()
        {
            _conflictDetector = new ShortcutConflictDetector();
            InitializeDefaultLayers();
            LoadDefaultShortcuts();
        }
        
        /// <summary>
        /// Register a new shortcut
        /// </summary>
        public void RegisterShortcut(KeyboardShortcut shortcut)
        {
            if (_shortcuts.ContainsKey(shortcut.Id))
            {
                _logger.Warn($"Shortcut {shortcut.Id} is already registered");
                return;
            }
            
            _shortcuts[shortcut.Id] = shortcut;
            _logger.Debug($"Registered shortcut: {shortcut.Name} ({shortcut.GetKeyDisplayString()})");
            
            CheckForConflicts(shortcut);
            ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs { Action = ChangeAction.Added, Shortcut = shortcut });
        }
        
        /// <summary>
        /// Remove a shortcut
        /// </summary>
        public void UnregisterShortcut(string shortcutId)
        {
            if (_shortcuts.TryGetValue(shortcutId, out var shortcut))
            {
                _shortcuts.Remove(shortcutId);
                _logger.Debug($"Unregistered shortcut: {shortcut.Name}");
                ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs { Action = ChangeAction.Removed, Shortcut = shortcut });
            }
        }
        
        /// <summary>
        /// Execute a shortcut by key
        /// </summary>
        public bool ExecuteShortcut(Keys key, ShortcutContext context)
        {
            var candidates = GetMatchingShortcuts(key, context)
                .OrderByDescending(s => s.LayerPriority)
                .ToList();
            
            if (!candidates.Any())
                return false;
            
            // Execute the highest priority shortcut
            var shortcut = candidates.First();
            if (shortcut.Action != null && shortcut.IsEnabled)
            {
                var executionContext = new ShortcutExecutionContext
                {
                    Context = context,
                    Shortcut = shortcut,
                    TriggeredKey = key
                };
                
                try
                {
                    shortcut.Action(executionContext);
                    _logger.Info($"Executed shortcut: {shortcut.Name} via {FormatKey(key)}");
                    ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs { 
                        Shortcut = shortcut, 
                        Key = key, 
                        Context = context 
                    });
                    return !executionContext.SuppressDefault;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error executing shortcut {shortcut.Name}", ex);
                    return false;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Get all shortcuts
        /// </summary>
        public ReadOnlyCollection<KeyboardShortcut> GetAllShortcuts()
        {
            return new ReadOnlyCollection<KeyboardShortcut>(_shortcuts.Values.ToList());
        }
        
        /// <summary>
        /// Get shortcuts by category
        /// </summary>
        public ReadOnlyCollection<KeyboardShortcut> GetShortcutsByCategory(string category)
        {
            var shortcuts = _shortcuts.Values.Where(s => s.Category == category).ToList();
            return new ReadOnlyCollection<KeyboardShortcut>(shortcuts);
        }
        
        /// <summary>
        /// Get shortcuts by context
        /// </summary>
        public ReadOnlyCollection<KeyboardShortcut> GetShortcutsByContext(ShortcutContext context)
        {
            var shortcuts = _shortcuts.Values.Where(s => (s.Context & context) != 0).ToList();
            return new ReadOnlyCollection<KeyboardShortcut>(shortcuts);
        }
        
        /// <summary>
        /// Find shortcuts by key combination
        /// </summary>
        public ReadOnlyCollection<KeyboardShortcut> FindShortcutsByKey(Keys key)
        {
            var shortcuts = _shortcuts.Values.Where(s => s.MatchesKey(key)).ToList();
            return new ReadOnlyCollection<KeyboardShortcut>(shortcuts);
        }
        
        /// <summary>
        /// Update a shortcut's key binding
        /// </summary>
        public bool UpdateShortcutKey(string shortcutId, Keys newKey)
        {
            if (!_shortcuts.TryGetValue(out var shortcut))
                return false;
            
            var oldKey = shortcut.PrimaryKey;
            shortcut.PrimaryKey = newKey;
            
            if (CheckForConflicts(shortcut))
            {
                // Restore old key if there's a conflict
                shortcut.PrimaryKey = oldKey;
                return false;
            }
            
            _logger.Info($"Updated shortcut {shortcut.Name} key: {FormatKey(oldKey)} -> {FormatKey(newKey)}");
            ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs { Action = ChangeAction.Modified, Shortcut = shortcut });
            return true;
        }
        
        /// <summary>
        /// Export shortcuts to JSON
        /// </summary>
        public string ExportToJson()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(_shortcuts.Values.ToList(), options);
        }
        
        /// <summary>
        /// Import shortcuts from JSON
        /// </summary>
        public bool ImportFromJson(string json, bool merge = true)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var importedShortcuts = JsonSerializer.Deserialize<List<KeyboardShortcut>>(json, options);
                
                if (importedShortcuts == null)
                    return false;
                
                if (!merge)
                    _shortcuts.Clear();
                
                foreach (var shortcut in importedShortcuts)
                {
                    if (!_shortcuts.ContainsKey(shortcut.Id))
                    {
                        RegisterShortcut(shortcut);
                    }
                }
                
                _logger.Info($"Imported {importedShortcuts.Count} shortcuts");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to import shortcuts", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Reset to default shortcuts
        /// </summary>
        public void ResetToDefaults()
        {
            _shortcuts.Clear();
            InitializeDefaultLayers();
            LoadDefaultShortcuts();
            _logger.Info("Reset to default shortcuts");
        }
        
        private void InitializeDefaultLayers()
        {
            _layers.Clear();
            
            // Define shortcut layers in priority order
            _layers.Add(new ShortcutLayer
            {
                Name = "System",
                Priority = 100,
                Description = "System-level shortcuts (highest priority)",
                Context = ShortcutContext.Global
            });
            
            _layers.Add(new ShortcutLayer
            {
                Name = "Application",
                Priority = 80,
                Description = "Application-level shortcuts",
                Context = ShortcutContext.Global
            });
            
            _layers.Add(new ShortcutLayer
            {
                Name = "Editor",
                Priority = 60,
                Description = "Editor-specific shortcuts",
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline
            });
            
            _layers.Add(new ShortcutLayer
            {
                Name = "Tools",
                Priority = 40,
                Description = "Tool-specific shortcuts",
                Context = ShortcutContext.Properties | ShortcutContext.AssetBrowser
            });
            
            _layers.Add(new ShortcutLayer
            {
                Name = "User",
                Priority = 20,
                Description = "User-customized shortcuts",
                Context = ShortcutContext.None
            });
        }
        
        private void LoadDefaultShortcuts()
        {
            // File operations
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "file.new",
                Name = "New Project",
                Description = "Create a new project",
                Category = "File",
                PrimaryKey = Keys.Control | Keys.N,
                Context = ShortcutContext.Global,
                LayerPriority = 80,
                Icon = "📄"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "file.open",
                Name = "Open Project",
                Description = "Open an existing project",
                Category = "File",
                PrimaryKey = Keys.Control | Keys.O,
                Context = ShortcutContext.Global,
                LayerPriority = 80,
                Icon = "📂"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "file.save",
                Name = "Save Project",
                Description = "Save the current project",
                Category = "File",
                PrimaryKey = Keys.Control | Keys.S,
                Context = ShortcutContext.Global,
                LayerPriority = 80,
                Icon = "💾"
            });
            
            // Edit operations
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "edit.undo",
                Name = "Undo",
                Description = "Undo the last action",
                Category = "Edit",
                PrimaryKey = Keys.Control | Keys.Z,
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "↶"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "edit.redo",
                Name = "Redo",
                Description = "Redo the last undone action",
                Category = "Edit",
                PrimaryKey = Keys.Control | Keys.Y,
                AlternativeKey = Keys.Control | Keys.Shift | Keys.Z,
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "↷"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "edit.copy",
                Name = "Copy",
                Description = "Copy selected items",
                Category = "Edit",
                PrimaryKey = Keys.Control | Keys.C,
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "📋"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "edit.paste",
                Name = "Paste",
                Description = "Paste items from clipboard",
                Category = "Edit",
                PrimaryKey = Keys.Control | Keys.V,
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "📋"
            });
            
            // View operations
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "view.fit",
                Name = "Fit to View",
                Description = "Fit content to current view",
                Category = "View",
                PrimaryKey = Keys.F,
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "🔍"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "view.zoom.in",
                Name = "Zoom In",
                Description = "Zoom into the view",
                Category = "View",
                PrimaryKey = Keys.Control | Keys.Oemplus,
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "🔍+"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "view.zoom.out",
                Name = "Zoom Out",
                Description = "Zoom out of the view",
                Category = "View",
                PrimaryKey = Keys.Control | Keys.OemMinus,
                Context = ShortcutContext.GraphEditor | ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "🔍-"
            });
            
            // Timeline operations
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "timeline.play",
                Name = "Play/Pause",
                Description = "Play or pause timeline playback",
                Category = "Timeline",
                PrimaryKey = Keys.Space,
                Context = ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "▶️"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "timeline.stop",
                Name = "Stop",
                Description = "Stop timeline playback",
                Category = "Timeline",
                PrimaryKey = Keys.Escape,
                Context = ShortcutContext.Timeline,
                LayerPriority = 60,
                Icon = "⏹️"
            });
            
            // Graph Editor operations
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "graph.select.all",
                Name = "Select All",
                Description = "Select all items in the graph",
                Category = "Graph Editor",
                PrimaryKey = Keys.Control | Keys.A,
                Context = ShortcutContext.GraphEditor,
                LayerPriority = 60,
                Icon = "🔘"
            });
            
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "graph.delete",
                Name = "Delete",
                Description = "Delete selected items",
                Category = "Graph Editor",
                PrimaryKey = Keys.Delete,
                Context = ShortcutContext.GraphEditor,
                LayerPriority = 60,
                Icon = "🗑️"
            });
            
            // Performance shortcuts
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "perf.toggle",
                Name = "Toggle Performance Monitor",
                Description = "Show or hide performance information",
                Category = "Performance",
                PrimaryKey = Keys.F11,
                Context = ShortcutContext.Global,
                LayerPriority = 80,
                Icon = "📊"
            });
            
            // Help shortcuts
            RegisterShortcut(new KeyboardShortcut
            {
                Id = "help.shortcuts",
                Name = "Show Shortcuts",
                Description = "Display keyboard shortcuts reference",
                Category = "Help",
                PrimaryKey = Keys.F1,
                Context = ShortcutContext.Global,
                LayerPriority = 80,
                Icon = "❓"
            });
        }
        
        private IEnumerable<KeyboardShortcut> GetMatchingShortcuts(Keys key, ShortcutContext context)
        {
            return _shortcuts.Values.Where(s => 
                s.MatchesKey(key) && 
                (s.Context == ShortcutContext.Global || (s.Context & context) != 0));
        }
        
        private bool CheckForConflicts(KeyboardShortcut shortcut)
        {
            var conflicts = _conflictDetector.DetectConflicts(shortcut, _shortcuts.Values.ToList());
            
            if (conflicts.Any())
            {
                ShortcutConflictDetected?.Invoke(this, new ShortcutConflictEventArgs { Conflicts = conflicts });
                return true;
            }
            
            return false;
        }
        
        private string FormatKey(Keys key)
        {
            var parts = new List<string>();
            
            if (key.HasFlag(Keys.Control)) parts.Add("Ctrl");
            if (key.HasFlag(Keys.Alt)) parts.Add("Alt");
            if (key.HasFlag(Keys.Shift)) parts.Add("Shift");
            
            var keyCode = key & Keys.KeyCode;
            if (keyCode != Keys.None)
                parts.Add(keyCode.ToString());
            
            return string.Join("+", parts);
        }
    }
    
    /// <summary>
    /// Represents a layer of shortcuts with priority and context
    /// </summary>
    public class ShortcutLayer
    {
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Description { get; set; } = string.Empty;
        public ShortcutContext Context { get; set; }
    }
    
    /// <summary>
    /// Detects conflicts between keyboard shortcuts
    /// </summary>
    public class ShortcutConflictDetector
    {
        public List<ShortcutConflict> DetectConflicts(KeyboardShortcut newShortcut, IEnumerable<KeyboardShortcut> existingShortcuts)
        {
            var conflicts = new List<ShortcutConflict>();
            
            foreach (var existing in existingShortcuts)
            {
                if (existing.Id == newShortcut.Id) continue;
                
                // Check for primary key conflicts
                if (existing.PrimaryKey == newShortcut.PrimaryKey || 
                    (existing.PrimaryKey == newShortcut.AlternativeKey) ||
                    (existing.AlternativeKey.HasValue && existing.AlternativeKey.Value == newShortcut.PrimaryKey))
                {
                    if (HasContextOverlap(existing.Context, newShortcut.Context))
                    {
                        conflicts.Add(new ShortcutConflict
                        {
                            PrimaryShortcut = newShortcut,
                            ConflictingShortcut = existing,
                            Type = ConflictType.KeyBinding,
                            Description = $"Key conflict: {FormatKey(newShortcut.PrimaryKey)}"
                        });
                    }
                }
                
                // Check for alternative key conflicts
                if (newShortcut.AlternativeKey.HasValue && 
                    existing.MatchesKey(newShortcut.AlternativeKey.Value))
                {
                    if (HasContextOverlap(existing.Context, newShortcut.Context))
                    {
                        conflicts.Add(new ShortcutConflict
                        {
                            PrimaryShortcut = newShortcut,
                            ConflictingShortcut = existing,
                            Type = ConflictType.AlternativeKey,
                            Description = $"Alternative key conflict: {FormatKey(newShortcut.AlternativeKey.Value)}"
                        });
                    }
                }
            }
            
            return conflicts;
        }
        
        private bool HasContextOverlap(ShortcutContext context1, ShortcutContext context2)
        {
            return (context1 == ShortcutContext.Global || context2 == ShortcutContext.Global || 
                    (context1 & context2) != 0);
        }
        
        private string FormatKey(Keys key)
        {
            var parts = new List<string>();
            
            if (key.HasFlag(Keys.Control)) parts.Add("Ctrl");
            if (key.HasFlag(Keys.Alt)) parts.Add("Alt");
            if (key.HasFlag(Keys.Shift)) parts.Add("Shift");
            
            var keyCode = key & Keys.KeyCode;
            if (keyCode != Keys.None)
                parts.Add(keyCode.ToString());
            
            return string.Join("+", parts);
        }
    }
    
    /// <summary>
    /// Represents a conflict between two shortcuts
    /// </summary>
    public class ShortcutConflict
    {
        public KeyboardShortcut PrimaryShortcut { get; set; }
        public KeyboardShortcut ConflictingShortcut { get; set; }
        public ConflictType Type { get; set; }
        public string Description { get; set; } = string.Empty;
    }
    
    public enum ConflictType
    {
        KeyBinding,
        AlternativeKey,
        Context
    }
    
    /// <summary>
    /// Event arguments for shortcut conflicts
    /// </summary>
    public class ShortcutConflictEventArgs : EventArgs
    {
        public List<ShortcutConflict> Conflicts { get; set; } = new();
    }
    
    /// <summary>
    /// Event arguments for shortcut execution
    /// </summary>
    public class ShortcutExecutedEventArgs : EventArgs
    {
        public KeyboardShortcut Shortcut { get; set; }
        public Keys Key { get; set; }
        public ShortcutContext Context { get; set; }
    }
    
    /// <summary>
    /// Event arguments for shortcut changes
    /// </summary>
    public class ShortcutChangedEventArgs : EventArgs
    {
        public ChangeAction Action { get; set; }
        public KeyboardShortcut Shortcut { get; set; }
    }
    
    public enum ChangeAction
    {
        Added,
        Removed,
        Modified
    }
    
    /// <summary>
    /// Logger for shortcut system events
    /// </summary>
    public class ShortcutLogger
    {
        private readonly ILogger _logger = LogManager.GetLogger("ShortcutSystem");
        
        public void Debug(string message) => _logger.Debug(message);
        public void Info(string message) => _logger.Info(message);
        public void Warn(string message) => _logger.Warn(message);
        public void Error(string message, Exception ex = null) => _logger.Error(message, ex);
    }
}