using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenOff
{
    // Lets the user pick how long to wait before the display goes dark.
    internal sealed class PickerForm : Form
    {
        private static readonly int[] PresetMinutes = { 0, 1, 5, 10, 15, 30, 45, 60, 90, 120 };

        private readonly NumericUpDown customMinutes = new NumericUpDown();
        private readonly Font uiFont = Program.UiFont(9F, FontStyle.Regular);

        public int DelaySeconds { get; private set; }

        public PickerForm()
        {
            Text = Strings.PickerTitle;
            Font = uiFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Program.Px(14));

            FlowLayoutPanel root = new FlowLayoutPanel();
            root.FlowDirection = FlowDirection.TopDown;
            root.WrapContents = false;
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.Margin = new Padding(0);

            Label hint = new Label();
            hint.Text = Strings.PickerHint;
            hint.AutoSize = true;
            hint.Margin = new Padding(Program.Px(3), 0, Program.Px(3), Program.Px(10));
            root.Controls.Add(hint);

            FlowLayoutPanel presets = new FlowLayoutPanel();
            presets.FlowDirection = FlowDirection.LeftToRight;
            presets.WrapContents = true;
            presets.AutoSize = true;
            presets.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            presets.MaximumSize = new Size(Program.Px(384), 0);
            presets.Margin = new Padding(0);

            foreach (int minutes in PresetMinutes)
            {
                Button preset = new Button();
                preset.Text = minutes == 0 ? Strings.Immediately : Strings.MinuteButton(minutes);
                preset.Tag = minutes;
                preset.AutoSize = true;
                preset.AutoSizeMode = AutoSizeMode.GrowOnly;
                preset.MinimumSize = Program.Px(90, 32);
                preset.Margin = new Padding(Program.Px(3));
                preset.Click += PresetClicked;
                presets.Controls.Add(preset);
            }
            root.Controls.Add(presets);

            FlowLayoutPanel customRow = new FlowLayoutPanel();
            customRow.FlowDirection = FlowDirection.LeftToRight;
            customRow.WrapContents = false;
            customRow.AutoSize = true;
            customRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            customRow.Margin = new Padding(0, Program.Px(12), 0, 0);

            Label customLabel = new Label();
            customLabel.Text = Strings.CustomLabel;
            customLabel.AutoSize = true;
            customLabel.Margin = new Padding(Program.Px(3), Program.Px(8), Program.Px(6), Program.Px(3));
            customRow.Controls.Add(customLabel);

            customMinutes.Minimum = 1;
            customMinutes.Maximum = 1440;
            customMinutes.Value = 20;
            customMinutes.Width = Program.Px(78);
            customMinutes.Margin = new Padding(0, Program.Px(5), Program.Px(6), Program.Px(3));
            customRow.Controls.Add(customMinutes);

            Label unitLabel = new Label();
            unitLabel.Text = Strings.UnitMinutes;
            unitLabel.AutoSize = true;
            unitLabel.Margin = new Padding(0, Program.Px(8), Program.Px(12), Program.Px(3));
            customRow.Controls.Add(unitLabel);

            Button start = new Button();
            start.Text = Strings.Start;
            start.AutoSize = true;
            start.AutoSizeMode = AutoSizeMode.GrowOnly;
            start.MinimumSize = Program.Px(84, 30);
            start.Margin = new Padding(0, Program.Px(3), Program.Px(6), Program.Px(3));
            start.Click += StartClicked;
            customRow.Controls.Add(start);

            Button cancel = new Button();
            cancel.Text = Strings.Cancel;
            cancel.AutoSize = true;
            cancel.AutoSizeMode = AutoSizeMode.GrowOnly;
            cancel.MinimumSize = Program.Px(84, 30);
            cancel.Margin = new Padding(0, Program.Px(3), Program.Px(3), Program.Px(3));
            cancel.DialogResult = DialogResult.Cancel;
            customRow.Controls.Add(cancel);

            root.Controls.Add(customRow);
            Controls.Add(root);

            AcceptButton = start;
            CancelButton = cancel;
        }

        private void PresetClicked(object sender, EventArgs e)
        {
            DelaySeconds = ((int)((Button)sender).Tag) * 60;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void StartClicked(object sender, EventArgs e)
        {
            DelaySeconds = (int)customMinutes.Value * 60;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                uiFont.Dispose();

            base.Dispose(disposing);
        }
    }

    // Always-on-top window showing the remaining time, with a way out.
    internal sealed class CountdownForm : Form
    {
        private readonly Label remainingLabel = new Label();
        private readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer();
        private readonly Font uiFont = Program.UiFont(9F, FontStyle.Regular);
        private readonly Font clockFont = Program.UiFont(26F, FontStyle.Regular);

        private DateTime deadlineUtc;

        // True when the user pressed "off now" rather than letting the timer
        // run out, which decides whether the broadcast needs a settle delay.
        public bool EndedByUser { get; private set; }

        public CountdownForm(int seconds)
        {
            deadlineUtc = DateTime.UtcNow.AddSeconds(seconds);

            Text = Strings.CountdownTitle;
            Font = uiFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.Manual;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            ClientSize = Program.Px(312, 140);

            remainingLabel.Font = clockFont;
            remainingLabel.TextAlign = ContentAlignment.MiddleCenter;
            remainingLabel.Dock = DockStyle.Top;
            remainingLabel.Height = Program.Px(64);
            remainingLabel.Text = Program.FormatRemaining(TimeSpan.FromSeconds(seconds));

            Label caption = new Label();
            caption.Text = Strings.CountdownCaption;
            caption.TextAlign = ContentAlignment.MiddleCenter;
            caption.Dock = DockStyle.Top;
            caption.Height = Program.Px(22);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = Program.Px(46);
            buttons.Padding = new Padding(Program.Px(8), Program.Px(6), Program.Px(8), Program.Px(6));

            Button extend = new Button();
            extend.Text = Strings.ExtendFive;
            extend.AutoSize = true;
            extend.AutoSizeMode = AutoSizeMode.GrowOnly;
            extend.MinimumSize = Program.Px(92, 30);
            extend.Click += ExtendClicked;
            buttons.Controls.Add(extend);

            Button now = new Button();
            now.Text = Strings.OffNow;
            now.AutoSize = true;
            now.AutoSizeMode = AutoSizeMode.GrowOnly;
            now.MinimumSize = Program.Px(92, 30);
            now.Click += NowClicked;
            buttons.Controls.Add(now);

            Button cancel = new Button();
            cancel.Text = Strings.Cancel;
            cancel.AutoSize = true;
            cancel.AutoSizeMode = AutoSizeMode.GrowOnly;
            cancel.MinimumSize = Program.Px(92, 30);
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);

            Controls.Add(buttons);
            Controls.Add(caption);
            Controls.Add(remainingLabel);

            CancelButton = cancel;

            ticker.Interval = 250;
            ticker.Tick += Tick;
            ticker.Start();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // The countdown is the only way to extend or call off the blank, so
            // it belongs on the display the user is actually looking at rather
            // than always on the primary one.
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int inset = Program.Px(24);
            Location = new Point(work.Right - Width - inset, work.Bottom - Height - inset);
        }

        private void Tick(object sender, EventArgs e)
        {
            TimeSpan left = deadlineUtc - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                Finish(false);
                return;
            }
            remainingLabel.Text = Program.FormatRemaining(left);
        }

        private void ExtendClicked(object sender, EventArgs e)
        {
            deadlineUtc = deadlineUtc.AddMinutes(5);
            remainingLabel.Text = Program.FormatRemaining(deadlineUtc - DateTime.UtcNow);
        }

        private void NowClicked(object sender, EventArgs e)
        {
            Finish(true);
        }

        private void Finish(bool byUser)
        {
            EndedByUser = byUser;
            ticker.Stop();
            Hide();
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ticker.Dispose();
                clockFont.Dispose();
                uiFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
