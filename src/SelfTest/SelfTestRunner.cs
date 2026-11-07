using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using StationVoice.Audio;
using StationVoice.Synth;

namespace StationVoice.SelfTest
{
    /// <summary>端到端自测：DSP 精度、SSML、SAPI、MP3/M4A 转码、批量合成。</summary>
    public static class SelfTestRunner
    {
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _pass;
        private static int _fail;
        private static int _skip;
        private static bool _hasChinese;
        private static bool _sapiOk;

        public static int Run(string outDir)
        {
            _pass = 0;
            _fail = 0;
            _skip = 0;
            Report.Length = 0;

            if (string.IsNullOrEmpty(outDir)) outDir = "selftest-output";
            Directory.CreateDirectory(outDir);

            Line("StationVoice 自测报告");
            Line("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Line("系统：" + Environment.OSVersion.VersionString + "　CLR：" + Environment.Version);
            List<VoiceEntry> voices = SafeVoices();
            string voiceNames = "";
            for (int i = 0; i < voices.Count; i++) voiceNames += (i > 0 ? " / " : "") + voices[i].Description;
            Line("系统音色：" + (voices.Count == 0 ? "(无)" : voiceNames));
            bool hasChinese = false;
            for (int i = 0; i < voices.Count; i++)
            {
                if (voices[i].IsChinese) hasChinese = true;
            }
            Line("中文音色：" + (hasChinese ? "有" : "无（中文朗读相关用例将跳过）"));
            _hasChinese = hasChinese;
            Line("");

            string wavPath = Path.Combine(outDir, "tone440.wav");
            string wav16k = Path.Combine(outDir, "tone440_16k.wav");
            string maleWav = Path.Combine(outDir, "synth_male.wav");
            string femaleWav = Path.Combine(outDir, "synth_female.wav");

            Test("01 WAV 写入/读取回环", delegate { return TestWavRoundtrip(wavPath); });
            Test("02 FFT 频率定位", delegate { return TestFft(); });
            Test("03 基频检测（110/220Hz）", delegate { return TestPitch(); });
            Test("04 重采样保持音高", delegate { return TestResample(wavPath, wav16k); });
            Test("05 声色分析：男/女声区分", delegate { return TestAnalyze(maleWav, femaleWav); });
            Test("06 SSML 结构合法", delegate { return TestSsml(); });
            Test("07 SAPI 渲染 WAV" + (hasChinese ? "" : "（跳过）"), delegate { return TestSapi(outDir, hasChinese); });
            Test("08 MP3 转码", delegate { return TestMp3(outDir); });
            Test("09 M4A 转码", delegate { return TestM4a(outDir); });
            Test("10 批量报站 + 合集", delegate { return TestBatch(outDir); });

            Line("");
            Line(string.Format(CultureInfo.InvariantCulture, "汇总：{0} 通过 / {1} 失败 / {2} 跳过", _pass, _fail, _skip));
            bool ok = _fail == 0;

            string reportPath = Path.Combine(outDir, "selftest-report.txt");
            File.WriteAllText(reportPath, Report.ToString(), new UTF8Encoding(false));
            Console.WriteLine();
            Console.WriteLine(Report.ToString());
            Console.WriteLine("报告已写入：" + Path.GetFullPath(reportPath));
            return ok ? 0 : 2;
        }

        private delegate bool TestBody();

        private static void Test(string name, TestBody body)
        {
            try
            {
                bool ok = body();
                if (ok) { _pass++; Line("[PASS] " + name); }
                else { _fail++; Line("[FAIL] " + name); }
            }
            catch (SkipException ex)
            {
                _skip++;
                Line("[SKIP] " + name + "：" + ex.Message);
            }
            catch (Exception ex)
            {
                _fail++;
                Line("[FAIL] " + name + "：异常 " + ex.GetType().Name + " - " + ex.Message);
            }
        }

        private class SkipException : Exception
        {
            public SkipException(string message) : base(message) { }
        }

        private static void Line(string text)
        {
            Report.AppendLine(text);
        }

        private static List<VoiceEntry> SafeVoices()
        {
            try
            {
                return SapiEngine.InstalledVoices();
            }
            catch (Exception)
            {
                return new List<VoiceEntry>();
            }
        }

        private static short[] Sine(double freq, double seconds, int rate, double amplitude)
        {
            int n = (int)(rate * seconds);
            short[] s = new short[n];
            for (int i = 0; i < n; i++)
            {
                double v = Math.Sin(2.0 * Math.PI * freq * i / rate) * amplitude;
                s[i] = (short)Math.Round(v * 32767);
            }
            return s;
        }

        /// <summary>合成近似人声：谐波堆 + 两个共振峰 + 音节包络 + 轻微噪声。</summary>
        private static short[] SynthVoice(double f0, double seconds, int rate, double formant1, double formant2, double syllableRate)
        {
            int n = (int)(rate * seconds);
            short[] s = new short[n];
            Random rnd = new Random(12345);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate;
                double v = 0;
                for (int h = 1; h <= 14; h++)
                {
                    double freq = f0 * h;
                    if (freq > rate / 2.0 - 200) break;
                    double amp = 1.0 / (1.0 + Math.Abs(freq - formant1) / 320.0)
                               + 0.65 / (1.0 + Math.Abs(freq - formant2) / 420.0);
                    v += amp * Math.Sin(2.0 * Math.PI * freq * t + h * 0.7);
                }
                double envelope = 0.35 + 0.65 * Math.Pow(Math.Abs(Math.Sin(Math.PI * syllableRate * t)), 1.6);
                v = v * envelope * 0.09 + (rnd.NextDouble() - 0.5) * 0.004;
                if (v > 1) v = 1;
                if (v < -1) v = -1;
                s[i] = (short)Math.Round(v * 32767);
            }
            return s;
        }

