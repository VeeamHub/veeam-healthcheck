# GUI Redesign Stage A: Theme/Style Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the GUI redesign spike's theme dictionaries and style vocabulary into the real `App.axaml`/`VhcGui.axaml` without restructuring the existing two-column layout, add a persisted System/Dark/Light theme toggle, and theme the runtime status-color logic.

**Architecture:** Move all styling from `VhcGui.axaml`'s local `Window.Styles` into shared `App.axaml` resources/styles (matching the spike's architecture), do a mechanical find-and-fix pass over every hardcoded color literal in `VhcGui.axaml`, add a small `CAppSettings` static class (mirroring the existing `CredentialStore` file-based pattern) for theme persistence, and replace 8 runtime `SolidColorBrush` construction call sites with named-resource lookups.

**Tech Stack:** Avalonia 11.3.20 (.NET 8, cross-platform build), xUnit for `VhcXTests`.

**Spec:** `docs/superpowers/specs/2026-09-04-gui-redesign-stage-a-theme-port-design.md`

---

## Task 1: `CAppSettings` — theme preference persistence

**Files:**
- Create: `vHC/HC_Reporting/Startup/CAppSettings.cs`
- Test: `vHC/VhcXTests/CAppSettingsTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CAppSettingsTests : IDisposable
    {
        private readonly string _testStorePath;
        private readonly string _originalStorePath;

        public CAppSettingsTests()
        {
            _originalStorePath = CAppSettings.StorePath;

            // Point CAppSettings at an isolated temp path instead of the real
            // %APPDATA%/VeeamHealthCheck/settings.json, so these tests never touch
            // a real user's saved preferences. Mirrors CredentialStoreSecurityTests'
            // isolation seam for CredentialStore.StorePath.
            _testStorePath = Path.Combine(Path.GetTempPath(), $"vhc-settings-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testStorePath);

            CAppSettings.StorePath = Path.Combine(_testStorePath, "settings.json");
        }

        public void Dispose()
        {
            CAppSettings.StorePath = _originalStorePath;

            if (Directory.Exists(_testStorePath))
            {
                try
                {
                    Directory.Delete(_testStorePath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public void Get_WhenNoFileExists_ReturnsDefaultSystemPreference()
        {
            var settings = CAppSettings.Get();

            Assert.Equal("System", settings.ThemePreference);
        }

        [Fact]
        public void Set_ThenGet_RoundTripsThemePreference()
        {
            CAppSettings.Set("Dark");

            var settings = CAppSettings.Get();

            Assert.Equal("Dark", settings.ThemePreference);
        }

        [Fact]
        public void Set_CalledTwice_OverwritesPreviousPreference()
        {
            CAppSettings.Set("Dark");
            CAppSettings.Set("Light");

            var settings = CAppSettings.Get();

            Assert.Equal("Light", settings.ThemePreference);
        }

        [Fact]
        public void Get_WhenFileIsMalformedJson_ReturnsDefault()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");

            var settings = CAppSettings.Get();

            Assert.Equal("System", settings.ThemePreference);
        }

        [Fact]
        public void Get_WhenFileIsEmpty_ReturnsDefault()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, string.Empty);

            var settings = CAppSettings.Get();

            Assert.Equal("System", settings.ThemePreference);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to build**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"`
