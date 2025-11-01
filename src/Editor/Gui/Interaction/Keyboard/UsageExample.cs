// Usage Example for TiXL Layered Keyboard Shortcut System
// Demonstrates how to integrate and use the keyboard shortcut system in TiXL

using System;
using System.Windows.Forms;
using TiXL.Core.Logging;
using TiXL.Editor.Gui.Interaction.Keyboard;

namespace TiXL.Examples.KeyboardShortcuts
{
    /// <summary>
    /// Example implementation showing how to integrate the keyboard shortcut system
    /// </summary>
    public partial class ShortcutSystemExample : Form
    {
        private ShortcutSystemManager _shortcutSystem;
        private bool _isPlaying = false;
        
        public ShortcutSystemExample()
        {
            InitializeComponent();
            InitializeShortcutSystem();
            SetupExampleActions();
        }
        
        private void InitializeComponent()
        {
            // Main form
            Text = "TiXL Keyboard Shortcut System Example";
            Size = new System.Drawing.Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            
            // Create a simple UI to demonstrate shortcuts
            CreateUI();
        }
        
        private void CreateUI()
        {
            // Status bar
            var statusBar = new StatusBar();
            var statusPanel = new StatusBarPanel();
            statusPanel.Text = "Ready";
            statusBar.Panels.Add(statusPanel);
            Controls.Add(statusBar);
            
            // Main menu
            var mainMenu = new MainMenu();
            var fileMenu = mainMenu.MenuItems.Add("&File");
            fileMenu.MenuItems.Add(new MenuItem("&New Project", OnNewProject));
            fileMenu.MenuItems.Add(new MenuItem("&Open Project", OnOpenProject));
            fileMenu.MenuItems.Add(new MenuItem("&Save Project", OnSaveProject));
            fileMenu.MenuItems.Add("-");
            fileMenu.MenuItems.Add(new MenuItem("E&xit", OnExit));
            
            var editMenu = mainMenu.MenuItems.Add("&Edit");
            editMenu.MenuItems.Add(new MenuItem("&Undo", OnUndo));
            editMenu.MenuItems.Add(new MenuItem("&Redo", OnRedo));
            editMenu.MenuItems.Add("-");
            editMenu.MenuItems.Add(new MenuItem("&Copy", OnCopy));
            editMenu.MenuItems.Add(new MenuItem("&Paste", OnPaste));
            
            var toolsMenu = mainMenu.MenuItems.Add("&Tools");
            toolsMenu.MenuItems.Add(new MenuItem("&Keyboard Shortcuts...", OnShowShortcuts));
            toolsMenu.MenuItems.Add(new MenuItem("&Conflict Resolver...", OnShowConflicts));
            toolsMenu.MenuItems.Add(new MenuItem("&Keyboard Visualization", OnShowVisualization));
            
            Menu = mainMenu;
            
            // Context selection
            var contextPanel = new Panel
            {
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(300, 100),
                BackColor = System.Drawing.Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var contextLabel = new Label
            {
                Text = "Current Context:",
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true
            };
            
            var globalRadio = new RadioButton
            {
                Text = "Global",
                Location = new System.Drawing.Point(10, 35),
                Checked = true,
                Tag = ShortcutContext.Global
            };
            globalRadio.CheckedChanged += OnContextChanged;
            
            var graphRadio = new RadioButton
            {
                Text = "Graph Editor",
                Location = new System.Drawing.Point(80, 35),
                Tag = ShortcutContext.GraphEditor
            };
            graphRadio.CheckedChanged += OnContextChanged;
            
            var timelineRadio = new RadioButton
            {
                Text = "Timeline",
                Location = new System.Drawing.Point(10, 60),
                Tag = ShortcutContext.Timeline
            };
            timelineRadio.CheckedChanged += OnContextChanged;
            
            contextPanel.Controls.AddRange(new Control[]
            {
                contextLabel, globalRadio, graphRadio, timelineRadio
            });
            Controls.Add(contextPanel);
            
            // Shortcut display
            var displayPanel = new Panel
            {
                Location = new System.Drawing.Point(20, 140),
                Size = new System.Drawing.Size(740, 350),
                BackColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var displayLabel = new Label
            {
                Text = "Last Executed Shortcut:",
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true,
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold)
            };
            
            _lastShortcutLabel = new Label
            {
                Text = "None",
                Location = new System.Drawing.Point(10, 40),
                Size = new System.Drawing.Size(700, 50),
                Font = new System.Drawing.Font("Arial", 14),
                ForeColor = System.Drawing.Color.Blue
            };
            
            var instructionsLabel = new Label
            {
                Text = "Try these shortcuts:\n" +
                       "• Ctrl+N (New Project)\n" +
                       "• Ctrl+O (Open Project)\n" +
                       "• Ctrl+S (Save Project)\n" +
                       "• Ctrl+Z (Undo)\n" +
                       "• F (Fit to View)\n" +
                       "• Space (Play/Pause - in Timeline context)\n" +
                       "• F1 (Show Help)",
                Location = new System.Drawing.Point(10, 100),
                AutoSize = true
            };
            
            displayPanel.Controls.AddRange(new Control[]
            {
                displayLabel, _lastShortcutLabel, instructionsLabel
            });
            Controls.Add(displayPanel);
        }
        
        private Label _lastShortcutLabel;
        
        private void InitializeShortcutSystem()
        {
            // Initialize the shortcut system
            _shortcutSystem = new ShortcutSystemManager();
            
            // Subscribe to events
            _shortcutSystem.ShortcutExecuted += OnShortcutExecuted;
            _shortcutSystem.ShortcutConflictDetected += OnShortcutConflictDetected;
            
            // Register example actions
            RegisterExampleActions();
        }
        
        private void SetupExampleActions()
        {
            // Set initial context
            _shortcutSystem.SetCurrentContext(ShortcutContext.Global);
        }
        
        private void RegisterExampleActions()
        {
            // Example: Add custom shortcuts
            var playAction = new KeyboardShortcut
            {
                Id = "example.play",
                Name = "Toggle Playback",
                Description = "Toggle timeline playback state",
                Category = "Example",
                PrimaryKey = Keys.Space,
                Context = ShortcutContext.Timeline,
                LayerPriority = 60,
                Action = TogglePlayback
            };
            
            _shortcutSystem.GetShortcutManager().RegisterShortcut(playAction);
        }
        
        private void TogglePlayback(ShortcutExecutionContext context)
        {
            _isPlaying = !_isPlaying;
            UpdateStatus($"Playback: {(_isPlaying ? "Playing" : "Paused")}");
            
            if (_isPlaying)
            {
                _lastShortcutLabel.Text = "▶️ Playback Started (Space)";
            }
            else
            {
                _lastShortcutLabel.Text = "⏸️ Playback Paused (Space)";
            }
        }
        
        private void OnContextChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton radio && radio.Checked)
            {
                _shortcutSystem.SetCurrentContext((ShortcutContext)radio.Tag);
                UpdateStatus($"Context changed to: {radio.Text}");
            }
        }
        