        private static bool TestWavRoundtrip(string path)
        {
            short[] src = Sine(441.0, 1.0, 44100, 0.5);
            WavIO.Write(path, src, 44100);
            WavData data = WavIO.Read(path);

            if (Math.Abs(data.Samples.Length - src.Length) > 2) return false;
            if (data.SampleRate != 44100) return false;

            double err = 0;
            for (int i = 0; i < Math.Min(2000, src.Length); i++) err += Math.Abs(src[i] - data.Samples[i]);
            err /= 2000.0;
            Line("       样本数 " + data.Samples.Length + "，平均误差 " + err.ToString("0.0", CultureInfo.InvariantCulture));
            return err < 2.0;
        }

        private static bool TestFft()
        {
            int fftSize = 1024;
            double[] frame = new double[fftSize];
            for (int i = 0; i < fftSize; i++) frame[i] = Math.Sin(2.0 * Math.PI * 1000.0 * i / 16000.0);
            double[] mag = Dsp.Spectrum(frame, fftSize);

            int peak = 1;
            for (int i = 2; i < mag.Length; i++) if (mag[i] > mag[peak]) peak = i;
            double freq = peak * 16000.0 / fftSize;
            Line("       峰值频点 " + freq.ToString("0.0", CultureInfo.InvariantCulture) + " Hz");
            return Math.Abs(freq - 1000.0) <= 20.0;
        }

        private static bool TestPitch()
        {
            int rate = 16000;
            short[] low = SynthVoice(110, 1.2, rate, 600, 1100, 3.5);
            short[] high = SynthVoice(220, 1.2, rate, 800, 1600, 3.5);

            double c1, c2;
            double f1 = Dsp.DetectPitch(Dsp.ExtractFrame(low, 4000, 640), rate, out c1);
            double f2 = Dsp.DetectPitch(Dsp.ExtractFrame(high, 4000, 640), rate, out c2);
            Line(string.Format(CultureInfo.InvariantCulture, "       测得 {0:0.0} Hz / {1:0.0} Hz（清晰度 {2:0.00}/{3:0.00}）", f1, f2, c1, c2));

            return Math.Abs(f1 - 110) / 110.0 < 0.08 && Math.Abs(f2 - 220) / 220.0 < 0.08;
        }

        private static bool TestResample(string srcPath, string dstPath)
        {
            WavData src = WavIO.Read(srcPath);
            short[] resampled = WavIO.Resample(src.Samples, src.SampleRate, 16000);
            WavIO.Write(dstPath, resampled, 16000);

            double clarity;
            double f = Dsp.DetectPitch(Dsp.ExtractFrame(resampled, 4000, 640), 16000, out clarity);
            Line(string.Format(CultureInfo.InvariantCulture, "       重采样后测得 {0:0.0} Hz（期望 441）", f));
            return Math.Abs(f - 441.0) < 12.0;
        }

