using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace StationVoice.Synth
{
    /// <summary>一个可用的 SAPI 音色。</summary>
    public class VoiceEntry
    {
        public string Id = "";
        public string Description = "";
        public bool IsChinese;

        public override string ToString()
        {
            return Description;
        }
    }

    /// <summary>
    /// 通过 SAPI 5 COM 接口（SAPI.SpVoice / SAPI.SpFileStream）合成语音。
    /// 用 COM 而不是 System.Speech，原因有两点：
    /// 1) System.Speech 在初始化时会枚举默认音频输出设备，无声卡的机器（服务器 / CI）直接抛 NRE；
    /// 2) SpFileStream 可以完全绕过音频设备，把 SSML 直接渲染成 WAV 文件。
    /// </summary>
    public class SapiEngine : IDisposable
    {
        private const int SVSFIsXML = 8;
        private const int SSFMCreateForWrite = 3;

        private readonly Type _voiceType;
        private readonly object _voice;
        private VoiceEntry _current;

        /// <summary>最近一次合成失败的原因（用于诊断）。</summary>
        public string LastError = "";

        public SapiEngine()
        {
            _voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (_voiceType == null)
                throw new InvalidOperationException("系统没有注册 SAPI 语音引擎（SAPI.SpVoice），无法合成语音。");
            _voice = Activator.CreateInstance(_voiceType);
        }

        public VoiceEntry CurrentVoice
        {
            get { return _current; }
        }

        // ———————————————————— 音色枚举 ————————————————————

        public static List<VoiceEntry> InstalledVoices()
        {
            List<VoiceEntry> list = new List<VoiceEntry>();
            Type voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (voiceType == null) return list;

            object voice = null;
            object tokens = null;
            try
            {
                voice = Activator.CreateInstance(voiceType);
                tokens = voiceType.InvokeMember("GetVoices", BindingFlags.InvokeMethod, null, voice, new object[] { "", "" });
                Type tokensType = tokens.GetType();
                int count = Convert.ToInt32(tokensType.InvokeMember("Count", BindingFlags.GetProperty, null, tokens, null), CultureInfo.InvariantCulture);
                for (int i = 0; i < count; i++)
                {
                    object token = tokensType.InvokeMember("Item", BindingFlags.InvokeMethod, null, tokens, new object[] { i });
                    VoiceEntry entry = new VoiceEntry();
                    entry.Id = SafeString(token, "Id");
                    entry.Description = SafeString(token, "GetDescription", 0);
                    if (string.IsNullOrEmpty(entry.Description)) entry.Description = entry.Id;
                    entry.IsChinese = LooksChinese(entry.Id + " " + entry.Description);
                    list.Add(entry);
                }
            }
            catch (Exception)
            {
                // 枚举失败时返回已经拿到的部分，调用方会提示用户
            }
            finally
            {
                ReleaseCom(tokens);
                ReleaseCom(voice);
            }
            return list;
        }

        public static List<VoiceEntry> ChineseVoices()
        {
            List<VoiceEntry> all = InstalledVoices();
            List<VoiceEntry> result = new List<VoiceEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsChinese) result.Add(all[i]);
            }
            return result;
        }

        public static bool LooksChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string lower = text.ToLowerInvariant();
            if (lower.IndexOf("zh-cn", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("zh_cn", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("chinese", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("huihui", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("kangkang", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("yaoyao", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("xiaoxiao", StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("yunxi", StringComparison.Ordinal) >= 0) return true;
            if (text.IndexOf("中文", StringComparison.Ordinal) >= 0) return true;
            if (text.IndexOf("普通话", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>按 Id / 描述 / 模糊匹配选音色；空字符串表示优先中文音色。找不到返回 null。</summary>
        public VoiceEntry SelectVoice(string match)
        {
            List<VoiceEntry> all = InstalledVoices();
            if (all.Count == 0) return null;

            VoiceEntry chosen = null;
            if (!string.IsNullOrEmpty(match))
            {
                for (int i = 0; i < all.Count && chosen == null; i++)
                    if (string.Equals(all[i].Id, match, StringComparison.OrdinalIgnoreCase)) chosen = all[i];
                for (int i = 0; i < all.Count && chosen == null; i++)
                    if (string.Equals(all[i].Description, match, StringComparison.OrdinalIgnoreCase)) chosen = all[i];
                for (int i = 0; i < all.Count && chosen == null; i++)
                    if (all[i].Description.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0) chosen = all[i];
            }
            if (chosen == null)
            {
                for (int i = 0; i < all.Count && chosen == null; i++)
                    if (all[i].IsChinese) chosen = all[i];
            }
            if (chosen == null) chosen = all[0];

            ApplyToken(chosen);
            _current = chosen;
            return chosen;
        }

        private void ApplyToken(VoiceEntry entry)
        {
            object tokens = null;
            try
            {
                tokens = _voiceType.InvokeMember("GetVoices", BindingFlags.InvokeMethod, null, _voice, new object[] { "", "" });
                Type tokensType = tokens.GetType();
                int count = Convert.ToInt32(tokensType.InvokeMember("Count", BindingFlags.GetProperty, null, tokens, null), CultureInfo.InvariantCulture);
                for (int i = 0; i < count; i++)
                {
                    object token = tokensType.InvokeMember("Item", BindingFlags.InvokeMethod, null, tokens, new object[] { i });
                    string id = SafeString(token, "Id");
                    if (string.Equals(id, entry.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _voiceType.InvokeMember("Voice", BindingFlags.SetProperty, null, _voice, new object[] { token });
                        return;
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseCom(tokens);
            }
        }

        // ———————————————————— 合成 ————————————————————

        /// <summary>渲染到 WAV。返回 true 表示 SSML（含韵律参数）生效，false 表示回退为纯文本。</summary>
        public bool Render(string ssml, string plainText, string wavPath, int sampleRate, int fallbackRate)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(wavPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (!string.IsNullOrEmpty(ssml) && SpeakToFile(ssml, SVSFIsXML, wavPath, sampleRate, 0))
                return true;

            int rate = fallbackRate / 3;
            if (rate < -10) rate = -10;
            if (rate > 10) rate = 10;
            SpeakToFile(string.IsNullOrEmpty(plainText) ? " " : plainText, 0, wavPath, sampleRate, rate);
            return false;
        }

        private bool SpeakToFile(string text, int flags, string path, int sampleRate, int rate)
        {
            object stream = null;
            Type streamType = null;
            try
            {
                if (File.Exists(path)) File.Delete(path);
                streamType = Type.GetTypeFromProgID("SAPI.SpFileStream");
                if (streamType == null) throw new InvalidOperationException("系统没有注册 SAPI.SpFileStream，无法写音频文件。");

                stream = Activator.CreateInstance(streamType);
                streamType.InvokeMember("Open", BindingFlags.InvokeMethod, null, stream,
                    new object[] { path, SSFMCreateForWrite, false });
                TrySetFormat(stream, streamType, sampleRate);

                _voiceType.InvokeMember("AudioOutputStream", BindingFlags.SetProperty, null, _voice, new object[] { stream });
                SetRate(rate);
                try
                {
                    _voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, _voice, new object[] { text, flags });
                }
                finally
                {
                    SetRate(0);
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = Describe(ex);
                return false;
            }
            finally
            {
                CloseStream(stream, streamType);
            }
        }

        private static string Describe(Exception ex)
        {
            string text = ex.GetType().Name + ": " + ex.Message;
            Exception inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth < 3)
            {
                text += " <- " + inner.GetType().Name + ": " + inner.Message;
                inner = inner.InnerException;
                depth++;
            }
            return text;
        }

        private void SetRate(int rate)
        {
            try
            {
                _voiceType.InvokeMember("Rate", BindingFlags.SetProperty, null, _voice, new object[] { rate });
            }
            catch (Exception)
            {
            }
        }

        private static void TrySetFormat(object stream, Type streamType, int sampleRate)
        {
            int saft = MapFormat(sampleRate);
            if (saft < 0) return;
            try
            {
                Type formatType = Type.GetTypeFromProgID("SAPI.SpAudioFormat");
                if (formatType == null) return;
                object format = Activator.CreateInstance(formatType);
                formatType.InvokeMember("Type", BindingFlags.SetProperty, null, format, new object[] { saft });
                streamType.InvokeMember("Format", BindingFlags.SetProperty, null, stream, new object[] { format });
            }
            catch (Exception)
            {
                // 设置失败则使用音色默认格式，后续按实际文件采样率处理
            }
        }

        private static int MapFormat(int sampleRate)
        {
            switch (sampleRate)
            {
                case 8000: return 2;     // SAFT8kHz16BitMono
                case 11025: return 6;
                case 16000: return 14;
                case 22050: return 18;
                case 24000: return 22;
                case 32000: return 26;
                case 44100: return 30;   // SAFT44kHz16BitMono
                case 48000: return 34;
                default: return -1;
            }
        }

        private static void CloseStream(object stream, Type streamType)
        {
            if (stream == null || streamType == null) return;
            try
            {
                streamType.InvokeMember("Close", BindingFlags.InvokeMethod, null, stream, new object[0]);
            }
            catch (Exception)
            {
            }
            ReleaseCom(stream);
        }

        private static string SafeString(object comObject, string member, params object[] args)
        {
            try
            {
                object value = comObject.GetType().InvokeMember(member, BindingFlags.InvokeMethod, null, comObject, args);
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
            }
            try
            {
                object value = comObject.GetType().InvokeMember(member, BindingFlags.GetProperty, null, comObject, null);
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static void ReleaseCom(object comObject)
        {
            if (comObject == null) return;
            try
            {
                if (System.Runtime.InteropServices.Marshal.IsComObject(comObject))
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>逐步探测 SAPI 写文件流程，输出每一步的结果（排查环境问题用）。</summary>
        public string Probe(string wavPath)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (File.Exists(wavPath)) File.Delete(wavPath);

            object stream = null;
            Type streamType = Type.GetTypeFromProgID("SAPI.SpFileStream");
            sb.AppendLine("SpFileStream 类型 = " + (streamType == null ? "缺失" : "OK"));
            if (streamType == null) return sb.ToString();

            try
            {
                stream = Activator.CreateInstance(streamType);
                streamType.InvokeMember("Open", BindingFlags.InvokeMethod, null, stream,
                    new object[] { wavPath, SSFMCreateForWrite, false });
                sb.AppendLine("Open = OK，文件存在=" + File.Exists(wavPath) + "，大小=" + SizeOf(wavPath));

                _voiceType.InvokeMember("AudioOutputStream", BindingFlags.SetProperty, null, _voice, new object[] { stream });
                sb.AppendLine("设置 AudioOutputStream = OK");

                try
                {
                    object cur = _voiceType.InvokeMember("Voice", BindingFlags.GetProperty, null, _voice, new object[0]);
                    sb.AppendLine("当前音色 = " + SafeString(cur, "Id"));
                }
                catch (Exception ex)
                {
                    sb.AppendLine("读取当前音色失败: " + Describe(ex));
                }

                object back = _voiceType.InvokeMember("AudioOutputStream", BindingFlags.GetProperty, null, _voice, new object[0]);
                bool same = false;
                try
                {
                    IntPtr a = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(back);
                    IntPtr b = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(stream);
                    same = a == b;
                }
                catch (Exception)
                {
                }
                sb.AppendLine("回读 AudioOutputStream 与设置值相同 = " + same);

                try
                {
                    _voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, _voice, new object[] { "测试一二三", 0 });
                    sb.AppendLine("Speak(纯文本) = OK");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("Speak(纯文本) 失败: " + Describe(ex));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("异常: " + Describe(ex));
            }
            finally
            {
                CloseStream(stream, streamType);
                sb.AppendLine("关闭后文件大小 = " + SizeOf(wavPath));
            }
            return sb.ToString();
        }

        private static long SizeOf(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                return info.Exists ? info.Length : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public void Dispose()
        {
            try
            {
                _voiceType.InvokeMember("AudioOutputStream", BindingFlags.SetProperty, null, _voice, new object[] { null });
            }
            catch (Exception)
            {
            }
            ReleaseCom(_voice);
        }
    }
}
