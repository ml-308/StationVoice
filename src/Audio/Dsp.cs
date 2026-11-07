using System;
using System.Collections.Generic;

namespace StationVoice.Audio
{
    /// <summary>基础 DSP：分帧、加窗、FFT、自相关基频、谱质心、频带能量、包络峰值。</summary>
    public static class Dsp
    {
        public static double[] ExtractFrame(short[] src, int offset, int length)
        {
            double[] frame = new double[length];
            for (int i = 0; i < length; i++)
            {
                int idx = offset + i;
                frame[i] = (idx >= 0 && idx < src.Length) ? src[idx] / 32768.0 : 0.0;
            }
            return frame;
        }

        public static void ApplyHamming(double[] frame)
        {
            int n = frame.Length;
            if (n < 2) return;
            double denom = n - 1;
            for (int i = 0; i < n; i++)
                frame[i] *= 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * i / denom);
        }

        public static double Rms(double[] frame)
        {
            if (frame.Length == 0) return 0;
            double sum = 0;
            for (int i = 0; i < frame.Length; i++) sum += frame[i] * frame[i];
            return Math.Sqrt(sum / frame.Length);
        }

        public static double ZeroCrossingRate(double[] frame)
        {
            if (frame.Length < 2) return 0;
            int count = 0;
            for (int i = 1; i < frame.Length; i++)
            {
                if ((frame[i] >= 0 && frame[i - 1] < 0) || (frame[i] < 0 && frame[i - 1] >= 0)) count++;
            }
            return count / (double)(frame.Length - 1);
        }

        /// <summary>原地基-2 FFT，长度必须为 2 的幂。</summary>
        public static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            if (n <= 1) return;

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                while ((j & bit) != 0)
                {
                    j ^= bit;
                    bit >>= 1;
                }
                j ^= bit;
                if (i < j)
                {
                    double tr = re[i]; re[i] = re[j]; re[j] = tr;
                    double ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2.0 * Math.PI / len;
                double wr = Math.Cos(ang);
                double wi = Math.Sin(ang);
                int half = len >> 1;
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1.0, ci = 0.0;
                    for (int k = 0; k < half; k++)
                    {
                        int a = i + k;
                        int b = i + k + half;
                        double xr = re[b] * cr - im[b] * ci;
                        double xi = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - xr;
                        im[b] = im[a] - xi;
                        re[a] += xr;
                        im[a] += xi;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }

        /// <summary>返回 0..fftSize/2 的幅度谱（自动补零并加汉明窗）。</summary>
        public static double[] Spectrum(double[] frame, int fftSize)
        {
            double[] re = new double[fftSize];
            double[] im = new double[fftSize];
            int n = Math.Min(frame.Length, fftSize);
            for (int i = 0; i < n; i++) re[i] = frame[i];
            ApplyHamming(re);
            Fft(re, im);

            int bins = fftSize / 2 + 1;
            double[] mag = new double[bins];
            for (int i = 0; i < bins; i++) mag[i] = Math.Sqrt(re[i] * re[i] + im[i] * im[i]);
            return mag;
        }

        public static double SpectralCentroid(double[] magnitude, int sampleRate, int fftSize)
        {
            double num = 0, den = 0;
            for (int i = 1; i < magnitude.Length; i++)
            {
                double freq = (double)i * sampleRate / fftSize;
                num += freq * magnitude[i];
                den += magnitude[i];
            }
            if (den <= 1e-9) return 0;
            return num / den;
        }

        /// <summary>按给定边界（Hz，升序，末位为奈奎斯特）汇总频带能量占比。</summary>
        public static double[] BandRatios(double[] magnitude, int sampleRate, int fftSize, double[] edges)
        {
            int bands = edges.Length - 1;
            double[] energy = new double[bands];
            double total = 0;
            for (int i = 1; i < magnitude.Length; i++)
            {
                double freq = (double)i * sampleRate / fftSize;
                for (int b = 0; b < bands; b++)
                {
                    if (freq >= edges[b] && freq < edges[b + 1])
                    {
                        energy[b] += magnitude[i] * magnitude[i];
                        total += magnitude[i] * magnitude[i];
                        break;
                    }
                }
            }
            double[] ratio = new double[bands];
            if (total <= 1e-12) return ratio;
            for (int b = 0; b < bands; b++) ratio[b] = energy[b] / total;
            return ratio;
        }

        /// <summary>归一化自相关基频估计，返回 Hz；失败返回 0。</summary>
        public static double DetectPitch(double[] frame, int sampleRate, out double clarity)
        {
            clarity = 0;
            if (frame.Length < 64 || sampleRate <= 0) return 0;

            int minLag = Math.Max(2, (int)(sampleRate / 500.0));
            int maxLag = (int)(sampleRate / 50.0);
            if (maxLag >= frame.Length - 2) maxLag = frame.Length - 3;
            if (maxLag <= minLag) return 0;

            double energy0 = 0;
            for (int i = 0; i < frame.Length; i++) energy0 += frame[i] * frame[i];
            if (energy0 <= 1e-8) return 0;

            double[] r = new double[maxLag + 1];
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                int count = frame.Length - lag;
                double sum = 0, energyLag = 0;
                for (int i = 0; i < count; i++)
                {
                    sum += frame[i] * frame[i + lag];
                    energyLag += frame[i + lag] * frame[i + lag];
                }
                double denom = Math.Sqrt(energy0 * energyLag);
                r[lag] = denom > 1e-9 ? sum / denom : 0;
            }

            double best = 0;
            int bestLag = 0;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                if (r[lag] > best) { best = r[lag]; bestLag = lag; }
            }
            if (bestLag == 0 || best < 0.30) return 0;