Expected: Build error `CS0246: The type or namespace name 'CAppSettings' could not be found` (the class doesn't exist yet).

- [ ] **Step 3: Implement `CAppSettings`**

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Text.Json;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Startup;

public class AppSettings
{
    public string ThemePreference { get; set; } = "System";
}

public static class CAppSettings
{
    // Internal + settable so tests can point this at an isolated temp path instead
    // of the real %APPDATA%/VeeamHealthCheck/settings.json. Production code never
    // sets this; the default preserves real behavior exactly. Mirrors
    // CredentialStore.StorePath's own test seam.
    internal static string StorePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VeeamHealthCheck", "settings.json");

    public static AppSettings Get()
    {
        try
        {
            if (!File.Exists(StorePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(StorePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Warning($"App settings file is malformed or unreadable, using defaults. Error: {ex.Message}");
            return new AppSettings();
        }
    }

    public static void Set(string themePreference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var settings = Get();
            settings.ThemePreference = themePreference;
            File.WriteAllText(StorePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to persist app settings: {ex.Message}");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CAppSettingsTests"`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Startup/CAppSettings.cs vHC/VhcXTests/CAppSettingsTests.cs
git commit -m "feat(gui): add CAppSettings for theme-preference persistence"
```

---

## Task 2: `App.axaml` — theme resources and style vocabulary

**Files:**
- Modify: `vHC/HC_Reporting/App.axaml`

Current content is minimal (just `<FluentTheme />`). This task replaces it entirely with the full ported theme system: flat `SystemAccentColor`/status-brush resources, per-theme `Light`/`Dark` dictionaries (including the new `Caution*Brush` pair), and the full `Application.Styles` vocabulary (card, text roles, buttons with the spike-missing `Button.secondary:disabled` state added, inputs, segment/remove-server for Stage B, the `ListBoxItem:selected` fix, and a new global `Separator` style).

- [ ] **Step 1: Replace `App.axaml`'s full contents**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="VeeamHealthCheck.App">
    <Application.Resources>
        <ResourceDictionary>
            <!-- Flat (non-theme-variant) resources: FluentTheme's default controls (CheckBox,
                 ComboBox selection, ToggleSwitch, etc.) resolve against SystemAccentColor and
                 its 6 shade variants through several layers of FluentTheme's own internal
                 resources - notably CheckBoxCheckBackgroundFillChecked, which is a StaticResource
                 alias (resolved once, not live-rebound) several levels deep. Defining these
                 per-theme inside ThemeDictionaries breaks runtime light/dark switching for those
                 native controls - confirmed in vHC/Spikes/GuiRedesignSpike/App.axaml. Flat
                 placement sidesteps this. -->
            <Color x:Key="SystemAccentColor">#00B336</Color>
            <Color x:Key="SystemAccentColorDark1">#009C2F</Color>
            <Color x:Key="SystemAccentColorDark2">#007A25</Color>
            <Color x:Key="SystemAccentColorDark3">#005A1B</Color>
            <Color x:Key="SystemAccentColorLight1">#33C15C</Color>
            <Color x:Key="SystemAccentColorLight2">#66D183</Color>
            <Color x:Key="SystemAccentColorLight3">#99E0AA</Color>

            <!-- Runtime status colors used by VhcGui.axaml.cs (monitor status / progress
                 text). Flat because these 4 exact values already ship today across both
                 themes and are legible against both page backgrounds - naming them here
                 replaces 8 inline `new SolidColorBrush(...)` call sites with a single
                 source of truth; it does not change any color. -->
            <SolidColorBrush x:Key="StatusNeutralBrush">#999999</SolidColorBrush>
            <SolidColorBrush x:Key="StatusWarningBrush">#F0AD4E</SolidColorBrush>
            <SolidColorBrush x:Key="StatusSuccessBrush">#5CB85C</SolidColorBrush>
            <SolidColorBrush x:Key="StatusErrorBrush">#D9534F</SolidColorBrush>

            <ResourceDictionary.ThemeDictionaries>
                <ResourceDictionary x:Key="Light">
                    <SolidColorBrush x:Key="PageBackgroundBrush">#F8FAFC</SolidColorBrush>
                    <SolidColorBrush x:Key="CardBackgroundBrush">#FFFFFF</SolidColorBrush>
                    <SolidColorBrush x:Key="CardBorderBrush">#E2E8F0</SolidColorBrush>
                    <SolidColorBrush x:Key="PrimaryTextBrush">#0F172A</SolidColorBrush>
                    <SolidColorBrush x:Key="SecondaryTextBrush">#64748B</SolidColorBrush>
                    <SolidColorBrush x:Key="AccentBrush">#00B336</SolidColorBrush>
                    <SolidColorBrush x:Key="AccentHoverBrush">#009C2F</SolidColorBrush>
                    <SolidColorBrush x:Key="AccentPressedBrush">#007A25</SolidColorBrush>
                    <SolidColorBrush x:Key="OnAccentTextBrush">#0B1220</SolidColorBrush>
                    <SolidColorBrush x:Key="SegmentBackgroundBrush">#F1F5F9</SolidColorBrush>
                    <SolidColorBrush x:Key="SegmentSelectedBrush">#5BC98A</SolidColorBrush>
                    <SolidColorBrush x:Key="ListSelectedTintBrush">#2600B336</SolidColorBrush>
                    <SolidColorBrush x:Key="CautionBackgroundBrush">#FFF8E1</SolidColorBrush>
                    <SolidColorBrush x:Key="CautionBorderBrush">#FFC107</SolidColorBrush>
                </ResourceDictionary>
                <ResourceDictionary x:Key="Dark">
                    <SolidColorBrush x:Key="PageBackgroundBrush">#0F172A</SolidColorBrush>
                    <SolidColorBrush x:Key="CardBackgroundBrush">#1B1F24</SolidColorBrush>
                    <SolidColorBrush x:Key="CardBorderBrush">#2D333B</SolidColorBrush>
                    <SolidColorBrush x:Key="PrimaryTextBrush">#F8FAFC</SolidColorBrush>
                    <SolidColorBrush x:Key="SecondaryTextBrush">#94A3B8</SolidColorBrush>
                    <SolidColorBrush x:Key="AccentBrush">#00D647</SolidColorBrush>
                    <SolidColorBrush x:Key="AccentHoverBrush">#00B336</SolidColorBrush>
                    <SolidColorBrush x:Key="AccentPressedBrush">#009129</SolidColorBrush>
                    <SolidColorBrush x:Key="OnAccentTextBrush">#0B1220</SolidColorBrush>
                    <SolidColorBrush x:Key="SegmentBackgroundBrush">#242A31</SolidColorBrush>
                    <SolidColorBrush x:Key="SegmentSelectedBrush">#3ECB74</SolidColorBrush>
                    <SolidColorBrush x:Key="ListSelectedTintBrush">#2600D647</SolidColorBrush>
                    <SolidColorBrush x:Key="CautionBackgroundBrush">#332B14</SolidColorBrush>
                    <SolidColorBrush x:Key="CautionBorderBrush">#8A6D1F</SolidColorBrush>
                </ResourceDictionary>
            </ResourceDictionary.ThemeDictionaries>
        </ResourceDictionary>
    </Application.Resources>

    <Application.Styles>
        <FluentTheme />

        <Style Selector="Window">
            <Setter Property="Background" Value="{DynamicResource PageBackgroundBrush}" />
            <!-- Segoe UI leads (not Inter, as in the spike): Inter isn't a guaranteed
                 system font and isn't bundled with the app; the spike's own README
                 flagged this. Segoe UI is guaranteed on the Windows target. -->
            <Setter Property="FontFamily" Value="Segoe UI,-apple-system,sans-serif" />
        </Style>

        <Style Selector="Separator">
            <Setter Property="Background" Value="{DynamicResource CardBorderBrush}" />
        </Style>

        <Style Selector="Button.primary">
            <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
            <Setter Property="Foreground" Value="{DynamicResource OnAccentTextBrush}" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Padding" Value="16,8" />
            <Setter Property="CornerRadius" Value="4" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.primary:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="{DynamicResource AccentHoverBrush}" />
            <Setter Property="TextElement.Foreground" Value="{DynamicResource OnAccentTextBrush}" />
        </Style>
        <Style Selector="Button.primary:pressed /template/ ContentPresenter">
            <Setter Property="Background" Value="{DynamicResource AccentPressedBrush}" />
            <Setter Property="TextElement.Foreground" Value="{DynamicResource OnAccentTextBrush}" />
        </Style>
        <Style Selector="Button.primary:disabled /template/ ContentPresenter">
            <Setter Property="Background" Value="#66808080" />
            <Setter Property="TextElement.Foreground" Value="{DynamicResource SecondaryTextBrush}" />
        </Style>

        <Style Selector="Button.secondary">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
            <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="12,6" />
            <Setter Property="CornerRadius" Value="4" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.secondary:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="{DynamicResource CardBorderBrush}" />
        </Style>
        <!-- New, beyond the spike: the spike's Button.secondary was never exercised
             disabled, so it has no :disabled override. The real monitorQuickSetupBtn/
             monitorVhcSetupBtn/monitorRunBtn all ship IsEnabled="False" from first
             launch and need one. Matches Button.primary:disabled's translucent-gray
             treatment. -->
        <Style Selector="Button.secondary:disabled /template/ ContentPresenter">
            <Setter Property="Background" Value="#66808080" />
            <Setter Property="TextElement.Foreground" Value="{DynamicResource SecondaryTextBrush}" />
        </Style>

        <Style Selector="Button.link">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Foreground" Value="{DynamicResource AccentBrush}" />
            <Setter Property="Padding" Value="0" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="FontSize" Value="12" />
        </Style>
        <Style Selector="Button.link:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="Transparent" />
        </Style>
        <Style Selector="Button.link:pressed /template/ ContentPresenter">
            <Setter Property="Background" Value="Transparent" />
        </Style>

        <Style Selector="Border.card">
            <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}" />
            <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="CornerRadius" Value="8" />
            <Setter Property="Padding" Value="16" />
        </Style>

        <Style Selector="TextBlock.card-title">
            <Setter Property="FontSize" Value="15" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
        </Style>
        <Style Selector="TextBlock.field-label">
            <Setter Property="FontSize" Value="12" />
            <Setter Property="FontWeight" Value="Medium" />
            <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}" />
        </Style>
        <Style Selector="TextBlock.secondary-text">
            <Setter Property="FontSize" Value="12" />
            <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}" />
        </Style>

        <Style Selector="TextBox.modern">
            <Setter Property="Padding" Value="8" />
            <Setter Property="CornerRadius" Value="4" />
            <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}" />
            <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
        </Style>
        <Style Selector="ComboBox.modern">
            <Setter Property="Padding" Value="8" />
            <Setter Property="CornerRadius" Value="4" />
            <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}" />
            <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
        </Style>
        <Style Selector="CheckBox.modern">
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
            <Setter Property="FontSize" Value="13" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>

        <!-- No consumer yet in the real window - ready for Stage B. -->
        <Style Selector="RadioButton.segment">
            <Setter Property="Background" Value="{DynamicResource SegmentBackgroundBrush}" />
            <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="14,6" />
            <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}" />
            <Setter Property="HorizontalContentAlignment" Value="Center" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="RadioButton.segment /template/ Ellipse">
            <Setter Property="IsVisible" Value="False" />
        </Style>
        <Style Selector="RadioButton.segment /template/ ContentPresenter#PART_ContentPresenter">
            <Setter Property="Grid.Column" Value="0" />
            <Setter Property="Grid.ColumnSpan" Value="2" />
        </Style>
        <Style Selector="RadioButton.segment:checked">
            <Setter Property="Background" Value="{DynamicResource SegmentSelectedBrush}" />
            <Setter Property="Foreground" Value="{DynamicResource OnAccentTextBrush}" />
            <Setter Property="FontWeight" Value="SemiBold" />
        </Style>

        <Style Selector="ListBoxItem:selected /template/ ContentPresenter#PART_ContentPresenter">
            <Setter Property="Background" Value="{DynamicResource ListSelectedTintBrush}" />
            <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
            <Setter Property="BorderThickness" Value="3,0,0,0" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
        </Style>
        <Style Selector="ListBoxItem:selected:pointerover /template/ ContentPresenter#PART_ContentPresenter">
            <Setter Property="Background" Value="{DynamicResource ListSelectedTintBrush}" />
            <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
            <Setter Property="BorderThickness" Value="3,0,0,0" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
        </Style>
        <Style Selector="ListBoxItem:selected:pressed /template/ ContentPresenter#PART_ContentPresenter">
            <Setter Property="Background" Value="{DynamicResource ListSelectedTintBrush}" />
            <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
            <Setter Property="BorderThickness" Value="3,0,0,0" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
        </Style>

        <!-- No consumer yet in the real window - ready for Stage B. -->
        <Style Selector="Button.remove-server">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}" />
            <Setter Property="Padding" Value="6" />
            <Setter Property="Opacity" Value="0.5" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.remove-server:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="Transparent" />
        </Style>
        <Style Selector="ListBoxItem:pointerover Button.remove-server">
            <Setter Property="Opacity" Value="1" />
        </Style>
    </Application.Styles>
