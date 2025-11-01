// Shortcut Visualization Panel and Supporting Components
// Provides visual representation and analysis of keyboard shortcuts

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Text;

namespace TiXL.Editor.Gui.Interaction.Keyboard
{
    /// <summary>
    /// Panel for visualizing keyboard shortcuts and their relationships
    /// </summary>
    public partial class ShortcutVisualizationPanel : UserControl
    {
        private List<KeyboardShortcut> _shortcuts;
        private VisualizationMode _mode;
        private PictureBox _canvas;
        private Panel _controls;
        private ComboBox _visualizationMode;
        private Button _btnExport;
        private ComboBox _filterCategory;
        private TrackBar _filterComplexity;
        private Label _complexityLabel;
        
        public ShortcutVisualizationPanel()
        {
            InitializeComponent();
            _mode = VisualizationMode.KeyboardMap;
            LoadSampleData();
        }
        
        public void LoadShortcuts(List<KeyboardShortcut> shortcuts)
        {
            _shortcuts = shortcuts;
            PopulateFilters();
            RedrawCanvas();
        }
        
        private void InitializeComponent()
        {
            SuspendLayout();
            
            // Canvas for visualization
            _canvas = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = T3Style.Colors.Background,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            _canvas.Paint += OnCanvasPaint;
            
            // Controls panel
            _controls = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = T3Style.Colors.HeaderBackground
            };
            
            // Visualization mode
            var modeLabel = new Label
            {
                Text = "Visualization:",
                Location = new Point(10, 10),
                Size = new Size(80, 20),
                ForeColor = T3Style.Colors.HeaderText
            };
            
            _visualizationMode = new ComboBox
            {
                Location = new Point(90, 7),
                Size = new Size(150, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = T3Style.Colors.InputBackground,
                ForeColor = T3Style.Colors.Text
            };
            _visualizationMode.Items.AddRange(Enum.GetNames(typeof(VisualizationMode)));
            _visualizationMode.SelectedIndex = 0;
            _visualizationMode.SelectedIndexChanged += OnVisualizationModeChanged;
            
            // Category filter
            var categoryLabel = new Label
            {
                Text = "Category:",
                Location = new Point(250, 10),
                Size = new Size(60, 20),
                ForeColor = T3Style.Colors.HeaderText
            };
            
            _filterCategory = new ComboBox
            {
                Location = new Point(310, 7),
                Size = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = T3Style.Colors.InputBackground,
                ForeColor = T3Style.Colors.Text
            };
            _filterCategory.SelectedIndexChanged += OnFilterChanged;
            
            // Complexity filter
            var complexityLabel = new Label
            {
                Text = "Complexity:",
                Location = new Point(440, 10),
                Size = new Size(70, 20),
                ForeColor = T3Style.Colors.HeaderText
            };
            
            _complexityLabel = new Label
            {
                Text = "All",
                Location = new Point(510, 10),
                Size = new Size(40, 20),
                ForeColor = T3Style.Colors.HeaderText
            };
            
            _filterComplexity = new TrackBar
            {
                Location = new Point(440, 30),
                Size = new Size(110, 20),
                Minimum = 0,
                Maximum = 3,
                Value = 0,
                TickStyle = TickStyle.Both,
                BackColor = T3Style.Colors.HeaderBackground
            };
            _filterComplexity.ValueChanged += OnComplexityChanged;
            
            // Export button
            _btnExport = new Button
            {
                Text = "Export Image",
                Location = new Point(570, 20),
                Size = new Size(100, 30),
                BackColor = T3Style.Colors.ButtonBackground,
                ForeColor = T3Style.Colors.ButtonText
            };
            _btnExport.Click += OnExportImage;
            
            _controls.Controls.AddRange(new Control[]
            {
                modeLabel, _visualizationMode, categoryLabel, _filterCategory,
                complexityLabel, _complexityLabel, _complexityLabel, _btnExport
            });
            
            Controls.AddRange(new Control[] { _controls, _canvas });
            
            ResumeLayout(false);
        }
        
