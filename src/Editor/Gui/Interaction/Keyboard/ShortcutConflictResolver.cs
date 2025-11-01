// Shortcut Conflict Resolver and Accessibility Support
// Provides intelligent conflict resolution and accessibility features for keyboard shortcuts

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TiXL.Core.Logging;

namespace TiXL.Editor.Gui.Interaction.Keyboard
{
    /// <summary>
    /// Intelligent conflict resolution system for keyboard shortcuts
    /// </summary>
    public class ShortcutConflictResolver
    {
        private readonly ILogger _logger = LogManager.GetLogger("ConflictResolver");
        
        public List<ResolvedShortcutConflict> ResolveConflicts(List<ShortcutConflict> conflicts)
        {
            var resolutions = new List<ResolvedShortcutConflict>();
            
            // Group conflicts by key combination
            var groupedConflicts = conflicts.GroupBy(c => c.PrimaryShortcut.PrimaryKey);
            
            foreach (var group in groupedConflicts)
            {
                var groupConflicts = group.ToList();
                var resolution = ResolveConflictGroup(groupConflicts);
                resolutions.Add(resolution);
            }
            
            return resolutions;
        }
        
        private ResolvedShortcutConflict ResolveConflictGroup(List<ShortcutConflict> conflicts)
        {
            var firstConflict = conflicts.First();
            var resolution = new ResolvedShortcutConflict
            {
                OriginalConflicts = conflicts,
                ResolutionType = DetermineBestResolution(conflicts)
            };
            
            switch (resolution.ResolutionType)
            {
                case ResolutionType.MoveToAlternativeKey:
                    resolution = ResolveByAlternativeKey(conflicts);
                    break;
                    
                case ResolutionType.ChangeContext:
                    resolution = ResolveByContext(conflicts);
                    break;
                    
                case ResolutionType.ChangeLayerPriority:
                    resolution = ResolveByLayerPriority(conflicts);
                    break;
                    
                case ResolutionType.UserChoiceRequired:
                    resolution = CreateUserChoiceResolution(conflicts);
                    break;
            }
            
            return resolution;
        }
        
        private ResolutionType DetermineBestResolution(List<ShortcutConflict> conflicts)
        {
            // If conflicts involve system shortcuts, prefer context changes
            var hasSystemShortcut = conflicts.Any(c => c.PrimaryShortcut.IsSystem || c.ConflictingShortcut.IsSystem);
            if (hasSystemShortcut)
                return ResolutionType.ChangeContext;
            
            // If conflicts are within the same category, prefer layer priority
            var sameCategory = conflicts.All(c => c.PrimaryShortcut.Category == c.ConflictingShortcut.Category);
            if (sameCategory)
                return ResolutionType.ChangeLayerPriority;
            
            // If there are available alternative keys, prefer that
            var hasAlternativeKeys = conflicts.Any(c => !c.PrimaryShortcut.AlternativeKey.HasValue);
            if (hasAlternativeKeys)
                return ResolutionType.MoveToAlternativeKey;
            
            return ResolutionType.UserChoiceRequired;
        }
        
        private ResolvedShortcutConflict ResolveByAlternativeKey(List<ShortcutConflict> conflicts)
        {
            var resolution = new ResolvedShortcutConflict
            {
                OriginalConflicts = conflicts,
                ResolutionType = ResolutionType.MoveToAlternativeKey,
                Description = "Move conflicting shortcuts to their alternative key bindings"
            };
            
            var proposedChanges = new List<ShortcutChange>();
            
            foreach (var conflict in conflicts)
            {
                if (!conflict.PrimaryShortcut.AlternativeKey.HasValue)
                {
                    // Find a suitable alternative key
                    var alternativeKey = FindSuitableAlternativeKey(conflict.ConflictingShortcut.PrimaryKey);
                    if (alternativeKey != Keys.None)
                    {
                        proposedChanges.Add(new ShortcutChange
                        {
                            Shortcut = conflict.PrimaryShortcut,
                            OldKey = conflict.PrimaryShortcut.PrimaryKey,
                            NewKey = alternativeKey,
                            ChangeType = ChangeType.ChangeToAlternative
                        });
                    }
                }
            }
            
            resolution.ProposedChanges = proposedChanges;
            return resolution;
        }
        