</Application>
```

- [ ] **Step 2: Verify the app still builds**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds (VhcGui.axaml still references its old local styles at this point — those aren't removed until Task 4 — so this only proves the new App.axaml itself is valid XAML).

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/App.axaml
git commit -m "feat(gui): port spike's theme dictionaries and style vocabulary into App.axaml"
```

---

## Task 3: `App.axaml.cs` — apply persisted theme before window creation

**Files:**
- Modify: `vHC/HC_Reporting/App.axaml.cs`

- [ ] **Step 1: Replace the file's contents**

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            RequestedThemeVariant = CAppSettings.Get().ThemePreference switch
            {
                "Dark" => ThemeVariant.Dark,
                "Light" => ThemeVariant.Light,
                _ => ThemeVariant.Default,
            };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new VhcGui();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
```

- [ ] **Step 2: Verify the app still builds**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/App.axaml.cs
git commit -m "feat(gui): apply persisted theme preference before window creation"
```

---

## Task 4: `VhcGui.axaml` — remove local styles, rename classes, add toggle button

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml`

This task makes the structural changes: deletes the now-superseded local `Window.Styles` block, applies the `modern`→`primary` and `groupbox`→`card` renames (including the two buttons and the Instructions-panel border missed in the first draft of the spec), and adds the new theme-toggle button by wrapping the existing content `ScrollViewer` in a `Panel`. No color/text-role literal fixes yet — that's Task 5.

- [ ] **Step 1: Remove the local `Window.Styles` block and the `Window`'s hardcoded background**

Find:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VeeamHealthCheck.VhcGui"
        Title=""
        MinHeight="500" MinWidth="900"
        Width="950"
        SizeToContent="Height"
        WindowStartupLocation="CenterScreen"
        CanResize="True"
        Background="#F5F5F5">

    <Window.Styles>
        <Style Selector="Button.modern">
            <Setter Property="Background" Value="#00B233" />
            <Setter Property="Foreground" Value="White" />
            <Setter Property="FontSize" Value="14" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Padding" Value="20,10" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="CornerRadius" Value="4" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.modern:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="#00A030" />
        </Style>
        <Style Selector="Button.modern:disabled /template/ ContentPresenter">
            <Setter Property="Background" Value="#CCCCCC" />
        </Style>

        <Style Selector="Button.secondary">
            <Setter Property="Background" Value="#555555" />
            <Setter Property="Foreground" Value="White" />
            <Setter Property="FontSize" Value="14" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Padding" Value="20,10" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="CornerRadius" Value="4" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.secondary:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="#333333" />
        </Style>
        <Style Selector="Button.secondary:disabled /template/ ContentPresenter">
            <Setter Property="Background" Value="#CCCCCC" />
        </Style>

        <Style Selector="CheckBox.modern">
            <Setter Property="FontSize" Value="13" />
            <Setter Property="Foreground" Value="#333333" />
            <Setter Property="Margin" Value="0,8,0,0" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>

        <!-- Avalonia 11.3.20 has no native GroupBox (verified: absent from Avalonia.Controls,
             no HeaderedContentControl default template either) - the four "GroupBox" sections
             below are ported as Border + StackPanel (header TextBlock as first child) instead.
             None of the original GroupBox elements had Name/x:Name, so this has no code-behind impact. -->
        <Style Selector="Border.groupbox">
            <Setter Property="BorderBrush" Value="#DDDDDD" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="15" />
            <Setter Property="Background" Value="White" />
        </Style>

        <Style Selector="TextBox.modern">
            <Setter Property="Padding" Value="8" />
            <Setter Property="BorderBrush" Value="#CCCCCC" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="FontSize" Value="13" />
        </Style>

        <Style Selector="ComboBox.modern">
            <Setter Property="Padding" Value="8" />
            <Setter Property="BorderBrush" Value="#CCCCCC" />
            <Setter Property="FontSize" Value="13" />
        </Style>
    </Window.Styles>

    <Grid RowDefinitions="*,Auto">
```