        private static bool TestAnalyze(string malePath, string femalePath)
        {
            short[] male = SynthVoice(112, 2.4, 44100, 550, 1000, 3.2);
            short[] female = SynthVoice(232, 2.4, 44100, 850, 1750, 4.2);
            WavIO.Write(malePath, male, 44100);
            WavIO.Write(femalePath, female, 44100);

            TimbreProfile pm = TimbreAnalyzer.AnalyzeFile(malePath);
            TimbreProfile pf = TimbreAnalyzer.AnalyzeFile(femalePath);

            Line(string.Format(CultureInfo.InvariantCulture, "       男声样本 F0={0:0.0}Hz → {1}（置信 {2:0}%）", pm.F0Median, pm.MatchedPreset, pm.Confidence * 100));
            Line(string.Format(CultureInfo.InvariantCulture, "       女声样本 F0={0:0.0}Hz → {1}（置信 {2:0}%）", pf.F0Median, pf.MatchedPreset, pf.Confidence * 100));
            Line(string.Format(CultureInfo.InvariantCulture, "       语速估计 {0:0.00} / {1:0.00} 音节每秒", pm.SpeakingRate, pf.SpeakingRate));

            bool f0ok = Math.Abs(pm.F0Median - 112) / 112.0 < 0.12 && Math.Abs(pf.F0Median - 232) / 232.0 < 0.12;
            bool genderOk = pm.MatchedGender == "男" && pf.MatchedGender == "女";
            bool rateOk = pm.SpeakingRate > 1.8 && pm.SpeakingRate < 6.5;
            return f0ok && genderOk && rateOk;
        }

        private static bool TestSsml()
        {
            TimbreProfile p = new TimbreProfile();
            TimbrePresets.ApplyPreset(p, TimbrePresets.Find("女声·地铁（清晰）"));

            List<ScriptLine> lines = new List<ScriptLine>();
            lines.Add(new ScriptLine("Next", "下一站，人民广场。", "人民广场"));
            lines.Add(ScriptLine.Of("Tips", "请从左侧车门下车 & 注意脚下安全。"));

            string ssml = SsmlBuilder.Build(p, "Microsoft Huihui Desktop", lines);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(ssml);

            bool hasProsody = ssml.IndexOf("<prosody", StringComparison.Ordinal) >= 0;
            bool hasBreak = ssml.IndexOf("<break", StringComparison.Ordinal) >= 0;
            bool hasEmphasis = ssml.IndexOf("<emphasis", StringComparison.Ordinal) >= 0;
            bool escaped = ssml.IndexOf("&amp;", StringComparison.Ordinal) >= 0;
            Line("       SSML 长度 " + ssml.Length + "，结构解析通过");
            return hasProsody && hasBreak && hasEmphasis && escaped;
        }

        private static bool TestSapi(string outDir, bool hasChinese)
        {
            string path = Path.Combine(outDir, "sapi_speech.wav");
            using (SapiEngine engine = new SapiEngine())
            {
                VoiceEntry voice = engine.SelectVoice("");
                if (voice == null)
                    throw new SkipException("系统没有可用的 SAPI 音色");

                TimbreProfile p = new TimbreProfile();
                TimbrePresets.ApplyPreset(p, TimbrePresets.Find("女声·地铁（清晰）"));
                List<ScriptLine> lines = new List<ScriptLine>();
                lines.Add(new ScriptLine("Next", "下一站，人民广场。", "人民广场"));
                lines.Add(ScriptLine.Of("Tip", "请从左侧车门下车。"));

                string ssml = SsmlBuilder.Build(p, "", lines);
                bool ssmlOk = engine.Render(ssml, Announcement.PlainText(lines), path, 44100, p.RatePct);
                long size = new FileInfo(path).Length;

                Line("       音色：" + voice.Description + "　SSML 生效：" + (ssmlOk ? "是" : "否") + "　文件 " + (size / 1024) + " KB");
                if (!ssmlOk && size < 2000)
                {
                    _sapiOk = false;
                    throw new SkipException("本机语音引擎无法渲染（" + engine.LastError + "）");
                }
                _sapiOk = true;

                if (!hasChinese)
                    throw new SkipException("未安装中文音色，中文合成质量无法评估");

                if (!ssmlOk) return false;
                return size > 2000;
            }
        }

