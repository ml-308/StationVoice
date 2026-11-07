using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using StationVoice.Audio;
using StationVoice.Core;

namespace StationVoice.Synth
{
    public class SynthOptions
    {
        public AudioFormat Format = AudioFormat.Mp3;
        public string VoiceName = "";
        public string OutputDir = "output";
        public int SampleRate = 44100;
        public int BitrateKbps = 128;
        public bool Merge = true;
        public bool KeepWav = true;
        public bool IncludeEnglish = false;
        public int GapMs = 350;
    }

    public class SynthFileInfo
    {
        public string Path = "";
        public string Title = "";
        public long Bytes;
        public double Seconds;
    }

    public class SynthResult
    {
        public List<SynthFileInfo> Files = new List<SynthFileInfo>();
        public SynthFileInfo Merged;
        public bool SsmlAccepted = true;
        public string VoiceUsed = "";
    }

    public delegate void SynthProgress(int done, int total, string title);

    /// <summary>把「脚本 + 声色画像」渲染成文件。</summary>
    public static class StationSynthesizer
    {
        public static SynthResult RenderStops(TimbreProfile profile, AnnounceMode mode, string lineName, string terminal,
            IList<Stop> stops, SynthOptions options, LogBus log, SynthProgress progress)
        {
            if (stops == null || stops.Count == 0)
                throw new ArgumentException("站点列表为空，至少需要一个站名。");

            List<List<ScriptLine>> jobs = new List<List<ScriptLine>>();
            List<string> titles = new List<string>();
            for (int i = 0; i < stops.Count; i++)
            {
                Stop next = i + 1 < stops.Count ? stops[i + 1] : null;
                jobs.Add(Announcement.Build(mode, lineName, terminal, stops[i], next, options.IncludeEnglish, i == 0));
                titles.Add(string.Format(CultureInfo.InvariantCulture, "{0:00}_{1}", i + 1, stops[i].Name));
            }

            string safeTitle = SafeName(string.IsNullOrEmpty(lineName) ? "报站合集" : lineName);
            return RenderJobs(profile, jobs, titles, safeTitle, options, log, progress);
        }

        public static SynthResult RenderLines(TimbreProfile profile, IList<ScriptLine> lines, string title,
            SynthOptions options, LogBus log)
        {
            List<List<ScriptLine>> jobs = new List<List<ScriptLine>>();
            jobs.Add(new List<ScriptLine>(lines));
            List<string> titles = new List<string>();
            titles.Add(string.IsNullOrEmpty(title) ? "报站" : title);
            return RenderJobs(profile, jobs, titles, SafeName(string.IsNullOrEmpty(title) ? "报站" : title), options, log, null);
        }

        public static SynthResult RenderText(TimbreProfile profile, string text, string title,
            SynthOptions options, LogBus log)
        {
            List<ScriptLine> lines = new List<ScriptLine>();
            string[] raw = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string t = raw[i].Trim();
                if (t.Length > 0) lines.Add(ScriptLine.Of("Tip", t));
            }
            return RenderLines(profile, lines, title, options, log);
        }