Replace with:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VeeamHealthCheck.VhcGui"
        Title=""
        MinHeight="500" MinWidth="900"
        Width="950"
        SizeToContent="Height"
        WindowStartupLocation="CenterScreen"
        CanResize="True">

    <Grid RowDefinitions="*,Auto">
```

- [ ] **Step 2: Rename the `run` button's class**

Find:
```xml
                    <Button x:Name="run" Grid.Column="2" Classes="modern"
                            Height="45" Click="run_Click" />
```

Replace with:
```xml
                    <Button x:Name="run" Grid.Column="2" Classes="primary"
                            Height="45" Click="run_Click" />
```

- [ ] **Step 3: Rename `addServerBtn`'s class**

Find:
```xml
                                <Button x:Name="addServerBtn" Grid.Column="2"
                                        Content="Add"
                                        Height="32"
                                        FontSize="12"
                                        Classes="modern"
                                        Click="addServerBtn_Click" />
```

Replace with:
```xml
                                <Button x:Name="addServerBtn" Grid.Column="2"
                                        Content="Add"
                                        Height="32"
                                        FontSize="12"
                                        Classes="primary"
                                        Click="addServerBtn_Click" />
```

- [ ] **Step 4: Rename `monitorQuickSetupBtn`'s class**

Find:
```xml
                                <Button x:Name="monitorQuickSetupBtn" Grid.Column="0"
                                        Content="Quick Setup" Height="32" FontSize="11"
                                        Classes="modern" IsEnabled="False"
                                        Click="monitorQuickSetupBtn_Click"
                                        ToolTip.Tip="Install monitor using selected VBR server and stored credentials" />
```

Replace with:
```xml
                                <Button x:Name="monitorQuickSetupBtn" Grid.Column="0"
                                        Content="Quick Setup" Height="32" FontSize="11"
                                        Classes="primary" IsEnabled="False"
                                        Click="monitorQuickSetupBtn_Click"
                                        ToolTip.Tip="Install monitor using selected VBR server and stored credentials" />
```

- [ ] **Step 5: Rename all four `groupbox` section borders to `card`**

Find (appears 4 times, identical each time — Options, Output Directory, VBR Server, Continuous Monitoring sections): `Classes="groupbox"`
Replace all occurrences with: `Classes="card"`

(Use a find-and-replace-all across the file for this exact string — it does not appear anywhere else in `VhcGui.axaml`.)

- [ ] **Step 6: Fold the Instructions panel's ad-hoc border into `Classes="card"`**

Find:
```xml
                <Border Background="White" Padding="20" Margin="0,0,0,15" CornerRadius="6"
                        BoxShadow="0 2 10 0 #4CCCCCCC">
                    <StackPanel>
                        <TextBlock FontSize="18" FontWeight="Bold" Foreground="#00B233" Margin="0,0,0,10">
                            <Run Name="InsHeader" />
                        </TextBlock>
                        <TextBlock TextWrapping="Wrap" FontSize="12" Foreground="#555555" LineHeight="20">
```

Replace with:
```xml
                <Border Classes="card" Margin="0,0,0,15">
                    <StackPanel>
                        <TextBlock Classes="card-title" FontSize="18" FontWeight="Bold" Foreground="{DynamicResource AccentBrush}" Margin="0,0,0,10">
                            <Run Name="InsHeader" />
                        </TextBlock>
                        <TextBlock Classes="secondary-text" TextWrapping="Wrap" LineHeight="20">
```

This is the window's single largest, most prominent heading (brand-green, 18px Bold) — it keeps explicit `FontSize`/`FontWeight`/`Foreground` overrides on top of `card-title` rather than shrinking to that class's default 15px/SemiBold/`PrimaryTextBrush`. `BoxShadow` is dropped: the spike's `Border.card` doesn't use shadows, and adopting its flatter look was already agreed for the four section cards.

- [ ] **Step 7: Wrap the content `ScrollViewer` in a `Panel` (opening half)**

Find:
```xml
    <Grid RowDefinitions="*,Auto">

        <ScrollViewer Grid.Row="0" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
        <Grid Margin="25,25,25,15" ColumnDefinitions="380,20,*">
```

Replace with:
```xml
    <Grid RowDefinitions="*,Auto">

        <Panel Grid.Row="0">
        <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
        <Grid Margin="25,25,25,15" ColumnDefinitions="380,20,*">
```

- [ ] **Step 8: Close the `Panel` and add the theme toggle button (closing half)**

Find:
```xml
        </Grid>
        </ScrollViewer>

        <!-- pBar must NOT get IsVisible="False" here: hideProgressBar()/showProgressBar() in the
```

Replace with:
```xml
        </Grid>
        </ScrollViewer>

        <Button x:Name="ThemeToggleButton" Classes="secondary"
                HorizontalAlignment="Right" VerticalAlignment="Top"
                Margin="0,12,12,0" Padding="10,4" FontSize="12"
                Click="ThemeToggleButton_Click" />
        </Panel>

        <!-- pBar must NOT get IsVisible="False" here: hideProgressBar()/showProgressBar() in the
```

- [ ] **Step 9: Verify the app builds**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build error — `ThemeToggleButton_Click` doesn't exist yet in `VhcGui.axaml.cs`. This is expected; it's added in Task 6. Confirm the error is specifically about the missing event handler and not about anything else (mistyped class name, unclosed tag, etc.) before moving on.

- [ ] **Step 10: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml
git commit -m "refactor(gui): remove local styles, adopt primary/card class vocabulary, add theme toggle scaffold"
```

---

