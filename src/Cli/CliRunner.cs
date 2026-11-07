using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using StationVoice.Audio;
using StationVoice.Synth;

namespace StationVoice.Cli
{
    /// <summary>命令行入口：便于脚本化与 CI 自动化测试。</summary>
    public static class CliRunner
    {
        public static bool LooksLikeCli(string[] args)
        {
            if (args == null || args.Length == 0) return false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("-", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static int Run(string[] args)
        {
            Dictionary<string, string> opts;
            List<string> positional;
            ParseArgs(args, out opts, out positional);

            if (Has(opts, "help") || Has(opts, "h"))
            {
                PrintHelp();
                return 0;
            }
            if (Has(opts, "selftest"))
            {
                string outDir = Get(opts, "out", Path.Combine(Environment.CurrentDirectory, "selftest-output"));
                return SelfTest.SelfTestRunner.Run(outDir);
            }
            if (Has(opts, "analyze"))
            {
                string file = Get(opts, "analyze", "");
                if (string.IsNullOrEmpty(file) || !File.Exists(file))
                {
                    Console.Error.WriteLine("找不到要分析的 WAV 文件：" + file);
                    return 1;
                }
                TimbreProfile p = TimbreAnalyzer.AnalyzeFile(file);
                if (Has(opts, "json")) Console.WriteLine(p.ToJson());
                else Console.WriteLine(p.Describe());
                string save = Get(opts, "save", "");
                if (!string.IsNullOrEmpty(save)) File.WriteAllText(save, p.ToJson(), Encoding.UTF8);
                return 0;
            }
            if (Has(opts, "presets"))
            {
                Console.WriteLine("可用风格预置：");
                for (int i = 0; i < TimbrePresets.All.Length; i++)
                {
                    VoicePreset ps = TimbrePresets.All[i];
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  {0}　F0≈{1:0}Hz　语速{2:+0;-0;0}%　音调{3:+0;-0;0}%　停顿{4}ms　{5}",
                        ps.Name, ps.F0, ps.RatePct, ps.PitchPct, ps.PauseMs, ps.Memo));
                }
                return 0;
            }
            if (Has(opts, "stops"))
                return RunStops(opts);

            if (Has(opts, "text"))
            {
                string text = Get(opts, "text", "");
                if (string.IsNullOrEmpty(text)) { Console.Error.WriteLine("--text 内容为空"); return 1; }
                return RunText(opts, text);
            }

            PrintHelp();
            return 0;
        }

        private static int RunStops(Dictionary<string, string> opts)
        {
            string stopsFile = Get(opts, "stops", "");
            if (!File.Exists(stopsFile))
            {
                Console.Error.WriteLine("找不到站点文件：" + stopsFile);
                return 1;
            }

            List<Stop> stops = new List<Stop>();
            string[] lines = File.ReadAllLines(stopsFile, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                Stop s = Stop.Parse(lines[i]);
                if (s != null && !string.IsNullOrEmpty(s.Name)) stops.Add(s);
            }
            if (stops.Count == 0)
            {
                Console.Error.WriteLine("站点文件里没有有效站名（每行一个，可用 站名|English|换乘）");
                return 1;
            }

            TimbreProfile profile = BuildProfile(opts);
            SynthOptions options = BuildOptions(opts);
            AnnounceMode mode = Get(opts, "mode", "metro").ToLowerInvariant().StartsWith("b") ? AnnounceMode.Bus : AnnounceMode.Metro;
            string lineName = Get(opts, "line", "地铁1号线");
            string terminal = Get(opts, "terminal", stops[stops.Count - 1].Name);

            Console.WriteLine("模式：" + (mode == AnnounceMode.Metro ? "地铁" : "公交") + "　线路：" + lineName + "　终点：" + terminal);
            Console.WriteLine("站点：" + stops.Count + " 个　格式：" + AudioFormats.Display(options.Format) + "　输出：" + options.OutputDir);
            PrintProfile(profile);

            try
            {
                SynthResult result = StationSynthesizer.RenderStops(profile, mode, lineName, terminal, stops, options, null, null);
                PrintResult(result);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("合成失败：" + ex.Message);
                return 1;
            }
        }

        private static int RunText(Dictionary<string, string> opts, string text)
        {
            TimbreProfile profile = BuildProfile(opts);
            SynthOptions options = BuildOptions(opts);
            string title = Get(opts, "title", "报站");
            Console.WriteLine("格式：" + AudioFormats.Display(options.Format) + "　输出：" + options.OutputDir);
            PrintProfile(profile);
            try
            {
                SynthResult result = StationSynthesizer.RenderText(profile, text, title, options, null);
                PrintResult(result);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("合成失败：" + ex.Message);
                return 1;
            }
        }

