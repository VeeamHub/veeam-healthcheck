# GUI Redesign Stage B: Layout Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the real `VhcGui.axaml` into the spike's fixed-window / header / tab-strip / two-column-per-tab / fixed-bottom-bar structure, fixing the two Stage A deferred findings (doesn't fit 1080p, theme toggle overlaps content) without changing any existing interaction model.

**Architecture:** Four tasks, each independently buildable: (1) add the missing `Button.tab` styles to `App.axaml`, (2) create a new, self-contained About/Disclaimer dialog that absorbs the Instructions & Warnings content, (3) restructure `VhcGui.axaml`'s window/header/tab-strip/bottom-bar chrome with tab content temporarily left as single-column interim panels, (4) split those interim panels into their final two-column card layout. Every existing `x:Name`/event handler is preserved exactly — only XAML parents move.

**Tech Stack:** Avalonia XAML + C# code-behind, .NET 8.0. No new packages.

**Verification note (read before starting):** This codebase has zero unit test coverage of `VhcGui` and no Avalonia headless test infrastructure — confirmed by grepping `vHC/VhcXTests/` for `VhcGui` and `Headless` (both empty). Stage A hit the same constraint and verified via `dotnet build` after each task, a full `dotnet test` regression run, subagent spec-compliance/code-quality review per task, and a final hand-off to the user for real-machine visual verification (this sandbox cannot render Avalonia at all — confirmed during Stage A). This plan follows the same pattern: no new unit tests are written (there is nothing meaningful to unit-test — every change here is control wiring and layout, not business logic), and each task's "test" step is a build/regression-suite check instead of a red/green test.

**Spec:** `docs/superpowers/specs/2026-09-04-gui-redesign-stage-b-layout-restructure-design.md`

---

### Task 1: Add tab button styles to `App.axaml`

**Files:**
- Modify: `vHC/HC_Reporting/App.axaml:153` (insert after the `Button.link:pressed` block, before `Border.card`)

`Button.tab`/`Button.tab.tab-active` do not exist yet — Stage A had no tabs to style. Copied verbatim from the spike (`vHC/Spikes/GuiRedesignSpike/App.axaml:82-102`).

- [ ] **Step 1: Insert the style block**

Insert immediately after this existing block (ends at line 153):

```xml
        <Style Selector="Button.link:pressed /template/ ContentPresenter">
            <Setter Property="Background" Value="Transparent" />
        </Style>
```

New content to insert right after it, before the existing `<Style Selector="Border.card">` block:

```xml

        <!-- Tab header buttons -->
        <Style Selector="Button.tab">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0,0,0,2" />
            <Setter Property="BorderBrush" Value="Transparent" />
            <Setter Property="CornerRadius" Value="0" />
            <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}" />
            <Setter Property="Padding" Value="4,8" />
            <Setter Property="FontSize" Value="14" />
            <Setter Property="Cursor" Value="Hand" />
        </Style>
        <Style Selector="Button.tab.tab-active">
            <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
            <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
            <Setter Property="FontWeight" Value="SemiBold" />
        </Style>
        <Style Selector="Button.tab:pointerover /template/ ContentPresenter">
            <Setter Property="TextElement.Foreground" Value="{DynamicResource SecondaryTextBrush}" />
        </Style>
        <Style Selector="Button.tab.tab-active:pointerover /template/ ContentPresenter">
            <Setter Property="TextElement.Foreground" Value="{DynamicResource PrimaryTextBrush}" />
        </Style>
```

- [ ] **Step 2: Build to verify no XAML errors**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds (0 errors). These styles have no consumers yet, so nothing should visibly change.

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/App.axaml
git commit -m "feat(gui): add tab button styles, ported from the redesign spike"
```

---

### Task 2: Create the About / Disclaimer dialog

**Files:**
- Create: `vHC/HC_Reporting/Functions/AboutDialog/AboutDisclaimerDialog.axaml`
- Create: `vHC/HC_Reporting/Functions/AboutDialog/AboutDisclaimerDialog.axaml.cs`

New self-contained dialog absorbing the Instructions & Warnings content that will be removed from the main window in Task 3. Placed under `Functions/` following this project's existing dialog convention (`Functions/CredsWindow/CredentialPromptWindow.axaml`), not the spike's separate `Dialogs/` folder.

Per the spec, this content comes from three sources today and each is ported exactly as-is: `GuiInstHeader`/`GuiInstLine1-6`/`GuiInstCaveat1`/`GuiInstCaveat2` are resx-backed; `Cav3`/`Cav4` are hardcoded C# string literals (no `GuiInstCaveat3`/`4` resx keys exist anywhere); the credential-storage paragraph is hardcoded inline XAML `Run`s with no `x:Name`. The enclosing caution-colored box (`CautionBackgroundBrush`/`CautionBorderBrush`) and `kbLink`'s `CautionLinkBrush` override are dropped — downgraded to plain informational styling per the approved design decision (an About dialog is informational, not an active warning). The individual text runs otherwise keep their existing `FontSize`/`Foreground`/`Classes` exactly as they are today.

- [ ] **Step 1: Create the dialog XAML**

Create `vHC/HC_Reporting/Functions/AboutDialog/AboutDisclaimerDialog.axaml`:

```xml
<!--
Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
MIT License
-->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VeeamHealthCheck.Functions.AboutDialog.AboutDisclaimerDialog"
        Title="About / Disclaimer"
        Width="480" SizeToContent="Height" MaxHeight="600"
        CanResize="False"
        WindowStartupLocation="CenterOwner">
    <Grid RowDefinitions="Auto,Auto" Margin="20">
        <ScrollViewer Grid.Row="0">
            <StackPanel Spacing="10">
                <TextBlock Classes="card-title" FontSize="18" FontWeight="Bold" Foreground="{DynamicResource AccentBrush}">
                    <Run Name="InsHeader" />
                </TextBlock>
                <TextBlock Classes="secondary-text" TextWrapping="Wrap" LineHeight="20">
                    <Run Name="line1" /><LineBreak />
                    <Run Name="line2" /><LineBreak />
                    <Run Name="line3" /><LineBreak />
                    <Run Name="line4" /><LineBreak />
                    <Run Name="line5" /><LineBreak />
                    <Run Name="line6" />
                </TextBlock>
                <!-- This was inside a caution-colored Border (CautionBackgroundBrush/
                     CautionBorderBrush) in VhcGui.axaml. Downgraded to plain informational
                     styling here: an About dialog is informational, not an active warning
                     (design decision, see spec). The individual Run styling below is
                     otherwise unchanged from the original. -->
                <TextBlock FontSize="12" TextWrapping="Wrap" Foreground="{DynamicResource PrimaryTextBrush}" LineHeight="20">
                    <Run Name="Cav1Part1" />
                </TextBlock>
                <HyperlinkButton Name="kbLink"
                                 NavigateUri="https://www.veeam.com/kb2462"
                                 Content="KB 2462"
                                 FontSize="12"
                                 Margin="0,-4,0,0" />
                <TextBlock FontSize="12" TextWrapping="Wrap" Foreground="{DynamicResource PrimaryTextBrush}" LineHeight="20">
                    <Run Name="Cav2" />
                </TextBlock>
                <TextBlock Classes="secondary-text" TextWrapping="Wrap" FontSize="11" LineHeight="16">
                    <Run Name="Cav3" /><LineBreak />
                    <Run Name="Cav4" /><LineBreak /><LineBreak />
                    <Run FontWeight="SemiBold">Credential Storage:</Run><Run> Credentials are stored encrypted at </Run><Run FontFamily="Consolas">%AppData%\VeeamHealthCheck\creds.json</Run><Run>. You can delete this file to remove saved credentials or use the "Clear Saved Credentials" option.</Run>
                </TextBlock>
            </StackPanel>
        </ScrollViewer>
        <Button Grid.Row="1" Content="Close" Classes="secondary" HorizontalAlignment="Right"
                Margin="0,12,0,0" IsCancel="True" Click="Close_Click" />
    </Grid>