        private ResolvedShortcutConflict ResolveByContext(List<ShortcutConflict> conflicts)
        {
            var resolution = new ResolvedShortcutConflict
            {
                OriginalConflicts = conflicts,
                ResolutionType = ResolutionType.ChangeContext,
                Description = "Restrict conflicting shortcuts to specific contexts to avoid overlap"
            };
            
            var proposedChanges = new List<ShortcutChange>();
            
            // Sort by priority and assign non-overlapping contexts
            var sortedConflicts = conflicts.OrderByDescending(c => c.PrimaryShortcut.LayerPriority).ToList();
            var usedContexts = new List<ShortcutContext>();
            
            foreach (var conflict in sortedConflicts)
            {
                var availableContext = FindAvailableContext(conflict.ConflictingShortcut.Context, usedContexts);
                if (availableContext != ShortcutContext.None)
                {
                    proposedChanges.Add(new ShortcutChange
                    {
                        Shortcut = conflict.PrimaryShortcut,
                        OldContext = conflict.PrimaryShortcut.Context,
                        NewContext = availableContext,
                        ChangeType = ChangeType.ChangeContext
                    });
                    usedContexts.Add(availableContext);
                }
            }
            
            resolution.ProposedChanges = proposedChanges;
            return resolution;
        }
        
        private ResolvedShortcutConflict ResolveByLayerPriority(List<ShortcutConflict> conflicts)
        {
            var resolution = new ResolvedShortcutConflict
            {
                OriginalConflicts = conflicts,
                ResolutionType = ResolutionType.ChangeLayerPriority,
                Description = "Adjust layer priorities to establish clear precedence"
            };
            
            var proposedChanges = new List<ShortcutChange>();
            
            // Sort by existing priority and adjust
            var sortedConflicts = conflicts.OrderBy(c => c.PrimaryShortcut.LayerPriority).ToList();
            
            for (int i = 0; i < sortedConflicts.Count; i++)
            {
                var conflict = sortedConflicts[i];
                var newPriority = (i + 1) * 10; // Simple priority assignment
                
                if (conflict.PrimaryShortcut.LayerPriority != newPriority)
                {
                    proposedChanges.Add(new ShortcutChange
                    {
                        Shortcut = conflict.PrimaryShortcut,
                        OldPriority = conflict.PrimaryShortcut.LayerPriority,
                        NewPriority = newPriority,
                        ChangeType = ChangeType.ChangePriority
                    });
                }
            }
            
            resolution.ProposedChanges = proposedChanges;
            return resolution;
        }
        
        private ResolvedShortcutConflict CreateUserChoiceResolution(List<ShortcutConflict> conflicts)
        {
            return new ResolvedShortcutConflict
            {
                OriginalConflicts = conflicts,
                ResolutionType = ResolutionType.UserChoiceRequired,
                Description = "Manual resolution required - multiple valid options available",
                ProposedChanges = conflicts.SelectMany(c => GenerateManualChoices(c)).ToList()
            };
        }
        
