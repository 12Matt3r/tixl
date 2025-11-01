# TiXL UI Component Consistency Standardization

## Executive Summary

This document establishes comprehensive design system standards and implementation guidelines to address UI component consistency issues identified in TiXL. The standardization focuses on creating a unified visual language, interaction patterns, and component library that ensures consistency across all UI modules including ImGui, Silk.NET/Forms, and SystemUI components.

## 1. Design System Foundation

### 1.1 Design Principles

- **Consistency First**: All UI elements follow established patterns and tokens
- **Accessibility by Design**: High contrast, keyboard navigation, and reduced motion support
- **Performance-Conscious**: Efficient rendering and smooth interactions
- **Progressive Disclosure**: Essential controls prominent, advanced options discoverable
- **Immediate Feedback**: Clear state changes and user interaction confirmation

### 1.2 Design Tokens Structure

```css
/* Color System */
:root {
  /* Primary Colors */
  --color-primary-50: #eff6ff;
  --color-primary-100: #dbeafe;
  --color-primary-500: #3b82f6;
  --color-primary-600: #2563eb;
  --color-primary-900: #1e3a8a;

  /* Neutral Colors */
  --color-neutral-0: #ffffff;
  --color-neutral-50: #f9fafb;
  --color-neutral-100: #f3f4f6;
  --color-neutral-200: #e5e7eb;
  --color-neutral-300: #d1d5db;
  --color-neutral-400: #9ca3af;
  --color-neutral-500: #6b7280;
  --color-neutral-600: #4b5563;
  --color-neutral-700: #374151;
  --color-neutral-800: #1f2937;
  --color-neutral-900: #111827;

  /* Semantic Colors */
  --color-success: #10b981;
  --color-warning: #f59e0b;
  --color-error: #ef4444;
  --color-info: #3b82f6;

  /* Background Colors */
  --bg-primary: var(--color-neutral-0);
  --bg-secondary: var(--color-neutral-50);
  --bg-tertiary: var(--color-neutral-100);
  --bg-panel: var(--color-neutral-0);
  --bg-overlay: rgba(0, 0, 0, 0.5);

  /* Text Colors */
  --text-primary: var(--color-neutral-900);
  --text-secondary: var(--color-neutral-600);
  --text-tertiary: var(--color-neutral-400);
  --text-inverse: var(--color-neutral-0);

  /* Border Colors */
  --border-primary: var(--color-neutral-200);
  --border-secondary: var(--color-neutral-300);
  --border-focus: var(--color-primary-500);

  /* Spacing Scale (4px base) */
  --space-1: 0.25rem; /* 4px */
  --space-2: 0.5rem;  /* 8px */
  --space-3: 0.75rem; /* 12px */
  --space-4: 1rem;    /* 16px */
  --space-5: 1.25rem; /* 20px */
  --space-6: 1.5rem;  /* 24px */
  --space-8: 2rem;    /* 32px */
  --space-10: 2.5rem; /* 40px */
  --space-12: 3rem;   /* 48px */
  --space-16: 4rem;   /* 64px */

  /* Typography Scale */
  --font-size-xs: 0.75rem;   /* 12px */
  --font-size-sm: 0.875rem;  /* 14px */
  --font-size-base: 1rem;    /* 16px */
  --font-size-lg: 1.125rem;  /* 18px */
  --font-size-xl: 1.25rem;   /* 20px */
  --font-size-2xl: 1.5rem;   /* 24px */
  --font-size-3xl: 1.875rem; /* 30px */

  /* Border Radius */
  --radius-sm: 0.125rem; /* 2px */
  --radius-base: 0.25rem; /* 4px */
  --radius-md: 0.375rem; /* 6px */
  --radius-lg: 0.5rem;   /* 8px */
  --radius-xl: 0.75rem;  /* 12px */
  --radius-full: 9999px;

  /* Shadows */
  --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
  --shadow-base: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06);
  --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
  --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05);
  --shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);

  /* Animation Duration */
  --duration-fast: 150ms;
  --duration-normal: 250ms;
  --duration-slow: 350ms;

  /* Animation Easing */
  --ease-base: cubic-bezier(0.4, 0, 0.2, 1);
  --ease-in: cubic-bezier(0.4, 0, 1, 1);
  --ease-out: cubic-bezier(0, 0, 0.2, 1);
  --ease-in-out: cubic-bezier(0.4, 0, 0.2, 1);
}

/* Dark Theme */
[data-theme="dark"] {
  --bg-primary: var(--color-neutral-900);
  --bg-secondary: var(--color-neutral-800);
  --bg-tertiary: var(--color-neutral-700);
  --bg-panel: var(--color-neutral-800);
  --bg-overlay: rgba(0, 0, 0, 0.7);

  --text-primary: var(--color-neutral-0);
  --text-secondary: var(--color-neutral-300);
  --text-tertiary: var(--color-neutral-500);

  --border-primary: var(--color-neutral-700);
  --border-secondary: var(--color-neutral-600);
}

/* High Contrast Theme */
[data-theme="high-contrast"] {
  --bg-primary: #ffffff;
  --bg-secondary: #f5f5f5;
  --text-primary: #000000;
  --text-secondary: #333333;
  --color-primary-500: #0000ff;
  --color-primary-600: #0000cc;
  --color-success: #008000;
  --color-warning: #ff6600;
  --color-error: #cc0000;
  --border-primary: #000000;
  --border-focus: #0000ff;
}
```

### 1.3 Typography System

```css
/* Font Families */
--font-family-sans: 'Inter', 'Segoe UI', 'Roboto', sans-serif;
--font-family-mono: 'JetBrains Mono', 'Fira Code', 'Consolas', monospace;

/* Typography Classes */
.text-xs {
  font-size: var(--font-size-xs);
  line-height: 1rem;
}

.text-sm {
  font-size: var(--font-size-sm);
  line-height: 1.25rem;
}

.text-base {
  font-size: var(--font-size-base);
  line-height: 1.5rem;
}

.text-lg {
  font-size: var(--font-size-lg);
  line-height: 1.75rem;
}

.text-xl {
  font-size: var(--font-size-xl);
  line-height: 1.75rem;
}

.text-2xl {
  font-size: var(--font-size-2xl);
  line-height: 2rem;
}

.font-normal { font-weight: 400; }
.font-medium { font-weight: 500; }
.font-semibold { font-weight: 600; }
.font-bold { font-weight: 700; }

/* Typography Roles */
.text-heading {
  font-family: var(--font-family-sans);
  font-weight: var(--font-semibold);
  color: var(--text-primary);
}

.text-body {
  font-family: var(--font-family-sans);
  font-weight: var(--font-normal);
  color: var(--text-primary);
}

.text-caption {
  font-family: var(--font-family-sans);
  font-size: var(--font-size-sm);
  color: var(--text-secondary);
}

.text-code {
  font-family: var(--font-family-mono);
  font-size: var(--font-size-sm);
  background-color: var(--color-neutral-100);
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-base);
}
```