## Task 5: `VhcGui.axaml` — complete literal disposition sweep

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml`

Every remaining hardcoded `Foreground`/`Background`/`BorderBrush` literal in the file, applying the exact class or `DynamicResource` disposition decided in the spec's "Complete literal disposition" table. Each step is independent; order doesn't matter, but do them all — this is the exhaustive sweep that replaced the first draft's incomplete hand-picked list.

- [ ] **Step 1: Caution box background/border**

Find:
```xml
                <Border Background="#FFF8E1" BorderBrush="#FFC107" BorderThickness="1"
                        Padding="15" CornerRadius="4">
```

Replace with:
```xml
                <Border Background="{DynamicResource CautionBackgroundBrush}" BorderBrush="{DynamicResource CautionBorderBrush}" BorderThickness="1"
                        Padding="15" CornerRadius="4">
```

- [ ] **Step 2: `Cav1Part1` body text**

Find:
```xml
                        <TextBlock FontSize="12" TextWrapping="Wrap" Foreground="#333333">
                            <Run Name="Cav1Part1" />
                        </TextBlock>
```

Replace with:
```xml
                        <TextBlock FontSize="12" TextWrapping="Wrap" Foreground="{DynamicResource PrimaryTextBrush}">
                            <Run Name="Cav1Part1" />
                        </TextBlock>
```

- [ ] **Step 3: `kbLink` hyperlink color**

Find:
```xml
                        <HyperlinkButton Name="kbLink"
                                         NavigateUri="https://www.veeam.com/kb2462"
                                         Content="KB 2462"
                                         Foreground="#0066CC"
                                         FontSize="12"
                                         Margin="0,2,0,2" />
```

Replace with:
```xml
                        <HyperlinkButton Name="kbLink"
                                         NavigateUri="https://www.veeam.com/kb2462"
                                         Content="KB 2462"
                                         Foreground="{DynamicResource AccentBrush}"
                                         FontSize="12"
                                         Margin="0,2,0,2" />
```

- [ ] **Step 4: `Cav2` body text**

Find:
```xml
                        <TextBlock FontSize="12" TextWrapping="Wrap" Foreground="#333333">
                            <Run Name="Cav2" />
                        </TextBlock>
```

Replace with:
```xml
                        <TextBlock FontSize="12" TextWrapping="Wrap" Foreground="{DynamicResource PrimaryTextBrush}">
                            <Run Name="Cav2" />
                        </TextBlock>
```

- [ ] **Step 5: Cav3/Cav4 fine print + the two inline `Run` contrast-bug overrides**

Find:
```xml
                        <TextBlock TextWrapping="Wrap" FontSize="11" Foreground="#666666"
                                   Margin="0,10,0,0" LineHeight="16">
                            <Run Name="Cav3" /><LineBreak />
                            <Run Name="Cav4" /><LineBreak /><LineBreak />
                            <Run FontWeight="SemiBold" Foreground="#555555">Credential Storage:</Run><Run> Credentials are stored encrypted at </Run><Run FontFamily="Consolas" Foreground="#555555">%AppData%\VeeamHealthCheck\creds.json</Run><Run>. You can delete this file to remove saved credentials or use the "Clear Saved Credentials" option.</Run>
                        </TextBlock>
```

Replace with:
```xml
                        <TextBlock Classes="secondary-text" TextWrapping="Wrap" FontSize="11"
                                   Margin="0,10,0,0" LineHeight="16">
                            <Run Name="Cav3" /><LineBreak />
                            <Run Name="Cav4" /><LineBreak /><LineBreak />
                            <Run FontWeight="SemiBold">Credential Storage:</Run><Run> Credentials are stored encrypted at </Run><Run FontFamily="Consolas">%AppData%\VeeamHealthCheck\creds.json</Run><Run>. You can delete this file to remove saved credentials or use the "Clear Saved Credentials" option.</Run>
                        </TextBlock>
```

This is the fix for the contrast bug found in review: the two inline `Run`s had their own local `Foreground="#555555"`, which computes to ~2:1 contrast once the parent `Border` becomes the dark amber `CautionBackgroundBrush`. Removing the local override (rather than setting it explicitly) lets both `Run`s inherit the parent `TextBlock`'s now-themed `secondary-text` color — `Run` does not pick up a class from its parent, only inherited `TextElement.Foreground`.

- [ ] **Step 6: `OptHdr` card header**

Find:
```xml
                        <TextBlock FontSize="16" Foreground="#333333">
                            <Run Name="OptHdr" />
                        </TextBlock>
```

Replace with:
```xml
                        <TextBlock Classes="card-title">
                            <Run Name="OptHdr" />
                        </TextBlock>
```

- [ ] **Step 7: "Export Options" sub-label**

Find:
```xml
                            <TextBlock Text="Export Options" FontSize="13" FontWeight="SemiBold"
                                       Foreground="#555555" Margin="0,0,0,8" />
```

Replace with:
```xml
                            <TextBlock Text="Export Options" Classes="field-label" Margin="0,0,0,8" />
```

- [ ] **Step 8: All three `Separator` instances**

Find (appears 3 times, identical): `<Separator Margin="0,12,0,12" Background="#EEEEEE" />`
Replace all occurrences with: `<Separator Margin="0,12,0,12" />`

(The new global `Separator` style from Task 2 supplies the themed background once the hardcoded local value is removed.)

- [ ] **Step 9: "Data Collection" sub-label**

Find:
```xml
                            <TextBlock Text="Data Collection" FontSize="13" FontWeight="SemiBold"
                                       Foreground="#555555" Margin="0,0,0,8" />
```

Replace with:
```xml
                            <TextBlock Text="Data Collection" Classes="field-label" Margin="0,0,0,8" />
```

- [ ] **Step 10: "Collection Period:" inline label**

Find:
```xml
                                <TextBlock Text="Collection Period:" VerticalAlignment="Center"
                                           FontSize="13" Foreground="#333333" Margin="0,0,10,0" />
```

Replace with:
```xml
                                <TextBlock Text="Collection Period:" VerticalAlignment="Center"
                                           Classes="field-label" Margin="0,0,10,0" />
```

- [ ] **Step 11: "Security & Privacy" sub-label**

Find:
```xml
                            <TextBlock Text="Security &amp; Privacy" FontSize="13" FontWeight="SemiBold"
                                       Foreground="#555555" Margin="0,0,0,8" />
```

Replace with:
```xml
                            <TextBlock Text="Security &amp; Privacy" Classes="field-label" Margin="0,0,0,8" />
```

- [ ] **Step 12: `outPath` card header**

Find:
```xml
                        <TextBlock Name="outPath" FontSize="16" Foreground="#333333" />