</Window>
```

- [ ] **Step 2: Create the dialog code-behind**

Create `vHC/HC_Reporting/Functions/AboutDialog/AboutDisclaimerDialog.axaml.cs`:

```csharp
// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia.Controls;
using Avalonia.Interactivity;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Functions.AboutDialog
{
    public partial class AboutDisclaimerDialog : Window
    {
        public AboutDisclaimerDialog()
        {
            InitializeComponent();

            this.InsHeader.Text = VbrLocalizationHelper.GuiInstHeader;
            this.line1.Text = VbrLocalizationHelper.GuiInstLine1;
            this.line2.Text = VbrLocalizationHelper.GuiInstLine2;
            this.line3.Text = VbrLocalizationHelper.GuiInstLine3;
            this.line4.Text = VbrLocalizationHelper.GuiInstLine4;
            this.line5.Text = VbrLocalizationHelper.GuiInstLine5;
            this.line6.Text = VbrLocalizationHelper.GuiInstLine6;
            this.Cav1Part1.Text = VbrLocalizationHelper.GuiInstCaveat1;
            this.Cav2.Text = VbrLocalizationHelper.GuiInstCaveat2;
            this.Cav3.Text = "*** This tool is community supported and not an officially supported Veeam product.\r\n";
            this.Cav4.Text = "**** The tool does not automatically phone home, or reach out to any network infrastructure beyond the Veeam Backup and Replication components or the Veeam Backup for 365 components if appropriate.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
```

- [ ] **Step 3: Build to verify the new dialog compiles**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds (0 errors). Nothing references this dialog yet, so it's dead code at this point — that's expected and resolved in Task 3.

- [ ] **Step 4: Commit**

```bash
git add vHC/HC_Reporting/Functions/AboutDialog/
git commit -m "feat(gui): add About/Disclaimer dialog, not yet wired"
```

---

### Task 3: Window/header/tab-strip/bottom-bar restructure (interim single-column tab content)

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml` (full replacement — see Step 1)
- Modify: `vHC/HC_Reporting/VhcGui.axaml.cs:3-17` (usings), `:249-260` (SetUiText), `:233-243` (SetImportRelease/Debug), `:287-307` (hideProgressBar/showProgressBar), plus new methods

This task establishes the chrome (fixed window size, header, tab strip, fixed bottom bar) and removes the Instructions/Warnings card (now living in the About dialog from Task 2). Tab content is **temporarily** single-column — Server/Output/Options cards stacked under the Ad-hoc tab, the Continuous Monitoring card moved bodily (unsplit) under its own tab. Task 4 splits both into their final two-column layout. This intermediate state is fully valid, buildable Avalonia XAML; nothing is a stub.

- [ ] **Step 1: Replace `VhcGui.axaml` with the restructured version**

Replace the entire contents of `vHC/HC_Reporting/VhcGui.axaml` with:

```xml
<!--
Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
MIT License
-->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="VeeamHealthCheck.VhcGui"
        Title=""
        MinWidth="860" MinHeight="580"
        Width="900" Height="620"
        WindowStartupLocation="CenterScreen"
        CanResize="True">

    <Grid RowDefinitions="Auto,Auto,*,Auto">

        <!-- Header row -->
        <Grid Grid.Row="0" ColumnDefinitions="*,Auto,Auto,Auto,Auto" Margin="25,16,25,8">
            <TextBlock Grid.Column="0" Text="Veeam Health Check" FontSize="20" FontWeight="Bold" VerticalAlignment="Center" />
            <Button x:Name="importButton" Grid.Column="1" Classes="secondary"
                    Margin="0,0,12,0" Click="Import_click"
                    ToolTip.Tip="Create a report from previously collected data" />
            <Border x:Name="importDivider" Grid.Column="2" Width="1" Height="18" VerticalAlignment="Center" Margin="0,0,12,0"
                    Background="{DynamicResource CardBorderBrush}" />
            <Button x:Name="ThemeToggleButton" Grid.Column="3" Classes="secondary"
                    Margin="0,0,8,0" Click="ThemeToggleButton_Click" ToolTip.Tip="Toggle light/dark theme" />
            <Button x:Name="aboutButton" Grid.Column="4" Content="About / Disclaimer" Classes="secondary"
                    Click="AboutButton_Click" />
        </Grid>

        <!-- Tab strip row -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="8" Margin="25,0,25,12">
            <Button x:Name="AdHocTabButton" Content="Ad-hoc Health Check" Classes="tab tab-active"
                    Click="AdHocTabButton_Click" />
            <Button x:Name="MonitoringTabButton" Content="Continuous Monitoring" Classes="tab"
                    Click="MonitoringTabButton_Click" />
        </StackPanel>

        <!-- Tab content row -->
        <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Margin="25,0,25,15">
            <Panel>

                <!-- Ad-hoc Health Check tab (interim: single column - Task 4 splits into two) -->
                <StackPanel x:Name="AdHocTabPanel">
                    <Border Classes="card" Margin="0,0,0,15">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Text="VBR Server" Classes="card-title" />
                            <StackPanel Margin="5,10,5,0">
                                <Grid Margin="0,0,0,10" ColumnDefinitions="*,10,80">
                                    <TextBox x:Name="serverTextBox" Grid.Column="0"
                                             Classes="modern"
                                             Height="32"
                                             FontSize="12"
                                             ToolTip.Tip="Enter VBR server hostname or IP address" />
                                    <Button x:Name="addServerBtn" Grid.Column="2"
                                            Content="Add"
                                            Height="32"
                                            FontSize="12"
                                            Classes="primary"
                                            Click="addServerBtn_Click" />
                                </Grid>

                                <Border BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="1"
                                        Background="{DynamicResource CardBackgroundBrush}" Height="120">
                                    <ListBox x:Name="serverListBox"
                                             BorderThickness="0"
                                             SelectionMode="Single"
                                             FontSize="13"
                                             Padding="5"
                                             SelectionChanged="serverListBox_SelectionChanged" />
                                </Border>

                                <Grid Margin="0,10,0,0" ColumnDefinitions="*,10,*">
                                    <Button x:Name="removeServerBtn" Grid.Column="0"
                                            Content="Remove Selected"
                                            Height="32"
                                            FontSize="12"
                                            Padding="10,5"
                                            Classes="secondary"
                                            Click="removeServerBtn_Click" />
                                    <Button x:Name="clearServersBtn" Grid.Column="2"
                                            Content="Clear All"
                                            Height="32"
                                            FontSize="12"
                                            Padding="10,5"
                                            Classes="secondary"
                                            Click="clearServersBtn_Click" />
                                </Grid>

                                <Separator Margin="0,12,0,12" />

                                <TextBlock Text="Product Type:" Classes="field-label" Margin="0,0,0,8" />
                                <ComboBox x:Name="productTypeSelector"
                                          Classes="modern"
                                          Width="220"
                                          SelectedIndex="0"
                                          HorizontalAlignment="Left"
                                          SelectionChanged="productTypeSelector_SelectionChanged"
                                          ToolTip.Tip="For remote servers, specifying the product type avoids connection failures. If set to Auto-detect, the tool will try both connection types.">
                                    <ComboBoxItem Content="Auto-detect" IsSelected="True" />
                                    <ComboBoxItem Content="VBR (Backup &amp; Replication)" />
                                    <ComboBoxItem Content="VB365 (Backup for Microsoft 365)" />
                                    <ComboBoxItem Content="Both" />
                                </ComboBox>
                            </StackPanel>
                        </StackPanel>
                    </Border>

                    <Border Classes="card" Margin="0,0,0,15">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Name="outPath" Classes="card-title" />
                            <TextBox x:Name="pathBox" Classes="modern"
                                     TextChanged="pathBox_TextChanged" Height="32" Margin="0,10,0,0" />
                        </StackPanel>
                    </Border>

                    <Border Classes="card" Margin="0,0,0,15">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Classes="card-title">
                                <Run Name="OptHdr" />
                            </TextBlock>
                            <StackPanel Margin="5,10,5,0">
                                <TextBlock Text="Export Options" Classes="field-label" Margin="0,0,0,8" />
                                <CheckBox x:Name="pdfCheckBox" Classes="modern"
                                          Checked="pdfCheckBox_Checked" Unchecked="pdfCheckBox_Unchecked"
                                          IsChecked="False" />
                                <CheckBox x:Name="explorerShowBox" Classes="modern"
                                          Checked="explorerShowBox_Checked" Unchecked="explorerShowBox_Unchecked"
                                          IsChecked="True" />
                                <CheckBox Name="htmlCheckBox" Classes="modern"
                                          Checked="htmlChecked" Unchecked="htmlUnchecked"
                                          ToolTip.Tip="Display the HTML report in your default browser after generation" />

                                <Separator Margin="0,12,0,12" />

                                <TextBlock Text="Data Collection" Classes="field-label" Margin="0,0,0,8" />
                                <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                    <TextBlock Text="Collection Period:" VerticalAlignment="Center"
                                               Classes="field-label" Margin="0,0,10,0" />
                                    <ComboBox Name="daysSelector" Classes="modern"
                                              Width="120" SelectedIndex="0" SelectionChanged="ComboBox_SelectionChanged">
                                        <ComboBoxItem Content="7 Days" IsSelected="True" />
                                        <ComboBoxItem Content="30 Days" />
                                        <ComboBoxItem Content="90 Days" />
                                    </ComboBox>
                                </StackPanel>

                                <CheckBox x:Name="RescanBox" Classes="modern"
                                        Checked="RescanBox_Checked" Unchecked="RescanBox_Unchecked"
                                        Margin="0,8,0,0"
                                        ToolTip.Tip="Rescan for Hardware change" />

                                <Separator Margin="0,12,0,12" />

                                <TextBlock Text="Security &amp; Privacy" Classes="field-label" Margin="0,0,0,8" />
                                <CheckBox x:Name="scrubBox" Classes="modern"
                                          Checked="HandleCheck" Unchecked="HandleUnchecked"
                                          ToolTip.Tip="Anonymize sensitive data according to Veeam KB 2462" />
                                <CheckBox x:Name="clearCredsCheckBox" Classes="modern"
                                          Checked="clearCredsCheckBox_Checked" Unchecked="clearCredsCheckBox_Unchecked"
                                          IsChecked="False"
                                          ToolTip.Tip="Clear any previously saved credentials before running" />
                            </StackPanel>
                        </StackPanel>
                    </Border>
                </StackPanel>

                <!-- Continuous Monitoring tab (interim: card moved bodily, unsplit - Task 4 splits into two) -->
                <StackPanel x:Name="MonitoringTabPanel" IsVisible="False">
                    <Border Classes="card" Margin="0,0,0,15">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Text="Continuous Monitoring" Classes="card-title" />
                            <StackPanel Margin="5,10,5,0">

                                <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
                                    <TextBlock Text="Status: " Classes="secondary-text" VerticalAlignment="Center" />
                                    <TextBlock x:Name="monitorStatusText" FontSize="13" FontWeight="SemiBold"
                                               Foreground="{DynamicResource StatusNeutralBrush}" VerticalAlignment="Center" Text="Checking..." />
                                </StackPanel>

                                <TextBlock x:Name="monitorLastRunText" Classes="secondary-text" FontSize="11"
                                           Margin="0,0,0,10" IsVisible="False" TextWrapping="Wrap" />

                                <TextBlock Text="Alert Notifications" Classes="field-label" Margin="0,0,0,6" />
                                <Grid Margin="0,0,0,8" ColumnDefinitions="100,8,*">
                                    <ComboBox x:Name="notifTypeBox" Grid.Column="0"
                                              Classes="modern" Height="32" FontSize="12"
                                              SelectedIndex="0"
                                              SelectionChanged="notifTypeBox_SelectionChanged">
                                        <ComboBoxItem Content="ntfy" IsSelected="True" />
                                        <ComboBoxItem Content="Teams" />
                                        <ComboBoxItem Content="Slack" />
                                        <ComboBoxItem Content="PagerDuty" />
                                    </ComboBox>
                                    <TextBox x:Name="notifUrlBox" Grid.Column="2"
                                             Classes="modern" Height="32" FontSize="12"
                                             Tag="https://ntfy.sh/your-topic" />
                                </Grid>
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                                    <TextBlock Text="Min severity: " Classes="field-label"
                                               VerticalAlignment="Center" Margin="0,0,8,0" />
                                    <ComboBox x:Name="notifSeverityBox" Width="110"
                                              Classes="modern" Height="28" FontSize="12"
                                              SelectedIndex="0">
                                        <ComboBoxItem Content="warning" IsSelected="True" />
                                        <ComboBoxItem Content="critical" />
                                        <ComboBoxItem Content="ok" />
                                    </ComboBox>
                                </StackPanel>

                                <Grid ColumnDefinitions="*,8,*,8,*">
                                    <Button x:Name="monitorQuickSetupBtn" Grid.Column="0"
                                            Content="Quick Setup" Height="32" FontSize="11"
                                            Classes="primary" IsEnabled="False"
                                            Click="monitorQuickSetupBtn_Click"
                                            ToolTip.Tip="Install monitor using selected VBR server and stored credentials" />
                                    <Button x:Name="monitorVhcSetupBtn" Grid.Column="2"
                                            Content="Setup from VHC" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorVhcSetupBtn_Click"
                                            ToolTip.Tip="Configure monitor using data collected from the last health check run" />
                                    <Button x:Name="monitorRunBtn" Grid.Column="4"
                                            Content="Run Now" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorRunBtn_Click"
                                            ToolTip.Tip="Trigger an immediate monitor check" />
                                </Grid>

                            </StackPanel>
                        </StackPanel>
                    </Border>
                </StackPanel>

            </Panel>
        </ScrollViewer>

        <!-- Bottom bar row (fixed - never inside the ScrollViewer). termsBtn/run follow the
             active tab (ad-hoc-only workflow); the progress stack does NOT - a real health
             check run can take minutes, and a user switching to Continuous Monitoring
             mid-run must not lose visibility into it. -->
        <Border Grid.Row="3" Background="{DynamicResource CardBackgroundBrush}" BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="0,1,0,0">
            <Grid ColumnDefinitions="Auto,*,Auto" Margin="25,15,25,15">
                <Button x:Name="termsBtn" Grid.Column="0" Classes="secondary"
                        Height="45" Click="AcceptButton_click" />

                <!-- progressText now hides via Opacity, not IsVisible (see code-behind) -
                     sharing this column with nothing else to collapse against means
                     IsVisible=false would shrink the column and shift termsBtn/run
                     vertically on every run start/stop. -->
                <StackPanel Grid.Column="1" Margin="16,0" VerticalAlignment="Center">
                    <ProgressBar Name="pBar"
                                 Height="20"
                                 IsIndeterminate="True"
                                 BorderThickness="0"
                                 Background="{DynamicResource CardBorderBrush}"
                                 Foreground="{DynamicResource AccentBrush}"
                                 BorderBrush="Transparent" />
                    <TextBlock Name="progressText"
                               Classes="secondary-text"
                               Text="Processing health check..."
                               Margin="0,8,0,0"
                               HorizontalAlignment="Center"
                               Opacity="0" />
                </StackPanel>

                <Button x:Name="run" Grid.Column="2" Classes="primary"
                        Height="45" Click="run_Click" />
            </Grid>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Remove the moved-to-dialog fields from `SetUiText()`**

In `vHC/HC_Reporting/VhcGui.axaml.cs`, find:

```csharp
        private void SetUiText()
        {
            this.InsHeader.Text = VbrLocalizationHelper.GuiInstHeader;
            this.line1.Text = VbrLocalizationHelper.GuiInstLine1;
            this.line2.Text = VbrLocalizationHelper.GuiInstLine2;
            this.line3.Text = VbrLocalizationHelper.GuiInstLine3;
            this.line4.Text = VbrLocalizationHelper.GuiInstLine4;
            this.line5.Text = VbrLocalizationHelper.GuiInstLine5;
            this.line6.Text = VbrLocalizationHelper.GuiInstLine6;
            this.Cav1Part1.Text = VbrLocalizationHelper.GuiInstCaveat1;
            this.Cav2.Text = VbrLocalizationHelper.GuiInstCaveat2;
            this.Cav3.Text = "*** This tool is community supported and not an officially supported Veeam product.\r\n";
            this.Cav4.Text = "**** The tool does not automatically phone home, or reach out to any network infrastructure beyond the Veeam Backup and Replication components or the Veeam Backup for 365 components if appropriate.";
            this.OptHdr.Text = VbrLocalizationHelper.GuiOptionsHeader;
```

Replace with:

```csharp
        private void SetUiText()
        {
            this.OptHdr.Text = VbrLocalizationHelper.GuiOptionsHeader;
```

(These fields now live on `AboutDisclaimerDialog`, set in its own constructor from Task 2. Everything below this point in `SetUiText()` — `htmlCheckBox.Content`, `scrubBox.Content`, etc. — is unchanged.)

- [ ] **Step 3: Fix `progressText`'s hide mechanism**

Find:

```csharp
        private void hideProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                // run.IsEnabled = true;
                pBar.Opacity = 0;
                pBar.IsHitTestVisible = false;
                progressText.IsVisible = false;
            });
        }

        private void showProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                run.IsEnabled = false;
                pBar.Opacity = 1;
                pBar.IsHitTestVisible = true;
                progressText.IsVisible = true;
            });
        }