## 2. Theme Implementation

### 2.1 Theme System Architecture

```csharp
// ThemeManager.cs - Core theme management system
namespace TiXL.UI.Theming
{
    public enum ThemeVariant
    {
        Light,
        Dark,
        HighContrast
    }

    public class ThemeData
    {
        public string Name { get; set; }
        public ThemeVariant Variant { get; set; }
        public ColorPalette Colors { get; set; }
        public TypographyConfig Typography { get; set; }
        public SpacingConfig Spacing { get; set; }
        public AnimationConfig Animation { get; set; }
    }

    public class ThemeManager : IDisposable
    {
        private readonly Dictionary<ThemeVariant, ThemeData> _themes;
        private ThemeVariant _currentTheme;
        public event EventHandler<ThemeVariant> ThemeChanged;

        public ThemeManager()
        {
            _themes = InitializeThemes();
            _currentTheme = ThemeVariant.Light;
        }

        public void SetTheme(ThemeVariant theme)
        {
            if (_currentTheme != theme)
            {
                _currentTheme = theme;
                ApplyTheme(_themes[theme]);
                ThemeChanged?.Invoke(this, theme);
            }
        }

        private void ApplyTheme(ThemeData theme)
        {
            // Apply to ImGui
            ApplyImGuiTheme(theme);
            
            // Apply to Silk.NET components
            ApplySilkTheme(theme);
            
            // Apply to SystemUI components
            ApplySystemUITheme(theme);
        }
    }
}
```

### 2.2 Theme Configuration

```csharp
// ThemeConfigurations.cs - Predefined theme configurations
namespace TiXL.UI.Theming
{
    public static class ThemeConfigurations
    {
        public static ThemeData LightTheme => new()
        {
            Name = "Light",
            Variant = ThemeVariant.Light,
            Colors = new ColorPalette
            {
                Background = Color.FromHex("#FFFFFF"),
                BackgroundSecondary = Color.FromHex("#F9FAFB"),
                BackgroundTertiary = Color.FromHex("#F3F4F6"),
                TextPrimary = Color.FromHex("#111827"),
                TextSecondary = Color.FromHex("#6B7280"),
                TextTertiary = Color.FromHex("#9CA3AF"),
                BorderPrimary = Color.FromHex("#E5E7EB"),
                BorderSecondary = Color.FromHex("#D1D5DB"),
                BorderFocus = Color.FromHex("#3B82F6"),
                Primary = Color.FromHex("#3B82F6"),
                PrimaryHover = Color.FromHex("#2563EB"),
                Success = Color.FromHex("#10B981"),
                Warning = Color.FromHex("#F59E0B"),
                Error = Color.FromHex("#EF4444"),
                Info = Color.FromHex("#3B82F6")
            },
            Typography = new TypographyConfig
            {
                FontFamily = "Inter",
                FontSizes = new Dictionary<string, float>
                {
                    { "xs", 12f }, { "sm", 14f }, { "base", 16f },
                    { "lg", 18f }, { "xl", 20f }, { "2xl", 24f }
                },
                LineHeights = new Dictionary<string, float>
                {
                    { "xs", 16f }, { "sm", 20f }, { "base", 24f },
                    { "lg", 28f }, { "xl", 28f }, { "2xl", 32f }
                }
            },
            Animation = new AnimationConfig
            {
                DurationFast = TimeSpan.FromMilliseconds(150),
                DurationNormal = TimeSpan.FromMilliseconds(250),
                DurationSlow = TimeSpan.FromMilliseconds(350),
                EasingBase = EasingFunctions.CubicBezier(0.4f, 0f, 0.2f, 1f)
            }
        };

        public static ThemeData DarkTheme => LightTheme with
        {
            Name = "Dark",
            Variant = ThemeVariant.Dark,
            Colors = new ColorPalette
            {
                Background = Color.FromHex("#111827"),
                BackgroundSecondary = Color.FromHex("#1F2937"),
                BackgroundTertiary = Color.FromHex("#374151"),
                TextPrimary = Color.FromHex("#F9FAFB"),
                TextSecondary = Color.FromHex("#D1D5DB"),
                TextTertiary = Color.FromHex("#6B7280"),
                BorderPrimary = Color.FromHex("#374151"),
                BorderSecondary = Color.FromHex("#4B5563"),
                BorderFocus = Color.FromHex("#60A5FA"),
                Primary = Color.FromHex("#60A5FA"),
                PrimaryHover = Color.FromHex("#3B82F6")
            }
        };

        public static ThemeData HighContrastTheme => LightTheme with
        {
            Name = "High Contrast",
            Variant = ThemeVariant.HighContrast,
            Colors = new ColorPalette
            {
                Background = Color.FromHex("#FFFFFF"),
                BackgroundSecondary = Color.FromHex("#F5F5F5"),
                TextPrimary = Color.FromHex("#000000"),
                TextSecondary = Color.FromHex("#333333"),
                Primary = Color.FromHex("#0000FF"),
                PrimaryHover = Color.FromHex("#0000CC"),
                Success = Color.FromHex("#008000"),
                Warning = Color.FromHex("#FF6600"),
                Error = Color.FromHex("#CC0000"),
                BorderPrimary = Color.FromHex("#000000"),
                BorderFocus = Color.FromHex("#0000FF")
            }
        };
    }
}
```

## 3. Component Library Standards

### 3.1 Button Components

```csharp
// ButtonVariants.cs - Standardized button implementations
namespace TiXL.UI.Components.Buttons
{
    public enum ButtonVariant
    {
        Primary,
        Secondary,
        Tertiary,
        Ghost,
        Link,
        Destructive
    }

    public enum ButtonSize
    {
        Small,
        Medium,
        Large
    }

    public struct ButtonState
    {
        public bool Hovered { get; set; }
        public bool Active { get; set; }
        public bool Focused { get; set; }
        public bool Disabled { get; set; }
    }

    public interface IButtonComponent
    {
        ButtonVariant Variant { get; set; }
        ButtonSize Size { get; set; }
        string Text { get; set; }
        string Tooltip { get; set; }
        bool Enabled { get; set; }
        bool Loading { get; set; }
        EventHandler Clicked { get; set; }
        
        void Draw();
    }

    public class ButtonComponent : IButtonComponent
    {
        public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
        public ButtonSize Size { get; set; } = ButtonSize.Medium;
        public string Text { get; set; } = "Button";
        public string Tooltip { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Loading { get; set; } = false;
        public EventHandler Clicked { get; set; }

        private ButtonState _state;

        public void Draw()
        {
            ApplySizeConstraints();
            ApplyVisualStyles();
            HandleInteraction();
            RenderContent();
        }

        private void ApplySizeConstraints()
        {
            var padding = Size switch
            {
                ButtonSize.Small => new Padding(8, 4),
                ButtonSize.Medium => new Padding(12, 8),
                ButtonSize.Large => new Padding(16, 12),
                _ => new Padding(12, 8)
            };

            var fontSize = Size switch
            {
                ButtonSize.Small => 12,
                ButtonSize.Medium => 14,
                ButtonSize.Large => 16,
                _ => 14
            };

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
            ImGui.PushFont(GetFontForSize(fontSize));
        }
    }
}
```