        private List<ShortcutChange> GenerateManualChoices(ShortcutConflict conflict)
        {
            var choices = new List<ShortcutChange>();
            
            // Option 1: Keep primary, modify alternative
            if (!conflict.PrimaryShortcut.AlternativeKey.HasValue)
            {
                var altKey = FindSuitableAlternativeKey(conflict.ConflictingShortcut.PrimaryKey);
                if (altKey != Keys.None)
                {
                    choices.Add(new ShortcutChange
                    {
                        Shortcut = conflict.PrimaryShortcut,
                        OldKey = Keys.None,
                        NewKey = altKey,
                        ChangeType = ChangeType.ChangeToAlternative,
                        Description = $"Use {FormatKey(altKey)} as alternative key"
                    });
                }
            }
            
            // Option 2: Change context
            var availableContext = FindAvailableContext(conflict.ConflictingShortcut.Context, new List<ShortcutContext>());
            if (availableContext != ShortcutContext.None)
            {
                choices.Add(new ShortcutChange
                {
                    Shortcut = conflict.PrimaryShortcut,
                    OldContext = conflict.PrimaryShortcut.Context,
                    NewContext = availableContext,
                    ChangeType = ChangeType.ChangeContext,
                    Description = $"Restrict to {availableContext} context"
                });
            }
            
            // Option 3: Different key combination
            var altKey2 = FindCompletelyDifferentKey(conflict.ConflictingShortcut.PrimaryKey);
            if (altKey2 != Keys.None)
            {
                choices.Add(new ShortcutChange
                {
                    Shortcut = conflict.PrimaryShortcut,
                    OldKey = conflict.PrimaryShortcut.PrimaryKey,
                    NewKey = altKey2,
                    ChangeType = ChangeType.ChangeToAlternative,
                    Description = $"Use {FormatKey(altKey2)} instead"
                });
            }
            
            return choices;
        }
        
        private Keys FindSuitableAlternativeKey(Keys conflictKey)
        {
            // Find keys that don't conflict with the given key
            var commonKeys = new[]
            {
                Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5,
                Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5,
                Keys.Q, Keys.W, Keys.E, Keys.R, Keys.T,
                Keys.A, Keys.S, Keys.D, Keys.F, Keys.G,
                Keys.Z, Keys.X, Keys.C, Keys.V, Keys.B
            };
            
            foreach (var key in commonKeys)
            {
                if (key != conflictKey && !IsModifierConflict(key, conflictKey))
                    return key;
            }
            
            return Keys.None;
        }
        
        private Keys FindCompletelyDifferentKey(Keys conflictKey)
        {
            // Try to find a completely different key combination
            var alternatives = new[]
            {
                Keys.Control | Keys.Shift | Keys.D1,
                Keys.Control | Keys.Shift | Keys.D2,
                Keys.Control | Keys.Shift | Keys.D3,
                Keys.Alt | Keys.D1,
                Keys.Alt | Keys.D2,
                Keys.Alt | Keys.D3,
                Keys.Shift | Keys.F1,
                Keys.Shift | Keys.F2,
                Keys.Shift | Keys.F3
            };
            
            foreach (var key in alternatives)
            {
                if (key != conflictKey)
                    return key;
            }
            
            return Keys.None;
        }
        
        private bool IsModifierConflict(Keys key1, Keys key2)
        {
            var mod1 = GetModifiers(key1);
            var mod2 = GetModifiers(key2);
            
            return (mod1 & mod2) != 0;
        }
        
        private ShortcutContext FindAvailableContext(ShortcutContext currentContext, List<ShortcutContext> usedContexts)
        {
            var allContexts = Enum.GetValues(typeof(ShortcutContext))
                .Cast<ShortcutContext>()
                .Where(c => c != ShortcutContext.None && c != ShortcutContext.Global)
                .ToList();
            
            foreach (var context in allContexts)
            {
                if ((context & currentContext) != 0 && !usedContexts.Contains(context))
                    return context;
            }
            
            return ShortcutContext.None;
        }
        
        private Keys GetModifiers(Keys key)
        {
            var modifiers = Keys.None;
            if (key.HasFlag(Keys.Control)) modifiers |= Keys.Control;
            if (key.HasFlag(Keys.Alt)) modifiers |= Keys.Alt;
            if (key.HasFlag(Keys.Shift)) modifiers |= Keys.Shift;
            if (key.HasFlag(Keys.LWin) || key.HasFlag(Keys.RWin)) modifiers |= Keys.LWin | Keys.RWin;
            return modifiers;
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
    }
    
    /// <summary>
    /// Represents a resolved shortcut conflict
    /// </summary>
    public class ResolvedShortcutConflict
    {
        public List<ShortcutConflict> OriginalConflicts { get; set; } = new();
        public ResolutionType ResolutionType { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<ShortcutChange> ProposedChanges { get; set; } = new();
        public bool CanAutoResolve => ResolutionType != ResolutionType.UserChoiceRequired;
    }
    
