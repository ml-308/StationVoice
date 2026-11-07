using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using StationVoice.Audio;

namespace StationVoice.Synth
{
    /// <summary>一句报站词。</summary>
    public class ScriptLine
    {
        public string Kind;       // Start / Next / Arrive / Transfer / Tip / English
        public string Text;
        public string Highlight;  // 需要重读的片段（通常是站名）

        public ScriptLine(string kind, string text, string highlight)
        {
            Kind = kind;
            Text = text;
            Highlight = highlight;
        }

        public static ScriptLine Of(string kind, string text)
        {
            return new ScriptLine(kind, text, null);
        }
    }

    /// <summary>把「声色画像」翻译成 SAPI 可解析的 SSML。</summary>
    public static class SsmlBuilder
    {
        public static string Build(TimbreProfile profile, string voiceName, IList<ScriptLine> lines)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\">");
            if (!string.IsNullOrEmpty(voiceName))
            {
                sb.Append("<voice name=\"").Append(Escape(voiceName)).Append("\">");
            }

            sb.Append("<prosody").Append(ProsodyAttributes(profile)).Append(">");

            for (int i = 0; i < lines.Count; i++)
            {
                ScriptLine line = lines[i];
                if (string.IsNullOrEmpty(line.Text)) continue;
                AppendSentence(sb, profile, line);
                if (i < lines.Count - 1)
                {
                    sb.Append("<break time=\"").Append(PauseFor(profile, line.Kind)).Append("ms\"/>");
                }
            }

            sb.Append("</prosody>");
            if (!string.IsNullOrEmpty(voiceName)) sb.Append("</voice>");
            sb.Append("</speak>");
            return sb.ToString();
        }

        private static string ProsodyAttributes(TimbreProfile p)
        {
            StringBuilder sb = new StringBuilder();
            if (p.RatePct != 0) sb.Append(" rate=\"").Append(Signed(p.RatePct)).Append("%\"");
            if (p.PitchPct != 0) sb.Append(" pitch=\"").Append(Signed(p.PitchPct)).Append("%\"");
            int vol = p.VolumePct;
            if (vol > 100) vol = 100;
            if (vol < 1) vol = 1;
            sb.Append(" volume=\"").Append(vol.ToString(CultureInfo.InvariantCulture)).Append("\"");
            return sb.ToString();
        }

        private static string Signed(int v)
        {
            if (v > 0) return "+" + v.ToString(CultureInfo.InvariantCulture);
            return v.ToString(CultureInfo.InvariantCulture);
        }

        private static void AppendSentence(StringBuilder sb, TimbreProfile p, ScriptLine line)
        {
            string text = line.Text;
            if (!string.IsNullOrEmpty(line.Highlight))
            {
                int idx = text.IndexOf(line.Highlight, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    sb.Append(Escape(text.Substring(0, idx)));
                    sb.Append("<emphasis level=\"strong\">").Append(Escape(line.Highlight)).Append("</emphasis>");
                    sb.Append(Escape(text.Substring(idx + line.Highlight.Length)));
                    return;
                }
            }
            sb.Append(Escape(text));
        }

        public static int PauseFor(TimbreProfile profile, string kind)
        {
            int basePause = profile.PauseMs;
            if (basePause < 120) basePause = 120;

            if (kind == "Next") return (int)(basePause * 0.75);
            if (kind == "Arrive") return (int)(basePause * 1.05);
            if (kind == "Transfer") return (int)(basePause * 0.65);
            if (kind == "Tip") return (int)(basePause * 0.55);
            if (kind == "English") return (int)(basePause * 1.10);
            return basePause;
        }

        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                       .Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}
