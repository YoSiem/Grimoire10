using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Grimoire.Utilities
{
    public static class ModernTheme
    {
        private static readonly string BodyFontFamily = "Bahnschrift";
        private static readonly string HeadingFontFamily = "Bahnschrift SemiBold";

        private static readonly Color AppBackground = Color.FromArgb(241, 246, 252);
        private static readonly Color Surface = Color.FromArgb(255, 255, 255);
        private static readonly Color SurfaceMuted = Color.FromArgb(233, 241, 250);
        private static readonly Color SurfaceMutedAlt = Color.FromArgb(246, 249, 253);
        private static readonly Color Border = Color.FromArgb(198, 214, 232);
        private static readonly Color Foreground = Color.FromArgb(24, 36, 51);
        private static readonly Color ForegroundMuted = Color.FromArgb(70, 85, 104);
        private static readonly Color Accent = Color.FromArgb(0, 122, 204);
        private static readonly Color AccentHover = Color.FromArgb(0, 104, 184);
        private static readonly Color AccentSoft = Color.FromArgb(219, 236, 252);

        private static readonly HashSet<Control> HookedContainers = new();
        private static readonly HashSet<Form> StyledForms = new();
        private static readonly HashSet<TabControl> StyledTabControls = new();
        private static readonly HashSet<Button> StyledButtons = new();
        private static readonly HashSet<ToolStripDropDownItem> HookedDropDownItems = new();
        private static readonly Dictionary<Button, ButtonPalette> ButtonPalettes = new();

        private static readonly ToolStripRenderer Renderer = new ToolStripProfessionalRenderer(new ModernColorTable());

        private static bool initialized;

        private readonly record struct ButtonPalette(Color Base, Color Hover, Color Fore, Color Border);

        public static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            Application.Idle += (_, _) => ApplyOpenForms();
        }

        public static void Apply(Control root)
        {
            if (root == null)
                return;

            ApplyRecursive(root);
        }

        private static void ApplyOpenForms()
        {
            for (int i = 0; i < Application.OpenForms.Count; i++)
            {
                if (Application.OpenForms[i] is Form form && StyledForms.Add(form))
                    Apply(form);
            }
        }

        private static void ApplyRecursive(Control control)
        {
            ApplyControlStyle(control);
            HookChildAdds(control);

            foreach (Control child in control.Controls)
                ApplyRecursive(child);
        }

        private static void HookChildAdds(Control control)
        {
            if (!HookedContainers.Add(control))
                return;

            control.ControlAdded += (_, e) => ApplyRecursive(e.Control);
        }

        private static void ApplyControlStyle(Control control)
        {
            switch (control)
            {
                case Form form:
                    StyleForm(form);
                    break;

                case TabControl tabControl:
                    StyleTabControl(tabControl);
                    break;

                case TabPage tabPage:
                    tabPage.BackColor = Surface;
                    tabPage.ForeColor = Foreground;
                    SetControlFont(tabPage);
                    break;

                case ContextMenuStrip contextMenu:
                    StyleToolStrip(contextMenu);
                    break;

                case MenuStrip menuStrip:
                    StyleToolStrip(menuStrip);
                    break;

                case ToolStrip toolStrip:
                    StyleToolStrip(toolStrip);
                    break;

                case DataGridView dataGrid:
                    StyleDataGrid(dataGrid);
                    break;

                case GroupBox groupBox:
                    StyleGroupBox(groupBox);
                    break;

                case TreeView treeView:
                    StyleTreeView(treeView);
                    break;

                case ListView listView:
                    StyleListView(listView);
                    break;

                case PropertyGrid propertyGrid:
                    StylePropertyGrid(propertyGrid);
                    break;

                case ProgressBar progressBar:
                    progressBar.Style = ProgressBarStyle.Continuous;
                    break;

                case Button button:
                    StyleButton(button);
                    break;

                case TextBox textBox:
                    StyleTextBox(textBox);
                    break;

                case ComboBox comboBox:
                    StyleComboBox(comboBox);
                    break;

                case NumericUpDown numericUpDown:
                    StyleNumericUpDown(numericUpDown);
                    break;

                case UserControl userControl:
                    userControl.BackColor = Surface;
                    userControl.ForeColor = Foreground;
                    SetControlFont(userControl);
                    SetDoubleBuffered(userControl);
                    break;

                case Panel panel:
                    panel.BackColor = Surface;
                    panel.ForeColor = Foreground;
                    SetControlFont(panel);
                    SetDoubleBuffered(panel);
                    break;

                case Label label:
                    label.ForeColor = Foreground;
                    SetControlFont(label);
                    break;

                case CheckBox checkBox:
                    checkBox.ForeColor = Foreground;
                    checkBox.BackColor = Color.Transparent;
                    SetControlFont(checkBox);
                    break;

                case RadioButton radioButton:
                    radioButton.ForeColor = Foreground;
                    radioButton.BackColor = Color.Transparent;
                    SetControlFont(radioButton);
                    break;

                default:
                    control.ForeColor = Foreground;
                    SetControlFont(control);
                    break;
            }
        }

        private static void StyleForm(Form form)
        {
            form.BackColor = AppBackground;
            form.ForeColor = Foreground;
            SetControlFont(form);
            SetDoubleBuffered(form);
        }

        private static void StyleGroupBox(GroupBox groupBox)
        {
            groupBox.BackColor = Surface;
            groupBox.ForeColor = Foreground;
            groupBox.Font = CreateFont(groupBox.Font.Size + 0.5f, FontStyle.Bold, heading: true);
        }

        private static void StyleTextBox(TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = textBox.ReadOnly ? SurfaceMutedAlt : Surface;
            textBox.ForeColor = Foreground;
            SetControlFont(textBox);
        }

        private static void StyleComboBox(ComboBox comboBox)
        {
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = Surface;
            comboBox.ForeColor = Foreground;
            SetControlFont(comboBox);
        }

        private static void StyleNumericUpDown(NumericUpDown numericUpDown)
        {
            numericUpDown.BorderStyle = BorderStyle.FixedSingle;
            numericUpDown.BackColor = Surface;
            numericUpDown.ForeColor = Foreground;
            SetControlFont(numericUpDown);
        }

        private static void StyleButton(Button button)
        {
            bool primary = IsPrimaryAction(button);
            ButtonPalette palette = primary
                ? new ButtonPalette(Accent, AccentHover, Color.White, AccentHover)
                : new ButtonPalette(SurfaceMuted, Surface, Foreground, Border);

            ButtonPalettes[button] = palette;

            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = palette.Border;
            button.FlatAppearance.MouseDownBackColor = primary ? AccentHover : SurfaceMutedAlt;
            button.FlatAppearance.MouseOverBackColor = palette.Hover;
            button.Padding = new Padding(10, 2, 10, 2);
            button.Cursor = Cursors.Hand;
            SetControlFont(button);

            ApplyButtonPalette(button, hovered: false);

            if (!StyledButtons.Add(button))
                return;

            button.MouseEnter += (_, _) => ApplyButtonPalette(button, hovered: true);
            button.MouseLeave += (_, _) => ApplyButtonPalette(button, hovered: false);
            button.EnabledChanged += (_, _) => ApplyButtonPalette(button, hovered: false);
            button.Disposed += (_, _) => ButtonPalettes.Remove(button);
        }

        private static void ApplyButtonPalette(Button button, bool hovered)
        {
            if (!ButtonPalettes.TryGetValue(button, out ButtonPalette palette))
                return;

            if (!button.Enabled)
            {
                button.BackColor = SurfaceMutedAlt;
                button.ForeColor = ForegroundMuted;
                button.FlatAppearance.BorderColor = Border;
                return;
            }

            button.BackColor = hovered ? palette.Hover : palette.Base;
            button.ForeColor = palette.Fore;
            button.FlatAppearance.BorderColor = palette.Border;
        }

        private static bool IsPrimaryAction(Button button)
        {
            string key = $"{button.Name} {button.Text}".ToLowerInvariant();

            return key.Contains("new") ||
                   key.Contains("create") ||
                   key.Contains("build") ||
                   key.Contains("dump") ||
                   key.Contains("save") ||
                   key.Contains("launch") ||
                   key.Contains("copy") ||
                   key.Contains("generate") ||
                   key.Contains("gen");
        }

        private static void StyleTreeView(TreeView treeView)
        {
            treeView.BackColor = Surface;
            treeView.ForeColor = Foreground;
            treeView.BorderStyle = BorderStyle.None;
            treeView.HideSelection = false;
            treeView.FullRowSelect = true;
            SetControlFont(treeView);
        }

        private static void StyleListView(ListView listView)
        {
            listView.BackColor = Surface;
            listView.ForeColor = Foreground;
            listView.BorderStyle = BorderStyle.None;
            listView.FullRowSelect = true;
            listView.HideSelection = false;
            SetControlFont(listView);
        }

        private static void StylePropertyGrid(PropertyGrid grid)
        {
            grid.CategoryForeColor = Foreground;
            grid.CategorySplitterColor = Border;
            grid.CommandsActiveLinkColor = Accent;
            grid.CommandsBorderColor = Border;
            grid.CommandsDisabledLinkColor = ForegroundMuted;
            grid.CommandsForeColor = Foreground;
            grid.HelpBackColor = Surface;
            grid.HelpForeColor = Foreground;
            grid.LineColor = Border;
            grid.SelectedItemWithFocusBackColor = AccentSoft;
            grid.SelectedItemWithFocusForeColor = Foreground;
            grid.ViewBackColor = Surface;
            grid.ViewForeColor = Foreground;
            SetControlFont(grid);
        }

        private static void StyleDataGrid(DataGridView grid)
        {
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Border;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, 32);
            grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 28);

            SetControlFont(grid);

            DataGridViewCellStyle columnStyle = new(grid.ColumnHeadersDefaultCellStyle)
            {
                BackColor = SurfaceMuted,
                ForeColor = Foreground,
                SelectionBackColor = SurfaceMuted,
                SelectionForeColor = Foreground,
                Font = CreateFont(grid.Font.Size, FontStyle.Bold)
            };

            DataGridViewCellStyle rowStyle = new(grid.DefaultCellStyle)
            {
                BackColor = Surface,
                ForeColor = Foreground,
                SelectionBackColor = AccentSoft,
                SelectionForeColor = Foreground
            };

            DataGridViewCellStyle alternatingStyle = new(grid.AlternatingRowsDefaultCellStyle)
            {
                BackColor = SurfaceMutedAlt,
                ForeColor = Foreground,
                SelectionBackColor = AccentSoft,
                SelectionForeColor = Foreground
            };

            grid.ColumnHeadersDefaultCellStyle = columnStyle;
            grid.DefaultCellStyle = rowStyle;
            grid.AlternatingRowsDefaultCellStyle = alternatingStyle;

            SetDoubleBuffered(grid);
        }

        private static void StyleToolStrip(ToolStrip strip)
        {
            strip.RenderMode = ToolStripRenderMode.Professional;
            strip.Renderer = Renderer;
            strip.BackColor = Surface;
            strip.ForeColor = Foreground;
            strip.GripStyle = ToolStripGripStyle.Hidden;
            strip.Padding = new Padding(4, 3, 4, 3);
            strip.Font = CreateFont(strip.Font.Size, strip.Font.Style);

            foreach (ToolStripItem item in strip.Items)
                StyleToolStripItem(item);
        }

        private static void StyleToolStripItem(ToolStripItem item)
        {
            item.ForeColor = Foreground;
            item.BackColor = Surface;
            item.Font = CreateFont(item.Font.Size, item.Font.Style);

            if (item is ToolStripDropDownItem dropDownItem)
            {
                dropDownItem.DropDown.BackColor = Surface;
                dropDownItem.DropDown.ForeColor = Foreground;

                if (HookedDropDownItems.Add(dropDownItem))
                {
                    dropDownItem.DropDownOpening += (_, _) =>
                    {
                        foreach (ToolStripItem child in dropDownItem.DropDownItems)
                            StyleToolStripItem(child);
                    };
                }

                foreach (ToolStripItem child in dropDownItem.DropDownItems)
                    StyleToolStripItem(child);
            }
        }

        private static void StyleTabControl(TabControl tabControl)
        {
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(148, 34);
            tabControl.Padding = new Point(20, 8);
            tabControl.Appearance = TabAppearance.Normal;
            tabControl.BackColor = AppBackground;
            tabControl.ForeColor = Foreground;

            if (!StyledTabControls.Add(tabControl))
                return;

            tabControl.DrawItem += TabControl_DrawItem;
            tabControl.SelectedIndexChanged += (_, _) => tabControl.Invalidate();
            tabControl.ControlAdded += (_, _) => tabControl.Invalidate();
        }

        private static void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabs)
                return;

            if (e.Index < 0 || e.Index >= tabs.TabCount)
                return;

            TabPage page = tabs.TabPages[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            Rectangle bounds = e.Bounds;
            bounds.Inflate(-4, -4);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = selected ? Accent : SurfaceMuted;
            Color fg = selected ? Color.White : Foreground;
            Color border = selected ? AccentHover : Border;

            using (GraphicsPath path = CreateRoundedRect(bounds, 8))
            using (SolidBrush fill = new(bg))
            using (Pen pen = new(border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                CreateFont(9.5f, selected ? FontStyle.Bold : FontStyle.Regular),
                bounds,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new();

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        private static void SetDoubleBuffered(Control control)
        {
            try
            {
                typeof(Control)
                    .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(control, true, null);
            }
            catch
            {
                // Ignore reflection failures for controls that do not expose this property.
            }
        }

        private static void SetControlFont(Control control)
        {
            if (control.Font == null)
                return;

            control.Font = CreateFont(control.Font.Size, control.Font.Style);
        }

        private static Font CreateFont(float size, FontStyle style, bool heading = false)
        {
            string family = heading ? HeadingFontFamily : BodyFontFamily;
            return new Font(family, size, style, GraphicsUnit.Point);
        }

        private sealed class ModernColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Surface;
            public override Color MenuItemBorder => Border;
            public override Color MenuItemSelected => AccentSoft;
            public override Color MenuItemSelectedGradientBegin => AccentSoft;
            public override Color MenuItemSelectedGradientEnd => AccentSoft;
            public override Color MenuItemPressedGradientBegin => SurfaceMuted;
            public override Color MenuItemPressedGradientMiddle => SurfaceMuted;
            public override Color MenuItemPressedGradientEnd => SurfaceMuted;
            public override Color ButtonSelectedHighlight => AccentSoft;
            public override Color ButtonSelectedBorder => Border;
            public override Color ButtonPressedGradientBegin => SurfaceMuted;
            public override Color ButtonPressedGradientMiddle => SurfaceMuted;
            public override Color ButtonPressedGradientEnd => SurfaceMuted;
            public override Color ButtonCheckedGradientBegin => SurfaceMuted;
            public override Color ButtonCheckedGradientMiddle => SurfaceMuted;
            public override Color ButtonCheckedGradientEnd => SurfaceMuted;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
        }
    }
}