    /// <summary>
    /// Types of conflict resolution
    /// </summary>
    public enum ResolutionType
    {
        MoveToAlternativeKey,
        ChangeContext,
        ChangeLayerPriority,
        UserChoiceRequired
    }
    
    /// <summary>
    /// Represents a proposed change to resolve a conflict
    /// </summary>
    public class ShortcutChange
    {
        public KeyboardShortcut Shortcut { get; set; }
        public ChangeType ChangeType { get; set; }
        public Keys OldKey { get; set; }
        public Keys NewKey { get; set; }
        public ShortcutContext OldContext { get; set; }
        public ShortcutContext NewContext { get; set; }
        public int OldPriority { get; set; }
        public int NewPriority { get; set; }
        public string Description { get; set; } = string.Empty;
    }
    
    public enum ChangeType
    {
        ChangeToAlternative,
        ChangeContext,
        ChangePriority
    }
    
    /// <summary>
    /// Form for resolving shortcut conflicts interactively
    /// </summary>
    public partial class ConflictResolverForm : Form
    {
        private readonly List<ResolvedShortcutConflict> _resolutions;
        private readonly LayeredKeyboardShortcutManager _shortcutManager;
        
        private ListView _conflictList;
        private Panel _resolutionPanel;
        private Button _btnApply;
        private Button _btnSkip;
        private Label _currentConflictLabel;
        private RadioButton _rbAutoResolve;
        private RadioButton _rbManualResolve;
        private ListView _resolutionOptions;
        
        private int _currentResolutionIndex;
        
        public ConflictResolverForm(List<ResolvedShortcutConflict> resolutions, LayeredKeyboardShortcutManager shortcutManager)
        {
            _resolutions = resolutions;
            _shortcutManager = shortcutManager;
            _currentResolutionIndex = 0;
            
            InitializeComponent();
            LoadCurrentConflict();
        }
        