### 3.2 Input Components

```csharp
// StandardInputComponents.cs - Unified input system
namespace TiXL.UI.Components.Inputs
{
    public abstract class BaseInputComponent<T> : IInputComponent<T>
    {
        public string Label { get; set; }
        public string Placeholder { get; set; }
        public string Tooltip { get; set; }
        public bool Required { get; set; }
        public bool Enabled { get; set; } = true;
        public ValidationRule<T> Validation { get; set; }
        public EventHandler<T> ValueChanged { get; set; }

        protected T _value;
        public T Value
        {
            get => _value;
            set
            {
                if (!_value.Equals(value))
                {
                    var oldValue = _value;
                    _value = value;
                    ValueChanged?.Invoke(this, value);
                    
                    if (Validation?.Validate(value) == false)
                    {
                        ShowValidationError();
                    }
                }
            }
        }

        public abstract InputEditStateFlags Draw();
    }

    public class TextInputComponent : BaseInputComponent<string>
    {
        public int MaxLength { get; set; } = 255;
        public bool Multiline { get; set; } = false;
        public InputType InputType { get; set; } = InputType.Text;

        public override InputEditStateFlags Draw()
        {
            var stateFlags = InputEditStateFlags.None;

            // Apply consistent styling
            ApplyInputStyling();

            // Draw label
            if (!string.IsNullOrEmpty(Label))
            {
                DrawLabel();
            }

            // Apply constraints based on InputType
            switch (InputType)
            {
                case InputType.Password:
                    ImGui.InputText($"##{GetHashCode()}", ref _value, MaxLength, 
                        ImGuiInputTextFlags.Password | ImGuiInputTextFlags.AutoSelectAll);
                    break;
                case InputType.FilePath:
                    DrawFilePathInput();
                    break;
                case InputType.Email:
                    DrawEmailInput();
                    break;
                default:
                    DrawStandardTextInput();
                    break;
            }

            // Show validation feedback
            if (Validation != null && !Validation.Validate(_value))
            {
                DrawValidationError();
            }

            return stateFlags;
        }

        private void ApplyInputStyling()
        {
            var colors = ThemeManager.Current.Colors;
            var spacing = ThemeManager.Current.Spacing;

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, spacing.Padding2);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ThemeManager.Current.BorderRadius);
            
            // Apply state-based colors
            var frameBg = Enabled ? colors.BackgroundSecondary : colors.BackgroundTertiary;
            ImGui.PushStyleColor(ImGuiCol.FrameBg, frameBg);
            ImGui.PushStyleColor(ImGuiCol.Text, colors.TextPrimary);
        }
    }

    public enum InputType
    {
        Text,
        Password,
        Email,
        FilePath,
        DirectoryPath,
        Url,
        Phone
    }
}
```

### 3.3 Panel and Layout Components

```csharp
// PanelComponents.cs - Consistent panel system
namespace TiXL.UI.Components.Panels
{
    public abstract class BasePanel : IPanelComponent
    {
        public string Title { get; set; }
        public string Id { get; set; }
        public PanelFlags Flags { get; set; } = PanelFlags.Default;
        public bool CanClose { get; set; } = true;
        public bool CanDock { get; set; } = true;
        public Vector2 InitialSize { get; set; } = new Vector2(400, 300);
        public Vector2 InitialPosition { get; set; } = Vector2.Zero;

        protected PanelState _state = new();

        public virtual void Draw()
        {
            ApplyWindowStyling();
            
            if (ImGui.Begin($"{Title}##{Id}", Flags))
            {
                _state.IsVisible = true;
                _state.IsFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.AnyWindow);
                
                DrawContent();
                DrawFooter();
            }
            else
            {
                _state.IsVisible = false;
            }

            ImGui.End();
        }

        protected virtual void ApplyWindowStyling()
        {
            var colors = ThemeManager.Current.Colors;
            var spacing = ThemeManager.Current.Spacing;

            // Apply consistent padding
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, spacing.Padding4);

            // Apply panel background
            ImGui.PushStyleColor(ImGuiCol.WindowBg, colors.Background);
            ImGui.PushStyleColor(ImGuiCol.Border, colors.BorderPrimary);
            
            // Title bar styling
            ImGui.PushStyleColor(ImGuiCol.TitleBg, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, colors.BackgroundTertiary);
            ImGui.PushStyleColor(ImGuiCol.TitleText, colors.TextPrimary);
        }

        protected abstract void DrawContent();
        protected virtual void DrawFooter() { }

        public void SetCollapsed(bool collapsed)
        {
            _state.IsCollapsed = collapsed;
            Flags = collapsed ? Flags | ImGuiWindowFlags.NoCollapse : Flags & ~ImGuiWindowFlags.NoCollapse;
        }
    }

    public class PropertyPanel : BasePanel
    {
        private List<IPropertyItem> _properties = new();

        protected override void DrawContent()
        {
            foreach (var property in _properties)
            {
                property.Draw();
                ImGui.Spacing();
            }
        }

        public void AddProperty(IPropertyItem property)
        {
            _properties.Add(property);
        }
    }

    public class CollapsibleSection : ILayoutComponent
    {
        public string Title { get; set; }
        public bool IsExpanded { get; set; } = true;
        public PanelFlags Flags { get; set; } = PanelFlags.Default;
        private readonly List<ILayoutComponent> _children = new();

        public void Draw()
        {
            if (ImGui.CollapsingHeader(Title, Flags))
            {
                ImGui.Indent(1);
                foreach (var child in _children)
                {
                    child.Draw();
                }
                ImGui.Unindent(1);
            }
        }

        public void AddChild(ILayoutComponent child) => _children.Add(child);
    }
}
```

## 4. Icon System Standardization

### 4.1 Icon System Architecture

