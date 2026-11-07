using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StationVoice.Audio
{
    /// <summary>内置声色预置：一种「报站腔」的风格原型。</summary>
    public class VoicePreset
    {
        public string Name;
        public string Gender;      // 男 / 女
        public double F0;          // 典型基频 Hz
        public double Rate;        // 典型语速（音节/秒）
        public double Centroid;    // 典型谱质心 Hz
        public int RatePct;        // SSML 语速百分比
        public int PitchPct;       // SSML 音调百分比
        public int VolumePct;      // SSML 音量百分比
        public int PauseMs;        // 句间停顿
        public string Memo;        // 风格描述

        public VoicePreset(string name, string gender, double f0, double rate, double centroid,
            int ratePct, int pitchPct, int volumePct, int pauseMs, string memo)
        {
            Name = name;
            Gender = gender;
            F0 = f0;
            Rate = rate;
            Centroid = centroid;
            RatePct = ratePct;
            PitchPct = pitchPct;
            VolumePct = volumePct;
            PauseMs = pauseMs;
            Memo = memo;
        }
    }

    /// <summary>从参考音频提取出的「声色画像」，同时承载推导出的合成参数。</summary>
    public class TimbreProfile
    {
        // —— 测量特征 ——
        public double F0Median = 165;
        public double F0Low = 120;
        public double F0High = 220;
        public double F0StdSemitone = 2.0;
        public double SpeakingRate = 3.6;      // 音节/秒
        public double Centroid = 1400;         // Hz
        public double Brightness = 0.18;       // 质心/奈奎斯特
        public double Clarity = 0.6;           // 谐噪比近似 0..1
        public double LoudnessDbfs = -20;
        public double VoicedRatio = 0.5;
        public double[] Bands = new double[] { 0.2, 0.25, 0.25, 0.2, 0.1 };
        public double DurationSeconds = 0;
        public string SourceName = "";

        // —— 匹配结果 ——
        public string MatchedPreset = "未匹配";
        public string MatchedGender = "";
        public double Confidence = 0;
        public string[] TopMatches = new string[0];

        // —— 推导出的合成参数 ——
        public int RatePct = 0;
        public int PitchPct = 0;
        public int VolumePct = 92;
        public int PauseMs = 500;
        public string VoiceHint = "";

        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("样本：" + (string.IsNullOrEmpty(SourceName) ? "(内存)" : SourceName));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "时长：{0:0.00} s   有效发声占比：{1:0}%", DurationSeconds, VoicedRatio * 100));
            sb.AppendLine();
            sb.AppendLine("—— 声学特征 ——");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "基频 F0 中位数：{0:0.0} Hz（P10 {1:0.0} / P90 {2:0.0}）", F0Median, F0Low, F0High));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "基频起伏：{0:0.00} 半音", F0StdSemitone));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "谱质心：{0:0} Hz（亮度 {1:0.000}）", Centroid, Brightness));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "语速估计：{0:0.00} 音节/秒", SpeakingRate));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "谐噪清晰度：{0:0.00}   响度：{1:0.0} dBFS", Clarity, LoudnessDbfs));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "频带占比 0-0.5k/1k/2k/4k/8k：{0:0.00} {1:0.00} {2:0.00} {3:0.00} {4:0.00}",
                Bands[0], Bands[1], Bands[2], Bands[3], Bands[4]));
            sb.AppendLine();
            sb.AppendLine("—— 适配结果 ——");
            sb.AppendLine("最接近的报站风格：" + MatchedPreset + (string.IsNullOrEmpty(MatchedGender) ? "" : "（" + MatchedGender + "声）"));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "匹配置信度：{0:0}%", Confidence * 100));
            if (TopMatches.Length > 0)
                sb.AppendLine("候选：" + string.Join("；", TopMatches));
            sb.AppendLine();
            sb.AppendLine("—— 合成参数 ——");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "语速 {0:+0;-0;0}%　音调 {1:+0;-0;0}%　音量 {2}%　句间停顿 {3} ms",
                RatePct, PitchPct, VolumePct, PauseMs));
            if (!string.IsNullOrEmpty(VoiceHint)) sb.AppendLine("建议音色：" + VoiceHint);
            return sb.ToString();
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"f0_median\":").Append(N(F0Median)).Append(",");
            sb.Append("\"f0_p10\":").Append(N(F0Low)).Append(",");
            sb.Append("\"f0_p90\":").Append(N(F0High)).Append(",");
            sb.Append("\"f0_std_semitone\":").Append(N(F0StdSemitone)).Append(",");
            sb.Append("\"speaking_rate\":").Append(N(SpeakingRate)).Append(",");
            sb.Append("\"centroid\":").Append(N(Centroid)).Append(",");
            sb.Append("\"brightness\":").Append(N(Brightness)).Append(",");
            sb.Append("\"clarity\":").Append(N(Clarity)).Append(",");
            sb.Append("\"loudness_dbfs\":").Append(N(LoudnessDbfs)).Append(",");
            sb.Append("\"voiced_ratio\":").Append(N(VoicedRatio)).Append(",");
            sb.Append("\"duration\":").Append(N(DurationSeconds)).Append(",");
            sb.Append("\"bands\":[").Append(N(Bands[0])).Append(",").Append(N(Bands[1])).Append(",")
              .Append(N(Bands[2])).Append(",").Append(N(Bands[3])).Append(",").Append(N(Bands[4])).Append("],");
            sb.Append("\"preset\":").Append(Quote(MatchedPreset)).Append(",");
            sb.Append("\"confidence\":").Append(N(Confidence)).Append(",");
            sb.Append("\"rate_pct\":").Append(RatePct.ToString(CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"pitch_pct\":").Append(PitchPct.ToString(CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"volume_pct\":").Append(VolumePct.ToString(CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"pause_ms\":").Append(PauseMs.ToString(CultureInfo.InvariantCulture));
            sb.Append("}");
            return sb.ToString();
        }

        private static string N(double v)
        {
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Quote(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }

    /// <summary>预置库与「声色 → 合成参数」的匹配/推导。</summary>
    public static class TimbrePresets
    {
        public static readonly VoicePreset[] All = new VoicePreset[]
        {
            new VoicePreset("男声·地铁（沉稳）", "男", 108, 3.20, 900, -8, -10, 95, 650, "低平直、句尾下压，典型的车厢内报站腔"),
            new VoicePreset("女声·地铁（清晰）", "女", 208, 3.60, 1500, -5, 0, 92, 600, "中音区、咬字清楚，站台广播常用"),
            new VoicePreset("男声·公交（亲切）", "男", 125, 4.00, 1200, 0, -4, 98, 450, "语速偏快、句尾上扬，车厢内提示音"),
            new VoicePreset("女声·公交（明亮）", "女", 232, 4.30, 1900, 5, 2, 96, 400, "高音区、明亮轻快，适合短提示"),
            new VoicePreset("清亮女声·机场", "女", 250, 3.80, 2200, -2, 6, 90, 500, "高频泛音多，空间感强"),
            new VoicePreset("沉稳男声·高铁", "男", 98, 3.00, 800, -12, -12, 94, 700, "极稳、慢速、低音，强调安全提示"),
            new VoicePreset("中性播报（基准）", "中", 165, 3.90, 1400, 0, 0, 92, 500, "默认基准，无明显个人色彩")
        };

        public static VoicePreset Find(string name)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase)) return All[i];
            }
            return null;
        }

        /// <summary>用测量特征匹配预置并推导 SSML 参数。sigma 控制融合的软硬程度。</summary>
        public static void Match(TimbreProfile p, double sigma)
        {
            int n = All.Length;
            double[] dist = new double[n];
            double[] weight = new double[n];
            double minDist = double.MaxValue;

            for (int i = 0; i < n; i++)
            {
                VoicePreset ps = All[i];
                double df0 = 12.0 * Log2(SafeRatio(p.F0Median, ps.F0)) * 0.9;
                double dc = Log2(SafeRatio(p.Centroid, ps.Centroid)) * 1.0;
                double dr = (p.SpeakingRate - ps.Rate) * 0.45;
                double d = Math.Sqrt(df0 * df0 * 0.8 + dc * dc + dr * dr);
                dist[i] = d;
                if (d < minDist) { minDist = d; }
            }

            if (sigma < 0.2) sigma = 0.2;
            double sumW = 0;
            for (int i = 0; i < n; i++)
            {
                double d = dist[i] - minDist;
                double w = Math.Exp(-(d * d) / (2.0 * sigma * sigma));
                weight[i] = w;
                sumW += w;
            }
            if (sumW <= 1e-12) sumW = 1;

            double baseF0 = 0, baseRate = 0, baseRatePct = 0, basePitchPct = 0, baseVol = 0, basePause = 0;
            int bestIndex = 0;
            for (int i = 0; i < n; i++)
            {
                double w = weight[i] / sumW;
                baseF0 += w * All[i].F0;
                baseRate += w * All[i].Rate;
                baseRatePct += w * All[i].RatePct;
                basePitchPct += w * All[i].PitchPct;
                baseVol += w * All[i].VolumePct;
                basePause += w * All[i].PauseMs;
                if (dist[i] <= minDist + 1e-9) bestIndex = i;
            }

            p.MatchedPreset = All[bestIndex].Name;
            p.MatchedGender = All[bestIndex].Gender;
            p.Confidence = weight[bestIndex] / sumW;

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (dist[order[j]] < dist[order[i]])
                    {
                        int t = order[i]; order[i] = order[j]; order[j] = t;
                    }
                }
            }
            List<string> tops = new List<string>();
            for (int i = 0; i < n && i < 3; i++)
                tops.Add(string.Format(CultureInfo.InvariantCulture, "{0} ({1:0.00})", All[order[i]].Name, dist[order[i]]));
            p.TopMatches = tops.ToArray();

            int pitch = (int)Math.Round(basePitchPct + 1.5 * 12.0 * Log2(SafeRatio(p.F0Median, baseF0)));
            int rate = (int)Math.Round(baseRatePct + 6.0 * (p.SpeakingRate - baseRate));
            int vol = (int)Math.Round(baseVol + (p.LoudnessDbfs + 22.0) * 0.7);
            double pauseScale = baseRate > 0.3 ? baseRate / Math.Max(0.6, p.SpeakingRate) : 1.0;
            int pause = (int)Math.Round(basePause * pauseScale);

            p.PitchPct = Clamp(pitch, -30, 30);
            p.RatePct = Clamp(rate, -30, 30);
            p.VolumePct = Clamp(vol, 55, 100);
            p.PauseMs = Clamp(pause, 250, 1200);
            p.VoiceHint = All[bestIndex].Gender == "女" ? "建议使用系统中文女声（如 Huihui / Xiaoxiao）" :
                          All[bestIndex].Gender == "男" ? "建议使用系统中文男声（如 Kangkang / Yunxi）" : "任意中文音色均可";

            if (p.Brightness > 0.30) p.VolumePct = Clamp(p.VolumePct, 60, 100);
        }

        /// <summary>用人工指定的预置覆盖匹配结果（保留测量特征）。</summary>
        public static void ApplyPreset(TimbreProfile p, VoicePreset ps)
        {
            if (ps == null) return;
            p.MatchedPreset = ps.Name;
            p.MatchedGender = ps.Gender;
            p.Confidence = 1.0;
            p.RatePct = ps.RatePct;
            p.PitchPct = ps.PitchPct;
            p.VolumePct = ps.VolumePct;
            p.PauseMs = ps.PauseMs;
            p.VoiceHint = ps.Gender == "女" ? "建议使用系统中文女声" : ps.Gender == "男" ? "建议使用系统中文男声" : "任意中文音色均可";
        }

        private static double SafeRatio(double a, double b)
        {
            if (b <= 1e-6) b = 1e-6;
            if (a <= 1e-6) a = 1e-6;
            return a / b;
        }

        private static double Log2(double v)
        {
            if (v <= 1e-9) return 0;
            return Math.Log(v) / Math.Log(2.0);
        }

        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