        private void PopulateFilters()
        {
            if (_shortcuts == null) return;
            
            var categories = _shortcuts.GroupBy(s => s.Category)
                .Select(g => g.Key)
                .OrderBy(c => c)
                .ToArray();
            
            _filterCategory.Items.Clear();
            _filterCategory.Items.Add("All Categories");
            _filterCategory.Items.AddRange(categories);
            _filterCategory.SelectedIndex = 0;
        }
        
        private void LoadSampleData()
        {
            _shortcuts = GenerateSampleShortcuts();
        }
        
        private List<KeyboardShortcut> GenerateSampleShortcuts()
        {
            return new List<KeyboardShortcut>
            {
                new KeyboardShortcut { Name = "New Project", PrimaryKey = Keys.Control | Keys.N, Category = "File", Icon = "📄" },
                new KeyboardShortcut { Name = "Open Project", PrimaryKey = Keys.Control | Keys.O, Category = "File", Icon = "📂" },
                new KeyboardShortcut { Name = "Save Project", PrimaryKey = Keys.Control | Keys.S, Category = "File", Icon = "💾" },
                new KeyboardShortcut { Name = "Undo", PrimaryKey = Keys.Control | Keys.Z, Category = "Edit", Icon = "↶" },
                new KeyboardShortcut { Name = "Redo", PrimaryKey = Keys.Control | Keys.Y, Category = "Edit", Icon = "↷" },
                new KeyboardShortcut { Name = "Copy", PrimaryKey = Keys.Control | Keys.C, Category = "Edit", Icon = "📋" },
                new KeyboardShortcut { Name = "Paste", PrimaryKey = Keys.Control | Keys.V, Category = "Edit", Icon = "📋" },
                new KeyboardShortcut { Name = "Zoom In", PrimaryKey = Keys.Control | Keys.Oemplus, Category = "View", Icon = "🔍+" },
                new KeyboardShortcut { Name = "Zoom Out", PrimaryKey = Keys.Control | Keys.OemMinus, Category = "View", Icon = "🔍-" },
                new KeyboardShortcut { Name = "Play", PrimaryKey = Keys.Space, Category = "Timeline", Icon = "▶️" },
                new KeyboardShortcut { Name = "Stop", PrimaryKey = Keys.Escape, Category = "Timeline", Icon = "⏹️" }
            };
        }
        
        private void RedrawCanvas()
        {
            _canvas.Invalidate();
        }
        
        private void OnCanvasPaint(object sender, PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            
            // Clear canvas
            graphics.Clear(T3Style.Colors.Background);
            
            // Draw based on visualization mode
            switch (_mode)
            {
                case VisualizationMode.KeyboardMap:
                    DrawKeyboardMap(graphics);
                    break;
                case VisualizationMode.CategoryChart:
                    DrawCategoryChart(graphics);
                    break;
                case VisualizationMode.UsageGraph:
                    DrawUsageGraph(graphics);
                    break;
                case VisualizationMode.ComplexityMap:
                    DrawComplexityMap(graphics);
                    break;
                case VisualizationMode.NetworkView:
                    DrawNetworkView(graphics);
                    break;
            }
        }
        
        private void DrawKeyboardMap(Graphics g)
        {
            var shortcuts = GetFilteredShortcuts();
            if (!shortcuts.Any()) return;
            
            // Draw keyboard layout
            DrawKeyboardLayout(g);
            
            // Draw shortcuts mapped to keys
            foreach (var shortcut in shortcuts)
            {
                var keyPos = GetKeyPosition(shortcut.PrimaryKey);
                if (keyPos != Point.Empty)
                {
                    DrawShortcutOnKey(g, keyPos, shortcut);
                }
            }
        }
        
