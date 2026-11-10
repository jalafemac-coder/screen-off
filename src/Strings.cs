using System.Globalization;

namespace ScreenOff
{
    // Every user-visible string. Chinese is used when the OS UI language is
    // Chinese, English otherwise; --lang overrides the detection.
    internal static class Strings
    {
        private static bool chinese = Detect();

        private static bool Detect()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        }

        // Accepts "zh" or "en"; anything else restores culture detection.
        public static void Select(string language)
        {
            if (language == "zh")
                chinese = true;
            else if (language == "en")
                chinese = false;
            else
                chinese = Detect();
        }

        private static string Pick(string zh, string en)
        {
            return chinese ? zh : en;
        }

        public static string PickerTitle { get { return Pick("延时关闭屏幕", "Turn Off Display"); } }
        public static string PickerHint { get { return Pick("多久之后关闭屏幕？", "Turn the display off after:"); } }
        public static string Immediately { get { return Pick("立即", "Now"); } }
        public static string CustomLabel { get { return Pick("自定义", "Custom"); } }
        public static string UnitMinutes { get { return Pick("分钟", "minutes"); } }
        public static string Start { get { return Pick("开始", "Start"); } }
        public static string Cancel { get { return Pick("取消", "Cancel"); } }

        public static string CountdownTitle { get { return Pick("屏幕即将关闭", "Display Off Pending"); } }
        public static string CountdownCaption { get { return Pick("之后关闭屏幕", "until the display turns off"); } }
        public static string ExtendFive { get { return Pick("+5 分钟", "+5 min"); } }
        public static string OffNow { get { return Pick("立即关闭", "Off now"); } }

        public static string UsageTitle { get { return Pick("用法", "Usage"); } }

        public static string MinuteButton(int minutes)
        {
            return minutes + Pick(" 分钟", " min");
        }

        public static string BadDelay(string value)
        {
            return Pick("无法识别的时间参数：", "Unrecognized delay value: ") + value;
        }

        public static string UnknownOption(string value)
        {
            return Pick("无法识别的选项：", "Unrecognized option: ") + value;
        }

        public static string TooManyArguments
        {
            get { return Pick("只能指定一个时间参数。", "Only one delay value may be given."); }
        }

        public static string Usage
        {
            get
            {
                return Pick(
                    "ScreenOff.exe            打开延时选择框\r\n" +
                    "ScreenOff.exe 0          立即关闭屏幕\r\n" +
                    "ScreenOff.exe now        同上\r\n" +
                    "ScreenOff.exe 25         25 分钟后关闭\r\n" +
                    "ScreenOff.exe 90s        后缀 s 秒 / m 分 / h 小时\r\n" +
                    "ScreenOff.exe 1.5h       支持小数\r\n\r\n" +
                    "--lang=zh | --lang=en    指定界面语言\r\n" +
                    "--help                   显示本说明\r\n\r\n" +
                    "不带后缀的数字按分钟计，上限 24 小时。\r\n" +
                    "小于 1 秒的延时按 1 秒处理。",

                    "ScreenOff.exe            open the delay picker\r\n" +
                    "ScreenOff.exe 0          turn the display off now\r\n" +
                    "ScreenOff.exe now        the same\r\n" +
                    "ScreenOff.exe 25         turn it off after 25 minutes\r\n" +
                    "ScreenOff.exe 90s        suffix s = seconds, m = minutes, h = hours\r\n" +
                    "ScreenOff.exe 1.5h       fractions are accepted\r\n\r\n" +
                    "--lang=zh | --lang=en    force the interface language\r\n" +
                    "--help                   show this text\r\n\r\n" +
                    "A bare number means minutes. The delay is capped at 24 hours.\r\n" +
                    "A delay below one second is treated as one second.");
            }
        }
    }
}
