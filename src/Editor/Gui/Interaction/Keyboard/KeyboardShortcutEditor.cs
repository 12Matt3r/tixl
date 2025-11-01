// Visual Editor for Keyboard Shortcuts
// Provides GUI interface for managing, customizing, and organizing keyboard shortcuts

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TiXL.Core.Logging;
using TiXL.Editor.Gui.Styling;

namespace TiXL.Editor.Gui.Interaction.Keyboard
{
    /// <summary>
    /// Visual editor window for keyboard shortcuts
    /// </summary>
    public partial class KeyboardShortcutEditor : Form
    {
        private readonly LayeredKeyboardShortcutManager _shortcutManager;
        private readonly ShortcutVisualizationPanel _visualizationPanel;
        private readonly ShortcutConflictResolver _conflictResolver;
        
        private TreeView _shortcutTree;
        private PropertyGrid _propertyGrid;
        private ListView _conflictList;
        private TextBox _searchBox;
        private ComboBox _categoryFilter;
        private Button _btnAdd;
        private Button _btnRemove;
        private Button _btnReset;
        private Button _btnExport;
        private Button _btnImport;
        private Button _btnFindConflicts;
        private Panel _mainPanel;
        private TabControl _tabControl;
        
        private KeyboardShortcut _selectedShortcut;
        private List<KeyboardShortcut> _filteredShortcuts;
        
        public KeyboardShortcutEditor(LayeredKeyboardShortcutManager shortcutManager)
        {
            _shortcutManager = shortcutManager;
            _visualizationPanel = new ShortcutVisualizationPanel();
            _conflictResolver = new ShortcutConflictResolver();
            
            InitializeComponent();
            LoadShortcuts();
            SetupEventHandlers();
            
            // Subscribe to events
            _shortcutManager.ShortcutConflictDetected += OnShortcutConflictDetected;
            _shortcutManager.ShortcutChanged += OnShortcutChanged;
            
            // Initial UI update
            ApplyFilter();
        }
        
        private void InitializeComponent()
        {
            SuspendLayout();
            
            // Form settings
            Text = "Keyboard Shortcut Editor";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterParent;
            Icon = SystemIcons.Application;
            
            // Main panel
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = T3Style.Colors.Background
            };
            
            // Top toolbar
            var toolbarPanel = CreateToolbarPanel();
            _mainPanel.Controls.Add(toolbarPanel);
            
            // Tab control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, 50)
            };
            
            // Shortcuts tab
            var shortcutsTab = CreateShortcutsTab();
            _tabControl.TabPages.Add(shortcutsTab);
            
            // Conflicts tab
            var conflictsTab = CreateConflictsTab();
            _tabControl.TabPages.Add(conflictsTab);
            
            // Visualization tab
            var visualizationTab = CreateVisualizationTab();
            _tabControl.TabPages.Add(visualizationTab);
            
            _mainPanel.Controls.Add(_tabControl);
            Controls.Add(_mainPanel);
            