        private static SynthResult RenderJobs(TimbreProfile profile, List<List<ScriptLine>> jobs, List<string> titles,
            string safeTitle, SynthOptions options, LogBus log, SynthProgress progress)
        {
            SynthResult result = new SynthResult();
            if (log == null) log = new LogBus();
            string outDir = options.OutputDir;
            if (string.IsNullOrEmpty(outDir)) outDir = "output";
            string tmpDir = Path.Combine(outDir, "_wav");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(tmpDir);

            string ext = AudioFormats.Extension(options.Format);
            List<short[]> parts = new List<short[]>();

            using (SapiEngine engine = new SapiEngine())
            {
                string voice = engine.SelectVoice(options.VoiceName);
                result.VoiceUsed = voice;
                log.Info("音色：" + (string.IsNullOrEmpty(voice) ? "(系统默认)" : voice));
                if (!string.IsNullOrEmpty(voice) && !IsChineseVoice(voice))
                    log.Warn("当前音色不是中文语音，中文报站词可能会读不出来；建议在系统里安装中文语音包。");

                for (int i = 0; i < jobs.Count; i++)
                {
                    string title = titles[i];
                    string fileBase = SafeName(title);
                    string wavPath = Path.Combine(tmpDir, fileBase + ".wav");
                    string ssml = SsmlBuilder.Build(profile, "", jobs[i]);
                    string plain = Announcement.PlainText(jobs[i]);

                    bool ssmlOk = engine.Render(ssml, plain, wavPath, options.SampleRate, profile.RatePct);
                    if (!ssmlOk)
                    {
                        result.SsmlAccepted = false;
                        log.Warn(title + "：SSML 未被系统接受，已回退为纯文本（韵律参数失效）");
                    }

                    WavData data = WavIO.Read(wavPath);
                    short[] trimmed = WavIO.TrimSilence(data.Samples, data.SampleRate, 120);
                    parts.Add(trimmed);

                    string target = Path.Combine(outDir, fileBase + ext);
                    if (options.Format == AudioFormat.Wav)
                    {
                        WavIO.Write(target, trimmed, data.SampleRate);
                    }
                    else
                    {
                        MfTranscoder.Transcode(trimmed, data.SampleRate, target, options.Format, options.BitrateKbps);
                    }

                    if (!options.KeepWav) TryDelete(wavPath);

                    FileInfo fi = new FileInfo(target);
                    SynthFileInfo info = new SynthFileInfo();
                    info.Path = target;
                    info.Title = title;
                    info.Bytes = fi.Exists ? fi.Length : 0;
                    info.Seconds = trimmed.Length / (double)data.SampleRate;
                    result.Files.Add(info);

                    log.Info(string.Format(CultureInfo.InvariantCulture, "完成 {0} → {1}（{2:0.0} s，{3} KB）",
                        title, Path.GetFileName(target), info.Seconds, info.Bytes / 1024.0));

                    if (progress != null) progress(i + 1, jobs.Count, title);
                }
            }

            if (options.Merge && parts.Count > 0)
            {
                short[] merged = WavIO.Concat(parts, options.SampleRate, options.GapMs);
                string mergedWav = Path.Combine(tmpDir, safeTitle + "_合集.wav");
                WavIO.Write(mergedWav, merged, options.SampleRate);

                string mergedOut = Path.Combine(outDir, safeTitle + "_合集" + ext);
                if (options.Format == AudioFormat.Wav)
                    WavIO.Write(mergedOut, merged, options.SampleRate);
                else
                    MfTranscoder.Transcode(merged, options.SampleRate, mergedOut, options.Format, options.BitrateKbps);

                if (!options.KeepWav) TryDelete(mergedWav);

                FileInfo mfi = new FileInfo(mergedOut);
                SynthFileInfo mergedInfo = new SynthFileInfo();
                mergedInfo.Path = mergedOut;
                mergedInfo.Title = safeTitle + "（合集）";
                mergedInfo.Bytes = mfi.Exists ? mfi.Length : 0;
                mergedInfo.Seconds = merged.Length / (double)options.SampleRate;
                result.Merged = mergedInfo;

                log.Info(string.Format(CultureInfo.InvariantCulture, "合集 → {0}（{1:0.0} s，{2} KB）",
                    Path.GetFileName(mergedOut), mergedInfo.Seconds, mergedInfo.Bytes / 1024.0));
            }

            return result;
        }

        public static bool IsChineseVoice(string voiceName)
        {
            if (string.IsNullOrEmpty(voiceName)) return false;
            string lower = voiceName.ToLowerInvariant();
            if (lower.IndexOf("huihui", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("kangkang", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("yaoyao", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("xiaoxiao", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("yunxi", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("chinese", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("zh-cn", StringComparison.Ordinal) >= 0) return true;
            if (voiceName.IndexOf("中文", StringComparison.Ordinal) >= 0) return true;
            if (voiceName.IndexOf("华", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        public static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "track";
            char[] invalid = Path.GetInvalidFileNameChars();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j]) { bad = true; break; }
                }
                if (bad) continue;
                if (c == ' ' || c == '\t') continue;
                sb.Append(c);
            }
            string cleaned = sb.ToString();
            if (cleaned.Length == 0) return "track";
            if (cleaned.Length > 60) cleaned = cleaned.Substring(0, 60);
            return cleaned;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
            }
        }
    }
}
