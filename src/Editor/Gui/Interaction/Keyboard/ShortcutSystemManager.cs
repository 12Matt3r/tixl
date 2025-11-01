// Main Integration Manager for Layered Keyboard Shortcut System
// Coordinates all components and provides unified interface for the TiXL editor

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TiXL.Core.Logging;

namespace TiXL.Editor.Gui.Interaction.Keyboard
{
    /// <summary>
    /// Main manager that coordinates all keyboard shortcut system components
    /// </summary>
    public class ShortcutSystemManager : IDisposable
    {
        private readonly LayeredKeyboardShortcutManager _shortcutManager;
        private readonly ShortcutConflictResolver _conflictResolver;
        private readonly ShortcutAccessibilitySupport _accessibilitySupport;
        private readonly ShortcutHelpSystem _helpSystem;
        
        private bool _isEnabled = true;
        private ShortcutContext _currentContext = ShortcutContext.Global;
        private Dictionary<Keys, DateTime> _keyPressTimes = new();
        private Timer _keyPressTimer;
        
        public event EventHandler<ShortcutExecutedEventArgs> ShortcutExecuted;
        public event EventHandler<ShortcutConflictEventArgs> ShortcutConflictDetected;
        public event EventHandler<ShortcutChangedEventArgs> ShortcutChanged;
        
        public ShortcutSystemManager()
        {
            _shortcutManager = new LayeredKeyboardShortcutManager();
            _conflictResolver = new ShortcutConflictResolver();
            _accessibilitySupport = new ShortcutAccessibilitySupport(_shortcutManager);
            _helpSystem = new ShortcutHelpSystem(_shortcutManager);
            
            InitializeComponents();
            SetupEventHandlers();
        }
        
        private void InitializeComponents()
        {
            _keyPressTimer = new Timer();
            _keyPressTimer.Interval = 100; // Check every 100ms
            _keyPressTimer.Tick += OnKeyPressTimerTick;
            _keyPressTimer.Start();
        }
        
        private void SetupEventHandlers()
        {
            _shortcutManager.ShortcutConflictDetected += (s, e) =>
            {
                ShortcutConflictDetected?.Invoke(s, e);
                LogConflicts(e.Conflicts);
            };
            
            _shortcutManager.ShortcutChanged += (s, e) =>
            {
                ShortcutChanged?.Invoke(s, e);
                UpdateAccessibilityAnnouncements();
            };
            
            _shortcutManager.ShortcutExecuted += (s, e) =>
            {
                ShortcutExecuted?.Invoke(s, e);
                _accessibilitySupport.AnnounceShortcutExecution(e.Shortcut);
            };
        }
        
        /// <summary>
        /// Process a keyboard event and execute matching shortcuts
        /// </summary>
        public bool ProcessKeyEvent(Keys keyData, bool isKeyDown)
        {
            if (!_isEnabled || !isKeyDown) return false;
            
            // Track key press timing for debouncing
            var currentTime = DateTime.Now;
            if (_keyPressTimes.ContainsKey(keyData))
            {
                var lastPress = _keyPressTimes[keyData];
                if ((currentTime - lastPress).TotalMilliseconds < 50)
                    return false; // Debounce rapid key presses
            }
            _keyPressTimes[keyData] = currentTime;
            
            // Try to execute the shortcut
            var executed = _shortcutManager.ExecuteShortcut(keyData, _currentContext);
            
            if (executed)
            {
                LogSuccessfulExecution(keyData, _currentContext);
            }
            
            return executed;
        }
        
        /// <summary>
        /// Set the current editor context
        /// </summary>
        public void SetCurrentContext(ShortcutContext context)
        {
            _currentContext = context;
            LogContextChange(context);
        }
        
        /// <summary>
        /// Get the current editor context
        /// </summary>
        public ShortcutContext GetCurrentContext() => _currentContext;
        
        /// <summary>
        /// Enable or disable the shortcut system
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            LogSystemStateChange(enabled);
        }
        
        /// <summary>
        /// Show the keyboard shortcut editor
        /// </summary>
        public void ShowShortcutEditor(Form parentForm)
        {
            var editor = new KeyboardShortcutEditor(_shortcutManager);
            editor.ShowDialog(parentForm);
        }
        