        private void DrawKeyboardLayout(Graphics g)
        {
            var keyboardRect = new Rectangle(50, 50, 700, 400);
            g.FillRectangle(new SolidBrush(T3Style.Colors.InputBackground), keyboardRect);
            g.DrawRectangle(new Pen(T3Style.Colors.Border), keyboardRect);
            
            // Function keys row
            var functionKeys = new[] { Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12 };
            var keyWidth = (keyboardRect.Width - 20) / functionKeys.Length;
            for (int i = 0; i < functionKeys.Length; i++)
            {
                var keyRect = new Rectangle(
                    keyboardRect.X + 10 + i * keyWidth,
                    keyboardRect.Y + 10,
                    keyWidth - 5,
                    30);
                
                DrawKey(g, keyRect, functionKeys[i].ToString().Replace("F", "F"));
            }
            
            // Number row
            var numberKeys = new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
            keyWidth = (keyboardRect.Width - 20) / numberKeys.Length;
            for (int i = 0; i < numberKeys.Length; i++)
            {
                var keyRect = new Rectangle(
                    keyboardRect.X + 10 + i * keyWidth,
                    keyboardRect.Y + 50,
                    keyWidth - 5,
                    30);
                
                DrawKey(g, keyRect, numberKeys[i]);
            }
            
            // Letter rows
            var letterRows = new[]
            {
                new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
                new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" },
                new[] { "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/" }
            };
            
            int rowY = keyboardRect.Y + 90;
            foreach (var row in letterRows)
            {
                keyWidth = (keyboardRect.Width - 20) / row.Length;
                for (int i = 0; i < row.Length; i++)
                {
                    var keyRect = new Rectangle(
                        keyboardRect.X + 10 + i * keyWidth,
                        rowY,
                        keyWidth - 5,
                        30);
                    
                    DrawKey(g, keyRect, row[i]);
                }
                rowY += 40;
            }
            
            // Special keys
            DrawSpecialKey(g, new Rectangle(keyboardRect.X + 10, rowY, 60, 40), "Ctrl");
            DrawSpecialKey(g, new Rectangle(keyboardRect.X + 80, rowY, 60, 40), "Alt");
            DrawSpecialKey(g, new Rectangle(keyboardRect.X + 150, rowY, 60, 40), "Shift");
            
            DrawSpecialKey(g, new Rectangle(keyboardRect.Right - 150, rowY, 120, 40), "Space");
        }
        
        private void DrawKey(Graphics g, Rectangle rect, string text)
        {
            g.FillRectangle(new SolidBrush(T3Style.Colors.KeyBackground), rect);
            g.DrawRectangle(new Pen(T3Style.Colors.KeyBorder), rect);
            
            var textFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            
            g.DrawString(text, T3Style.Fonts.Key, new SolidBrush(T3Style.Colors.KeyText), rect, textFormat);
        }
        
        private void DrawSpecialKey(Graphics g, Rectangle rect, string text)
        {
            DrawKey(g, rect, text);
            
            // Add modifier indicator
            g.FillRectangle(new SolidBrush(T3Style.Colors.ModifierBackground), 
                rect.X, rect.Y, 15, rect.Height);
        }
        
        private void DrawShortcutOnKey(Graphics g, Point keyPos, KeyboardShortcut shortcut)
        {
            var text = shortcut.Icon + "\n" + shortcut.Name;
            var textFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            
            // Draw background color based on category
            var categoryColor = GetCategoryColor(shortcut.Category);
            g.FillRectangle(new SolidBrush(Color.FromArgb(128, categoryColor)), 
                keyPos.X + 2, keyPos.Y + 2, 46, 26);
            
            // Draw text
            g.DrawString(text, T3Style.Fonts.Shortcut, 
                new SolidBrush(T3Style.Colors.Text), 
                new Rectangle(keyPos.X, keyPos.Y, 50, 30), 
                textFormat);
        }
        
        private Point GetKeyPosition(Keys key)
        {
            // Simplified key position mapping
            // In a real implementation, this would be more comprehensive
            return key switch
            {
                Keys.Space => new Point(400, 280),
                Keys.Control => new Point(100, 280),
                Keys.Alt => new Point(160, 280),
                Keys.Shift => new Point(220, 280),
                _ => Point.Empty
            };
        }
        