```

Replace with:

```csharp
        private void hideProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                // run.IsEnabled = true;
                pBar.Opacity = 0;
                pBar.IsHitTestVisible = false;
                progressText.Opacity = 0;
            });
        }

        private void showProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                run.IsEnabled = false;
                pBar.Opacity = 1;
                pBar.IsHitTestVisible = true;
                progressText.Opacity = 1;
            });
        }
```

Also update the comment above `hideProgressBar()` (originally explained the `IsVisible` vs `Opacity` split that no longer exists):

Find:

```csharp
        // pBar deliberately never gets IsVisible=false - WPF's Visibility.Hidden
        // (what this replaces) keeps the control's layout slot reserved so the
        // progress bar area doesn't reflow when hidden; only Opacity/hit-testing
        // toggle. progressText mirrors WPF's Visibility.Collapsed, which does
        // remove it from layout - IsVisible is the correct match there.
```

Replace with:

```csharp
        // Both pBar and progressText hide via Opacity, not IsVisible: they now
        // share a bottom-bar column with nothing else to collapse against, so
        // IsVisible=false would shrink the column and shift termsBtn/run
        // vertically on every run start/stop.
```

- [ ] **Step 4: Toggle the import divider alongside the import button**

Find:

```csharp
        private void SetImportRelease()
        {
            importButton.IsEnabled = false;
            importButton.Width = 0;
        }

        private void SetImportDebug()
        {
            importButton.IsEnabled = true;
            importButton.Width = 100;
        }