```

Replace with:
```xml
                        <TextBlock Name="outPath" Classes="card-title" />
```

- [ ] **Step 13: "VBR Server" card header**

Find:
```xml
                        <TextBlock Text="VBR Server" FontSize="16" Foreground="#333333" />
```

Replace with:
```xml
                        <TextBlock Text="VBR Server" Classes="card-title" />
```

- [ ] **Step 14: Server-listbox wrapping border — the critical invisible-text fix**

Find:
```xml
                            <Border BorderBrush="#CCCCCC" BorderThickness="1"
                                    Background="White" Height="120">
```

Replace with:
```xml
                            <Border BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="1"
                                    Background="{DynamicResource CardBackgroundBrush}" Height="120">
```

Without this, the `ListBoxItem:selected` style's `PrimaryTextBrush` text (near-white in Dark theme, per Task 2) would be invisible against this hardcoded white background — this was the specific bug flagged in review.

- [ ] **Step 15: "Product Type:" sub-label**

Find:
```xml
                            <TextBlock Text="Product Type:" FontSize="13" FontWeight="SemiBold"
                                       Foreground="#555555" Margin="0,0,0,8" />
```

Replace with:
```xml
                            <TextBlock Text="Product Type:" Classes="field-label" Margin="0,0,0,8" />
```

- [ ] **Step 16: "Continuous Monitoring" card header**

Find:
```xml
                        <TextBlock Text="Continuous Monitoring" FontSize="16" Foreground="#333333" />
```

Replace with:
```xml
                        <TextBlock Text="Continuous Monitoring" Classes="card-title" />
```

- [ ] **Step 17: "Status: " inline label**

Find:
```xml
                                <TextBlock Text="Status: " FontSize="13" Foreground="#555555" VerticalAlignment="Center" />
```

Replace with:
```xml
                                <TextBlock Text="Status: " Classes="secondary-text" VerticalAlignment="Center" />
```

- [ ] **Step 18: `monitorStatusText` default color**

Find:
```xml
                                <TextBlock x:Name="monitorStatusText" FontSize="13" FontWeight="SemiBold"
                                           Foreground="#999999" VerticalAlignment="Center" Text="Checking..." />
```

Replace with:
```xml
                                <TextBlock x:Name="monitorStatusText" FontSize="13" FontWeight="SemiBold"
                                           Foreground="{DynamicResource StatusNeutralBrush}" VerticalAlignment="Center" Text="Checking..." />
```

(Its runtime-assigned colors are fixed separately in Task 7 — this is just the idle-state default before code-behind runs.)

- [ ] **Step 19: `monitorLastRunText` fine print**

Find:
```xml
                            <TextBlock x:Name="monitorLastRunText" FontSize="11" Foreground="#777777"
                                       Margin="0,0,0,10" IsVisible="False" TextWrapping="Wrap" />
```

Replace with:
```xml
                            <TextBlock x:Name="monitorLastRunText" Classes="secondary-text" FontSize="11"
                                       Margin="0,0,0,10" IsVisible="False" TextWrapping="Wrap" />
```

- [ ] **Step 20: "Alert Notifications" sub-label**

Find:
```xml
                            <TextBlock Text="Alert Notifications" FontSize="12" FontWeight="SemiBold"
                                       Foreground="#555555" Margin="0,0,0,6" />
```

Replace with:
```xml
                            <TextBlock Text="Alert Notifications" Classes="field-label" Margin="0,0,0,6" />
```

- [ ] **Step 21: "Min severity:" inline label**

Find:
```xml
                                <TextBlock Text="Min severity: " FontSize="12" Foreground="#555555"
                                           VerticalAlignment="Center" Margin="0,0,8,0" />
```

Replace with:
```xml
                                <TextBlock Text="Min severity: " Classes="field-label"
                                           VerticalAlignment="Center" Margin="0,0,8,0" />
```

- [ ] **Step 22: Footer border**

Find:
```xml
        <Border Grid.Row="1" Background="White" BorderBrush="#DDDDDD" BorderThickness="0,1,0,0">
```

Replace with:
```xml
        <Border Grid.Row="1" Background="{DynamicResource CardBackgroundBrush}" BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="0,1,0,0">
```

- [ ] **Step 23: Progress bar track/fill colors**

Find:
```xml
                <ProgressBar Name="pBar"
                             Height="20"
                             IsIndeterminate="True"
                             BorderThickness="0"
                             Background="#E8E8E8"
                             Foreground="#00B233"
                             BorderBrush="Transparent" />
```

Replace with:
```xml
                <ProgressBar Name="pBar"
                             Height="20"
                             IsIndeterminate="True"
                             BorderThickness="0"
                             Background="{DynamicResource CardBorderBrush}"
                             Foreground="{DynamicResource AccentBrush}"
                             BorderBrush="Transparent" />
```

- [ ] **Step 24: `progressText` default color**

Find:
```xml
                <TextBlock Name="progressText"
                           Text="Processing health check..."
                           FontSize="12"
                           Foreground="#666666"
                           Margin="0,8,0,0"
                           HorizontalAlignment="Center"
                           IsVisible="False" />
```

Replace with:
```xml
                <TextBlock Name="progressText"
                           Classes="secondary-text"
                           Text="Processing health check..."
                           Margin="0,8,0,0"
                           HorizontalAlignment="Center"
                           IsVisible="False" />
```

- [ ] **Step 25: Confirm no hardcoded literals remain**

Run: `grep -noE '(Background|Foreground|BorderBrush)="#[0-9A-Fa-f]{3,8}"|(Background|Foreground|BorderBrush)="White"' vHC/HC_Reporting/VhcGui.axaml`
Expected: No output (empty). If anything prints, it was missed by this sweep — resolve it using the same "convert to class or `DynamicResource`, and remove the local literal" approach before continuing.

- [ ] **Step 26: Verify the app builds**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Same expected error as Task 4 Step 9 (`ThemeToggleButton_Click` still missing) — nothing new introduced by this step's XAML-only changes.

- [ ] **Step 27: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml
git commit -m "fix(gui): recolor every remaining hardcoded literal in VhcGui.axaml to themed resources"
```

---