            // 优先选择首个足够高的局部峰，抑制八度误判
            int chosen = bestLag;
            for (int lag = minLag + 1; lag < maxLag; lag++)
            {
                if (r[lag] >= best * 0.86 && r[lag] > r[lag - 1] && r[lag] >= r[lag + 1])
                {
                    chosen = lag;
                    break;
                }
            }

            double refined = chosen;
            if (chosen > minLag && chosen < maxLag)
            {
                double y0 = r[chosen - 1], y1 = r[chosen], y2 = r[chosen + 1];
                double denom = (y0 - 2 * y1 + y2);
                if (Math.Abs(denom) > 1e-9) refined = chosen + 0.5 * (y0 - y2) / denom;
            }

            clarity = r[chosen];
            if (refined <= 0) return 0;
            return sampleRate / refined;
        }

        /// <summary>能量包络峰值位置（近似音节数），用于语速估计。</summary>
        public static List<int> EnvelopePeaks(short[] samples, int sampleRate, int windowMs, int minDistanceMs, double thresholdRatio)
        {
            List<int> peaks = new List<int>();
            int win = Math.Max(1, (int)((long)sampleRate * windowMs / 1000));
            int frames = samples.Length / win;
            if (frames < 3) return peaks;

            double[] env = new double[frames];
            double maxEnv = 0;
            for (int i = 0; i < frames; i++)
            {
                double sum = 0;
                int off = i * win;
                for (int j = 0; j < win; j++)
                {
                    double v = samples[off + j] / 32768.0;
                    sum += v * v;
                }
                env[i] = Math.Sqrt(sum / win);
                if (env[i] > maxEnv) maxEnv = env[i];
            }
            if (maxEnv <= 1e-6) return peaks;

            double thr = maxEnv * thresholdRatio;
            int minDist = Math.Max(1, minDistanceMs / Math.Max(1, windowMs));

            int lastPeak = -minDist;
            for (int i = 1; i < frames - 1; i++)
            {
                if (env[i] >= thr && env[i] >= env[i - 1] && env[i] > env[i + 1])
                {
                    if (i - lastPeak >= minDist)
                    {
                        peaks.Add(i * win + win / 2);
                        lastPeak = i;
                    }
                    else if (peaks.Count > 0 && env[i] > env[peaks[peaks.Count - 1] / win])
                    {
                        peaks[peaks.Count - 1] = i * win + win / 2;
                        lastPeak = i;
                    }
                }
            }
            return peaks;
        }

        public static double Percentile(double[] values, double p)
        {
            if (values == null || values.Length == 0) return 0;
            double[] copy = (double[])values.Clone();
            Array.Sort(copy);
            if (p <= 0) return copy[0];
            if (p >= 1) return copy[copy.Length - 1];
            double pos = p * (copy.Length - 1);
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);
            if (lo == hi) return copy[lo];
            double frac = pos - lo;
            return copy[lo] * (1 - frac) + copy[hi] * frac;
        }

        public static double Mean(double[] values)
        {
            if (values == null || values.Length == 0) return 0;
            double sum = 0;
            for (int i = 0; i < values.Length; i++) sum += values[i];
            return sum / values.Length;
        }

        public static double StdDev(double[] values)
        {
            if (values == null || values.Length == 0) return 0;
            double mean = Mean(values);
            double sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                double d = values[i] - mean;
                sum += d * d;
            }
            return Math.Sqrt(sum / values.Length);
        }
    }
}