```

Replace with:

```csharp
        private void SetImportRelease()
        {
            importButton.IsEnabled = false;
            importButton.Width = 0;
            importDivider.IsVisible = false;
        }

        private void SetImportDebug()
        {
            importButton.IsEnabled = true;
            importButton.Width = 100;
            importDivider.IsVisible = true;
        }
```

(`importButton` hides via `Width="0"`, never `IsVisible`, so the divider needs its own explicit toggle in these same two methods rather than a binding.)

- [ ] **Step 5: Add the tab-switching and About-dialog handlers**

Add the `VeeamHealthCheck.Functions.AboutDialog` using at the top of the file. Find:

```csharp
using VeeamHealthCheck.Functions.Monitor;
using VeeamHealthCheck.Functions.UserInteraction;
```

Replace with:

```csharp
using VeeamHealthCheck.Functions.AboutDialog;
using VeeamHealthCheck.Functions.Monitor;
using VeeamHealthCheck.Functions.UserInteraction;
```

Then add the new handlers. Find:

```csharp
        private static string ThemeLabelFor(ThemeVariant variant) =>
            variant == ThemeVariant.Dark ? "🌙 Dark" :
            variant == ThemeVariant.Light ? "☀ Light" : "🖥 System";
```

Replace with:

```csharp
        private static string ThemeLabelFor(ThemeVariant variant) =>
            variant == ThemeVariant.Dark ? "🌙 Dark" :
            variant == ThemeVariant.Light ? "☀ Light" : "🖥 System";

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            await new AboutDisclaimerDialog().ShowDialog(this);
        }

        private void AdHocTabButton_Click(object sender, RoutedEventArgs e) => SelectTab(isAdHoc: true);

        private void MonitoringTabButton_Click(object sender, RoutedEventArgs e) => SelectTab(isAdHoc: false);

        // termsBtn/run belong to the ad-hoc workflow and follow the active tab; the
        // progress stack does not (see the bottom-bar comment in VhcGui.axaml) - a
        // real run can take minutes and must stay visible from either tab.
        //
        // termsBtn and run each sit alone in their own Auto column of the bottom
        // bar's Grid. IsVisible=false removes a control from layout entirely, so
        // an Auto column with nothing else to measure collapses to zero width -
        // the same reflow this plan already fixed for progressText, just
        // triggered by a tab switch instead of a run starting/stopping, and
        // horizontal instead of vertical. Toggling Opacity/IsHitTestVisible
        // instead (matching pBar's existing pattern) keeps both Auto columns at
        // their reserved width always, so the progress stack's position never
        // shifts when switching tabs.
        private void SelectTab(bool isAdHoc)
        {
            AdHocTabPanel.IsVisible = isAdHoc;
            MonitoringTabPanel.IsVisible = !isAdHoc;

            termsBtn.Opacity = isAdHoc ? 1 : 0;
            termsBtn.IsHitTestVisible = isAdHoc;
            run.Opacity = isAdHoc ? 1 : 0;
            run.IsHitTestVisible = isAdHoc;

            AdHocTabButton.Classes.Set("tab-active", isAdHoc);
            MonitoringTabButton.Classes.Set("tab-active", !isAdHoc);
        }
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds (0 errors). If it fails on a missing field reference, check for a card/control accidentally dropped while copying content into `AdHocTabPanel`/`MonitoringTabPanel` in Step 1 — every `x:Name` from the original file must appear exactly once in the new one (except `InsHeader`/`line1-6`/`Cav1Part1`/`Cav2`/`Cav3`/`Cav4`/`kbLink`, which moved to `AboutDisclaimerDialog` in Task 2).