```csharp
// IconSystem.cs - Unified icon management
namespace TiXL.UI.Icons
{
    public enum IconSet
    {
        Solid,
        Regular,
        Light,
        Duotone
    }

    public enum IconSize
    {
        Small = 12,
        Medium = 16,
        Large = 20,
        XLarge = 24
    }

    public struct IconDefinition
    {
        public string Name { get; set; }
        public char Unicode { get; set; }
        public string[] Tags { get; set; }
        public IconSize DefaultSize { get; set; }
        public bool SupportsMultipleSizes { get; set; }
    }

    public class IconManager
    {
        private readonly Dictionary<string, IconDefinition> _icons;
        private readonly Dictionary<IconSize, ImFontPtr> _fonts;

        public IconManager()
        {
            _icons = LoadIconDefinitions();
            _fonts = LoadIconFonts();
        }

        public bool DrawIcon(string name, IconSize size, Color? color = null)
        {
            if (!_icons.TryGetValue(name, out var icon))
                return false;

            var font = _fonts[size];
            if (font == IntPtr.Zero)
                return false;

            ImGui.PushFont(font);
            ImGui.TextColored(color ?? ThemeManager.Current.Colors.TextPrimary, icon.Unicode.ToString());
            ImGui.PopFont();

            return true;
        }

        public List<string> SearchIcons(string query)
        {
            return _icons.Values
                .Where(icon => icon.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                              icon.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Select(icon => icon.Name)
                .ToList();
        }

        private Dictionary<string, IconDefinition> LoadIconDefinitions()
        {
            return new Dictionary<string, IconDefinition>
            {
                // Navigation
                ["home"] = new IconDefinition { Name = "home", Unicode = '\uf015', Tags = new[] { "home", "house" }, DefaultSize = IconSize.Medium },
                ["folder"] = new IconDefinition { Name = "folder", Unicode = '\uf07b', Tags = new[] { "folder", "directory" }, DefaultSize = IconSize.Medium },
                ["file"] = new IconDefinition { Name = "file", Unicode = '\uf15b', Tags = new[] { "file", "document" }, DefaultSize = IconSize.Medium },
                
                // Actions
                ["plus"] = new IconDefinition { Name = "plus", Unicode = '\uf067', Tags = new[] { "add", "create", "new" }, DefaultSize = IconSize.Medium },
                ["minus"] = new IconDefinition { Name = "minus", Unicode = '\uf068', Tags = new[] { "remove", "delete", "subtract" }, DefaultSize = IconSize.Medium },
                ["edit"] = new IconDefinition { Name = "edit", Unicode = '\uf044', Tags = new[] { "edit", "pencil", "modify" }, DefaultSize = IconSize.Medium },
                ["trash"] = new IconDefinition { Name = "trash", Unicode = '\uf1f8', Tags = new[] { "delete", "remove", "trash" }, DefaultSize = IconSize.Medium },
                
                // Interface
                ["settings"] = new IconDefinition { Name = "settings", Unicode = '\uf013', Tags = new[] { "settings", "gear", "preferences" }, DefaultSize = IconSize.Medium },
                ["info"] = new IconDefinition { Name = "info", Unicode = '\uf05a', Tags = new[] { "info", "information", "help" }, DefaultSize = IconSize.Medium },
                ["warning"] = new IconDefinition { Name = "warning", Unicode = '\uf071', Tags = new[] { "warning", "alert", "caution" }, DefaultSize = IconSize.Medium },
                ["error"] = new IconDefinition { Name = "error", Unicode = '\uf057', Tags = new[] { "error", "close", "cancel" }, DefaultSize = IconSize.Medium },
                
                // Audio/Video
                ["play"] = new IconDefinition { Name = "play", Unicode = '\uf04b', Tags = new[] { "play", "start", "audio" }, DefaultSize = IconSize.Medium },
                ["pause"] = new IconDefinition { Name = "pause", Unicode = '\uf04c', Tags = new[] { "pause", "stop" }, DefaultSize = IconSize.Medium },
                ["volume"] = new IconDefinition { Name = "volume", Unicode = '\uf027', Tags = new[] { "volume", "audio", "sound" }, DefaultSize = IconSize.Medium },
                
                // Graph
                ["node"] = new IconDefinition { Name = "node", Unicode = '\uf0db', Tags = new[] { "node", "graph", "point" }, DefaultSize = IconSize.Medium },
                ["connection"] = new IconDefinition { Name = "connection", Unicode = '\uf0c1', Tags = new[] { "connection", "link", "graph" }, DefaultSize = IconSize.Medium },
                
                // Status
                ["loading"] = new IconDefinition { Name = "loading", Unicode = '\uf110', Tags = new[] { "loading", "spinner", "busy" }, DefaultSize = IconSize.Medium, SupportsMultipleSizes = false },
                ["check"] = new IconDefinition { Name = "check", Unicode = '\uf00c', Tags = new[] { "check", "success", "done" }, DefaultSize = IconSize.Medium },
                ["times"] = new IconDefinition { Name = "times", Unicode = '\uf00d', Tags = new[] { "times", "close", "cancel" }, DefaultSize = IconSize.Medium }
            };
        }
    }
}
```

### 4.2 Icon Usage Guidelines

```csharp
// IconUsageHelper.cs - Consistent icon usage patterns
namespace TiXL.UI.Icons
{
    public static class IconHelper
    {
        public static void DrawActionIcon(string iconName, string tooltip, Action onClick)
        {
            var size = ImGui.CalcTextSize(iconName);
            if (ImGui.Button($" {iconName} ", new Vector2(size.X + 8, size.Y + 4)))
            {
                onClick?.Invoke();
            }

            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
        }

        public static void DrawIconButton(string iconName, Vector2 size, string tooltip = null)
        {
            var colors = ThemeManager.Current.Colors;
            
            // Apply button styling
            ImGui.PushStyleColor(ImGuiCol.Button, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.BackgroundTertiary);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors.Primary);
            
            if (ImGui.Button(iconName, size))
            {
                // Handle click
            }

            ImGui.PopStyleColor(3);

            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
        }

        public static void DrawStatusIcon(string status, IconSize size = IconSize.Medium)
        {
            var icon = status.ToLower() switch
            {
                "success" => "check",
                "warning" => "warning",
                "error" => "times",
                "loading" => "loading",
                "info" => "info",
                _ => "info"
            };

            var color = status.ToLower() switch
            {
                "success" => ThemeManager.Current.Colors.Success,
                "warning" => ThemeManager.Current.Colors.Warning,
                "error" => ThemeManager.Current.Colors.Error,
                "loading" => ThemeManager.Current.Colors.Info,
                "info" => ThemeManager.Current.Colors.Info,
                _ => ThemeManager.Current.Colors.TextSecondary
            };

            IconManager.Instance.DrawIcon(icon, size, color);
        }
    }
}
```

## 5. Layout Consistency Standards

### 5.1 Window Management System