        private Color GetCategoryColor(string category)
        {
            return category.ToLower() switch
            {
                "file" => Color.FromArgb(70, 130, 180),      // SteelBlue
                "edit" => Color.FromArgb(34, 139, 34),       // ForestGreen
                "view" => Color.FromArgb(255, 140, 0),       // DarkOrange
                "timeline" => Color.FromArgb(255, 20, 147),   // DeepPink
                "tools" => Color.FromArgb(75, 0, 130),       // Indigo
                _ => Color.FromArgb(128, 128, 128)           // Gray
            };
        }
        
        private void DrawCategoryChart(Graphics g)
        {
            var shortcuts = GetFilteredShortcuts();
            if (!shortcuts.Any()) return;
            
            var chartRect = new Rectangle(50, 50, 400, 300);
            var center = new Point(chartRect.X + chartRect.Width / 2, chartRect.Y + chartRect.Height / 2);
            var radius = Math.Min(chartRect.Width, chartRect.Height) / 2 - 20;
            
            // Group by category
            var categories = shortcuts.GroupBy(s => s.Category);
            var total = shortcuts.Count;
            
            float startAngle = 0;
            var colors = new[] { Color.Red, Color.Blue, Color.Green, Color.Orange, Color.Purple, Color.Yellow };
            
            int colorIndex = 0;
            foreach (var category in categories)
            {
                var sweepAngle = (float)category.Count() / total * 360f;
                
                var pieRect = new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);
                g.FillPie(new SolidBrush(colors[colorIndex % colors.Length]), pieRect, startAngle, sweepAngle);
                g.DrawPie(new Pen(T3Style.Colors.Border), pieRect, startAngle, sweepAngle);
                
                // Draw label
                var labelAngle = startAngle + sweepAngle / 2;
                var labelRadius = radius + 30;
                var labelX = center.X + (float)(Math.Cos(labelAngle * Math.PI / 180) * labelRadius);
                var labelY = center.Y + (float)(Math.Sin(labelAngle * Math.PI / 180) * labelRadius);
                
                var label = $"{category.Key}\n({category.Count()})";
                g.DrawString(label, T3Style.Fonts.Default, 
                    new SolidBrush(T3Style.Colors.Text), 
                    labelX - 30, labelY - 20);
                
                startAngle += sweepAngle;
                colorIndex++;
            }
        }
        
        private void DrawUsageGraph(Graphics g)
        {
            var shortcuts = GetFilteredShortcuts();
            if (!shortcuts.Any()) return;
            
            var graphRect = new Rectangle(50, 50, 700, 400);
            g.FillRectangle(new SolidBrush(T3Style.Colors.PanelBackground), graphRect);
            g.DrawRectangle(new Pen(T3Style.Colors.Border), graphRect);
            
            var barHeight = 30;
            var spacing = 10;
            var maxWidth = graphRect.Width - 40;
            
            // Sort by complexity (key combination length)
            var sortedShortcuts = shortcuts
                .OrderByDescending(s => GetKeyComplexity(s.PrimaryKey))
                .ToList();
            
            for (int i = 0; i < Math.Min(sortedShortcuts.Count, 10); i++)
            {
                var shortcut = sortedShortcuts[i];
                var y = graphRect.Y + 20 + i * (barHeight + spacing);
                var barWidth = maxWidth * (float)(100 - GetKeyComplexity(shortcut.PrimaryKey) * 10) / 100;
                
                // Draw bar
                var barRect = new Rectangle(graphRect.X + 20, y, (int)barWidth, barHeight);
                g.FillRectangle(new SolidBrush(GetCategoryColor(shortcut.Category)), barRect);
                g.DrawRectangle(new Pen(T3Style.Colors.Border), barRect);
                
                // Draw label
                g.DrawString($"{shortcut.Name} ({shortcut.GetKeyDisplayString()})", 
                    T3Style.Fonts.Default, 
                    new SolidBrush(T3Style.Colors.Text), 
                    graphRect.X + 20 + barWidth + 10, y + 5);
            }
        }
        