- [ ] **Step 7: Run the existing test suite to check for regressions**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj`
Expected: Same pass/fail counts as before this task (no `VhcGui` tests exist, so this is a pure regression check on everything else — confirms nothing else in the solution referenced the removed `VhcGui` fields).

- [ ] **Step 8: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml vHC/HC_Reporting/VhcGui.axaml.cs
git commit -m "feat(gui): restructure VhcGui into header/tabs/fixed bottom bar

Window is now fixed-size (900x620, min 860x580) instead of
SizeToContent, fixing the root cause of not fitting 1080p. Instructions
& Warnings content moved to the new About/Disclaimer dialog. Tab
content is temporarily single-column - Task 4 splits it into the
final two-column layout."
```

---

### Task 4: Split tab content into the final two-column card layout

**Files:**
- Modify: `vHC/HC_Reporting/VhcGui.axaml` (only the `AdHocTabPanel` and `MonitoringTabPanel` contents)

Rearranges the two interim single-column panels from Task 3 into the approved two-column split: Ad-hoc tab groups VBR Server + Output Directory on the left against Options on the right (≈470px vs. ≈422px — the balanced split chosen during brainstorming over the 174px-imbalanced alternative). Continuous Monitoring splits its one card into two: status/buttons on the left, notifications on the right — new hardcoded titles `"Monitor Status"`/`"Alert Notifications"` (Stage D localizes these, like every other new string in this stage).