```csharp
// WindowManager.cs - Consistent window behavior
namespace TiXL.UI.Windows
{
    public class WindowLayout
    {
        public string Name { get; set; }
        public Dictionary<string, WindowConfig> Windows { get; set; }
        public Vector2 Size { get; set; } = new(1200, 800);
        public bool IsMaximized { get; set; }
    }

    public class WindowConfig
    {
        public string Title { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; } = new(400, 300);
        public bool IsVisible { get; set; } = true;
        public bool IsCollapsed { get; set; }
        public PanelFlags Flags { get; set; } = PanelFlags.Default;
        public DockConfig Dock { get; set; }
    }

    public class WindowManager
    {
        private readonly Dictionary<string, IWindow> _windows = new();
        private readonly WindowLayout _currentLayout;

        public WindowManager()
        {
            _currentLayout = LoadDefaultLayout();
            InitializeWindows();
        }

        public void DrawDockingSpace()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            
            ImGui.DockSpaceOverViewport(
                ImGui.GetMainViewport(),
                ImGuiDockNodeFlags.PassthruCentralNode | ImGuiDockNodeFlags.NoDockingInCentralNode
            );

            ImGui.PopStyleVar();
        }

        public void SaveLayout(string layoutName)
        {
            var layout = new WindowLayout { Name = layoutName };
            
            foreach (var (id, window) in _windows)
            {
                var config = new WindowConfig
                {
                    Title = window.Title,
                    Position = window.Position,
                    Size = window.Size,
                    IsVisible = window.IsVisible,
                    IsCollapsed = window.IsCollapsed
                };
                
                layout.Windows[id] = config;
            }

            LayoutSerializer.Save(layout);
        }

        private void InitializeWindows()
        {
            // Initialize standard windows with consistent configurations
            RegisterWindow("properties", new PropertyWindow());
            RegisterWindow("timeline", new TimelineWindow());
            RegisterWindow("preview", new PreviewWindow());
            RegisterWindow("graph", new GraphWindow());
        }
    }
}
```

### 5.2 Dialog Standardization

```csharp
// DialogSystem.cs - Consistent dialog behavior
namespace TiXL.UI.Dialogs
{
    public abstract class BaseDialog
    {
        public string Title { get; set; }
        public Vector2? Size { get; set; } = new Vector2(400, 300);
        public DialogFlags Flags { get; set; } = DialogFlags.Modal;
        public bool Result { get; set; }

        protected virtual void Draw()
        {
            var isOpen = true;
            
            if (ImGui.BeginPopupModal(Title, ref isOpen, Flags))
            {
                DrawContent();
                DrawButtons();

                if (!isOpen)
                {
                    OnClose();
                }

                ImGui.EndPopup();
            }
        }

        protected abstract void DrawContent();
        protected virtual void DrawButtons()
        {
            ImGui.Spacing();
            
            var buttonSize = new Vector2(80, 0);
            var totalWidth = buttonSize.X * 2 + 16; // 16px spacing
            
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 
                (ImGui.GetContentRegionAvail().X - totalWidth) / 2);

            if (ImGui.Button("OK", buttonSize))
            {
                Result = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16);

            if (ImGui.Button("Cancel", buttonSize))
            {
                Result = false;
                ImGui.CloseCurrentPopup();
            }
        }

        protected virtual void OnClose() { }
    }

    public class FileOpenDialog : BaseDialog
    {
        private string _selectedPath = "";
        private readonly List<string> _recentPaths = new();

        protected override void DrawContent()
        {
            // Draw file browser with consistent styling
            ImGui.InputText("File Path:", ref _selectedPath, 256);
            
            ImGui.Spacing();
            
            if (ImGui.BeginChild("FileList", new Vector2(0, 200), true))
            {
                foreach (var path in _recentPaths)
                {
                    if (ImGui.Selectable(path))
                    {
                        _selectedPath = path;
                    }
                }
            }
            ImGui.EndChild();

            if (ImGui.Button("Browse..."))
            {
                // Open file browser
            }
        }
    }
}
```

## 6. Interaction Patterns Standardization

### 6.1 Hover States and Visual Feedback

```csharp
// InteractionManager.cs - Consistent interaction patterns
namespace TiXL.UI.Interactions
{
    public class InteractionState
    {
        public bool IsHovered { get; set; }
        public bool IsActive { get; set; }
        public bool IsFocused { get; set; }
        public bool IsDisabled { get; set; }
        public DateTime LastInteraction { get; set; }
    }

    public static class InteractionStyles
    {
        public static void ApplyButtonHoverStyles(InteractionState state)
        {
            var colors = ThemeManager.Current.Colors;
            
            var bgColor = state switch
            {
                { IsDisabled: true } => colors.BackgroundTertiary,
                { IsActive: true } => colors.Primary,
                { IsHovered: true } => colors.PrimaryHover,
                _ => colors.BackgroundSecondary
            };

            ImGui.PushStyleColor(ImGuiCol.Button, bgColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.PrimaryHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors.Primary);
        }

        public static void ApplyInputFocusStyles(bool isFocused)
        {
            var colors = ThemeManager.Current.Colors;
            var focusColor = isFocused ? colors.BorderFocus : colors.BorderPrimary;
            
            ImGui.PushStyleColor(ImGuiCol.FrameBg, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, focusColor);
            ImGui.PushStyleColor(ImGuiCol.BorderHovered, colors.BorderFocus);
        }
    }

    public class HoverFeedback
    {
        private DateTime _hoverStartTime;
        private bool _isShowingHover;
        private string _hoverText;

        public void UpdateHover(string text)
        {
            if (ImGui.IsItemHovered())
            {
                if (!_isShowingHover)
                {
                    _hoverStartTime = DateTime.Now;
                    _isShowingHover = true;
                }
                else if (DateTime.Now - _hoverStartTime > TimeSpan.FromMilliseconds(500))
                {
                    _hoverText = text;
                    ShowTooltip();
                }
            }
            else
            {
                _isShowingHover = false;
            }
        }

        private void ShowTooltip()
        {
            if (!string.IsNullOrEmpty(_hoverText))
            {
                ImGui.SetTooltip(_hoverText);
            }
        }
    }
}
```

### 6.2 Animation System

```csharp
// AnimationSystem.cs - Consistent animation patterns
namespace TiXL.UI.Animations
{
    public struct AnimationConfig
    {
        public TimeSpan Duration { get; set; }
        public EasingFunction Easing { get; set; }
        public bool Enabled { get; set; }
    }

    public abstract class BaseAnimation
    {
        protected TimeSpan _startTime;
        protected bool _isRunning;
        protected AnimationConfig _config;

        public void Start(AnimationConfig config)
        {
            _config = config;
            _startTime = DateTime.Now;
            _isRunning = true;
        }

        public void Update()
        {
            if (!_isRunning || !_config.Enabled) return;

            var elapsed = DateTime.Now - _startTime;
            var progress = Math.Min(elapsed.TotalMilliseconds / _config.Duration.TotalMilliseconds, 1.0);
            
            var easedProgress = _config.Easing(progress);
            ApplyProgress(easedProgress);

            if (progress >= 1.0)
            {
                _isRunning = false;
                OnComplete();
            }
        }

        protected abstract void ApplyProgress(double progress);
        protected virtual void OnComplete() { }
    }

    public class FadeAnimation : BaseAnimation
    {
        private readonly float _startOpacity;
        private readonly float _endOpacity;
        private readonly Action<float> _applyOpacity;

        public FadeAnimation(float startOpacity, float endOpacity, Action<float> applyOpacity)
        {
            _startOpacity = startOpacity;
            _endOpacity = endOpacity;
            _applyOpacity = applyOpacity;
        }

        protected override void ApplyProgress(double progress)
        {
            var opacity = Lerp(_startOpacity, _endOpacity, progress);
            _applyOpacity(opacity);
        }

        private static float Lerp(float start, float end, double t) => (float)(start + (end - start) * t);
    }

    public static class EasingFunctions
    {
        public static double CubicBezier(float x1, float y1, float x2, float y2)
        {
            return t =>
            {
                // Simplified cubic bezier easing
                return t < 0.5
                    ? 4 * t * t * t
                    : 1 - Math.Pow(-2 * t + 2, 3) / 2;
            };
        }

        public static readonly EasingFunction EaseInOut = CubicBezier(0.4f, 0f, 0.2f, 1f);
        public static readonly EasingFunction EaseOut = t => 1 - Math.Pow(1 - t, 3);
        public static readonly EasingFunction EaseIn = t => t * t * t;
    }
}
```

