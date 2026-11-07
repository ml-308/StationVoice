using System;
using System.Collections.Generic;
using System.Globalization;

namespace StationVoice.Audio
{
    /// <summary>声色分析器：把一段参考录音压缩成可解释的声学画像。</summary>
    public static class TimbreAnalyzer
    {
        public const int AnalysisRate = 16000;
        private const int FrameLength = 640;   // 40 ms @16k
        private const int HopLength = 160;     // 10 ms
        private const int FftSize = 1024;

        private static readonly double[] BandEdges = new double[] { 0, 500, 1000, 2000, 4000, 8000 };

        public static TimbreProfile AnalyzeFile(string path)
        {
            WavData wav = WavIO.Read(path);
            TimbreProfile p = Analyze(wav.Samples, wav.SampleRate, System.IO.Path.GetFileName(path));
            p.DurationSeconds = wav.Duration;
            return p;
        }

        public static TimbreProfile Analyze(short[] samples, int sampleRate, string sourceName)
        {
            TimbreProfile p = new TimbreProfile();
            p.SourceName = sourceName;

            if (samples == null || samples.Length < FrameLength)
            {
                p.MatchedPreset = "样本过短";
                p.Confidence = 0;
                return p;
            }

            short[] mono = sampleRate == AnalysisRate ? samples : WavIO.Resample(samples, sampleRate, AnalysisRate);

            // 只分析前 12 秒，足够覆盖一句报站，也避免长录音拖慢速度
            int maxSamples = AnalysisRate * 12;
            if (mono.Length > maxSamples)
            {
                short[] trimmed = new short[maxSamples];
                Array.Copy(mono, trimmed, maxSamples);
                mono = trimmed;
            }

            int frames = (mono.Length - FrameLength) / HopLength + 1;
            if (frames < 3) frames = Math.Min(3, Math.Max(1, frames));

            double[] frameRms = new double[frames];
            double[] frameRaw = new double[frames];
            for (int i = 0; i < frames; i++)
            {
                double[] f = Dsp.ExtractFrame(mono, i * HopLength, FrameLength);
                frameRaw[i] = Dsp.Rms(f);
                frameRms[i] = frameRaw[i];
            }
            double maxRms = 0;
            for (int i = 0; i < frames; i++) if (frameRms[i] > maxRms) maxRms = frameRms[i];
            if (maxRms <= 1e-5)
            {
                p.MatchedPreset = "静音样本";
                p.Confidence = 0;
                return p;
            }

            double voicedThreshold = Math.Max(maxRms * 0.12, 0.004);

            List<double> f0s = new List<double>();
            List<double> centrums = new List<double>();
            List<double> clarities = new List<double>();
            List<double> energies = new List<double>();
            double[] bandAccum = new double[5];
            double bandWeightSum = 0;
            int voicedFrames = 0;

            for (int i = 0; i < frames; i++)
            {
                if (frameRms[i] < voicedThreshold) continue;

                double[] f = Dsp.ExtractFrame(mono, i * HopLength, FrameLength);
                double clarity;
                double f0 = Dsp.DetectPitch(f, AnalysisRate, out clarity);
                if (f0 <= 0 || clarity < 0.35) continue;
                if (f0 < 55 || f0 > 520) continue;

                voicedFrames++;
                f0s.Add(f0);
                clarities.Add(clarity);
                energies.Add(frameRms[i]);

                double[] mag = Dsp.Spectrum(f, FftSize);
                double centroid = Dsp.SpectralCentroid(mag, AnalysisRate, FftSize);
                centrums.Add(centroid);

                double[] ratios = Dsp.BandRatios(mag, AnalysisRate, FftSize, BandEdges);
                double w = frameRms[i] * frameRms[i];
                for (int b = 0; b < 5; b++) bandAccum[b] += ratios[b] * w;
                bandWeightSum += w;
            }

            p.VoicedRatio = frames > 0 ? voicedFrames / (double)frames : 0;
            p.DurationSeconds = mono.Length / (double)AnalysisRate;

            if (f0s.Count == 0)
            {
                p.MatchedPreset = "未检测到有效人声";
                p.Confidence = 0;
                p.Centroid = Dsp.Mean(centrums.ToArray());
                p.LoudnessDbfs = ToDbfs(Dsp.Mean(frameRaw));
                return p;
            }

            double[] f0arr = f0s.ToArray();
            p.F0Median = Dsp.Percentile(f0arr, 0.5);
            p.F0Low = Dsp.Percentile(f0arr, 0.10);
            p.F0High = Dsp.Percentile(f0arr, 0.90);

            double[] semitones = new double[f0arr.Length];
            for (int i = 0; i < f0arr.Length; i++) semitones[i] = 12.0 * Math.Log(Math.Max(1.0, f0arr[i]) / 100.0) / Math.Log(2.0);
            p.F0StdSemitone = Dsp.StdDev(semitones);

            p.Centroid = Dsp.Mean(centrums.ToArray());
            p.Brightness = p.Centroid / (AnalysisRate / 2.0);
            p.Clarity = Dsp.Mean(clarities.ToArray());
            p.LoudnessDbfs = ToDbfs(Dsp.Mean(energies.ToArray()));

            if (bandWeightSum > 1e-9)
            {
                for (int b = 0; b < 5; b++) p.Bands[b] = bandAccum[b] / bandWeightSum;
            }

            double voicedSeconds = voicedFrames * HopLength / (double)AnalysisRate;
            List<int> peaks = Dsp.EnvelopePeaks(mono, AnalysisRate, 25, 130, 0.28);
            if (voicedSeconds > 0.2 && peaks.Count > 0)
                p.SpeakingRate = peaks.Count / voicedSeconds;
            else
                p.SpeakingRate = 3.6;

            if (p.SpeakingRate < 1.2) p.SpeakingRate = 1.2;
            if (p.SpeakingRate > 9.0) p.SpeakingRate = 9.0;

            TimbrePresets.Match(p, 1.8);
            return p;
        }

        private static double ToDbfs(double rms)
        {
            if (rms <= 1e-6) return -96;
            double db = 20.0 * Math.Log10(rms);
            if (db < -96) db = -96;
            return db;
        }

        public static string FormatReport(TimbreProfile p)
        {
            return p.Describe();
        }
    }
}