- [ ] **Step 1: Convert `AdHocTabPanel` from a single `StackPanel` into a two-column `Grid`**

In `vHC/HC_Reporting/VhcGui.axaml`, find the opening/closing tags of `AdHocTabPanel` (from Task 3):

```xml
                <StackPanel x:Name="AdHocTabPanel">
                    <Border Classes="card" Margin="0,0,0,15">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Text="VBR Server" Classes="card-title" />
```

... (all the way through the Options card) ...

```xml
                                <CheckBox x:Name="clearCredsCheckBox" Classes="modern"
                                          Checked="clearCredsCheckBox_Checked" Unchecked="clearCredsCheckBox_Unchecked"
                                          IsChecked="False"
                                          ToolTip.Tip="Clear any previously saved credentials before running" />
                            </StackPanel>
                        </StackPanel>
                    </Border>
                </StackPanel>
```

Replace the outer wrapper and add the second column, keeping every card's internal content byte-for-byte identical to Task 3 (only the outer `StackPanel`/`Grid` structure and card grouping changes):

```xml
                <Grid x:Name="AdHocTabPanel" ColumnDefinitions="*,20,*">
                    <StackPanel Grid.Column="0">
                        <Border Classes="card" Margin="0,0,0,15">
                            <StackPanel TextElement.FontWeight="SemiBold">
                                <TextBlock Text="VBR Server" Classes="card-title" />
                                <StackPanel Margin="5,10,5,0">
                                    <Grid Margin="0,0,0,10" ColumnDefinitions="*,10,80">
                                        <TextBox x:Name="serverTextBox" Grid.Column="0"
                                                 Classes="modern"
                                                 Height="32"
                                                 FontSize="12"
                                                 ToolTip.Tip="Enter VBR server hostname or IP address" />
                                        <Button x:Name="addServerBtn" Grid.Column="2"
                                                Content="Add"
                                                Height="32"
                                                FontSize="12"
                                                Classes="primary"
                                                Click="addServerBtn_Click" />
                                    </Grid>

                                    <Border BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="1"
                                            Background="{DynamicResource CardBackgroundBrush}" Height="120">
                                        <ListBox x:Name="serverListBox"
                                                 BorderThickness="0"
                                                 SelectionMode="Single"
                                                 FontSize="13"
                                                 Padding="5"
                                                 SelectionChanged="serverListBox_SelectionChanged" />
                                    </Border>

                                    <Grid Margin="0,10,0,0" ColumnDefinitions="*,10,*">
                                        <Button x:Name="removeServerBtn" Grid.Column="0"
                                                Content="Remove Selected"
                                                Height="32"
                                                FontSize="12"
                                                Padding="10,5"
                                                Classes="secondary"
                                                Click="removeServerBtn_Click" />
                                        <Button x:Name="clearServersBtn" Grid.Column="2"
                                                Content="Clear All"
                                                Height="32"
                                                FontSize="12"
                                                Padding="10,5"
                                                Classes="secondary"
                                                Click="clearServersBtn_Click" />
                                    </Grid>

                                    <Separator Margin="0,12,0,12" />

                                    <TextBlock Text="Product Type:" Classes="field-label" Margin="0,0,0,8" />
                                    <ComboBox x:Name="productTypeSelector"
                                              Classes="modern"
                                              Width="220"
                                              SelectedIndex="0"
                                              HorizontalAlignment="Left"
                                              SelectionChanged="productTypeSelector_SelectionChanged"
                                              ToolTip.Tip="For remote servers, specifying the product type avoids connection failures. If set to Auto-detect, the tool will try both connection types.">
                                        <ComboBoxItem Content="Auto-detect" IsSelected="True" />
                                        <ComboBoxItem Content="VBR (Backup &amp; Replication)" />
                                        <ComboBoxItem Content="VB365 (Backup for Microsoft 365)" />
                                        <ComboBoxItem Content="Both" />
                                    </ComboBox>
                                </StackPanel>
                            </StackPanel>
                        </Border>

                        <Border Classes="card">
                            <StackPanel TextElement.FontWeight="SemiBold">
                                <TextBlock Name="outPath" Classes="card-title" />
                                <TextBox x:Name="pathBox" Classes="modern"
                                         TextChanged="pathBox_TextChanged" Height="32" Margin="0,10,0,0" />
                            </StackPanel>
                        </Border>
                    </StackPanel>

                    <StackPanel Grid.Column="2">
                        <Border Classes="card">
                            <StackPanel TextElement.FontWeight="SemiBold">
                                <TextBlock Classes="card-title">
                                    <Run Name="OptHdr" />
                                </TextBlock>
                                <StackPanel Margin="5,10,5,0">
                                    <TextBlock Text="Export Options" Classes="field-label" Margin="0,0,0,8" />
                                    <CheckBox x:Name="pdfCheckBox" Classes="modern"
                                              Checked="pdfCheckBox_Checked" Unchecked="pdfCheckBox_Unchecked"
                                              IsChecked="False" />
                                    <CheckBox x:Name="explorerShowBox" Classes="modern"
                                              Checked="explorerShowBox_Checked" Unchecked="explorerShowBox_Unchecked"
                                              IsChecked="True" />
                                    <CheckBox Name="htmlCheckBox" Classes="modern"
                                              Checked="htmlChecked" Unchecked="htmlUnchecked"
                                              ToolTip.Tip="Display the HTML report in your default browser after generation" />

                                    <Separator Margin="0,12,0,12" />

                                    <TextBlock Text="Data Collection" Classes="field-label" Margin="0,0,0,8" />
                                    <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                        <TextBlock Text="Collection Period:" VerticalAlignment="Center"
                                                   Classes="field-label" Margin="0,0,10,0" />
                                        <ComboBox Name="daysSelector" Classes="modern"
                                                  Width="120" SelectedIndex="0" SelectionChanged="ComboBox_SelectionChanged">
                                            <ComboBoxItem Content="7 Days" IsSelected="True" />
                                            <ComboBoxItem Content="30 Days" />
                                            <ComboBoxItem Content="90 Days" />
                                        </ComboBox>
                                    </StackPanel>

                                    <CheckBox x:Name="RescanBox" Classes="modern"
                                            Checked="RescanBox_Checked" Unchecked="RescanBox_Unchecked"
                                            Margin="0,8,0,0"
                                            ToolTip.Tip="Rescan for Hardware change" />

                                    <Separator Margin="0,12,0,12" />

                                    <TextBlock Text="Security &amp; Privacy" Classes="field-label" Margin="0,0,0,8" />
                                    <CheckBox x:Name="scrubBox" Classes="modern"
                                              Checked="HandleCheck" Unchecked="HandleUnchecked"
                                              ToolTip.Tip="Anonymize sensitive data according to Veeam KB 2462" />
                                    <CheckBox x:Name="clearCredsCheckBox" Classes="modern"
                                              Checked="clearCredsCheckBox_Checked" Unchecked="clearCredsCheckBox_Unchecked"
                                              IsChecked="False"
                                              ToolTip.Tip="Clear any previously saved credentials before running" />
                                </StackPanel>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </Grid>
```

