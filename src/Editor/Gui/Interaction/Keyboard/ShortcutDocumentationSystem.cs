// Keyboard Shortcut Documentation System and Cheat Sheets
// Provides comprehensive documentation, search, and help features for keyboard shortcuts

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TiXL.Editor.Gui.Interaction.Keyboard
{
    /// <summary>
    /// Comprehensive help system for keyboard shortcuts
    /// </summary>
    public partial class ShortcutHelpSystem
    {
        private readonly LayeredKeyboardShortcutManager _shortcutManager;
        private readonly Dictionary<string, ShortcutDocumentation> _documentation;
        
        public ShortcutHelpSystem(LayeredKeyboardShortcutManager shortcutManager)
        {
            _shortcutManager = shortcutManager;
            _documentation = new Dictionary<string, ShortcutDocumentation>();
            InitializeDocumentation();
        }
        
        private void InitializeDocumentation()
        {
            // Add documentation for default shortcuts
            AddDocumentation("file.new", "Create a new project", "Creates a blank project with default settings");
            AddDocumentation("file.open", "Open an existing project", "Opens a previously saved TiXL project file");
            AddDocumentation("file.save", "Save the current project", "Saves the current project to its existing file");
            AddDocumentation("file.saveAs", "Save project as", "Saves the current project with a new filename");
            AddDocumentation("file.exit", "Exit TiXL", "Closes TiXL, prompting to save if needed");
            
            AddDocumentation("edit.undo", "Undo the last action", "Reverts the most recent change");
            AddDocumentation("edit.redo", "Redo the last undone action", "Re-applies the most recent undone change");
            AddDocumentation("edit.copy", "Copy selected items", "Copies selected graph nodes or timeline elements to clipboard");
            AddDocumentation("edit.paste", "Paste from clipboard", "Inserts clipboard contents at current cursor position");
            AddDocumentation("edit.cut", "Cut selected items", "Removes selected items and copies them to clipboard");
            AddDocumentation("edit.delete", "Delete selected items", "Permanently removes selected items");
            AddDocumentation("edit.selectAll", "Select all items", "Selects all items in the current view");
            
            AddDocumentation("view.fit", "Fit content to view", "Adjusts zoom to show all content optimally");
            AddDocumentation("view.zoomIn", "Zoom in", "Increases zoom level for detailed work");
            AddDocumentation("view.zoomOut", "Zoom out", "Decreases zoom level to see more content");
            AddDocumentation("view.resetZoom", "Reset zoom", "Returns to default zoom level");
            AddDocumentation("view.toggleFullscreen", "Toggle fullscreen", "Switches between windowed and fullscreen modes");
            
            AddDocumentation("timeline.play", "Play/Pause", "Starts or pauses timeline playback");
            AddDocumentation("timeline.stop", "Stop playback", "Stops timeline and returns to beginning");
            AddDocumentation("timeline.rewind", "Rewind", "Jumps to the beginning of the timeline");
            AddDocumentation("timeline.fastForward", "Fast forward", "Jumps to the end of the timeline");
            AddDocumentation("timeline.addKeyframe", "Add keyframe", "Creates a new keyframe at current timeline position");
            
            AddDocumentation("graph.addNode", "Add new node", "Opens node browser to add new operator to graph");
            AddDocumentation("graph.deleteNode", "Delete node", "Removes selected graph node and its connections");
            AddDocumentation("graph.connectNodes", "Connect nodes", "Starts connection mode between graph nodes");
            AddDocumentation("graph.duplicate", "Duplicate selection", "Creates copies of selected items");
            
            AddDocumentation("perf.toggle", "Toggle performance monitor", "Shows/hides performance statistics overlay");
            AddDocumentation("perf.resetStats", "Reset performance stats", "Clears performance measurement data");
            
            AddDocumentation("help.shortcuts", "Show keyboard shortcuts", "Opens keyboard shortcut reference");
            AddDocumentation("help.about", "About TiXL", "Shows application information and version details");
        }
        
        public void AddDocumentation(string shortcutId, string title, string description)
        {
            _documentation[shortcutId] = new ShortcutDocumentation
            {
                ShortcutId = shortcutId,
                Title = title,
                Description = description,
                Category = GetCategoryFromId(shortcutId),
                AddedDate = DateTime.Now
            };
        }
        
        private string GetCategoryFromId(string shortcutId)
        {
            var parts = shortcutId.Split('.');
            return parts.Length > 0 ? parts[0] : "Unknown";
        }
        
        public ShortcutDocumentation GetDocumentation(string shortcutId)
        {
            return _documentation.TryGetValue(shortcutId, out var doc) ? doc : null;
        }
        
        public List<ShortcutDocumentation> SearchDocumentation(string query)
        {
            var lowerQuery = query.ToLower();
            return _documentation.Values
                .Where(doc => 
                    doc.Title.ToLower().Contains(lowerQuery) ||
                    doc.Description.ToLower().Contains(lowerQuery) ||
                    doc.Category.ToLower().Contains(lowerQuery))
                .OrderBy(doc => doc.Title)
                .ToList();
        }
        
        public string GenerateQuickReferenceGuide()
        {
            var guide = new StringBuilder();
            guide.AppendLine("# TiXL Keyboard Shortcut Quick Reference");
            guide.AppendLine("========================================");
            guide.AppendLine();
            
            var shortcuts = _shortcutManager.GetAllShortcuts().ToList();
            var groupedShortcuts = shortcuts.GroupBy(s => s.Category).OrderBy(g => g.Key);
            
            foreach (var categoryGroup in groupedShortcuts)
            {
                guide.AppendLine($"## {categoryGroup.Key}");
                guide.AppendLine();
                
                foreach (var shortcut in categoryGroup.OrderBy(s => s.Name))
                {
                    guide.AppendLine($"**{shortcut.Name}**");
                    guide.AppendLine($"  Key: `{shortcut.GetKeyDisplayString()}`");
                    guide.AppendLine($"  Description: {shortcut.Description}");
                    guide.AppendLine();
                }
            }
            
            return guide.ToString();
        }
        
        public string GenerateComprehensiveGuide()
        {
            var guide = new StringBuilder();
            guide.AppendLine("# TiXL Keyboard Shortcuts - Comprehensive Guide");
            guide.AppendLine("=============================================");
            guide.AppendLine();
            guide.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}");
            guide.AppendLine();
            
            // Table of contents
            guide.AppendLine("## Table of Contents");
            guide.AppendLine();
            var categories = _shortcutManager.GetAllShortcuts()
                .GroupBy(s => s.Category)
                .OrderBy(g => g.Key)
                .ToList();
            
            foreach (var category in categories)
            {
                guide.AppendLine($"- [{category.Key}](#{category.Key.ToLower().Replace(" ", "-")})");
            }
            guide.AppendLine();
            
            // Detailed sections
            foreach (var categoryGroup in categories)
            {
                guide.AppendLine($"## {categoryGroup.Key}");
                guide.AppendLine();
                
                var categoryShortcuts = categoryGroup.OrderBy(s => s.Name).ToList();
                
                foreach (var shortcut in categoryShortcuts)
                {
                    var doc = GetDocumentation(shortcut.Id);
                    
                    guide.AppendLine($"### {shortcut.Name}");
                    guide.AppendLine();
                    guide.AppendLine($"**Key Combination:** `{shortcut.GetKeyDisplayString()}`");
                    
                    if (shortcut.AlternativeKey.HasValue)
                        guide.AppendLine($"**Alternative:** `{shortcut.GetKeyDisplayString().Split('/').Last()}`");
                    
                    guide.AppendLine($"**Context:** {shortcut.Context}");
                    guide.AppendLine($"**Description:** {shortcut.Description}");
                    
                    if (doc != null && !string.IsNullOrEmpty(doc.Description))
                    {
                        guide.AppendLine();
                        guide.AppendLine($"**Detailed Description:**");
                        guide.AppendLine(doc.Description);
                    }
                    
                    if (shortcut.Tags.Any())
                    {
                        guide.AppendLine($"**Tags:** {string.Join(", ", shortcut.Tags)}");
                    }
                    
                    guide.AppendLine();
                }
            }
            
            // Tips and best practices
            guide.AppendLine("## Tips and Best Practices");
            guide.AppendLine();
            guide.AppendLine("### Shortcut Efficiency");
            guide.AppendLine("- Use keyboard shortcuts for frequently performed actions");
            guide.AppendLine("- Customize shortcuts to match your workflow preferences");
            guide.AppendLine("- Learn shortcuts incrementally, starting with the most common actions");
            guide.AppendLine();
            
            guide.AppendLine("### Conflict Resolution");
            guide.AppendLine("- Use the Conflict Resolver to automatically resolve key conflicts");
            guide.AppendLine("- Context-aware shortcuts can share the same key combination safely");
            guide.AppendLine("- System shortcuts (like Ctrl+C for Copy) are protected and cannot be changed");
            guide.AppendLine();
            
            guide.AppendLine("### Accessibility");
            guide.AppendLine("- Enable speech output for action confirmation");
            guide.AppendLine("- Customize announcements for your specific needs");
            guide.AppendLine("- Use alternative keys if certain combinations are difficult");
            guide.AppendLine();
            
            return guide.ToString();
        }
        
        public List<string> GetShortcutTips(string shortcutId)
        {
            var tips = new List<string>();
            var shortcut = _shortcutManager.GetAllShortcuts().FirstOrDefault(s => s.Id == shortcutId);
            
            if (shortcut != null)
            {
                // Context-specific tips
                if (shortcut.Context == ShortcutContext.GraphEditor)
                    tips.Add("This shortcut only works in the Graph Editor view");
                
                if (shortcut.PrimaryKey == Keys.Control)
                    tips.Add("Hold Ctrl while pressing the main key");
                
                if (shortcut.LayerPriority > 60)
                    tips.Add("This is a high-priority shortcut that overrides others");
                
                if (shortcut.AlternativeKey.HasValue)
                    tips.Add("Alternative key combination available for accessibility");
            }
            
            return tips;
        }
        
        public void ExportToMarkdown(string filePath)
        {
            var guide = GenerateComprehensiveGuide();
            System.IO.File.WriteAllText(filePath, guide);
        }
        
        public void ExportToText(string filePath)
        {
            var shortcuts = _shortcutManager.GetAllShortcuts().ToList();
            var text = new StringBuilder();
            
            text.AppendLine("TiXL Keyboard Shortcuts");
            text.AppendLine("=======================");
            text.AppendLine();
            
            var groupedShortcuts = shortcuts.GroupBy(s => s.Category).OrderBy(g => g.Key);
            
            foreach (var categoryGroup in groupedShortcuts)
            {
                text.AppendLine(categoryGroup.Key.ToUpper());
                text.AppendLine(new string('-', categoryGroup.Key.Length));
                text.AppendLine();
                
                foreach (var shortcut in categoryGroup.OrderBy(s => s.Name))
                {
                    text.AppendLine($"{shortcut.Name,-30} {shortcut.GetKeyDisplayString(),20}");
                    text.AppendLine($"{shortcut.Description}");
                    text.AppendLine();
                }
            }
            
            System.IO.File.WriteAllText(filePath, text.ToString());
        }
    }
    
    /// <summary>
    /// Documentation for a keyboard shortcut
    /// </summary>
    public class ShortcutDocumentation
    {
        public string ShortcutId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime AddedDate { get; set; }
        public List<string> Examples { get; set; } = new();
        public List<string> RelatedShortcuts { get; set; } = new();
        public bool IsSystem { get; set; }
    }
    
    /// <summary>
    /// Visual cheat sheet dialog
    /// </summary>
    public partial class ShortcutCheatSheetForm : Form
    {
        private readonly LayeredKeyboardShortcutManager _shortcutManager;
        private readonly ShortcutHelpSystem _helpSystem;
        
        private ListView _shortcutList;
        private TextBox _searchBox;
        private ComboBox _categoryFilter;
        private PropertyGrid _detailGrid;
        private TabControl _tabControl;
        private WebBrowser _helpPreview;
        
        public ShortcutCheatSheetForm(LayeredKeyboardShortcutManager shortcutManager, ShortcutHelpSystem helpSystem)
        {
            _shortcutManager = shortcutManager;
            _helpSystem = helpSystem;
            
            InitializeComponent();
            LoadShortcuts();
            SetupEventHandlers();
        }
        
        private void InitializeComponent()
        {
            Size = new Size(1000, 700);
            Text = "TiXL Keyboard Shortcuts - Cheat Sheet";
            StartPosition = FormStartPosition.CenterParent;
            Icon = SystemIcons.Information;
            
            // Search panel
            var searchPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = T3Style.Colors.HeaderBackground
            };
            
            var searchLabel = new Label
            {
                Text = "Search:",
                Location = new Point(10, 15),
                Size = new Size(50, 20),
                ForeColor = T3Style.Colors.HeaderText
            };
            
            _searchBox = new TextBox
            {
                Location = new Point(60, 12),
                Size = new Size(200, 23),
                BackColor = T3Style.Colors.InputBackground,
                ForeColor = T3Style.Colors.Text
            };
            
            var categoryLabel = new Label
            {
                Text = "Category:",
                Location = new Point(270, 15),
                Size = new Size(60, 20),
                ForeColor = T3Style.Colors.HeaderText
            };
            
            _categoryFilter = new ComboBox
            {
                Location = new Point(330, 12),
                Size = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = T3Style.Colors.InputBackground,
                ForeColor = T3Style.Colors.Text
            };
            
            searchPanel.Controls.AddRange(new Control[]
            {
                searchLabel, _searchBox, categoryLabel, _categoryFilter
            });
            
            // Main content
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, 50)
            };
            
            // Cheat sheet tab
            var cheatSheetTab = CreateCheatSheetTab();
            _tabControl.TabPages.Add(cheatSheetTab);
            
            // Help tab
            var helpTab = CreateHelpTab();
            _tabControl.TabPages.Add(helpTab);
            
            // Search tab
            var searchTab = CreateSearchTab();
            _tabControl.TabPages.Add(searchTab);
            
            Controls.AddRange(new Control[] { searchPanel, _tabControl });
        }
        
        private TabPage CreateCheatSheetTab()
        {
            var tab = new TabPage("Cheat Sheet");
            
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 500
            };
            
            // Left: Shortcut list
            _shortcutList = new ListView
            {
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                GridLines = true,
                View = View.Details,
                BackColor = T3Style.Colors.Background,
                ForeColor = T3Style.Colors.Text,
                Font = T3Style.Fonts.Default
            };
            
            _shortcutList.Columns.Add("Name", 200);
            _shortcutList.Columns.Add("Key", 100);
            _shortcutList.Columns.Add("Category", 100);
            _shortcutList.Columns.Add("Context", 100);
            _shortcutList.Columns.Add("Description", 200);
            
            _shortcutList.SelectedIndexChanged += OnShortcutSelected;
            
            // Right: Details
            _detailGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                BackColor = T3Style.Colors.Background,
                ForeColor = T3Style.Colors.Text,
                Font = T3Style.Fonts.Default,
                ToolbarVisible = false
            };
            
            splitContainer.Panel1.Controls.Add(_shortcutList);
            splitContainer.Panel2.Controls.Add(_detailGrid);
            
            tab.Controls.Add(splitContainer);
            
            return tab;
        }
        
        private TabPage CreateHelpTab()
        {
            var tab = new TabPage("Help & Documentation");
            
            _helpPreview = new WebBrowser
            {
                Dock = DockStyle.Fill
            };
            
            tab.Controls.Add(_helpPreview);
            
            return tab;
        }
        
        private TabPage CreateSearchTab()
        {
            var tab = new TabPage("Advanced Search");
            
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var searchResults = new ListView
            {
                Location = new Point(20, 50),
                Size = new Size(900, 400),
                FullRowSelect = true,
                GridLines = true,
                View = View.Details,
                BackColor = T3Style.Colors.Background,
                ForeColor = T3Style.Colors.Text
            };
            
            searchResults.Columns.Add("Shortcut", 200);
            searchResults.Columns.Add("Key", 100);
            searchResults.Columns.Add("Description", 400);
            searchResults.Columns.Add("Tips", 200);
            
            panel.Controls.Add(searchResults);
            tab.Controls.Add(panel);
            
            return tab;
        }
        
        private void LoadShortcuts()
        {
            // Populate category filter
            var categories = _shortcutManager.GetAllShortcuts()
                .GroupBy(s => s.Category)
                .Select(g => g.Key)
                .OrderBy(c => c)
                .ToList();
            
            _categoryFilter.Items.Clear();
            _categoryFilter.Items.Add("All Categories");
            _categoryFilter.Items.AddRange(categories.ToArray());
            _categoryFilter.SelectedIndex = 0;
            
            // Load shortcuts into list
            RefreshShortcutList();
            
            // Load comprehensive guide in help tab
            var guide = _helpSystem.GenerateComprehensiveGuide();
            _helpPreview.DocumentText = ConvertMarkdownToHtml(guide);
        }
        
        private void RefreshShortcutList()
        {
            _shortcutList.Items.Clear();
            
            var searchText = _searchBox.Text.ToLower();
            var selectedCategory = _categoryFilter.SelectedIndex > 0 ? _categoryFilter.SelectedItem.ToString() : null;
            
            var shortcuts = _shortcutManager.GetAllShortcuts().Where(s =>
            {
                var matchesSearch = string.IsNullOrEmpty(searchText) ||
                    s.Name.ToLower().Contains(searchText) ||
                    s.Description.ToLower().Contains(searchText);
                
                var matchesCategory = selectedCategory == null || s.Category == selectedCategory;
                
                return matchesSearch && matchesCategory;
            }).OrderBy(s => s.Category).ThenBy(s => s.Name);
            
            foreach (var shortcut in shortcuts)
            {
                var item = new ListViewItem(shortcut.Name);
                item.SubItems.Add(shortcut.GetKeyDisplayString());
                item.SubItems.Add(shortcut.Category);
                item.SubItems.Add(shortcut.Context.ToString());
                item.SubItems.Add(shortcut.Description);
                item.Tag = shortcut;
                
                _shortcutList.Items.Add(item);
            }
        }
        
        private string ConvertMarkdownToHtml(string markdown)
        {
            var html = new StringBuilder();
            html.AppendLine("<html><head><style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #333; border-bottom: 2px solid #333; }");
            html.AppendLine("h2 { color: #666; border-bottom: 1px solid #666; }");
            html.AppendLine("h3 { color: #999; }");
            html.AppendLine("code { background-color: #f5f5f5; padding: 2px 4px; }");
            html.AppendLine("</style></head><body>");
            
            var lines = markdown.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("# "))
                    html.AppendLine($"<h1>{line.Substring(2)}</h1>");
                else if (line.StartsWith("## "))
                    html.AppendLine($"<h2>{line.Substring(3)}</h2>");
                else if (line.StartsWith("### "))
                    html.AppendLine($"<h3>{line.Substring(4)}</h3>");
                else if (line.StartsWith("**") && line.EndsWith("**"))
                    html.AppendLine($"<strong>{line.Substring(2, line.Length - 4)}</strong>");
                else if (line.StartsWith("- "))
                    html.AppendLine($"<li>{line.Substring(2)}</li>");
                else if (!string.IsNullOrWhiteSpace(line))
                    html.AppendLine($"<p>{line}</p>");
                else
                    html.AppendLine("<br>");
            }
            
            html.AppendLine("</body></html>");
            return html.ToString();
        }
        
        private void SetupEventHandlers()
        {
            _searchBox.TextChanged += (s, e) => RefreshShortcutList();
            _categoryFilter.SelectedIndexChanged += (s, e) => RefreshShortcutList();
        }
        
        private void OnShortcutSelected(object sender, EventArgs e)
        {
            if (_shortcutList.SelectedItems.Count > 0)
            {
                var shortcut = _shortcutList.SelectedItems[0].Tag as KeyboardShortcut;
                if (shortcut != null)
                {
                    _detailGrid.SelectedObject = shortcut;
                }
            }
        }
    }
}