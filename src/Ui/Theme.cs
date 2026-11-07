using System;
using System.Drawing;
using System.Windows.Forms;

namespace StationVoice.Ui
{
    /// <summary>统一的深色配色与小工具。</summary>
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(24, 26, 31);
        public static readonly Color Panel = Color.FromArgb(32, 35, 42);
        public static readonly Color Input = Color.FromArgb(40, 44, 52);
        public static readonly Color Border = Color.FromArgb(58, 63, 74);
        public static readonly Color Fg = Color.FromArgb(226, 230, 238);
        public static readonly Color Muted = Color.FromArgb(150, 158, 172);
        public static readonly Color Accent = Color.FromArgb(86, 156, 214);
        public static readonly Color AccentSoft = Color.FromArgb(38, 70, 100);
        public static readonly Color Ok = Color.FromArgb(120, 200, 140);
        public static readonly Color Warn = Color.FromArgb(226, 180, 96);

        public static Font TitleFont(float size)
        {
            return new Font("Microsoft YaHei UI", size, FontStyle.Bold, GraphicsUnit.Point);
        }

        public static Font BodyFont(float size)
        {
            return new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font MonoFont(float size)
        {
            return new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Label MakeLabel(string text, int x, int y, int w, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(w, 22);
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        public static TextBox MakeText(string text, int x, int y, int w)
        {
            TextBox box = new TextBox();
            box.Text = text;
            box.Location = new Point(x, y);
            box.Size = new Size(w, 24);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Input;
            box.ForeColor = Fg;
            box.Font = BodyFont(9.5f);
            return box;
        }

        public static Button MakeButton(string text, int x, int y, int w, int h, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(w, h);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.BackColor = primary ? AccentSoft : Panel;
            button.ForeColor = primary ? Color.White : Fg;
            button.Font = BodyFont(9.5f);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        public static ComboBox MakeCombo(int x, int y, int w, bool dropDownList)
        {
            ComboBox combo = new ComboBox();
            combo.Location = new Point(x, y);
            combo.Size = new Size(w, 24);
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Input;
            combo.ForeColor = Fg;
            combo.Font = BodyFont(9.5f);
            combo.DropDownStyle = dropDownList ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown;
            return combo;
        }

        public static RichTextBox MakeRichText(int x, int y, int w, int h, bool readOnly)
        {
            RichTextBox box = new RichTextBox();
            box.Location = new Point(x, y);
            box.Size = new Size(w, h);
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Panel;
            box.ForeColor = Fg;
            box.Font = BodyFont(9.5f);
            box.ReadOnly = readOnly;
            box.DetectUrls = false;
            box.WordWrap = true;
            box.ScrollBars = RichTextBoxScrollBars.Vertical;
            return box;
        }

        public static CheckBox MakeCheck(string text, int x, int y, int w, bool value)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Location = new Point(x, y);
            box.Size = new Size(w, 24);
            box.ForeColor = Fg;
            box.BackColor = Color.Transparent;
            box.Font = BodyFont(9.5f);
            box.Checked = value;
            return box;
        }

        public static TrackBar MakeTrack(int x, int y, int w, int min, int max, int value)
        {
            TrackBar bar = new TrackBar();
            bar.Location = new Point(x, y);
            bar.Size = new Size(w, 32);
            bar.Minimum = min;
            bar.Maximum = max;
            bar.Value = value;
            bar.TickStyle = TickStyle.None;
            bar.BackColor = Panel;
            bar.AutoSize = false;
            bar.Height = 26;
            return bar;
        }
    }
}