        /// <summary>
        /// Show the conflict resolver
        /// </summary>
        public void ShowConflictResolver(Form parentForm)
        {
            var allConflicts = DetectAllConflicts();
            if (!allConflicts.Any())
            {
                MessageBox.Show("No conflicts detected.", "Conflict Resolution", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var resolutions = _conflictResolver.ResolveConflicts(allConflicts);
            var resolverForm = new ConflictResolverForm(resolutions, _shortcutManager);
            resolverForm.ShowDialog(parentForm);
        }
        
        /// <summary>
        /// Show the keyboard shortcut help
        /// </summary>
        public void ShowShortcutHelp(Form parentForm)
        {
            var helpForm = new ShortcutCheatSheetForm(_shortcutManager, _helpSystem);
            helpForm.ShowDialog(parentForm);
        }
        
        /// <summary>
        /// Show the keyboard visualization
        /// </summary>
        public void ShowKeyboardVisualization(Form parentForm)
        {
            var shortcuts = _shortcutManager.GetAllShortcuts().ToList();
            var visualization = new ShortcutVisualizationPanel();
            visualization.LoadShortcuts(shortcuts);
            
            var form = new Form
            {
                Text = "Keyboard Shortcut Visualization",
                Size = new System.Drawing.Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                Icon = SystemIcons.Application
            };
            
            form.Controls.Add(visualization);
            form.ShowDialog(parentForm);
        }
        
        /// <summary>
        /// Export shortcuts to file
        /// </summary>
        public bool ExportShortcuts(string filePath)
        {
            try
            {
                var json = _shortcutManager.ExportToJson();
                System.IO.File.WriteAllText(filePath, json);
                
                LogOperation("Export", $"Exported {shortcuts.Count} shortcuts to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogError("Export", $"Failed to export shortcuts: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Import shortcuts from file
        /// </summary>
        public bool ImportShortcuts(string filePath, bool merge = true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                var success = _shortcutManager.ImportFromJson(json, merge);
                
                var operation = merge ? "Merged" : "Replaced";
                LogOperation("Import", $"{operation} shortcuts from {filePath}");
                
                return success;
            }
            catch (Exception ex)
            {
                LogError("Import", $"Failed to import shortcuts: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Generate and export documentation
        /// </summary>
        public void ExportDocumentation(string filePath, DocumentationFormat format)
        {
            try
            {
                switch (format)
                {
                    case DocumentationFormat.Markdown:
                        _helpSystem.ExportToMarkdown(filePath);
                        break;
                    case DocumentationFormat.Text:
                        _helpSystem.ExportToText(filePath);
                        break;
                }
                
                LogOperation("Documentation Export", $"Exported documentation to {filePath} ({format})");
            }
            catch (Exception ex)
            {
                LogError("Documentation Export", $"Failed to export documentation: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Search shortcuts
        /// </summary>
        public List<KeyboardShortcut> SearchShortcuts(string query)
        {
            var allShortcuts = _shortcutManager.GetAllShortcuts().ToList();
            var lowerQuery = query.ToLower();
            
            return allShortcuts.Where(s =>
                s.Name.ToLower().Contains(lowerQuery) ||
                s.Description.ToLower().Contains(lowerQuery) ||
                s.Category.ToLower().Contains(lowerQuery) ||
                s.Tags.Any(t => t.ToLower().Contains(lowerQuery))
            ).OrderBy(s => s.Name).ToList();
        }
        
        /// <summary>
        /// Get shortcuts by category
        /// </summary>
        public ReadOnlyCollection<KeyboardShortcut> GetShortcutsByCategory(string category)
        {
            return _shortcutManager.GetShortcutsByCategory(category);
        }
        
        /// <summary>
        /// Get shortcuts by context
        /// </summary>
        public ReadOnlyCollection<KeyboardShortcut> GetShortcutsByContext(ShortcutContext context)
        {
            return _shortcutManager.GetShortcutsByContext(context);
        }
        
        /// <summary>
        /// Reset to default shortcuts
        /// </summary>
        public void ResetToDefaults()
        {
            _shortcutManager.ResetToDefaults();
            _accessibilitySupport = new ShortcutAccessibilitySupport(_shortcutManager);
            LogOperation("Reset", "Reset all shortcuts to defaults");
        }
        
        /// <summary>
        /// Get accessibility settings
        /// </summary>
        public ShortcutAccessibilitySupport GetAccessibilitySupport() => _accessibilitySupport;
        
        /// <summary>
        /// Get help system
        /// </summary>
        public ShortcutHelpSystem GetHelpSystem() => _helpSystem;
        
        private List<ShortcutConflict> DetectAllConflicts()
        {
            var shortcuts = _shortcutManager.GetAllShortcuts().ToList();
            var conflicts = new List<ShortcutConflict>();
            
            for (int i = 0; i < shortcuts.Count; i++)
            {
                for (int j = i + 1; j < shortcuts.Count; j++)
                {
                    var newConflicts = _conflictResolver.DetectConflicts(shortcuts[i], shortcuts.Skip(j).ToList());
                    conflicts.AddRange(newConflicts);
                }
            }
            
            return conflicts;
        }
        
        private void UpdateAccessibilityAnnouncements()
        {
            // Update accessibility announcements for new or modified shortcuts
            var shortcuts = _shortcutManager.GetAllShortcuts().ToList();
            
            foreach (var shortcut in shortcuts)
            {
                if (_accessibilitySupport.GetAnnouncement(shortcut.Id) == null)
                {
                    _accessibilitySupport.AddAnnouncement(shortcut.Id, $"{shortcut.Name} executed");
                }
            }
        }
        
        private void OnKeyPressTimerTick(object sender, EventArgs e)
        {
            // Clean up old key press tracking
            var cutoff = DateTime.Now.AddSeconds(-1);
            var keysToRemove = _keyPressTimes.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
            
            foreach (var key in keysToRemove)
            {
                _keyPressTimes.Remove(key);
            }
        }
        
        private void LogConflicts(List<ShortcutConflict> conflicts)
        {
            if (conflicts.Any())
            {
                var logger = LogManager.GetLogger("ShortcutSystem");
                logger.Warn($"Detected {conflicts.Count} keyboard shortcut conflicts");
                
                foreach (var conflict in conflicts)
                {
                    logger.Warn($"  {conflict.Description}: {conflict.PrimaryShortcut.Name} vs {conflict.ConflictingShortcut.Name}");
                }
            }
        }
        
        private void LogSuccessfulExecution(Keys key, ShortcutContext context)
        {
            var logger = LogManager.GetLogger("ShortcutSystem");
            logger.Debug($"Executed shortcut via {FormatKey(key)} in {context} context");
        }
        
        private void LogContextChange(ShortcutContext context)
        {
            var logger = LogManager.GetLogger("ShortcutSystem");
            logger.Debug($"Shortcut context changed to: {context}");
        }
        
        private void LogSystemStateChange(bool enabled)
        {
            var logger = LogManager.GetLogger("ShortcutSystem");
            logger.Info($"Shortcut system {(enabled ? "enabled" : "disabled")}");
        }
        
        private void LogOperation(string operation, string message)
        {
            var logger = LogManager.GetLogger("ShortcutSystem");
            logger.Info($"{operation}: {message}");
        }
        
        private void LogError(string operation, string message)
        {
            var logger = LogManager.GetLogger("ShortcutSystem");
            logger.Error($"{operation} failed: {message}");
        }
        
        private string FormatKey(Keys key)
        {
            var parts = new List<string>();
            
            if (key.HasFlag(Keys.Control)) parts.Add("Ctrl");
            if (key.HasFlag(Keys.Alt)) parts.Add("Alt");
            if (key.HasFlag(Keys.Shift)) parts.Add("Shift");
            
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
        
        public void Dispose()
        {
            _keyPressTimer?.Stop();
            _keyPressTimer?.Dispose();
        }
    }
    
    public enum DocumentationFormat
    {
        Markdown,
        Text,
        HTML
    }
}