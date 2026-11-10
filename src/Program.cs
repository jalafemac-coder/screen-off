using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace ScreenOff
{
    // Turns the display off without suspending the machine. With no delay
    // argument it opens a picker; with one it arms a countdown and shows a
    // small window that can extend, fire early, or call the whole thing off.
    internal static class Program
    {
        // A click or key release is itself an input event, and an input event
        // arriving right after the broadcast turns the display straight back
        // on, so a user-triggered blank waits this long before broadcasting.
        internal const int GestureSettleMs = 900;

        private const int MaxDelaySeconds = 86400;

        private enum Outcome
        {
            Run,
            ShowUsage,
            BadArgument
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string delayText;
            string problem;

            switch (ReadArguments(args, out delayText, out problem))
            {
                case Outcome.ShowUsage:
                    MessageBox.Show(Strings.Usage, Strings.UsageTitle,
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;

                case Outcome.BadArgument:
                    MessageBox.Show(problem + "\r\n\r\n" + Strings.Usage, Strings.UsageTitle,
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 2;
            }

            int seconds;
            bool fromGesture;

            if (delayText == null)
            {
                using (PickerForm picker = new PickerForm())
                {
                    if (picker.ShowDialog() != DialogResult.OK)
                        return 1;
                    seconds = picker.DelaySeconds;
                }

                // Picking "immediately" is a click, so it settles like a hotkey.
                fromGesture = true;
            }
            else if (!TryParseDelay(delayText, out seconds))
            {
                MessageBox.Show(Strings.BadDelay(delayText) + "\r\n\r\n" + Strings.Usage,
                                Strings.UsageTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }
            else
            {
                fromGesture = true;
            }

            if (seconds > 0)
            {
                using (CountdownForm countdown = new CountdownForm(seconds))
                {
                    if (countdown.ShowDialog() != DialogResult.OK)
                        return 1;

                    // Expiry has no gesture to settle; "off now" is a click.
                    fromGesture = countdown.EndedByUser;
                }
            }

            Display.PowerOff(fromGesture ? GestureSettleMs : 0);
            return 0;
        }

        // delayText is left null when no positional argument was given, which
        // means "open the picker". --lang is read in a first pass so that it
        // applies to the usage and error text of this same invocation.
        private static Outcome ReadArguments(string[] args, out string delayText, out string problem)
        {
            delayText = null;
            problem = null;

            foreach (string raw in args)
            {
                string arg = raw.Trim();
                if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                    Strings.Select(arg.Substring(7).Trim().ToLowerInvariant());
            }

            foreach (string raw in args)
            {
                string arg = raw.Trim();
                if (arg.Length == 0)
                    continue;

                if (arg == "-h" || arg == "--help" || arg == "/?" || arg == "/h")
                    return Outcome.ShowUsage;

                if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Anything else that looks like a switch is a typo, not a delay.
                if (arg[0] == '-' || arg[0] == '/')
                {
                    problem = Strings.UnknownOption(arg);
                    return Outcome.BadArgument;
                }

                if (delayText != null)
                {
                    problem = Strings.TooManyArguments;
                    return Outcome.BadArgument;
                }

                delayText = arg;
            }

            return Outcome.Run;
        }

        // A bare number means minutes; an s/m/h suffix overrides that.
        private static bool TryParseDelay(string raw, out int seconds)
        {
            seconds = 0;

            string text = raw.Trim().ToLowerInvariant();
            if (text.Length == 0)
                return false;
            if (text == "now")
                return true;

            double factor = 60.0;
            char unit = text[text.Length - 1];
            if (unit == 's' || unit == 'm' || unit == 'h')
            {
                factor = unit == 's' ? 1.0 : (unit == 'm' ? 60.0 : 3600.0);
                text = text.Substring(0, text.Length - 1).Trim();
            }

            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            double total = value * factor;
            if (value < 0 || total > MaxDelaySeconds)
                return false;

            // Rounding a fraction of a second down to zero would blank the
            // display at once, with no countdown window and so no way out.
            seconds = (int)Math.Round(total);
            if (seconds == 0 && total > 0.0)
                seconds = 1;

            return true;
        }

        internal static string FormatRemaining(TimeSpan left)
        {
            if (left < TimeSpan.Zero)
                left = TimeSpan.Zero;

            int total = (int)Math.Ceiling(left.TotalSeconds);
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;

            return hours > 0
                ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, secs)
                : string.Format("{0:00}:{1:00}", minutes, secs);
        }

        internal static Font UiFont(float size, FontStyle style)
        {
            try
            {
                using (Font template = SystemFonts.MessageBoxFont)
                    return new Font(template.FontFamily, size, style);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, size, style);
            }
        }

        private static float scaleFactor;

        // Control sizes below are written for 96 DPI. Point-sized fonts grow
        // with the display's DPI on their own, but literal pixel sizes do not:
        // AutoScaleMode.Font cannot help a form built in code, because it has
        // no design-time baseline to compare the runtime font against. Scaling
        // the literals here keeps text inside its controls at any scaling.
        internal static float ScaleFactor
        {
            get
            {
                if (scaleFactor <= 0f)
                {
                    try
                    {
                        using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
                            scaleFactor = screen.DpiX / 96f;
                    }
                    catch
                    {
                        scaleFactor = 1f;
                    }

                    if (scaleFactor < 1f)
                        scaleFactor = 1f;
                }

                return scaleFactor;
            }
        }

        internal static int Px(int value)
        {
            return (int)Math.Round(value * ScaleFactor);
        }

        internal static Size Px(int width, int height)
        {
            return new Size(Px(width), Px(height));
        }
    }
}