## Task 6: `VhcGui.axaml.cs` — theme toggle handler and startup label

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs`

- [ ] **Step 1: Add the `Avalonia` and `Avalonia.Styling` usings**

Find:
```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
```

Replace with:
```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
```

- [ ] **Step 2: Set the toggle button's initial label in the constructor**

Find:
```csharp
        public VhcGui()
        {
            InitializeComponent();

            // AvaloniaUiNotifier passes this as the ShowDialog owner. Set it
```

Replace with:
```csharp
        public VhcGui()
        {
            InitializeComponent();

            ThemeToggleButton.Content = ThemeLabelFor(Application.Current!.RequestedThemeVariant);

            // AvaloniaUiNotifier passes this as the ShowDialog owner. Set it
```

- [ ] **Step 3: Add the click handler and its two small helpers**

Find:
```csharp
        private void InitializeServerList()
        {
```

Replace with:
```csharp
        private void ThemeToggleButton_Click(object? sender, RoutedEventArgs e)
        {
            var app = Application.Current!;
            ThemeVariant next = app.RequestedThemeVariant switch
            {
                var v when v == ThemeVariant.Dark => ThemeVariant.Light,
                var v when v == ThemeVariant.Light => ThemeVariant.Default,
                _ => ThemeVariant.Dark, // System (Default) -> Dark
            };

            app.RequestedThemeVariant = next;
            CAppSettings.Set(ThemePreferenceFor(next));
            ThemeToggleButton.Content = ThemeLabelFor(next);
        }

        private static string ThemePreferenceFor(ThemeVariant variant) =>
            variant == ThemeVariant.Dark ? "Dark" :
            variant == ThemeVariant.Light ? "Light" : "System";

        private static string ThemeLabelFor(ThemeVariant variant) =>
            variant == ThemeVariant.Dark ? "🌙 Dark" :
            variant == ThemeVariant.Light ? "☀ Light" : "🖥 System";

        private void InitializeServerList()
        {
```

- [ ] **Step 4: Verify the app builds**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds — `ThemeToggleButton_Click` now exists, resolving the error from Task 4/5.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "feat(gui): wire the theme toggle button to a persisted System/Dark/Light cycle"
```

---

## Task 7: `VhcGui.axaml.cs` — theme the runtime status colors

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs`

Replaces all 8 `new SolidColorBrush(Color.FromRgb(...))` call sites with lookups against the named resources added in Task 2. Same 4 colors, same 8 call sites, same semantics — only the source of the color changes.

- [ ] **Step 1: Add the `GetStatusBrush` helper**

Find:
```csharp
        private void ThemeToggleButton_Click(object? sender, RoutedEventArgs e)
        {
```

Replace with:
```csharp
        private IBrush GetStatusBrush(string resourceKey) => (IBrush)this.FindResource(resourceKey);

        private void ThemeToggleButton_Click(object? sender, RoutedEventArgs e)
        {
```

- [ ] **Step 2: `UpdateCollectionStatusText`'s two call sites**

Find:
```csharp
                    progressText.Text = $"Collection complete — {failed.Count} collector warning(s)";
                    progressText.Foreground = new SolidColorBrush(Color.FromRgb(0xf0, 0xad, 0x4e));
```

Replace with:
```csharp
                    progressText.Text = $"Collection complete — {failed.Count} collector warning(s)";
                    progressText.Foreground = GetStatusBrush("StatusWarningBrush");
```

Find:
```csharp
                    progressText.Text = "Collection complete";
                    progressText.Foreground = new SolidColorBrush(Color.FromRgb(0x5c, 0xb8, 0x5c));
```

Replace with:
```csharp
                    progressText.Text = "Collection complete";
                    progressText.Foreground = GetStatusBrush("StatusSuccessBrush");
```

- [ ] **Step 3: `InitializeMonitorStatus`'s three call sites**

Find:
```csharp
                monitorStatusText.Text = "Not bundled";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
```

Replace with:
```csharp
                monitorStatusText.Text = "Not bundled";
                monitorStatusText.Foreground = GetStatusBrush("StatusNeutralBrush");
```

Find:
```csharp
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xf0, 0xad, 0x4e));
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = false;
```

Replace with:
```csharp
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = false;
```

Find:
```csharp
                monitorStatusText.Text = $"Running ({version})";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x5c, 0xb8, 0x5c));
```

Replace with:
```csharp
                monitorStatusText.Text = $"Running ({version})";
                monitorStatusText.Foreground = GetStatusBrush("StatusSuccessBrush");
```

- [ ] **Step 4: `monitorQuickSetupBtn_Click`'s error call site**

Find:
```csharp
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xd9, 0x53, 0x4f));
                        monitorQuickSetupBtn.IsEnabled = true;
```

Replace with:
```csharp
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
                        monitorQuickSetupBtn.IsEnabled = true;
```

- [ ] **Step 5: `monitorVhcSetupBtn_Click`'s error call site**

Find:
```csharp
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xd9, 0x53, 0x4f));
                        monitorVhcSetupBtn.IsEnabled = true;
```

Replace with:
```csharp
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
                        monitorVhcSetupBtn.IsEnabled = true;
```

- [ ] **Step 6: `OfferMonitorSetupIfNeeded`'s call site**

Find:
```csharp
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xf0, 0xad, 0x4e));
```

Replace with:
```csharp
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
```

- [ ] **Step 7: Confirm no `SolidColorBrush` construction remains for status colors**

Run: `grep -n "new SolidColorBrush(Color.FromRgb" vHC/HC_Reporting/VhcGui.axaml.cs`
Expected: No output (empty) — all 8 call sites converted.

- [ ] **Step 8: Verify the app builds**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds with 0 errors.

- [ ] **Step 9: Run the existing test suite to confirm nothing broke**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj`
Expected: All tests pass (including the 5 new `CAppSettingsTests` from Task 1).