        private void OnShortcutExecuted(object sender, ShortcutExecutedEventArgs e)
        {
            var shortcut = e.Shortcut;
            _lastShortcutLabel.Text = $"{shortcut.Icon} {shortcut.Name} ({shortcut.GetKeyDisplayString()})";
            UpdateStatus($"Executed: {shortcut.Name}");
        }
        
        private void OnShortcutConflictDetected(object sender, ShortcutConflictEventArgs e)
        {
            var conflictCount = e.Conflicts.Count;
            UpdateStatus($"Shortcut conflicts detected: {conflictCount}");
            
            if (conflictCount > 0)
            {
                MessageBox.Show($"Detected {conflictCount} shortcut conflicts.\n" +
                               "Use Tools > Conflict Resolver to resolve them.",
                               "Shortcut Conflicts",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Warning);
            }
        }
        
        #region Menu Event Handlers
        
        private void OnNewProject(object sender, EventArgs e)
        {
            _lastShortcutLabel.Text = "📄 New Project Created (Ctrl+N)";
            UpdateStatus("New project created");
        }
        
        private void OnOpenProject(object sender, EventArgs e)
        {
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "TiXL Projects (*.tixl)|*.tixl|All Files (*.*)|*.*";
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    _lastShortcutLabel.Text = $"📂 Project Opened: {System.IO.Path.GetFileName(openDialog.FileName)} (Ctrl+O)";
                    UpdateStatus($"Project opened: {openDialog.FileName}");
                }
            }
        }
        
        private void OnSaveProject(object sender, EventArgs e)
        {
            _lastShortcutLabel.Text = "💾 Project Saved (Ctrl+S)";
            UpdateStatus("Project saved successfully");
        }
        
        private void OnUndo(object sender, EventArgs e)
        {
            _lastShortcutLabel.Text = "↶ Action Undone (Ctrl+Z)";
            UpdateStatus("Last action undone");
        }
        
        private void OnRedo(object sender, EventArgs e)
        {
            _lastShortcutLabel.Text = "↷ Action Redone (Ctrl+Y)";
            UpdateStatus("Action redone");
        }
        
        private void OnCopy(object sender, EventArgs e)
        {
            _lastShortcutLabel.Text = "📋 Content Copied (Ctrl+C)";
            UpdateStatus("Content copied to clipboard");
        }
        
        private void OnPaste(object sender, EventArgs e)
        {
            _lastShortcutLabel.Text = "📋 Content Pasted (Ctrl+V)";
            UpdateStatus("Content pasted from clipboard");
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
        
        private void OnExit(object sender, EventArgs e)
        {
            Close();
        }
        
        #endregion
        
        #region Keyboard Event Handling
        
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Process keyboard shortcuts
            if (_shortcutSystem.ProcessKeyEvent(keyData, true))
            {
                return true; // Handled by shortcut system
            }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }
        
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            // Alternative method for keyboard event handling
            if (_shortcutSystem.ProcessKeyEvent(e.KeyData, true))
            {
                e.Handled = true;
            }
        }
        
        #endregion
        
        private void UpdateStatus(string message)
        {
            var statusBar = Controls.OfType<StatusBar>().FirstOrDefault();
            if (statusBar?.Panels.Count > 0)
            {
                statusBar.Panels[0].Text = message;
            }
            
            // Also log the action
            var logger = LogManager.GetLogger("ExampleApp");
            logger.Info(message);
        }
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            // Show welcome message
            MessageBox.Show(
                "Welcome to the TiXL Keyboard Shortcut System Example!\n\n" +
                "This demo shows how to integrate and use keyboard shortcuts in TiXL.\n\n" +
                "Features demonstrated:\n" +
                "• Context-aware shortcuts\n" +
                "• Visual editor integration\n" +
                "• Conflict detection\n" +
                "• Accessibility support\n" +
                "• Documentation system\n\n" +
                "Try the menu options and keyboard shortcuts to explore the system.",
                "TiXL Keyboard Shortcuts",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Clean up resources
            _shortcutSystem?.Dispose();
            
            base.OnFormClosed(e);
        }
    }
    
    /// <summary>
    /// Main entry point for the example application
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                // Initialize logging
                LogManager.Initialize();
                
                // Run the example application
                Application.Run(new ShortcutSystemExample());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application error: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}