## 7. ImGui Customization and Extension

### 7.1 Style Application System

```csharp
// ImGuiStyler.cs - Consistent ImGui theming
namespace TiXL.UI.ImGui
{
    public static class ImGuiThemeApplier
    {
        public static void ApplyBaseTheme()
        {
            var theme = ThemeManager.Current;
            var style = ImGui.GetStyle();

            // Window styling
            style.WindowPadding = theme.Spacing.WindowPadding;
            style.WindowRounding = theme.BorderRadius;
            style.WindowBorderSize = 1;
            style.WindowMinSize = new Vector2(100, 100);

            // Frame styling
            style.FramePadding = theme.Spacing.FramePadding;
            style.FrameRounding = theme.BorderRadius;
            style.FrameBorderSize = 1;

            // Button styling
            style.ButtonTextAlign = new Vector2(0.5f, 0.5f);

            // Scrollbar styling
            style.ScrollbarRounding = theme.BorderRadius;
            style.ScrollbarSize = 16;

            // Colors
            ApplyColorScheme(theme.Colors);
        }

        public static void ApplyColorScheme(ColorPalette colors)
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, colors.Background);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, colors.Background);
            ImGui.PushStyleColor(ImGuiCol.PopupBg, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, colors.BorderPrimary);
            ImGui.PushStyleColor(ImGuiCol.BorderShadow, colors.BorderPrimary);
            ImGui.PushStyleColor(ImGuiCol.Text, colors.TextPrimary);
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, colors.TextTertiary);
            ImGui.PushStyleColor(ImGuiCol.Header, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, colors.BackgroundTertiary);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, colors.Primary);
            ImGui.PushStyleColor(ImGuiCol.Button, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.PrimaryHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors.Primary);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, colors.BackgroundTertiary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, colors.Primary);
            ImGui.PushStyleColor(ImGuiCol.TitleBg, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, colors.Primary);
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, colors.Background);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, colors.BackgroundTertiary);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, colors.Primary);
        }
    }

    // Custom ImGui widgets
    public static class CustomImGui
    {
        public static bool IconButton(string icon, string tooltip = null, Vector2 size = default)
        {
            var colors = ThemeManager.Current.Colors;
            var iconSize = ImGui.CalcTextSize(icon);
            var buttonSize = size.X == 0 ? iconSize + new Vector2(8, 4) : size;

            ImGui.PushStyleColor(ImGuiCol.Button, colors.BackgroundSecondary);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.BackgroundTertiary);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors.Primary);

            var clicked = ImGui.Button($"{icon}##{GetHashCode()}", buttonSize);

            ImGui.PopStyleColor(3);

            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }

            return clicked;
        }

        public static bool ThemedInputText(string label, ref string value, 
            ImGuiInputTextFlags flags = ImGuiInputTextFlags.None, int bufferSize = 256)
        {
            ImGuiThemeApplier.ApplyColorScheme(ThemeManager.Current.Colors);
            
            var result = ImGui.InputText(label, ref value, bufferSize, flags);
            
            ImGuiThemeApplier.ApplyColorScheme(ThemeManager.Current.Colors);
            
            return result;
        }
    }
}
```

### 7.2 Component Integration

```csharp
// ComponentIntegration.cs - Bridge between different UI systems
namespace TiXL.UI.Integration
{
    public class UIRenderer
    {
        public void Render<T>(T component) where T : IUIComponent
        {
            switch (component)
            {
                case IButtonComponent button:
                    RenderButton(button);
                    break;
                case IInputComponent input:
                    RenderInput(input);
                    break;
                case IPanelComponent panel:
                    RenderPanel(panel);
                    break;
            }
        }

        private void RenderButton(IButtonComponent button)
        {
            var oldFont = ImGui.GetFont();
            
            // Apply component styles
            ApplyButtonStyles(button);
            
            if (button.Loading)
            {
                DrawLoadingButton(button);
            }
            else
            {
                DrawStandardButton(button);
            }
            
            // Restore font
            ImGui.PushFont(oldFont);
        }

        private void RenderInput(IInputComponent input)
        {
            // Apply input styles
            ApplyInputStyles(input);
            
            input.Draw();
        }

        private void RenderPanel(IPanelComponent panel)
        {
            // Apply panel container styles
            ApplyPanelStyles(panel);
            
            panel.Draw();
        }

        private void ApplyButtonStyles(IButtonComponent button)
        {
            var colors = ThemeManager.Current.Colors;
            var spacing = ThemeManager.Current.Spacing;

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, spacing.Padding2);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ThemeManager.Current.BorderRadius);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);

            // Apply variant-specific colors
            switch (button.Variant)
            {
                case ButtonVariant.Primary:
                    ImGui.PushStyleColor(ImGuiCol.Button, colors.Primary);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.PrimaryHover);
                    break;
                case ButtonVariant.Secondary:
                    ImGui.PushStyleColor(ImGuiCol.Button, colors.BackgroundSecondary);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.BackgroundTertiary);
                    break;
                // Add more variants...
            }
        }
    }
}
```

## 8. Accessibility Implementation

### 8.1 Keyboard Navigation System

