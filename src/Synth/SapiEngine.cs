using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;

namespace StationVoice.Synth
{
    /// <summary>对 System.Speech（SAPI5）的薄封装：枚举音色、按 SSML 渲染为 WAV。</summary>
    public class SapiEngine : IDisposable
    {
        private readonly SpeechSynthesizer _synth;
        private string _voiceName = "";

        public SapiEngine()
        {
            _synth = new SpeechSynthesizer();
            _synth.SetOutputToNull();
        }

        public string CurrentVoice
        {
            get { return _voiceName; }
        }

        public static List<string> InstalledVoices()
        {
            List<string> result = new List<string>();
            using (SpeechSynthesizer probe = new SpeechSynthesizer())
            {
                foreach (InstalledVoice v in probe.GetInstalledVoices())
                {
                    if (v.Enabled) result.Add(v.VoiceInfo.Name);
                }
            }
            return result;
        }

        public static List<string> ChineseVoices()
        {
            List<string> result = new List<string>();
            using (SpeechSynthesizer probe = new SpeechSynthesizer())
            {
                foreach (InstalledVoice v in probe.GetInstalledVoices())
                {
                    if (!v.Enabled) continue;
                    string culture = v.VoiceInfo.Culture == null ? "" : v.VoiceInfo.Culture.Name;
                    if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                        result.Add(v.VoiceInfo.Name);
                }
            }
            return result;
        }

        /// <summary>选择音色：空则优先中文音色；找不到时退回系统默认音色，不抛异常。</summary>
        public string SelectVoice(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                List<string> chinese = ChineseVoices();
                if (chinese.Count > 0) name = chinese[0];
            }
            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    _synth.SelectVoice(name);
                    _voiceName = name;
                    return _voiceName;
                }
                catch (Exception)
                {
                    // 音色不存在，继续用默认
                }
            }
            _voiceName = _synth.Voice == null ? "" : _synth.Voice.Name;
            return _voiceName;
        }

        /// <summary>渲染到 WAV。返回 true 表示 SSML（含韵律参数）生效，false 表示回退到纯文本朗读。</summary>
        public bool Render(string ssml, string plainText, string wavPath, int sampleRate, int fallbackRate)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(wavPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            SpeechAudioFormatInfo format = new SpeechAudioFormatInfo(
                sampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono);

            bool spoken = false;
            if (!string.IsNullOrEmpty(ssml))
            {
                _synth.SetOutputToWaveFile(wavPath, format);
                try
                {
                    _synth.SpeakSsml(ssml);
                    spoken = true;
                }
                catch (Exception)
                {
                    spoken = false;
                }
                finally
                {
                    _synth.SetOutputToNull();
                }
            }

            if (!spoken)
            {
                if (File.Exists(wavPath)) File.Delete(wavPath);
                _synth.SetOutputToWaveFile(wavPath, format);
                try
                {
                    int rate = fallbackRate / 3;
                    if (rate < -10) rate = -10;
                    if (rate > 10) rate = 10;
                    _synth.Rate = rate;
                    _synth.Speak(string.IsNullOrEmpty(plainText) ? " " : plainText);
                }
                finally
                {
                    _synth.SetOutputToNull();
                    _synth.Rate = 0;
                }
            }
            return spoken;
        }

        public void Dispose()
        {
            try
            {
                _synth.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }
}