        private static TimbreProfile BuildProfile(Dictionary<string, string> opts)
        {
            TimbreProfile profile;
            string sample = Get(opts, "sample", "");
            if (!string.IsNullOrEmpty(sample))
            {
                if (!File.Exists(sample))
                    throw new FileNotFoundException("找不到参考音频：" + sample);
                profile = TimbreAnalyzer.AnalyzeFile(sample);
            }
            else
            {
                profile = new TimbreProfile();
                TimbrePresets.ApplyPreset(profile, TimbrePresets.Find("中性播报（基准）"));
            }

            string presetName = Get(opts, "preset", "");
            if (!string.IsNullOrEmpty(presetName))
            {
                VoicePreset preset = TimbrePresets.Find(presetName);
                if (preset == null)
                {
                    Console.Error.WriteLine("未知预置：" + presetName + "，可用：" +
                        string.Join(" / ", PresetNames()));
                }
                else
                {
                    TimbrePresets.ApplyPreset(profile, preset);
                }
            }

            string rate = Get(opts, "rate", "");
            string pitch = Get(opts, "pitch", "");
            string pause = Get(opts, "pause", "");
            if (rate.Length > 0) profile.RatePct = ParseInt(rate, profile.RatePct);
            if (pitch.Length > 0) profile.PitchPct = ParseInt(pitch, profile.PitchPct);
            if (pause.Length > 0) profile.PauseMs = ParseInt(pause, profile.PauseMs);
            return profile;
        }

        private static string[] PresetNames()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < TimbrePresets.All.Length; i++) names.Add(TimbrePresets.All[i].Name);
            return names.ToArray();
        }

        private static SynthOptions BuildOptions(Dictionary<string, string> opts)
        {
            SynthOptions options = new SynthOptions();
            options.Format = AudioFormats.Parse(Get(opts, "format", "mp3"));
            options.VoiceName = Get(opts, "voice", "");
            options.OutputDir = Get(opts, "out", Path.Combine(Environment.CurrentDirectory, "output"));
            options.BitrateKbps = ParseInt(Get(opts, "bitrate", "128"), 128);
            options.Merge = !Has(opts, "no-merge");
            options.KeepWav = Has(opts, "keep-wav");
            options.IncludeEnglish = Has(opts, "english");
            options.GapMs = ParseInt(Get(opts, "gap", "350"), 350);
            options.SampleRate = 44100;
            return options;
        }

        private static void PrintProfile(TimbreProfile p)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "风格：{0}　语速 {1:+0;-0;0}%　音调 {2:+0;-0;0}%　音量 {3}%　停顿 {4}ms",
                p.MatchedPreset, p.RatePct, p.PitchPct, p.VolumePct, p.PauseMs));
        }

        private static void PrintResult(SynthResult result)
        {
            Console.WriteLine("--- 生成结果 ---");
            for (int i = 0; i < result.Files.Count; i++)
            {
                SynthFileInfo f = result.Files[i];
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}  {1:0.0}s  {2} KB", f.Path, f.Seconds, f.Bytes / 1024.0));
            }
            if (result.Merged != null)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  合集  {0}  {1:0.0}s  {2} KB", result.Merged.Path, result.Merged.Seconds, result.Merged.Bytes / 1024.0));
            }
        }

        private static void ParseArgs(string[] args, out Dictionary<string, string> opts, out List<string> positional)
        {
            opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            positional = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--", StringComparison.Ordinal))
                {
                    string key = a.Substring(2);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        opts[key] = args[i + 1];
                        i++;
                    }
                    else
                    {
                        opts[key] = "true";
                    }
                }
                else if (a.StartsWith("-", StringComparison.Ordinal) && a.Length == 2)
                {
                    string key = a.Substring(1);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        opts[key] = args[i + 1];
                        i++;
                    }
                    else
                    {
                        opts[key] = "true";
                    }
                }
                else
                {
                    positional.Add(a);
                }
            }
        }

        private static bool Has(Dictionary<string, string> opts, string key)
        {
            return opts.ContainsKey(key);
        }

        private static string Get(Dictionary<string, string> opts, string key, string fallback)
        {
            string v;
            if (opts.TryGetValue(key, out v)) return v;
            return fallback;
        }

        private static int ParseInt(string text, int fallback)
        {
            int v;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        public static void PrintHelp()
        {
            Console.WriteLine(@"StationVoice 报站声色模仿器 v1.0.0
把一段参考录音的「声色」分析成参数，再用系统 SAPI 音色合成公交/地铁报站音频。

用法：
  StationVoice.exe                                   启动图形界面
  StationVoice.exe --analyze sample.wav [--json]     分析声色，输出画像
  StationVoice.exe --text ""下一站，人民广场。"" --format mp3
  StationVoice.exe --stops stops.txt --mode metro --line ""地铁2号线"" --format m4a
  StationVoice.exe --selftest --out test-output      运行自测

常用参数：
  --format wav|mp3|m4a      输出格式（默认 mp3）
  --out DIR                 输出目录（默认 ./output）
  --voice NAME              指定 SAPI 音色；省略则自动挑选中文音色
  --sample ref.wav          用参考录音的声色（自动匹配风格）
  --preset NAME             直接指定风格预置，见 --presets
  --presets                 列出所有风格预置
  --rate N --pitch N        覆盖语速/音调百分比（-30..30）
  --pause N                 覆盖句间停顿毫秒
  --bitrate N               MP3/AAC 码率 kbps（默认 128）
  --english                 中英双语报站
  --no-merge                不生成整线合集
  --keep-wav                保留中间 WAV 文件

站点文件格式（每行一个，UTF-8）：
  人民广场|People's Square|1号线
  南京东路|Nanjing East Road
  # 以 # 开头的行为注释");
        }
    }
}