        private void DrawComplexityMap(Graphics g)
        {
            var shortcuts = GetFilteredShortcuts();
            if (!shortcuts.Any()) return;
            
            var mapRect = new Rectangle(50, 50, 700, 400);
            g.FillRectangle(new SolidBrush(T3Style.Colors.PanelBackground), mapRect);
            g.DrawRectangle(new Pen(T3Style.Colors.Border), mapRect);
            
            var complexityLevels = shortcuts.GroupBy(s => GetKeyComplexity(s.PrimaryKey));
            
            var groupWidth = mapRect.Width / Math.Max(complexityLevels.Count(), 1);
            int groupIndex = 0;
            
            foreach (var level in complexityLevels.OrderBy(g => g.Key))
            {
                var groupRect = new Rectangle(
                    mapRect.X + groupIndex * groupWidth + 10,
                    mapRect.Y + 20,
                    groupWidth - 20,
                    mapRect.Height - 40);
                
                // Draw group background
                var groupColor = GetComplexityColor(level.Key);
                g.FillRectangle(new SolidBrush(Color.FromArgb(50, groupColor)), groupRect);
                
                // Draw shortcuts in this complexity level
                var shortcutsPerColumn = (int)Math.Sqrt(level.Count());
                var shortcutHeight = (groupRect.Height - 20) / Math.Max(shortcutsPerColumn, 1);
                var shortcutWidth = (groupRect.Width - 20) / Math.Max(shortcutsPerColumn, 1);
                
                int row = 0, col = 0;
                foreach (var shortcut in level)
                {
                    var shortcutRect = new Rectangle(
                        groupRect.X + 10 + col * shortcutWidth,
                        groupRect.Y + 10 + row * shortcutHeight,
                        Math.Min(shortcutWidth - 2, 80),
                        Math.Min(shortcutHeight - 2, 25));
                    
                    g.FillRectangle(new SolidBrush(groupColor), shortcutRect);
                    g.DrawRectangle(new Pen(T3Style.Colors.Border), shortcutRect);
                    
                    g.DrawString(shortcut.Name.Substring(0, Math.Min(shortcut.Name.Length, 8)), 
                        T3Style.Fonts.Small, 
                        new SolidBrush(T3Style.Colors.Text), 
                        shortcutRect, new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        });
                    
                    col++;
                    if (col >= shortcutsPerColumn)
                    {
                        col = 0;
                        row++;
                    }
                }
                
                // Draw level label
                g.DrawString($"Level {level.Key}\n({level.Count()} shortcuts)", 
                    T3Style.Fonts.Default, 
                    new SolidBrush(T3Style.Colors.HeaderText), 
                    groupRect.X, mapRect.Y);
                
                groupIndex++;
            }
        }
        
        private void DrawNetworkView(Graphics g)
        {
            var shortcuts = GetFilteredShortcuts();
            if (!shortcuts.Any()) return;
            
            var networkRect = new Rectangle(50, 50, 700, 400);
            g.FillRectangle(new SolidBrush(T3Style.Colors.PanelBackground), networkRect);
            g.DrawRectangle(new Pen(T3Style.Colors.Border), networkRect);
            
            // Group by categories and draw connections
            var categories = shortcuts.GroupBy(s => s.Category).ToList();
            var center = new Point(networkRect.X + networkRect.Width / 2, networkRect.Y + networkRect.Height / 2);
            var radius = Math.Min(networkRect.Width, networkRect.Height) / 2 - 50;
            
            // Draw category nodes
            for (int i = 0; i < categories.Count; i++)
            {
                var angle = (float)i / categories.Count * 2 * Math.PI;
                var x = center.X + (float)(Math.Cos(angle) * radius);
                var y = center.Y + (float)(Math.Sin(angle) * radius);
                
                var nodeRect = new Rectangle((int)x - 60, (int)y - 40, 120, 80);
                var categoryColor = GetCategoryColor(categories[i].Key);
                
                g.FillRectangle(new SolidBrush(categoryColor), nodeRect);
                g.DrawRectangle(new Pen(T3Style.Colors.Border), nodeRect);
                
                // Draw category name
                g.DrawString(categories[i].Key, T3Style.Fonts.Default, 
                    new SolidBrush(T3Style.Colors.HeaderText), 
                    nodeRect, new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    });
                
                // Draw connections between categories with shared shortcuts
                for (int j = i + 1; j < categories.Count; j++)
                {
                    var otherAngle = (float)j / categories.Count * 2 * Math.PI;
                    var otherX = center.X + (float)(Math.Cos(otherAngle) * radius);
                    var otherY = center.Y + (float)(Math.Sin(otherAngle) * radius);
                    
                    var hasConnection = shortcuts.Any(s => 
                        s.Category == categories[i].Key && 
                        s.Context == ShortcutContext.Global);
                    
                    if (hasConnection)
                    {
                        g.DrawLine(new Pen(T3Style.Colors.Connection, 2), x, y, otherX, otherY);
                    }
                }
            }
        }
        
        private int GetKeyComplexity(Keys key)
        {
            int complexity = 0;
            if (key.HasFlag(Keys.Control)) complexity++;
            if (key.HasFlag(Keys.Alt)) complexity++;
            if (key.HasFlag(Keys.Shift)) complexity++;
            if (key.HasFlag(Keys.LWin) || key.HasFlag(Keys.RWin)) complexity++;
            
            var keyCode = key & Keys.KeyCode;
            if (keyCode >= Keys.F1 && keyCode <= Keys.F12)
                complexity++;
            
            return complexity;
        }
        
        private Color GetComplexityColor(int level)
        {
            return level switch
            {
                0 => Color.FromArgb(144, 238, 144), // LightGreen
                1 => Color.FromArgb(255, 255, 224), // LightYellow
                2 => Color.FromArgb(255, 182, 193), // LightPink
                3 => Color.FromArgb(255, 160, 122), // LightSalmon
                _ => Color.FromArgb(211, 211, 211)  // LightGray
            };
        }
        
        private List<KeyboardShortcut> GetFilteredShortcuts()
        {
            if (_shortcuts == null) return new List<KeyboardShortcut>();
            
            var category = _filterCategory.SelectedIndex > 0 ? _filterCategory.SelectedItem.ToString() : null;
            var complexityLevel = _filterComplexity.Value;
            
            return _shortcuts.Where(s =>
            {
                var matchesCategory = category == null || s.Category == category;
                var matchesComplexity = complexityLevel == 0 || GetKeyComplexity(s.PrimaryKey) == complexityLevel - 1;
                
                return matchesCategory && matchesComplexity;
            }).ToList();
        }
        
        #region Event Handlers
        
        private void OnVisualizationModeChanged(object sender, EventArgs e)
        {
            _mode = (VisualizationMode)Enum.Parse(typeof(VisualizationMode), _visualizationMode.SelectedItem.ToString());
            RedrawCanvas();
        }
        
        private void OnFilterChanged(object sender, EventArgs e)
        {
            RedrawCanvas();
        }
        
        private void OnComplexityChanged(object sender, EventArgs e)
        {
            _complexityLabel.Text = _filterComplexity.Value switch
            {
                0 => "All",
                1 => "Simple",
                2 => "Medium",
                3 => "Complex",
                _ => "All"
            };
            RedrawCanvas();
        }
        
        private void OnExportImage(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*";
                saveDialog.DefaultExt = "png";
                saveDialog.FileName = $"shortcut-visualization-{_mode}.png";
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    using (var bitmap = new Bitmap(_canvas.Width, _canvas.Height))
                    {
                        _canvas.DrawToBitmap(bitmap, _canvas.ClientRectangle);
                        bitmap.Save(saveDialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    
                    MessageBox.Show("Visualization exported successfully!", "Export Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        
        #endregion
    }
    
    public enum VisualizationMode
    {
        KeyboardMap,
        CategoryChart,
        UsageGraph,
        ComplexityMap,
        NetworkView
    }
}