Note the VBR Server card's `Margin="0,0,0,15"` moved to separate the two left-column cards (Server from Output), and the Output Directory / Options cards dropped their own `Margin="0,0,0,15"` since each is now the last (or only) card in its column.

- [ ] **Step 2: Convert `MonitoringTabPanel` from a single `StackPanel`/card into a two-column `Grid`**

Find (from Task 3):

```xml
                <StackPanel x:Name="MonitoringTabPanel" IsVisible="False">
                    <Border Classes="card" Margin="0,0,0,15">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Text="Continuous Monitoring" Classes="card-title" />
                            <StackPanel Margin="5,10,5,0">

                                <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
                                    <TextBlock Text="Status: " Classes="secondary-text" VerticalAlignment="Center" />
                                    <TextBlock x:Name="monitorStatusText" FontSize="13" FontWeight="SemiBold"
                                               Foreground="{DynamicResource StatusNeutralBrush}" VerticalAlignment="Center" Text="Checking..." />
                                </StackPanel>

                                <TextBlock x:Name="monitorLastRunText" Classes="secondary-text" FontSize="11"
                                           Margin="0,0,0,10" IsVisible="False" TextWrapping="Wrap" />

                                <TextBlock Text="Alert Notifications" Classes="field-label" Margin="0,0,0,6" />
                                <Grid Margin="0,0,0,8" ColumnDefinitions="100,8,*">
                                    <ComboBox x:Name="notifTypeBox" Grid.Column="0"
                                              Classes="modern" Height="32" FontSize="12"
                                              SelectedIndex="0"
                                              SelectionChanged="notifTypeBox_SelectionChanged">
                                        <ComboBoxItem Content="ntfy" IsSelected="True" />
                                        <ComboBoxItem Content="Teams" />
                                        <ComboBoxItem Content="Slack" />
                                        <ComboBoxItem Content="PagerDuty" />
                                    </ComboBox>
                                    <TextBox x:Name="notifUrlBox" Grid.Column="2"
                                             Classes="modern" Height="32" FontSize="12"
                                             Tag="https://ntfy.sh/your-topic" />
                                </Grid>
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                                    <TextBlock Text="Min severity: " Classes="field-label"
                                               VerticalAlignment="Center" Margin="0,0,8,0" />
                                    <ComboBox x:Name="notifSeverityBox" Width="110"
                                              Classes="modern" Height="28" FontSize="12"
                                              SelectedIndex="0">
                                        <ComboBoxItem Content="warning" IsSelected="True" />
                                        <ComboBoxItem Content="critical" />
                                        <ComboBoxItem Content="ok" />
                                    </ComboBox>
                                </StackPanel>

                                <Grid ColumnDefinitions="*,8,*,8,*">
                                    <Button x:Name="monitorQuickSetupBtn" Grid.Column="0"
                                            Content="Quick Setup" Height="32" FontSize="11"
                                            Classes="primary" IsEnabled="False"
                                            Click="monitorQuickSetupBtn_Click"
                                            ToolTip.Tip="Install monitor using selected VBR server and stored credentials" />
                                    <Button x:Name="monitorVhcSetupBtn" Grid.Column="2"
                                            Content="Setup from VHC" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorVhcSetupBtn_Click"
                                            ToolTip.Tip="Configure monitor using data collected from the last health check run" />
                                    <Button x:Name="monitorRunBtn" Grid.Column="4"
                                            Content="Run Now" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorRunBtn_Click"
                                            ToolTip.Tip="Trigger an immediate monitor check" />
                                </Grid>

                            </StackPanel>
                        </StackPanel>
                    </Border>
                </StackPanel>
```