```csharp
// KeyboardNavigation.cs - Comprehensive keyboard support
namespace TiXL.UI.Accessibility
{
    public class KeyboardNavigator
    {
        private readonly Stack<NavigationContext> _navigationStack = new();
        private NavigationContext _currentContext;

        public void Initialize()
        {
            _currentContext = new NavigationContext
            {
                FocusableElements = new List<IFocusable>(),
                CurrentIndex = 0
            };
        }

        public void RegisterFocusableElement(IFocusable element)
        {
            _currentContext.FocusableElements.Add(element);
        }

        public void HandleKeyboardInput(ImGuiKey key, bool ctrl = false, bool alt = false, bool shift = false)
        {
            switch (key)
            {
                case ImGuiKey.Tab:
                    if (ctrl) break;
                    
                    if (shift)
                        MoveFocus(-1);
                    else
                        MoveFocus(1);
                    break;

                case ImGuiKey.Enter:
                case ImGuiKey.KeypadEnter:
                    ActivateCurrentElement();
                    break;

                case ImGuiKey.Escape:
                    EscapeCurrentContext();
                    break;

                case ImGuiKey.Space:
                    if (ctrl)
                    {
                        OpenCommandPalette();
                    }
                    else
                    {
                        ActivateCurrentElement();
                    }
                    break;

                case ImGuiKey.Home:
                    MoveToFirstElement();
                    break;

                case ImGuiKey.End:
                    MoveToLastElement();
                    break;
            }
        }

        private void MoveFocus(int direction)
        {
            if (_currentContext.FocusableElements.Count == 0) return;

            _currentContext.CurrentIndex = (_currentContext.CurrentIndex + direction + 
                _currentContext.FocusableElements.Count) % _currentContext.FocusableElements.Count;

            FocusCurrentElement();
        }

        private void FocusCurrentElement()
        {
            if (_currentContext.CurrentIndex < _currentContext.FocusableElements.Count)
            {
                var element = _currentContext.FocusableElements[_currentContext.CurrentIndex];
                element.Focus();
            }
        }
    }

    public interface IFocusable
    {
        bool CanReceiveFocus { get; }
        bool IsFocused { get; set; }
        void Focus();
        void Blur();
        Rectangle GetFocusBounds();
    }

    public class FocusRing
    {
        private readonly List<IFocusable> _focusedElements = new();
        
        public void Draw()
        {
            foreach (var element in _focusedElements.Where(e => e.CanReceiveFocus))
            {
                DrawFocusRing(element);
            }
        }

        private void DrawFocusRing(IFocusable element)
        {
            var colors = ThemeManager.Current.Colors;
            var bounds = element.GetFocusBounds();

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            var color = ImGui.ColorConvertFloat4ToU32(colors.BorderFocus with { W = 0.8f });

            drawList.AddRect(
                new Vector2(bounds.X - 2, bounds.Y - 2),
                new Vector2(bounds.X + bounds.Width + 2, bounds.Y + bounds.Height + 2),
                color,
                2,
                ImDrawFlags.RoundCornersAll,
                2
            );
        }
    }
}
```

### 8.2 High Contrast and Accessibility Features

```csharp
// AccessibilityFeatures.cs - Accessibility implementation
namespace TiXL.UI.Accessibility
{
    public class AccessibilityManager
    {
        private bool _highContrastEnabled;
        private bool _reducedMotionEnabled;
        private float _fontScale = 1.0f;
        private readonly KeyboardNavigator _navigator;
        private readonly FocusRing _focusRing;

        public AccessibilityManager()
        {
            _navigator = new KeyboardNavigator();
            _focusRing = new FocusRing();
            
            LoadPreferences();
        }

        public void ToggleHighContrast()
        {
            _highContrastEnabled = !_highContrastEnabled;
            
            if (_highContrastEnabled)
            {
                ThemeManager.Instance.SetTheme(ThemeVariant.HighContrast);
            }
            else
            {
                // Restore previous theme
                ThemeManager.Instance.SetTheme(GetCurrentTheme());
            }
            
            SavePreferences();
        }

        public void SetFontScale(float scale)
        {
            _fontScale = Math.Max(0.5f, Math.Min(2.0f, scale));
            ApplyFontScale();
            SavePreferences();
        }

        public void DrawAccessibilityMenu()
        {
            if (ImGui.BeginMenu("Accessibility"))
            {
                if (ImGui.MenuItem("High Contrast", "Ctrl+Shift+H", _highContrastEnabled))
                {
                    ToggleHighContrast();
                }

                if (ImGui.MenuItem("Reduced Motion", "Ctrl+Shift+M", _reducedMotionEnabled))
                {
                    _reducedMotionEnabled = !_reducedMotionEnabled;
                    SavePreferences();
                }

                ImGui.Separator();
                
                ImGui.Text("Font Scale:");
                if (ImGui.SliderFloat("##FontScale", ref _fontScale, 0.5f, 2.0f, "%.2f"))
                {
                    SetFontScale(_fontScale);
                }

                ImGui.Separator();
                
                if (ImGui.MenuItem("Focus Next Element", "Tab"))
                {
                    _navigator.HandleKeyboardInput(ImGuiKey.Tab);
                }

                ImGui.EndMenu();
            }
        }

        private void LoadPreferences()
        {
            _highContrastEnabled = UserPreferences.GetBool("accessibility.high_contrast", false);
            _reducedMotionEnabled = UserPreferences.GetBool("accessibility.reduced_motion", false);
            _fontScale = UserPreferences.GetFloat("accessibility.font_scale", 1.0f);
            
            if (_highContrastEnabled)
            {
                ThemeManager.Instance.SetTheme(ThemeVariant.HighContrast);
            }
            
            ApplyFontScale();
        }

        private void ApplyFontScale()
        {
            var baseFontSize = ThemeManager.Current.Typography.FontSizes["base"];
            var scaledSize = baseFontSize * _fontScale;
            
            // Apply to all fonts
            foreach (var font in FontManager.Fonts.Values)
            {
                font.Scale = _fontScale;
            }
        }

        private ThemeVariant GetCurrentTheme()
        {
            // Logic to determine current theme based on preferences
            return ThemeVariant.Light;
        }
    }

    // Screen reader support placeholder
    public class ScreenReaderSupport
    {
        private readonly Queue<string> _announcements = new();
        
        public void Announce(string message)
        {
            _announcements.Enqueue(message);
            
            // In a real implementation, this would interface with system accessibility APIs
            // For now, we can log or display announcements
            LogAccessibilityAnnouncement(message);
        }

        private void LogAccessibilityAnnouncement(string message)
        {
            // Log to console or status bar
            UserInterface.Instance.SetStatusMessage(message, TimeSpan.FromSeconds(3));
        }

        public void ProcessPendingAnnouncements()
        {
            while (_announcements.TryDequeue(out var message))
            {
                // Process screen reader announcements
            }
        }
    }
}
```

## 9. Component Library Documentation

### 9.1 Usage Guidelines

```markdown
# TiXL Component Library Guidelines

## Button Components

### Primary Button
Use for main actions that advance user workflows.

```csharp
var button = new ButtonComponent
{
    Text = "Create Project",
    Variant = ButtonVariant.Primary,
    Size = ButtonSize.Medium,
    Enabled = true
};