            ResumeLayout(false);
        }
        
        private Panel CreateToolbarPanel()
        {
            var panel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = T3Style.Colors.HeaderBackground
            };
            
            // Search box
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
            _searchBox.TextChanged += OnSearchChanged;
            
            // Category filter
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
                Size = new Size(150, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = T3Style.Colors.InputBackground,
                ForeColor = T3Style.Colors.Text
            };
            _categoryFilter.SelectedIndexChanged += OnCategoryFilterChanged;
            
            // Buttons
            _btnAdd = new Button
            {
                Text = "Add",
                Location = new Point(490, 10),
                Size = new Size(75, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnAdd.Click += OnAddShortcut;
            
            _btnRemove = new Button
            {
                Text = "Remove",
                Location = new Point(570, 10),
                Size = new Size(75, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnRemove.Click += OnRemoveShortcut;
            _btnRemove.Enabled = false;
            
            _btnReset = new Button
            {
                Text = "Reset",
                Location = new Point(650, 10),
                Size = new Size(75, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnReset.Click += OnResetShortcuts;
            
            _btnFindConflicts = new Button
            {
                Text = "Find Conflicts",
                Location = new Point(730, 10),
                Size = new Size(100, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnFindConflicts.Click += OnFindConflicts;
            
            _btnExport = new Button
            {
                Text = "Export",
                Location = new Point(840, 10),
                Size = new Size(75, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnExport.Click += OnExportShortcuts;
            
            _btnImport = new Button
            {
                Text = "Import",
                Location = new Point(920, 10),
                Size = new Size(75, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnImport.Click += OnImportShortcuts;
            
            panel.Controls.AddRange(new Control[]
            {
                searchLabel, _searchBox, categoryLabel, _categoryFilter,
                _btnAdd, _btnRemove, _btnReset, _btnFindConflicts,
                _btnExport, _btnImport
            });
            
            return panel;
        }
        
        private TabPage CreateShortcutsTab()
        {
            var tab = new TabPage("Shortcuts");
            
            // Split container for tree and properties
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 400
            };
            
            // Left side: Tree view
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            
            _shortcutTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = T3Style.Colors.Background,
                ForeColor = T3Style.Colors.Text,
                Font = T3Style.Fonts.Default,
                ShowPlusMinus = true,
                ShowRootLines = true
            };
            _shortcutTree.AfterSelect += OnShortcutTreeSelectionChanged;
            
            leftPanel.Controls.Add(_shortcutTree);
            
            // Right side: Property grid
            var rightPanel = new Panel { Dock = DockStyle.Fill };
            
            _propertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                BackColor = T3Style.Colors.Background,
                ForeColor = T3Style.Colors.Text,
                Font = T3Style.Fonts.Default,
                ToolbarVisible = true
            };
            _propertyGrid.PropertyValueChanged += OnPropertyValueChanged;
            
            rightPanel.Controls.Add(_propertyGrid);
            
            splitContainer.Panel1.Controls.Add(leftPanel);
            splitContainer.Panel2.Controls.Add(rightPanel);
            
            tab.Controls.Add(splitContainer);
            
            return tab;
        }
        
        private TabPage CreateConflictsTab()
        {
            var tab = new TabPage("Conflicts");
            
            var panel = new Panel { Dock = DockStyle.Fill };
            
            var label = new Label
            {
                Text = "Detected Conflicts:",
                Location = new Point(10, 10),
                Size = new Size(150, 20),
                ForeColor = T3Style.Colors.Text
            };
            
            _conflictList = new ListView
            {
                Location = new Point(10, 35),
                Size = new Size(740, 500),
                BackColor = T3Style.Colors.Background,
                ForeColor = T3Style.Colors.Text,
                Font = T3Style.Fonts.Default,
                FullRowSelect = true,
                GridLines = true,
                View = View.Details
            };
            
            _conflictList.Columns.Add("Type", 100);
            _conflictList.Columns.Add("Description", 300);
            _conflictList.Columns.Add("Primary Shortcut", 200);
            _conflictList.Columns.Add("Conflicting Shortcut", 200);
            
            panel.Controls.AddRange(new Control[] { label, _conflictList });
            tab.Controls.Add(panel);
            
            return tab;
        }
        
        private TabPage CreateVisualizationTab()
        {
            var tab = new TabPage("Visualization");
            
            _visualizationPanel.Dock = DockStyle.Fill;
            _visualizationPanel.LoadShortcuts(_shortcutManager.GetAllShortcuts().ToList());
            
            tab.Controls.Add(_visualizationPanel);
            
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
            
            // Build tree structure
            BuildShortcutTree();
        }
        
        private void BuildShortcutTree()
        {
            _shortcutTree.Nodes.Clear();
            
            var shortcuts = _shortcutManager.GetAllShortcuts().ToList();
            _filteredShortcuts = shortcuts;
            
            // Group by category
            var groupedByCategory = shortcuts.GroupBy(s => s.Category).OrderBy(g => g.Key);
            
            foreach (var categoryGroup in groupedByCategory)
            {
                var categoryNode = new TreeNode(categoryGroup.Key)
                {
                    ImageIndex = 0,
                    SelectedImageIndex = 0
                };
                
                // Group by context within category
                var groupedByContext = categoryGroup.GroupBy(s => s.Context.ToString());
                foreach (var contextGroup in groupedByContext.OrderBy(g => g.Key))
                {
                    var contextNode = new TreeNode(contextGroup.Key)
                    {
                        ImageIndex = 1,
                        SelectedImageIndex = 1
                    };
                    
                    foreach (var shortcut in contextGroup.OrderBy(s => s.Name))
                    {
                        var shortcutNode = new TreeNode($"{shortcut.Icon} {shortcut.Name} ({shortcut.GetKeyDisplayString()})")
                        {
                            ImageIndex = 2,
                            SelectedImageIndex = 2,
                            Tag = shortcut
                        };
                        
                        contextNode.Nodes.Add(shortcutNode);
                    }
                    
                    categoryNode.Nodes.Add(contextNode);
                }
                
                _shortcutTree.Nodes.Add(categoryNode);
            }
            
            // Expand root nodes
            foreach (TreeNode node in _shortcutTree.Nodes)
                node.Expand();
        }
        
        private void ApplyFilter()
        {
            var searchText = _searchBox.Text.ToLower();
            var selectedCategory = _categoryFilter.SelectedIndex > 0 ? _categoryFilter.SelectedItem.ToString() : null;
            
            var allShortcuts = _shortcutManager.GetAllShortcuts().ToList();
            
            _filteredShortcuts = allShortcuts.Where(s =>
            {
                // Search filter
                var matchesSearch = string.IsNullOrEmpty(searchText) ||
                    s.Name.ToLower().Contains(searchText) ||
                    s.Description.ToLower().Contains(searchText) ||
                    s.Category.ToLower().Contains(searchText) ||
                    s.Tags.Any(t => t.ToLower().Contains(searchText));
                
                // Category filter
                var matchesCategory = selectedCategory == null || s.Category == selectedCategory;
                
                return matchesSearch && matchesCategory;
            }).ToList();
            
            BuildShortcutTree();
        }
        
        private void SetupEventHandlers()
        {
            // Keyboard shortcut recording
            _propertyGrid.MouseDown += OnPropertyGridMouseDown;
        }
        
        #region Event Handlers
        
        private void OnSearchChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }
        
        private void OnCategoryFilterChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }
        
        private void OnShortcutTreeSelectionChanged(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is KeyboardShortcut shortcut)
            {
                _selectedShortcut = shortcut;
                _propertyGrid.SelectedObject = shortcut;
                _btnRemove.Enabled = !shortcut.IsSystem;
            }
            else
            {
                _selectedShortcut = null;
                _btnRemove.Enabled = false;
            }
        }
        
        private void OnPropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (_selectedShortcut != null)
            {
                // Validate changes and update tree
                if (e.ChangedItem.PropertyDescriptor.Name == "PrimaryKey")
                {
                    // Rebuild tree to show updated key display
                    BuildShortcutTree();
                }
                
                _shortcutManager.ShortcutChanged?.Invoke(this, 
                    new ShortcutChangedEventArgs { Action = ChangeAction.Modified, Shortcut = _selectedShortcut });
            }
        }
        
        private void OnPropertyGridMouseDown(object sender, MouseEventArgs e)
        {
            // Handle key recording for PrimaryKey and AlternativeKey properties
            if (e.Button == MouseButtons.Left && _selectedShortcut != null)
            {
                var hit = _propertyGrid.HitTest(e.Location);
                if (hit != null && hit.PropertyItem?.PropertyDescriptor.Name.Contains("Key") == true)
                {
                    StartKeyRecording(hit.PropertyItem.PropertyDescriptor.Name);
                }
            }
        }
        
        private void StartKeyRecording(string propertyName)
        {
            var keyInputForm = new KeyInputForm();
            if (keyInputForm.ShowDialog() == DialogResult.OK)
            {
                var recordedKey = keyInputForm.RecordedKey;
                if (recordedKey != Keys.None)
                {
                    if (propertyName == "PrimaryKey")
                        _selectedShortcut.PrimaryKey = recordedKey;
                    else if (propertyName == "AlternativeKey")
                        _selectedShortcut.AlternativeKey = recordedKey;
                    
                    _propertyGrid.Refresh();
                    BuildShortcutTree();
                }
            }
        }
        
        private void OnAddShortcut(object sender, EventArgs e)
        {
            var newShortcut = new KeyboardShortcut
            {
                Name = "New Shortcut",
                Category = "User",
                PrimaryKey = Keys.None,
                Context = ShortcutContext.Global,
                LayerPriority = 20,
                Description = "Custom user shortcut"
            };
            
            _shortcutManager.RegisterShortcut(newShortcut);
            _selectedShortcut = newShortcut;
            _propertyGrid.SelectedObject = newShortcut;
            
            // Select the new shortcut in tree
            foreach (TreeNode categoryNode in _shortcutTree.Nodes)
            {
                foreach (TreeNode contextNode in categoryNode.Nodes)
                {
                    foreach (TreeNode shortcutNode in contextNode.Nodes)
                    {
                        if (shortcutNode.Tag == newShortcut)
                        {
                            _shortcutTree.SelectedNode = shortcutNode;
                            break;
                        }
                    }
                }
            }
        }
        
        private void OnRemoveShortcut(object sender, EventArgs e)
        {
            if (_selectedShortcut != null && !_selectedShortcut.IsSystem)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the shortcut '{_selectedShortcut.Name}'?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    _shortcutManager.UnregisterShortcut(_selectedShortcut.Id);
                    _selectedShortcut = null;
                    _propertyGrid.SelectedObject = null;
                    BuildShortcutTree();
                }
            }
        }
        
        private void OnResetShortcuts(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset all shortcuts to their default values. Continue?",
                "Reset Shortcuts",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                _shortcutManager.ResetToDefaults();
                LoadShortcuts();
            }
        }
        
        private void OnFindConflicts(object sender, EventArgs e)
        {
            // Switch to conflicts tab
            _tabControl.SelectedIndex = 1;
            
            // Trigger conflict detection
            var allShortcuts = _shortcutManager.GetAllShortcuts().ToList();
            _conflictList.Items.Clear();
            
            for (int i = 0; i < allShortcuts.Count; i++)
            {
                for (int j = i + 1; j < allShortcuts.Count; j++)
                {
                    var conflicts = _conflictResolver.DetectConflicts(allShortcuts[i], allShortcuts.Skip(j).ToList());
                    foreach (var conflict in conflicts)
                    {
                        AddConflictToList(conflict);
                    }
                }
            }
        }
        
        private void AddConflictToList(ShortcutConflict conflict)
        {
            var item = new ListViewItem(conflict.Type.ToString());
            item.SubItems.Add(conflict.Description);
            item.SubItems.Add($"{conflict.PrimaryShortcut.Name} ({conflict.PrimaryShortcut.GetKeyDisplayString()})");
            item.SubItems.Add($"{conflict.ConflictingShortcut.Name} ({conflict.ConflictingShortcut.GetKeyDisplayString()})");
            item.Tag = conflict;
            
            // Color code based on severity
            if (conflict.Type == ConflictType.KeyBinding)
                item.BackColor = Color.LightYellow;
            else if (conflict.Type == ConflictType.AlternativeKey)
                item.BackColor = Color.LightSalmon;
            
            _conflictList.Items.Add(item);
        }
        
        private void OnExportShortcuts(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                saveDialog.DefaultExt = "json";
                saveDialog.FileName = "keyboard-shortcuts.json";
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = _shortcutManager.ExportToJson();
                        System.IO.File.WriteAllText(saveDialog.FileName, json);
                        MessageBox.Show("Shortcuts exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export shortcuts: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void OnImportShortcuts(object sender, EventArgs e)
        {
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(openDialog.FileName);
                        
                        var mergeResult = MessageBox.Show(
                            "Merge with existing shortcuts? Click Yes to merge, No to replace.",
                            "Import Options",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);
                        
                        if (mergeResult == DialogResult.Cancel)
                            return;
                        
                        var success = _shortcutManager.ImportFromJson(json, mergeResult == DialogResult.Yes);
                        
                        if (success)
                        {
                            LoadShortcuts();
                            MessageBox.Show("Shortcuts imported successfully!", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to import shortcuts. Please check the file format.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to import shortcuts: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void OnShortcutConflictDetected(object sender, ShortcutConflictEventArgs e)
        {
            if (e.Conflicts.Any())
            {
                var message = "Keyboard shortcut conflicts detected:\n\n";
                foreach (var conflict in e.Conflicts)
                {
                    message += $"• {conflict.Description}\n";
                    message += $"  {conflict.PrimaryShortcut.Name} vs {conflict.ConflictingShortcut.Name}\n\n";
                }
                message += "Please resolve these conflicts in the Shortcut Editor.";
                
                MessageBox.Show(message, "Shortcut Conflicts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        
        private void OnShortcutChanged(object sender, ShortcutChangedEventArgs e)
        {
            BuildShortcutTree();
        }
        
        #endregion
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            // Set initial selection
            if (_shortcutTree.Nodes.Count > 0 && _shortcutTree.Nodes[0].Nodes.Count > 0)
            {
                _shortcutTree.SelectedNode = _shortcutTree.Nodes[0].Nodes[0].Nodes[0];
            }
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _shortcutManager.ShortcutConflictDetected -= OnShortcutConflictDetected;
            _shortcutManager.ShortcutChanged -= OnShortcutChanged;
            
            base.OnFormClosed(e);
        }
    }
    
    /// <summary>
    /// Form for recording keyboard input
    /// </summary>
    public partial class KeyInputForm : Form
    {
        private Label _instructionLabel;
        private Label _displayLabel;
        private Button _btnClear;
        private Button _btnOK;
        private Button _btnCancel;
        private Keys _recordedKey = Keys.None;
        
        public Keys RecordedKey => _recordedKey;
        
        public KeyInputForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            Size = new Size(300, 150);
            Text = "Press Keys";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            
            _instructionLabel = new Label
            {
                Text = "Press the key combination you want to assign:",
                Location = new Point(20, 20),
                Size = new Size(260, 20),
                ForeColor = T3Style.Colors.Text
            };
            
            _displayLabel = new Label
            {
                Text = "Press any key...",
                Location = new Point(20, 50),
                Size = new Size(260, 30),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(T3Style.Fonts.Default, FontStyle.Bold),
                BackColor = T3Style.Colors.InputBackground,
                ForeColor = T3Style.Colors.Text
            };
            
            _btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(20, 90),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Retry
            };
            _btnClear.Click += OnClear;
            
            _btnOK = new Button
            {
                Text = "OK",
                Location = new Point(100, 90),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            
            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(180, 90),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel
            };
            
            Controls.AddRange(new Control[]
            {
                _instructionLabel, _displayLabel, _btnClear, _btnOK, _btnCancel
            });
            
            AcceptButton = _btnOK;
            CancelButton = _btnCancel;
        }
        
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Handle special keys
            if (keyData == Keys.Escape)
            {
                _recordedKey = Keys.None;
                _displayLabel.Text = "None (cleared)";
                _btnOK.Enabled = true;
                return true;
            }
            
            _recordedKey = keyData;
            _displayLabel.Text = FormatKey(keyData);
            _btnOK.Enabled = true;
            
            return true; // Don't process the key further
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
        
        private void OnClear(object sender, EventArgs e)
        {
            _recordedKey = Keys.None;
            _displayLabel.Text = "Press any key...";
            _btnOK.Enabled = false;
        }
    }
}