Replace with:

```xml
                <Grid x:Name="MonitoringTabPanel" ColumnDefinitions="*,20,*" IsVisible="False">
                    <Border Grid.Column="0" Classes="card">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Text="Monitor Status" Classes="card-title" />
                            <StackPanel Margin="5,10,5,0">
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
                                    <TextBlock Text="Status: " Classes="secondary-text" VerticalAlignment="Center" />
                                    <TextBlock x:Name="monitorStatusText" FontSize="13" FontWeight="SemiBold"
                                               Foreground="{DynamicResource StatusNeutralBrush}" VerticalAlignment="Center" Text="Checking..." />
                                </StackPanel>

                                <TextBlock x:Name="monitorLastRunText" Classes="secondary-text" FontSize="11"
                                           Margin="0,0,0,10" IsVisible="False" TextWrapping="Wrap" />

                                <Grid ColumnDefinitions="*,8,*,8,*">
                                    <Button x:Name="monitorQuickSetupBtn" Grid.Column="0"
                                            Content="Quick Setup" Height="32" FontSize="11"
                                            Classes="primary" IsEnabled="False"
                                            Click="monitorQuickSetupBtn_Click"
                                            ToolTip.Tip="Install monitor using selected VBR server and stored credentials" />
                                    <Button x:Name="monitorVhcSetupBtn" Grid.Column="2"
                                            Content="Setup from VHC" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorVhcSetupBtn_Click"
                                            ToolTip.Tip="Configure monitor using data collected from the last health check run" />
                                    <Button x:Name="monitorRunBtn" Grid.Column="4"
                                            Content="Run Now" Height="32" FontSize="11"
                                            Classes="secondary" IsEnabled="False"
                                            Click="monitorRunBtn_Click"
                                            ToolTip.Tip="Trigger an immediate monitor check" />
                                </Grid>
                            </StackPanel>
                        </StackPanel>
                    </Border>

                    <Border Grid.Column="2" Classes="card">
                        <StackPanel TextElement.FontWeight="SemiBold">
                            <TextBlock Text="Alert Notifications" Classes="card-title" />
                            <StackPanel Margin="5,10,5,0">
                                <Grid Margin="0,0,0,8" ColumnDefinitions="100,8,*">
                                    <ComboBox x:Name="notifTypeBox" Grid.Column="0"
                                              Classes="modern" Height="32" FontSize="12"
                                              SelectedIndex="0"
                                              SelectionChanged="notifTypeBox_SelectionChanged">
                                        <ComboBoxItem Content="ntfy" IsSelected="True" />
                                        <ComboBoxItem Content="Teams" />
                                        <ComboBoxItem Content="Slack" />
                                        <ComboBoxItem Content="PagerDuty" />
                                    </ComboBox>
                                    <TextBox x:Name="notifUrlBox" Grid.Column="2"
                                             Classes="modern" Height="32" FontSize="12"
                                             Tag="https://ntfy.sh/your-topic" />
                                </Grid>
                                <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                                    <TextBlock Text="Min severity: " Classes="field-label"
                                               VerticalAlignment="Center" Margin="0,0,8,0" />
                                    <ComboBox x:Name="notifSeverityBox" Width="110"
                                              Classes="modern" Height="28" FontSize="12"
                                              SelectedIndex="0">
                                        <ComboBoxItem Content="warning" IsSelected="True" />
                                        <ComboBoxItem Content="critical" />
                                        <ComboBoxItem Content="ok" />
                                    </ComboBox>
                                </StackPanel>
                            </StackPanel>
                        </StackPanel>
                    </Border>
                </Grid>
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: Build succeeds (0 errors). No code-behind changes are needed for this task — every `x:Name` and event handler wiring is identical to Task 3, only their XAML parents changed.

- [ ] **Step 4: Run the existing test suite to check for regressions**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj`
Expected: Same pass/fail counts as Task 3's run.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/VhcGui.axaml
git commit -m "feat(gui): split tab content into final two-column card layout

Ad-hoc tab: VBR Server + Output Directory (left, ~470px) against
Options (right, ~422px) - the balanced split from brainstorming.
Continuous Monitoring: Monitor Status + actions (left) against Alert
Notifications (right), splitting the one existing card into two with
new hardcoded titles (Stage D localizes them)."
```

---

## Self-review notes

**Spec coverage:** Window sizing (Task 3), header row incl. import/divider/theme/about ordering and importDivider toggle (Task 3), tab strip incl. new `Button.tab` styles (Tasks 1 & 3), tab-content scrolling (Task 3's `ScrollViewer`), Ad-hoc/Continuous Monitoring two-column card mapping (Task 4), bottom bar incl. progressText Opacity fix and tab-dependent Terms/Start visibility with tab-independent progress visibility (Task 3), About dialog incl. the three-source content split and dropped caution styling (Task 2). All non-goals (Terms flow, server-management model, selector control types, localization, cosmetic polish) are untouched by every task — no task modifies `AcceptButton_click`, the server list add/remove logic, `daysSelector`/`notifSeverityBox`, or introduces resx keys.

**Type/name consistency:** `AdHocTabPanel`/`MonitoringTabPanel`/`AdHocTabButton`/`MonitoringTabButton`/`termsBtn`/`run`/`importButton`/`importDivider`/`aboutButton`/`pBar`/`progressText` are spelled identically everywhere they appear across Tasks 3 and 4. `AboutDisclaimerDialog` (Task 2) and its namespace `VeeamHealthCheck.Functions.AboutDialog` match the `using` added in Task 3 and the `new AboutDisclaimerDialog()` call in `AboutButton_Click`.
