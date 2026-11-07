using System;
using System.Collections.Generic;
using System.Text;

namespace StationVoice.Synth
{
    public enum AnnounceMode
    {
        Bus = 0,
        Metro = 1
    }

    /// <summary>一个站点：中文名 | 英文名 | 换乘信息。</summary>
    public class Stop
    {
        public string Name = "";
        public string English = "";
        public string Transfer = "";

        public Stop(string name, string english, string transfer)
        {
            Name = name == null ? "" : name.Trim();
            English = english == null ? "" : english.Trim();
            Transfer = transfer == null ? "" : transfer.Trim();
        }

        /// <summary>解析一行站点，支持 "站名|English|换乘" 与 "站名, English" 两种写法。</summary>
        public static Stop Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new Stop("", "", "");
            string line = raw.Trim();
            if (line.StartsWith("#")) return null;
            if (line.Length == 0) return null;

            string[] parts = line.Split('|');
            if (parts.Length >= 2)
            {
                return new Stop(parts[0], parts[1], parts.Length >= 3 ? parts[2] : "");
            }
            int comma = line.IndexOf(',');
            if (comma > 0)
            {
                return new Stop(line.Substring(0, comma), line.Substring(comma + 1), "");
            }
            return new Stop(line, "", "");
        }
    }

    /// <summary>报站词模板与组装。</summary>
    public static class Announcement
    {
        public const string BusStart = "车辆起步，请坐稳扶好。";
        public const string BusNext = "下一站，{0}。要下车的乘客，请提前做好准备。";
        public const string BusArrive = "{0}，到了。请带好随身物品，从后门下车。";
        public const string BusTip = "开门请当心，下车请走好。";

        public const string MetroStart = "欢迎乘坐{0}，本次列车开往{1}方向。";
        public const string MetroNext = "下一站，{0}。";
        public const string MetroNextEnglish = "The next station is {0}.";
        public const string MetroArrive = "{0}，到了。请从左侧车门下车，注意列车与站台之间的空隙。";
        public const string MetroTransfer = "需换乘{0}的乘客，请提前做好准备。";
        public const string MetroTip = "请勿倚靠车门，注意乘车安全。";

        public static List<ScriptLine> Build(AnnounceMode mode, string lineName, string terminal,
            Stop stop, Stop next, bool includeEnglish, bool firstStop)
        {
            if (mode == AnnounceMode.Metro)
                return BuildMetro(lineName, terminal, stop, next, includeEnglish, firstStop);
            return BuildBus(lineName, terminal, stop, next, includeEnglish, firstStop);
        }

        public static List<ScriptLine> BuildBus(string lineName, string terminal, Stop stop, Stop next,
            bool includeEnglish, bool firstStop)
        {
            List<ScriptLine> lines = new List<ScriptLine>();
            if (firstStop && !string.IsNullOrEmpty(lineName))
                lines.Add(ScriptLine.Of("Start", string.Format("欢迎乘坐{0}，本车开往{1}方向。", lineName, Safe(terminal, "终点站"))));

            if (next != null && !string.IsNullOrEmpty(next.Name))
            {
                lines.Add(new ScriptLine("Next", string.Format(BusNext, next.Name), next.Name));
                if (includeEnglish && !string.IsNullOrEmpty(next.English))
                    lines.Add(ScriptLine.Of("English", string.Format("The next stop is {0}.", next.English)));
            }

            lines.Add(new ScriptLine("Arrive", string.Format(BusArrive, Safe(stop.Name, "本站")), Safe(stop.Name, "本站")));
            if (!string.IsNullOrEmpty(stop.Transfer))
                lines.Add(new ScriptLine("Transfer", string.Format("可换乘{0}。", stop.Transfer), stop.Transfer));
            lines.Add(ScriptLine.Of("Tip", BusTip));
            return lines;
        }

        public static List<ScriptLine> BuildMetro(string lineName, string terminal, Stop stop, Stop next,
            bool includeEnglish, bool firstStop)
        {
            List<ScriptLine> lines = new List<ScriptLine>();
            if (firstStop)
                lines.Add(ScriptLine.Of("Start", string.Format(MetroStart, Safe(lineName, "本次列车"), Safe(terminal, "终点站"))));

            if (next != null && !string.IsNullOrEmpty(next.Name))
            {
                lines.Add(new ScriptLine("Next", string.Format(MetroNext, next.Name), next.Name));
                if (includeEnglish && !string.IsNullOrEmpty(next.English))
                    lines.Add(ScriptLine.Of("English", string.Format(MetroNextEnglish, next.English)));
            }

            lines.Add(new ScriptLine("Arrive", string.Format(MetroArrive, Safe(stop.Name, "本站")), Safe(stop.Name, "本站")));
            if (!string.IsNullOrEmpty(stop.Transfer))
                lines.Add(new ScriptLine("Transfer", string.Format(MetroTransfer, stop.Transfer), stop.Transfer));
            lines.Add(ScriptLine.Of("Tip", MetroTip));
            return lines;
        }

        public static string PlainText(IList<ScriptLine> lines)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrEmpty(lines[i].Text)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(lines[i].Text);
            }
            return sb.ToString();
        }

        public static string Preview(AnnounceMode mode, string lineName, string terminal, IList<Stop> stops, bool includeEnglish)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stops.Count; i++)
            {
                Stop next = i + 1 < stops.Count ? stops[i + 1] : null;
                List<ScriptLine> lines = Build(mode, lineName, terminal, stops[i], next, includeEnglish, i == 0);
                sb.Append("【").Append(i + 1).Append("】").AppendLine(stops[i].Name);
                for (int j = 0; j < lines.Count; j++)
                    sb.Append("    ").AppendLine(lines[j].Text);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
