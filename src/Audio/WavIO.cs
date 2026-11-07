using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StationVoice.Audio
{
    /// <summary>单声道 16bit PCM 音频数据（读取时自动下混）。</summary>
    public class WavData
    {
        public int SampleRate = 44100;
        public short[] Samples = new short[0];
        public int Channels = 1;
        public int BitsPerSample = 16;
        public string SourcePath = "";

        public double Duration
        {
            get
            {
                if (SampleRate <= 0) return 0;
                return Samples.Length / (double)SampleRate;
            }
        }
    }

    /// <summary>WAV 读写（RIFF/PCM，支持 8/16/24/32bit 整数与 32bit 浮点，任意声道数下混为单声道）。</summary>
    public static class WavIO
    {
        public static WavData Read(string path)
        {
            byte[] all = File.ReadAllBytes(path);
            if (all.Length < 44)
                throw new InvalidDataException("文件太小，不是有效的 WAV：" + path);
            if (!(all[0] == 0x52 && all[1] == 0x49 && all[2] == 0x46 && all[3] == 0x46))
                throw new InvalidDataException("不是 RIFF/WAVE 文件：" + path);

            int pos = 12;
            int formatTag = 1;
            int channels = 1;
            int sampleRate = 44100;
            int bits = 16;
            int dataOffset = -1;
            int dataLength = 0;

            while (pos + 8 <= all.Length)
            {
                string id = Encoding.ASCII.GetString(all, pos, 4);
                int size = BitConverter.ToInt32(all, pos + 4);
                int body = pos + 8;
                if (size < 0 || body + size > all.Length) size = Math.Max(0, all.Length - body);

                if (id == "fmt " && size >= 16)
                {
                    formatTag = BitConverter.ToUInt16(all, body);
                    channels = BitConverter.ToUInt16(all, body + 2);
                    sampleRate = BitConverter.ToInt32(all, body + 4);
                    bits = BitConverter.ToUInt16(all, body + 14);
                    if (formatTag == 0xFFFE && size >= 26)
                        formatTag = BitConverter.ToUInt16(all, body + 24);
                }
                else if (id == "data")
                {
                    dataOffset = body;
                    dataLength = size;
                    break;
                }
                pos = body + size + (size % 2);
            }

            if (dataOffset < 0)
                throw new InvalidDataException("WAV 缺少 data 块：" + path);
            if (channels < 1) channels = 1;
            if (sampleRate <= 0) sampleRate = 44100;

            int bytesPerSample = Math.Max(1, bits / 8);
            int frameBytes = bytesPerSample * channels;
            int frames = dataLength / frameBytes;
            short[] mono = new short[frames];
            bool isFloat = (formatTag == 3);

            for (int f = 0; f < frames; f++)
            {
                int acc = 0;
                int baseOff = dataOffset + f * frameBytes;
                for (int c = 0; c < channels; c++)
                {
                    acc += DecodeSample(all, baseOff + c * bytesPerSample, bytesPerSample, isFloat);
                }
                int v = acc / channels;
                if (v > 32767) v = 32767;
                if (v < -32768) v = -32768;
                mono[f] = (short)v;
            }

            WavData data = new WavData();
            data.SampleRate = sampleRate;
            data.Samples = mono;
            data.Channels = channels;
            data.BitsPerSample = bits;
            data.SourcePath = path;
            return data;
        }

        private static int DecodeSample(byte[] buf, int offset, int bytesPerSample, bool isFloat)
        {
            if (offset < 0 || offset + bytesPerSample > buf.Length) return 0;
            if (isFloat && bytesPerSample == 4)
            {
                float fv = BitConverter.ToSingle(buf, offset);
                double scaled = fv * 32767.0;
                if (scaled > 32767) scaled = 32767;
                if (scaled < -32768) scaled = -32768;
                return (int)Math.Round(scaled);
            }
            switch (bytesPerSample)
            {
                case 1:
                    return (buf[offset] - 128) * 256;
                case 2:
                    return BitConverter.ToInt16(buf, offset);
                case 3:
                    {
                        int v = buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16);
                        if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                        return v >> 8;
                    }
                case 4:
                    {
                        int v = BitConverter.ToInt32(buf, offset);
                        return v >> 16;
                    }
                default:
                    return 0;
            }
        }

        public static void Write(string path, short[] samples, int sampleRate)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            int dataBytes = samples.Length * 2;
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII))
            {
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataBytes);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataBytes);
                byte[] raw = new byte[dataBytes];
                Buffer.BlockCopy(samples, 0, raw, 0, dataBytes);
                bw.Write(raw);
            }
        }

        public static short[] Resample(short[] src, int srcRate, int dstRate)
        {
            if (src == null || src.Length == 0) return new short[0];
            if (srcRate == dstRate || srcRate <= 0 || dstRate <= 0) return src;

            long dstLenLong = (long)src.Length * dstRate / srcRate;
            int dstLen = (int)Math.Max(1, dstLenLong);
            short[] dst = new short[dstLen];
            double step = (double)srcRate / dstRate;

            for (int i = 0; i < dstLen; i++)
            {
                double x = i * step;
                int i0 = (int)x;
                if (i0 >= src.Length) i0 = src.Length - 1;
                int i1 = i0 + 1 < src.Length ? i0 + 1 : src.Length - 1;
                double frac = x - i0;
                double v = src[i0] * (1.0 - frac) + src[i1] * frac;
                if (v > 32767) v = 32767;
                if (v < -32768) v = -32768;
                dst[i] = (short)Math.Round(v);
            }
            return dst;
        }

        /// <summary>把多段音频按顺序拼接，段间插入指定毫秒静音。</summary>
        public static short[] Concat(IList<short[]> parts, int sampleRate, int gapMs)
        {
            int gap = (int)((long)sampleRate * gapMs / 1000);
            int total = 0;
            for (int i = 0; i < parts.Count; i++) total += parts[i].Length;
            if (parts.Count > 1) total += gap * (parts.Count - 1);

            short[] result = new short[total];
            int cursor = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                Array.Copy(parts[i], 0, result, cursor, parts[i].Length);
                cursor += parts[i].Length;
                if (i < parts.Count - 1) cursor += gap;
            }
            return result;
        }

        public static short[] TrimSilence(short[] src, int sampleRate, int threshold)
        {
            if (src.Length == 0) return src;
            int start = 0, end = src.Length - 1;
            while (start < src.Length && Math.Abs((int)src[start]) < threshold) start++;
            while (end > start && Math.Abs((int)src[end]) < threshold) end--;
            if (start >= end) return src;

            int pad = (int)((long)sampleRate * 40 / 1000);
            start = Math.Max(0, start - pad);
            end = Math.Min(src.Length - 1, end + pad);
            int len = end - start + 1;
            short[] dst = new short[len];
            Array.Copy(src, start, dst, 0, len);
            return dst;
        }
    }
}