if (button.Clicked)
{
    CreateNewProject();
}
```

### Secondary Button
Use for supporting actions or alternate choices.

```csharp
var cancelButton = new ButtonComponent
{
    Text = "Cancel",
    Variant = ButtonVariant.Secondary,
    Size = ButtonSize.Medium
};
```

### Button States
- **Normal**: Default appearance
- **Hovered**: Mouse over (subtle visual change)
- **Active**: Being clicked (pressed appearance)
- **Disabled**: Non-interactive (reduced opacity, cursor change)
- **Loading**: With spinner, prevents interaction

## Input Components

### Text Input
Basic text entry with validation support.

```csharp
var textInput = new TextInputComponent
{
    Label = "Project Name",
    Placeholder = "Enter project name",
    MaxLength = 100,
    Validation = new RequiredStringValidation()
};
```

### Numeric Input
For number values with step controls.

```csharp
var numericInput = new NumericInputComponent
{
    Label = "Duration (seconds)",
    MinValue = 0,
    MaxValue = 3600,
    Step = 0.1f,
    Format = "F1"
};
```

## Panel Components

### Property Panel
Standard panel for displaying and editing properties.

```csharp
var propertyPanel = new PropertyPanel
{
    Title = "Object Properties",
    CanDock = true,
    CanClose = true
};

propertyPanel.AddProperty(new StringProperty("Name", "Object1"));
propertyPanel.AddProperty(new NumericProperty("Scale", 1.0f, 0.1f, 10.0f));
```

### Collapsible Section
For organizing content in expandable sections.

```csharp
var section = new CollapsibleSection
{
    Title = "Advanced Settings",
    IsExpanded = false
};

section.AddChild(new PropertyGroup("Rendering"));
section.AddChild(new PropertyGroup("Animation"));
```

## Icon Usage

### Consistency Rules
1. Use same icon size throughout interface (typically 16px)
2. Maintain consistent spacing around icons
3. Use semantic colors (green for success, red for errors)
4. Provide tooltips for complex icon meanings

### Icon Placement
- In buttons: Precede text with space
- In menus: At beginning of menu items
- In toolbars: As primary visual element
- In status indicators: Alone or with minimal text

## Layout Patterns

### Standard Spacing
- Internal padding: 8px (base unit)
- External margins: 8px between related elements
- Section spacing: 16px between major sections
- Form field spacing: 12px vertically

### Grid System
- Use 4px base grid for alignment
- Maintain consistent indentation (16px per level)
- Align related elements to visual grid

## Performance Guidelines

### Rendering Efficiency
1. Cache expensive calculations
2. Use conditional rendering for visibility
3. Batch similar draw calls
4. Minimize per-frame allocations

### Animation Performance
1. Limit simultaneous animations
2. Use easing functions for smooth transitions
3. Respect reduced motion preferences
4. Test on lower-end hardware

## Error Handling

### Input Validation
- Validate on change, not just on submit
- Provide clear, actionable error messages
- Prevent invalid states where possible
- Show validation feedback inline

### System Feedback
- Use consistent feedback patterns
- Provide progress indicators for long operations
- Show clear success/failure states
- Log errors for debugging while showing user-friendly messages
```

### 9.2 Implementation Checklist

```markdown
# UI Consistency Implementation Checklist

## Design System Foundation
- [ ] Implement design tokens structure
- [ ] Create color palette definitions
- [ ] Set up typography system
- [ ] Define spacing and sizing standards
- [ ] Establish border radius and shadow systems

## Theme Implementation
- [ ] Build theme manager system
- [ ] Implement light theme
- [ ] Implement dark theme
- [ ] Implement high contrast theme
- [ ] Add theme switching functionality

## Component Library
- [ ] Create standardized button components
- [ ] Build input component system
- [ ] Implement panel components
- [ ] Create dialog components
- [ ] Build icon system and management

## Layout System
- [ ] Implement window manager
- [ ] Create docking system standards
- [ ] Define layout persistence
- [ ] Build responsive layout system

## Interaction Patterns
- [ ] Standardize hover states
- [ ] Implement focus management
- [ ] Create animation system
- [ ] Add keyboard navigation
- [ ] Build tooltip system

## Accessibility Features
- [ ] Implement keyboard navigation
- [ ] Add focus ring system
- [ ] Create high contrast theme
- [ ] Add font scaling support
- [ ] Implement reduced motion support
- [ ] Add accessibility menu

## Cross-Platform Consistency
- [ ] ImGui component styling
- [ ] Silk.NET component integration
- [ ] SystemUI component alignment
- [ ] Font and icon management

## Testing and Validation
- [ ] Accessibility compliance testing
- [ ] Cross-platform visual testing
- [ ] Performance benchmarking
- [ ] User feedback integration
- [ ] Documentation completion

## Deployment and Migration
- [ ] Create migration guides
- [ ] Implement gradual rollout
- [ ] Add configuration options
- [ ] Create upgrade paths
- [ ] Monitor adoption metrics
```

## 10. Implementation Guidelines

### 10.1 Development Workflow

1. **Phase 1: Foundation** (Weeks 1-2)
   - Implement design tokens system
   - Create base theme manager
   - Build component interface definitions

2. **Phase 2: Core Components** (Weeks 3-4)
   - Implement button components
   - Create input component system
   - Build panel and layout components

3. **Phase 3: Integration** (Weeks 5-6)
   - Apply styling to existing components
   - Implement cross-platform consistency
   - Add animation and interaction patterns

4. **Phase 4: Accessibility** (Weeks 7-8)
   - Implement keyboard navigation
   - Add accessibility features
   - Create testing procedures

5. **Phase 5: Optimization** (Weeks 9-10)
   - Performance optimization
   - Documentation completion
   - User feedback integration

### 10.2 Code Examples

```csharp
// Example: Migrating existing button to new system
// Before:
if (ImGui.Button("Create"))
{
    CreateProject();
}

// After:
var createButton = new ButtonComponent
{
    Text = "Create",
    Variant = ButtonVariant.Primary,
    Size = ButtonSize.Medium,
    Tooltip = "Create new project"
};

createButton.Clicked += () => CreateProject();
componentRenderer.Render(createButton);
```

### 10.3 Migration Strategy

1. **Identify Components**: Catalog all existing UI components
2. **Map to Standards**: Match components to new design system
3. **Incremental Migration**: Replace components one module at a time
4. **Validation**: Test visual consistency after each change
5. **User Testing**: Gather feedback on new components
6. **Rollout**: Gradual deployment with configuration options

## Conclusion

This UI component consistency standardization document provides a comprehensive foundation for creating a unified, accessible, and performant user interface for TiXL. By implementing these design system standards, development guidelines, and component patterns, TiXL will achieve:

- **Visual Consistency**: Unified look and feel across all UI modules
- **Enhanced Accessibility**: Support for users with different needs and preferences
- **Improved Developer Experience**: Clear patterns and reusable components
- **Better User Experience**: Predictable interactions and responsive feedback
- **Future-Proof Architecture**: Extensible system that can evolve with requirements

The implementation should follow a phased approach, starting with foundation elements and building up to full system integration, ensuring smooth adoption and minimal disruption to existing workflows.