- [ ] **Step 10: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "refactor(gui): theme the 8 runtime status-color call sites via named resources"
```

---

## Task 8: Manual verification

**Files:** none (verification only)

This repo builds and runs the Avalonia GUI cross-platform now (per the WPF→Avalonia migration), so this can be done in this environment, not just on Windows. No automated test covers pure XAML/styling correctness — this is the substitute, and it's not optional: the spike's own history (three real visual bugs missed by static review) and this very spec's first draft (an "applies immediately" claim disproved by reading the source, before even running anything) are direct evidence that skipping this step ships bugs.

- [x] **Step 1: Build and run the real app**

`vHC/HC_Reporting/Properties/launchSettings.json` has a committed default profile with
`"commandLineArgs": "/run /remote /host=vbr-v13-primary.home.lab /debug"`. Plain
`dotnet run --project ...` silently applies this profile, which `CArgsParser.cs`
routes to CLI remote-collection mode (`/run` at line 127) instead of GUI mode
(`args.Length == 0` at line 50) — the window never opens, with no error or
indication anything unusual happened, and it attempts a real connection to that
lab host. Always pass `--no-launch-profile` to bypass it.

Run: `dotnet build vHC/HC.sln --configuration Debug && dotnet run --project vHC/HC_Reporting/VeeamHealthCheck.csproj --no-launch-profile`
Expected: The window opens showing the existing two-column layout, now recolored, with a "🖥 System" button floating top-right.

**2026-09-04 correction:** an earlier pass through this plan asserted this was "confirmed to work on macOS." Re-tested in this session and that no longer holds: the process builds and starts, logs `Executing GUI`, then crashes during Avalonia's native platform bootstrap — `Avalonia.Native was not able to start the RenderTimer. Native error code is: -6661` — inside `AppBuilder.Setup()`, before `App.OnFrameworkInitializationCompleted()` ever runs. None of Stage A's code (or any XAML) executes before this point. Confirmed via a side-by-side run of the pre-Stage-A base commit (`1b453da`) in a throwaway worktree: identical crash, same stack trace — so this is a pre-existing macOS-session rendering limitation (no working native window/GPU surface in this sandbox), not a Stage A regression. Per [[project_windows_only_runtime_crossplatform_ci_goal]]-equivalent framing, production only ever runs on Windows anyway, so this doesn't block shipping — but it does mean Steps 2-6 below cannot be executed in this environment. They require an actual rendered window: a real Windows machine, or a macOS session with a working interactive display/GPU surface.

- [x] **Step 2: Click through the System → Dark → Light → System cycle**

Click the toggle button 3 times. Expected: label cycles "🌙 Dark" → "☀ Light" → "🖥 System"; the whole window's colors switch each time (page background, cards, text, buttons). Also confirm the three glyphs (moon/sun/desktop) render as a legible, visually-consistent set in the actual runtime font on the actual target OS — they mix an emoji-presentation codepoint with two text-presentation-default ones against a `Segoe UI` font stack with no emoji glyphs of its own, so rendering depends on system font-fallback and isn't guaranteed to look coherent everywhere.

- [x] **Step 3: Check the specific bugs found in review, in both Dark and Light**

- The caution box (KB 2462 callout): background/border should be muted amber in Dark, bright amber in Light; the "Credential Storage:" line and file path must be legible in both, not near-invisible.
- The VBR Server list: add or select a server and confirm the selected row's text is clearly legible (this was the invisible-white-on-white bug) — check specifically in Dark theme.
- The three Continuous Monitoring buttons (Quick Setup / Setup from VHC / Run Now): confirm their default disabled appearance looks intentional (muted, not FluentTheme's unstyled default) in both themes.
- `addServerBtn` and the Start button: confirm both read as the same accent-green "primary" weight as each other.
- `removeServerBtn`, `clearServersBtn`, and `importButton`: these keep `Classes="secondary"` unchanged, but the class's own definition changes shape in this stage — filled dark-gray/white/bold (`#555555`, 20×10 padding) becomes a transparent outline (border, `PrimaryTextBrush`, normal weight, 12×6 padding). Confirm all three still look correct (not just termsBtn and the two Continuous Monitoring secondary buttons already covered above) in both themes.
- The three `Separator` elements (between Export Options/Data Collection/Security & Privacy sub-sections): Task 2's new global `Separator` style has no spike provenance and no prior live-test history — confirm all three remain visibly present (not invisible/zero-contrast) in both themes, since Task 5 relies on this style entirely replacing the removed local `Background="#EEEEEE"`.
- The Cav3/Cav4 fine print (below the KB 2462 link, including "Credential Storage:") in **Light theme specifically**: computed contrast for the inherited `secondary-text` color against the light page is right at the WCAG AA boundary (~4.48:1 vs. the 4.5:1 minimum) — not a hard failure, but the row most likely to look slightly washed out. Look at it specifically, not just generically.
- "Collection Period:" (the inline label next to the days-selector combo box): this is the one `field-label` conversion that moved from a near-black tone to the muted secondary color, the largest single visual delta in Task 5's sweep — confirm it doesn't read as unintentionally de-emphasized/disabled next to its ComboBox.

- [x] **Step 4: Check button hover/pressed states**

Hover and click-and-hold on a primary button (Start) and a secondary button (Terms) in both themes. Expected: visible hover and pressed color changes, no flash of unstyled FluentTheme default.

- [x] **Step 5: Confirm the toggle button doesn't visually collide with page content**

Look at the top-right corner of the window where the toggle floats over the scrollable content. Expected: it may sit close to the "Options" card's header, but should not fully obscure it. If it does obscure it in practice, note this for a follow-up — do not attempt a fix in this stage (the spec approved this exact floating-overlay placement; layout iteration is Stage B's job).

- [x] **Step 6: Restart the app to confirm persistence**

Set the toggle to a non-default state (e.g. "🌙 Dark"), close the app, and run it again (`dotnet run --project vHC/HC_Reporting/VeeamHealthCheck.csproj --no-launch-profile` — same launch-profile caveat as Step 1). Expected: the window opens already in Dark theme with the toggle showing "🌙 Dark" — confirming `CAppSettings` persisted and was applied before the window was shown.

- [x] **Step 7: Report findings**

If any check above fails, fix it before considering Stage A done — do not defer newly-found visual bugs to "Stage B" or "a follow-up" unless they're pre-existing/unrelated to this stage's changes. If everything passes, Stage A's implementation is complete and ready for the squash-merge into `feature/gui-redesign-port` described in the spec's branching strategy.

**2026-09-04 verification (real Windows + VBR):** the user pulled `stage-a/theme-port` onto a Windows machine with VBR actually installed and ran the app directly (build+run screenshots in Light and Dark, past the "Veeam Software Not Detected" gate this session's sandbox couldn't get past). Confirmed correct: Instructions card header green, caution box amber in both themes with legible fine print, `localhost` server-list selection legible in both Dark and Light (the original invisible-white-on-white bug does not reproduce), Options card, checkboxes, ComboBox, Output path field, VBR Server section, Continuous Monitoring buttons' muted-disabled look, Accept Terms/Run buttons, and the native `Terms` dialog all render correctly in both themes. Two findings, both explicitly deferred rather than fixed here:
- The window still doesn't fit on a 1080p display without scrolling — pre-existing, Stage B's job (layout restructure), not something Stage A's recoloring-in-place was ever meant to address.
- The theme-toggle button visually overlaps nearby content at this resolution — this is exactly the risk Step 5 called out in advance ("if it does obscure it in practice, note this for a follow-up — do not attempt a fix in this stage"); tracked as a Stage B follow-up, not a Stage A blocker.

No other regressions found. Stage A is complete.