        private void InitializeComponent()
        {
            Size = new Size(800, 600);
            Text = "Keyboard Shortcut Conflict Resolver";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ShowInTaskbar = false;
            
            // Current conflict display
            _currentConflictLabel = new Label
            {
                Text = "Resolving conflicts...",
                Location = new Point(20, 20),
                Size = new Size(760, 40),
                Font = new Font(T3Style.Fonts.Default, FontStyle.Bold),
                ForeColor = T3Style.Colors.HeaderText
            };
            
            // Resolution mode selection
            var modePanel = new Panel
            {
                Location = new Point(20, 70),
                Size = new Size(760, 40)
            };
            
            _rbAutoResolve = new RadioButton
            {
                Text = "Auto-resolve using recommended solution",
                Location = new Point(10, 10),
                Checked = true,
                ForeColor = T3Style.Colors.Text
            };
            _rbAutoResolve.CheckedChanged += OnResolutionModeChanged;
            
            _rbManualResolve = new RadioButton
            {
                Text = "Choose resolution manually",
                Location = new Point(250, 10),
                ForeColor = T3Style.Colors.Text
            };
            _rbManualResolve.CheckedChanged += OnResolutionModeChanged;
            
            modePanel.Controls.AddRange(new Control[] { _rbAutoResolve, _rbManualResolve });
            
            // Resolution options
            _resolutionOptions = new ListView
            {
                Location = new Point(20, 120),
                Size = new Size(760, 300),
                CheckBoxes = true,
                FullRowSelect = true,
                GridLines = true,
                View = View.Details
            };
            
            _resolutionOptions.Columns.Add("Action", 200);
            _resolutionOptions.Columns.Add("Description", 300);
            _resolutionOptions.Columns.Add("Shortcut", 200);
            _resolutionOptions.Columns.Add("Current", 60);
            _resolutionOptions.Columns.Add("Proposed", 60);
            
            _resolutionOptions.ItemChecked += OnResolutionOptionChecked;
            
            // Navigation buttons
            var buttonPanel = new Panel
            {
                Location = new Point(20, 430),
                Size = new Size(760, 40)
            };
            
            _btnSkip = new Button
            {
                Text = "Skip",
                Location = new Point(580, 5),
                Size = new Size(75, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnSkip.Click += OnSkipConflict;
            
            _btnApply = new Button
            {
                Text = "Apply & Next",
                Location = new Point(660, 5),
                Size = new Size(100, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnApply.Click += OnApplyResolution;
            
            buttonPanel.Controls.AddRange(new Control[] { _btnSkip, _btnApply });
            
            Controls.AddRange(new Control[]
            {
                _currentConflictLabel, modePanel, _resolutionOptions, buttonPanel
            });
        }
        
        private void LoadCurrentConflict()
        {
            if (_currentResolutionIndex >= _resolutions.Count)
            {
                Close();
                return;
            }
            
            var resolution = _resolutions[_currentResolutionIndex];
            
            _currentConflictLabel.Text = $"Conflict {_currentResolutionIndex + 1} of {_resolutions.Count}: {resolution.Description}";
            
            _resolutionOptions.Items.Clear();
            
            foreach (var change in resolution.ProposedChanges)
            {
                var item = new ListViewItem(change.ChangeType.ToString());
                item.SubItems.Add(change.Description);
                item.SubItems.Add(change.Shortcut.Name);
                item.SubItems.Add(GetKeyDisplay(change.OldKey, change.OldContext));
                item.SubItems.Add(GetKeyDisplay(change.NewKey, change.NewContext));
                item.Tag = change;
                
                // Auto-select if it's an auto-resolvable conflict
                if (resolution.CanAutoResolve)
                    item.Checked = true;
                
                _resolutionOptions.Items.Add(item);
            }
            
            // Update button states
            _btnApply.Enabled = _resolutionOptions.CheckedItems.Count > 0;
            _btnSkip.Enabled = _currentResolutionIndex < _resolutions.Count - 1;
        }
        
        private string GetKeyDisplay(Keys key, ShortcutContext context)
        {
            if (key == Keys.None)
                return "None";
            return FormatKey(key) + (context != ShortcutContext.None ? $" ({context})" : "");
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
        
        #region Event Handlers
        
        private void OnResolutionModeChanged(object sender, EventArgs e)
        {
            var resolution = _resolutions[_currentResolutionIndex];
            
            if (_rbAutoResolve.Checked && resolution.CanAutoResolve)
            {
                // Auto-select all proposed changes
                foreach (ListViewItem item in _resolutionOptions.Items)
                {
                    item.Checked = true;
                }
            }
            else
            {
                // Clear selections for manual mode
                foreach (ListViewItem item in _resolutionOptions.Items)
                {
                    item.Checked = false;
                }
            }
            
            _btnApply.Enabled = _resolutionOptions.CheckedItems.Count > 0;
        }
        
        private void OnResolutionOptionChecked(object sender, ItemCheckedEventArgs e)
        {
            _btnApply.Enabled = _resolutionOptions.CheckedItems.Count > 0;
        }
        
        private void OnApplyResolution(object sender, EventArgs e)
        {
            var resolution = _resolutions[_currentResolutionIndex];
            
            foreach (ListViewItem item in _resolutionOptions.CheckedItems)
            {
                var change = item.Tag as ShortcutChange;
                if (change != null)
                {
                    ApplyChange(change);
                }
            }
            
            _currentResolutionIndex++;
            LoadCurrentConflict();
        }
        
        private void OnSkipConflict(object sender, EventArgs e)
        {
            _currentResolutionIndex++;
            LoadCurrentConflict();
        }
        
        private void ApplyChange(ShortcutChange change)
        {
            switch (change.ChangeType)
            {
                case ChangeType.ChangeToAlternative:
                    change.Shortcut.AlternativeKey = change.NewKey;
                    break;
                    
                case ChangeType.ChangeContext:
                    change.Shortcut.Context = change.NewContext;
                    break;
                    
                case ChangeType.ChangePriority:
                    change.Shortcut.LayerPriority = change.NewPriority;
                    break;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Accessibility support for keyboard shortcuts
    /// </summary>
    public class ShortcutAccessibilitySupport
    {
        private readonly LayeredKeyboardShortcutManager _shortcutManager;
        private readonly Dictionary<string, AccessibilityAnnouncement> _announcements;
        
        public event EventHandler<ShortcutExecutedEventArgs> ShortcutAnnounced;
        
        public ShortcutAccessibilitySupport(LayeredKeyboardShortcutManager shortcutManager)
        {
            _shortcutManager = shortcutManager;
            _announcements = new Dictionary<string, AccessibilityAnnouncement>();
            InitializeAccessibilityFeatures();
        }
        
        private void InitializeAccessibilityFeatures()
        {
            // Initialize with default accessibility announcements
            AddAnnouncement("file.new", "New project created");
            AddAnnouncement("file.open", "Project opened");
            AddAnnouncement("file.save", "Project saved");
            AddAnnouncement("edit.undo", "Action undone");
            AddAnnouncement("edit.redo", "Action redone");
            AddAnnouncement("timeline.play", "Playback started");
            AddAnnouncement("timeline.stop", "Playback stopped");
            AddAnnouncement("view.zoom.in", "Zoomed in");
            AddAnnouncement("view.zoom.out", "Zoomed out");
        }
        
        public void AddAnnouncement(string shortcutId, string announcementText)
        {
            _announcements[shortcutId] = new AccessibilityAnnouncement
            {
                ShortcutId = shortcutId,
                Text = announcementText,
                Priority = AnnouncementPriority.Normal
            };
        }
        
        public void AnnounceShortcutExecution(KeyboardShortcut shortcut)
        {
            var announcement = GetAnnouncement(shortcut.Id);
            if (announcement != null)
            {
                // Trigger accessibility announcement
                ShortcutAnnounced?.Invoke(this, new ShortcutExecutedEventArgs
                {
                    Shortcut = shortcut,
                    Key = shortcut.PrimaryKey,
                    Context = shortcut.Context
                });
                
                // Log for debugging
                System.Diagnostics.Debug.WriteLine($"Accessibility: {announcement.Text}");
            }
        }
        
        public void EnableSpeechOutput(bool enabled)
        {
            // This would integrate with screen reader APIs
            // For now, just log the setting
            System.Diagnostics.Debug.WriteLine($"Speech output {(enabled ? "enabled" : "disabled")}");
        }
        
        public void SetAnnouncementVolume(float volume)
        {
            // Volume control for accessibility announcements
            System.Diagnostics.Debug.WriteLine($"Announcement volume set to {volume}");
        }
        
        public AccessibilityAnnouncement GetAnnouncement(string shortcutId)
        {
            return _announcements.TryGetValue(shortcutId, out var announcement) ? announcement : null;
        }
        
        public List<AccessibilityAnnouncement> GetAllAnnouncements()
        {
            return _announcements.Values.ToList();
        }
        
        public void UpdateAnnouncement(string shortcutId, string newText, AnnouncementPriority priority = AnnouncementPriority.Normal)
        {
            if (_announcements.ContainsKey(shortcutId))
            {
                _announcements[shortcutId] = new AccessibilityAnnouncement
                {
                    ShortcutId = shortcutId,
                    Text = newText,
                    Priority = priority
                };
            }
        }
        
        public void RemoveAnnouncement(string shortcutId)
        {
            _announcements.Remove(shortcutId);
        }
        
        public void GenerateAccessibilityReport()
        {
            var report = "Accessibility Report:\n";
            report += "=====================\n\n";
            
            foreach (var announcement in _announcements.Values.OrderBy(a => a.Priority))
            {
                report += $"{announcement.ShortcutId}: {announcement.Text} (Priority: {announcement.Priority})\n";
            }
            
            System.Diagnostics.Debug.WriteLine(report);
        }
    }
    
    public class AccessibilityAnnouncement
    {
        public string ShortcutId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public AnnouncementPriority Priority { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
    
    public enum AnnouncementPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}