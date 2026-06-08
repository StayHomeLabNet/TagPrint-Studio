using System.ComponentModel;
using System.Globalization;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed partial class DuplicateCleanupDialog : Form
{
    private void CloseDialog()
    {
        if (Applied)
        {
            DialogResult = DialogResult.OK;
        }

        Close();
    }

    private static ComboBox Combo() => new()
    {
        Dock = DockStyle.Fill,
        AutoSize = false,
        Height = 30,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Margin = Padding.Empty
    };

    private static Label TopLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true,
        Margin = Padding.Empty
    };

    private static CheckBox TopCheckBox(string text, bool isChecked = false, int width = 120) => new()
    {
        Text = text,
        AutoSize = false,
        Width = width,
        Height = 30,
        Checked = isChecked,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = Padding.Empty,
        Margin = new Padding(6, 2, 12, 2)
    };

    private static Control FieldGroup(string label, Control editor, int width)
    {
        editor.Dock = DockStyle.Fill;
        editor.Margin = Padding.Empty;

        var group = new TableLayoutPanel
        {
            Width = width,
            Height = 30,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 2, 16, 2),
            Padding = Padding.Empty
        };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        group.Controls.Add(TopLabel(label), 0, 0);
        group.Controls.Add(editor, 1, 0);
        return group;
    }

    private static void SetFieldGroupLabelWidth(Control control, int labelWidth)
    {
        if (control is TableLayoutPanel group && group.ColumnStyles.Count > 0)
        {
            group.ColumnStyles[0].Width = labelWidth;
        }
    }

    private static void SelectOption<T>(ComboBox combo, T value)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is OptionItem<T> item && EqualityComparer<T>.Default.Equals(item.Value, value))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private static Button Button(string text, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = 92,
            Height = 30,
            Margin = new Padding(4, 0, 4, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(31, 41, 55)
        };
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(216, 222, 230);
        button.Click += click;
        return button;
    }

    private static DataGridViewTextBoxColumn TextColumn(string property, string header, int width) => new()
    {
        DataPropertyName = property,
        HeaderText = header,
        Name = property,
        Width = width
    };

    private static string L(string japanese, string english) =>
        LocalizationService.CurrentLanguage == AppLanguage.Japanese ? japanese : english;

    private static void SelectFieldById(ComboBox combo, string fieldId, int fallbackIndex)
    {
        if (combo.Items.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(fieldId))
        {
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is FieldItem item &&
                    string.Equals(item.FieldId, fieldId, StringComparison.Ordinal))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < combo.Items.Count)
        {
            combo.SelectedIndex = fallbackIndex;
        }
        else
        {
            combo.SelectedIndex = 0;
        }
    }
}