        private static bool TestMp3(string outDir)
        {
            string wav = Path.Combine(outDir, "tone440.wav");
            if (!File.Exists(wav)) wav = EnsureTone(outDir);

            string mp3 = Path.Combine(outDir, "tone440.mp3");
            MfTranscoder.Transcode(wav, mp3, AudioFormat.Mp3, 128);
            if (!File.Exists(mp3)) return false;

            byte[] head = ReadHead(mp3, 8);
            bool isMp3 = (head[0] == 0x49 && head[1] == 0x44 && head[2] == 0x33) ||
                         (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0);
            long size = new FileInfo(mp3).Length;
            Line("       输出 " + (size / 1024) + " KB，文件头 " + ToHex(head));
            return isMp3 && size > 1000;
        }

        private static bool TestM4a(string outDir)
        {
            string wav = Path.Combine(outDir, "tone440.wav");
            if (!File.Exists(wav)) wav = EnsureTone(outDir);

            string m4a = Path.Combine(outDir, "tone440.m4a");
            MfTranscoder.Transcode(wav, m4a, AudioFormat.M4a, 128);
            if (!File.Exists(m4a)) return false;

            byte[] head = ReadHead(m4a, 12);
            bool isM4a = head.Length >= 8 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70;
            long size = new FileInfo(m4a).Length;
            Line("       输出 " + (size / 1024) + " KB，文件头 " + ToHex(head));
            return isM4a && size > 1000;
        }

        private static bool TestBatch(string outDir)
        {
            string dir = Path.Combine(outDir, "batch");
            List<Stop> stops = new List<Stop>();
            string lineName;
            string terminal;
            if (_hasChinese)
            {
                stops.Add(new Stop("人民广场", "People's Square", "1号线"));
                stops.Add(new Stop("南京东路", "Nanjing East Road", ""));
                stops.Add(new Stop("陆家嘴", "Lujiazui", "2号线"));
                lineName = "地铁2号线";
                terminal = "陆家嘴";
            }
            else
            {
                // 没有中文音色时改用英文站名，保证这条流水线用例在任意语言环境下都能验证
                stops.Add(new Stop("People's Square", "People's Square", ""));
                stops.Add(new Stop("Nanjing East Road", "Nanjing East Road", ""));
                stops.Add(new Stop("Lujiazui", "Lujiazui", ""));
                lineName = "Metro Line 2";
                terminal = "Lujiazui";
            }

            TimbreProfile p = new TimbreProfile();
            TimbrePresets.ApplyPreset(p, TimbrePresets.Find("女声·地铁（清晰）"));

            SynthOptions options = new SynthOptions();
            options.Format = AudioFormat.Mp3;
            options.OutputDir = dir;
            options.Merge = true;
            options.KeepWav = true;
            if (!_sapiOk)
            {
                options.MockSpeech = true;
                Line("       语音引擎不可用，本条用例改用管道自检模式（合成音代替人声）");
            }

            SynthResult result = StationSynthesizer.RenderStops(p, AnnounceMode.Metro, lineName, terminal, stops, options, null, null);
            Line("       生成 " + result.Files.Count + " 个分段，合集 " + (result.Merged != null ? "有" : "无"));
            for (int i = 0; i < result.Files.Count; i++)
            {
                Line(string.Format(CultureInfo.InvariantCulture, "         {0}  {1:0.0}s  {2} KB",
                    Path.GetFileName(result.Files[i].Path), result.Files[i].Seconds, result.Files[i].Bytes / 1024.0));
            }
            if (result.Merged != null)
            {
                Line(string.Format(CultureInfo.InvariantCulture, "         合集 {0:0.0}s  {1} KB",
                    result.Merged.Seconds, result.Merged.Bytes / 1024.0));
            }

            if (result.Files.Count != 3) return false;
            for (int i = 0; i < result.Files.Count; i++)
            {
                if (!File.Exists(result.Files[i].Path) || result.Files[i].Bytes < 500) return false;
            }
            if (result.Merged == null || !File.Exists(result.Merged.Path) || result.Merged.Bytes < 1000) return false;
            if (_hasChinese && !result.SsmlAccepted) return false;
            return true;
        }

        private static string EnsureTone(string outDir)
        {
            string path = Path.Combine(outDir, "tone440.wav");
            WavIO.Write(path, Sine(441.0, 1.0, 44100, 0.5), 44100);
            return path;
        }

        private static byte[] ReadHead(string path, int count)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[count];
                int read = fs.Read(buffer, 0, count);
                if (read == count) return buffer;
                byte[] small = new byte[read];
                Array.Copy(buffer, small, read);
                return small;
            }
        }

        private static string ToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++) sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
            return sb.ToString().Trim();
        }
    }
}
