using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace StationVoice.Audio
{
    public enum AudioFormat
    {
        Wav = 0,
        Mp3 = 1,
        M4a = 2
    }

    public static class AudioFormats
    {
        public static string Extension(AudioFormat f)
        {
            if (f == AudioFormat.Mp3) return ".mp3";
            if (f == AudioFormat.M4a) return ".m4a";
            return ".wav";
        }

        public static string Display(AudioFormat f)
        {
            if (f == AudioFormat.Mp3) return "MP3";
            if (f == AudioFormat.M4a) return "M4A (AAC)";
            return "WAV (PCM)";
        }

        public static AudioFormat Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return AudioFormat.Wav;
            string t = text.Trim().TrimStart('.').ToLowerInvariant();
            if (t == "mp3") return AudioFormat.Mp3;
            if (t == "m4a" || t == "aac" || t == "mp4") return AudioFormat.M4a;
            return AudioFormat.Wav;
        }
    }

    /// <summary>
    /// 基于 Windows Media Foundation 的 PCM → MP3 / M4A 转码器，零第三方依赖。
    /// 先尝试 MFCreateSinkWriterFromURL；MP3 失败时退回显式 MP3 媒体汇。
    /// </summary>
    public static class MfTranscoder
    {
        private const int MF_VERSION = 0x00020070;
        private static readonly object Gate = new object();

        public static void Transcode(string wavPath, string outputPath, AudioFormat format, int bitrateKbps)
        {
            if (format == AudioFormat.Wav)
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.Copy(wavPath, outputPath, true);
                return;
            }
            WavData wav = WavIO.Read(wavPath);
            Transcode(wav.Samples, wav.SampleRate, outputPath, format, bitrateKbps);
        }

        public static void Transcode(short[] samples, int sampleRate, string outputPath, AudioFormat format, int bitrateKbps)
        {
            if (format == AudioFormat.Wav)
            {
                WavIO.Write(outputPath, samples, sampleRate);
                return;
            }
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("没有可编码的音频数据");
            if (sampleRate != 44100 && sampleRate != 48000)
            {
                samples = WavIO.Resample(samples, sampleRate, 44100);
                sampleRate = 44100;
            }

            string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            lock (Gate)
            {
                int hr = MFStartup(MF_VERSION, 0);
                if (hr < 0) throw new COMException("MFStartup 失败（Media Foundation 不可用）", hr);
                try
                {
                    Encode(samples, sampleRate, outputPath, format, bitrateKbps);
                }
                finally
                {
                    MFShutdown();
                }
            }
        }

        private static void Encode(short[] samples, int sampleRate, string outputPath, AudioFormat format, int bitrateKbps)
        {
            IMFSinkWriter writer = null;
            int streamIndex;
            try
            {
                writer = CreateWriter(outputPath, format, sampleRate, bitrateKbps, out streamIndex);

                Check(writer.BeginWriting(), "BeginWriting");

                int chunk = Math.Max(320, sampleRate / 10);
                long time = 0;
                for (int offset = 0; offset < samples.Length; offset += chunk)
                {
                    int count = Math.Min(chunk, samples.Length - offset);
                    WriteChunk(writer, streamIndex, samples, offset, count, sampleRate, time);
                    time += (long)count * 10000000L / sampleRate;
                }

                Check(writer.Finish(), "Finalize");
            }
            finally
            {
                Release(writer);
            }
        }

        private static IMFSinkWriter CreateWriter(string outputPath, AudioFormat format, int sampleRate, int bitrateKbps, out int streamIndex)
        {
            streamIndex = 0;
            int index = 0;

            IMFSinkWriter writer = null;
            int hr = MFCreateSinkWriterFromURL(outputPath, IntPtr.Zero, IntPtr.Zero, out writer);
            if (hr >= 0)
            {
                try
                {
                    Configure(writer, format, sampleRate, bitrateKbps, out index);
                    streamIndex = index;
                    return writer;
                }
                catch (Exception)
                {
                    Release(writer);
                    writer = null;
                }
            }

            if (format == AudioFormat.Mp3)
            {
                IMFByteStream byteStream = null;
                IMFMediaSink sink = null;
                try
                {
                    hr = MFCreateFile(2, 2, 0, outputPath, out byteStream);
                    if (hr >= 0)
                    {
                        hr = MFCreateMP3MediaSink(byteStream, out sink);
                        if (hr >= 0)
                        {
                            hr = MFCreateSinkWriterFromMediaSink(sink, IntPtr.Zero, out writer);
                            if (hr >= 0)
                            {
                                try
                                {
                                    Configure(writer, format, sampleRate, bitrateKbps, out index);
                                    streamIndex = index;
                                    return writer;
                                }
                                catch (Exception)
                                {
                                    Release(writer);
                                    writer = null;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Release(sink);
                    Release(byteStream);
                }
            }

            throw new InvalidOperationException(
                "无法创建媒体汇（" + AudioFormats.Display(format) + "）。请确认系统已启用 Windows Media Foundation 与对应编码器。");
        }

        private static void Configure(IMFSinkWriter writer, AudioFormat format, int sampleRate, int bitrateKbps, out int streamIndex)
        {
            streamIndex = 0;
            IMFMediaType outType = null;
            IMFMediaType inType = null;
            try
            {
                Check(MFCreateMediaType(out outType), "MFCreateMediaType(out)");
                SetGuid(outType, MF_MT_MAJOR_TYPE, MFMediaType_Audio);
                SetGuid(outType, MF_MT_SUBTYPE, format == AudioFormat.Mp3 ? MFAudioFormat_MP3 : MFAudioFormat_AAC);
                SetUInt32(outType, MF_MT_AUDIO_NUM_CHANNELS, 1);
                SetUInt32(outType, MF_MT_AUDIO_SAMPLES_PER_SECOND, sampleRate);
                SetUInt32(outType, MF_MT_AUDIO_AVG_BYTES_PER_SECOND, bitrateKbps * 1000 / 8);
                if (format == AudioFormat.M4a) SetUInt32(outType, MF_MT_AUDIO_BITS_PER_SAMPLE, 16);

                int index;
                Check(writer.AddStream(outType, out index), "AddStream");
                streamIndex = index;

                Check(MFCreateMediaType(out inType), "MFCreateMediaType(in)");
                SetGuid(inType, MF_MT_MAJOR_TYPE, MFMediaType_Audio);
                SetGuid(inType, MF_MT_SUBTYPE, MFAudioFormat_PCM);
                SetUInt32(inType, MF_MT_AUDIO_NUM_CHANNELS, 1);
                SetUInt32(inType, MF_MT_AUDIO_SAMPLES_PER_SECOND, sampleRate);
                SetUInt32(inType, MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
                SetUInt32(inType, MF_MT_AUDIO_BLOCK_ALIGNMENT, 2);
                SetUInt32(inType, MF_MT_AUDIO_AVG_BYTES_PER_SECOND, sampleRate * 2);

                Check(writer.SetInputMediaType(streamIndex, inType, IntPtr.Zero), "SetInputMediaType");
            }
            finally
            {
                Release(inType);
                Release(outType);
            }
        }

        /// <summary>编码器自检：逐步打印 Media Foundation 调用的返回码，便于排查环境问题。</summary>
        public static string DebugProbe(int sampleRate)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int hr = MFStartup(MF_VERSION, 0);
            sb.AppendLine("MFStartup = " + Hex(hr));
            if (hr < 0) return sb.ToString();

            IMFMediaBuffer buffer = null;
            try
            {
                hr = MFCreateMemoryBuffer(4096, out buffer);
                sb.AppendLine("MFCreateMemoryBuffer = " + Hex(hr));
                if (hr >= 0)
                {
                    int maxLength;
                    hr = buffer.GetMaxLength(out maxLength);
                    sb.AppendLine("GetMaxLength = " + Hex(hr) + " value=" + maxLength);

                    int cur0;
                    hr = buffer.GetCurrentLength(out cur0);
                    sb.AppendLine("GetCurrentLength = " + Hex(hr) + " value=" + cur0);

                    hr = buffer.SetCurrentLength(1234);
                    sb.AppendLine("SetCurrentLength(1234) = " + Hex(hr));

                    int cur1;
                    hr = buffer.GetCurrentLength(out cur1);
                    sb.AppendLine("GetCurrentLength(after set) = " + Hex(hr) + " value=" + cur1);

                    IntPtr ptr;
                    int max2;
                    int cur;
                    hr = buffer.Lock(out ptr, out max2, out cur);
                    sb.AppendLine("Lock = " + Hex(hr) + " ptr=0x" + ptr.ToInt64().ToString("X") + " max=" + max2);
                    if (hr >= 0) sb.AppendLine("Unlock = " + Hex(buffer.Unlock()));
                }

                IntPtr samplePtr;
                hr = MFCreateSamplePtr(out samplePtr);
                sb.AppendLine("MFCreateSample = " + Hex(hr) + " ptr=0x" + samplePtr.ToInt64().ToString("X"));
                if (hr >= 0)
                {
                    Guid sampleIid = new Guid("C40A00F2-B93A-4D80-AE8C-5A1C634F58E4");
                    Guid attributesIid = new Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3");
                    IntPtr probeIntf;
                    sb.AppendLine("QI(IMFSample) = " + Hex(Marshal.QueryInterface(samplePtr, ref sampleIid, out probeIntf)));
                    sb.AppendLine("QI(IMFAttributes) = " + Hex(Marshal.QueryInterface(samplePtr, ref attributesIid, out probeIntf)));

                    string[] names = new string[] { "IMFMediaEvent", "IMFMediaType", "IMFMediaBuffer", "IMFSample2", "IMFGetService" };
                    string[] guids = new string[]
                    {
                        "DF598932-F10C-4E39-BBA2-C308F101DAA3",
                        "44AE0FA8-EA31-4109-8D2E-4CAE4997C555",
                        "045FA593-8799-42B8-BC8D-8968C6453507",
                        "EAD45AFC-3A1F-4BF7-8D6C-1A5F0E6B7C31",
                        "C6B14B33-F575-4998-83D2-8A1B1C6E7A33"
                    };
                    for (int i = 0; i < guids.Length; i++)
                    {
                        Guid g = new Guid(guids[i]);
                        sb.AppendLine("QI(" + names[i] + ") = " + Hex(Marshal.QueryInterface(samplePtr, ref g, out probeIntf)));
                    }
                    IMFSample typed = null;
                    try
                    {
                        hr = MFCreateSample(out typed);
                        sb.AppendLine("MFCreateSample(typed) = " + Hex(hr) + (typed == null ? " null" : " ok"));
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine("MFCreateSample(typed) 异常: " + ex.GetType().Name + " - " + ex.Message);
                    }
                }

                IMFSinkWriter writer = null;
                string probePath = Path.Combine(Environment.CurrentDirectory, "mfprobe_probe.m4a");
                hr = MFCreateSinkWriterFromURL(probePath, IntPtr.Zero, IntPtr.Zero, out writer);
                sb.AppendLine("MFCreateSinkWriterFromURL(.m4a) = " + Hex(hr));
                if (hr >= 0)
                {
                    IMFMediaType type = null;
                    MFCreateMediaType(out type);
                    SetGuid(type, MF_MT_MAJOR_TYPE, MFMediaType_Audio);
                    SetGuid(type, MF_MT_SUBTYPE, MFAudioFormat_AAC);
                    SetUInt32(type, MF_MT_AUDIO_NUM_CHANNELS, 1);
                    SetUInt32(type, MF_MT_AUDIO_SAMPLES_PER_SECOND, sampleRate);
                    SetUInt32(type, MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 16000);
                    int idx;
                    hr = writer.AddStream(type, out idx);
                    sb.AppendLine("AddStream(AAC) = " + Hex(hr));
                    Release(type);
                    Release(writer);
                }

                writer = null;
                string probeMp3 = Path.Combine(Environment.CurrentDirectory, "mfprobe_probe.mp3");
                hr = MFCreateSinkWriterFromURL(probeMp3, IntPtr.Zero, IntPtr.Zero, out writer);
                sb.AppendLine("MFCreateSinkWriterFromURL(.mp3) = " + Hex(hr));
                if (hr >= 0)
                {
                    IMFMediaType type = null;
                    MFCreateMediaType(out type);
                    SetGuid(type, MF_MT_MAJOR_TYPE, MFMediaType_Audio);
                    SetGuid(type, MF_MT_SUBTYPE, MFAudioFormat_MP3);
                    SetUInt32(type, MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 16000);
                    int idx;
                    hr = writer.AddStream(type, out idx);
                    sb.AppendLine("AddStream(MP3) = " + Hex(hr));
                    Release(type);
                    Release(writer);
                }
            }
            finally
            {
                Release(buffer);
                MFShutdown();
            }
            return sb.ToString();
        }

        private static string Hex(int hr)
        {
            return "0x" + hr.ToString("X8", CultureInfo.InvariantCulture) + (hr >= 0 ? " (ok)" : "");
        }

        private static void WriteChunk(IMFSinkWriter writer, int streamIndex, short[] samples, int offset, int count, int sampleRate, long time)
        {
            int bytes = count * 2;
            IMFMediaBuffer buffer = null;
            IMFSample sample = null;
            try
            {
                Check(MFCreateMemoryBuffer(bytes, out buffer), "MFCreateMemoryBuffer");

                IntPtr ptr;
                int maxLength;
                int curLength;
                Check(buffer.Lock(out ptr, out maxLength, out curLength), "IMFMediaBuffer.Lock");

                byte[] temp = new byte[bytes];
                Buffer.BlockCopy(samples, offset * 2, temp, 0, bytes);
                Marshal.Copy(temp, 0, ptr, bytes);
                buffer.Unlock();

                Check(buffer.SetCurrentLength(bytes), "SetCurrentLength");

                Check(MFCreateSample(out sample), "MFCreateSample");
                Check(sample.AddBuffer(buffer), "IMFSample.AddBuffer");
                Check(sample.SetSampleTime(time), "SetSampleTime");

                long duration = (long)count * 10000000L / sampleRate;
                Check(sample.SetSampleDuration(duration), "SetSampleDuration");

                Check(writer.WriteSample(streamIndex, sample), "WriteSample");
            }
            finally
            {
                Release(sample);
                Release(buffer);
            }
        }

        private static void SetUInt32(IMFMediaType type, Guid key, int value)
        {
            Guid k = key;
            Check(type.SetUINT32(ref k, value), "SetUINT32 " + key.ToString());
        }

        private static void SetGuid(IMFMediaType type, Guid key, Guid value)
        {
            Guid k = key;
            Guid v = value;
            Check(type.SetGUID(ref k, ref v), "SetGUID " + key.ToString());
        }

        private static void Check(int hr, string what)
        {
            if (hr < 0)
                throw new COMException(what + " 失败 (0x" + hr.ToString("X8", CultureInfo.InvariantCulture) + ")", hr);
        }

        private static void Release(object comObject)
        {
            if (comObject == null) return;
            try
            {
                if (Marshal.IsComObject(comObject)) Marshal.ReleaseComObject(comObject);
            }
            catch (Exception)
            {
                // 释放失败不影响结果
            }
        }

        // ——————————————— Media Foundation 互操作 ———————————————

        private static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
        private static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
        private static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT = new Guid("322de230-9eeb-43bd-ab7a-ff412251541d");
        private static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new Guid("1aab75c8-cfef-451c-ab95-ac034b8e1731");
        private static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");

        private static readonly Guid MFMediaType_Audio = new Guid("73647561-0000-0010-8000-00AA00389B71");
        private static readonly Guid MFAudioFormat_PCM = new Guid("00000001-0000-0010-8000-00AA00389B71");
        private static readonly Guid MFAudioFormat_MP3 = new Guid("00000055-0000-0010-8000-00AA00389B71");
        private static readonly Guid MFAudioFormat_AAC = new Guid("00001610-0000-0010-8000-00AA00389B71");

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFStartup(int version, int flags);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFShutdown();

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMediaType(out IMFMediaType ppMFType);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateSample(out IMFSample ppIMFSample);

        [DllImport("mfplat.dll", ExactSpelling = true, EntryPoint = "MFCreateSample")]
        private static extern int MFCreateSamplePtr(out IntPtr ppIMFSample);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMemoryBuffer(int cbMaxLength, out IMFMediaBuffer ppBuffer);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateFile(int accessMode, int openMode, int fileFlags,
            [MarshalAs(UnmanagedType.LPWStr)] string fileUrl, out IMFByteStream byteStream);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMP3MediaSink(IMFByteStream byteStream, out IMFMediaSink mediaSink);

        [DllImport("mfreadwrite.dll", ExactSpelling = true)]
        private static extern int MFCreateSinkWriterFromURL(
            [MarshalAs(UnmanagedType.LPWStr)] string outputUrl, IntPtr byteStream, IntPtr attributes,
            out IMFSinkWriter sinkWriter);

        [DllImport("mfreadwrite.dll", ExactSpelling = true)]
        private static extern int MFCreateSinkWriterFromMediaSink(
            IMFMediaSink mediaSink, IntPtr attributes, out IMFSinkWriter sinkWriter);

        [ComImport, Guid("44AE0FA8-EA31-4109-8D2E-4CAE4997C555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMFMediaType
        {
            [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
            [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
            [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out bool pbResult);
            [PreserveSig] int Compare(IntPtr pTheirs, int matchType, out bool pbResult);
            [PreserveSig] int GetUINT32(ref Guid guidKey, out int unValue);
            [PreserveSig] int GetUINT64(ref Guid guidKey, out long unValue);
            [PreserveSig] int GetDouble(ref Guid guidKey, out double fValue);
            [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
            [PreserveSig] int GetStringLength(ref Guid guidKey, out int cchLength);
            [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, int cchBufSize, IntPtr pcchLength);
            [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
            [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
            [PreserveSig] int GetBlob(ref Guid guidKey, byte[] pBuf, int cbBufSize, out int pcbBlobSize);
            [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
            [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
            [PreserveSig] int DeleteItem(ref Guid guidKey);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
            [PreserveSig] int SetUINT64(ref Guid guidKey, long unValue);
            [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
            [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
        }

        [ComImport, Guid("C40A00F2-B93A-4D80-AE8C-5A1C634F58E4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMFSample
        {
            [PreserveSig] int Slot00();
            [PreserveSig] int Slot01();
            [PreserveSig] int Slot02();
            [PreserveSig] int Slot03();
            [PreserveSig] int Slot04();
            [PreserveSig] int Slot05();
            [PreserveSig] int Slot06();
            [PreserveSig] int Slot07();
            [PreserveSig] int Slot08();
            [PreserveSig] int Slot09();
            [PreserveSig] int Slot10();
            [PreserveSig] int Slot11();
            [PreserveSig] int Slot12();
            [PreserveSig] int Slot13();
            [PreserveSig] int Slot14();
            [PreserveSig] int Slot15();
            [PreserveSig] int Slot16();
            [PreserveSig] int Slot17();
            [PreserveSig] int Slot18();
            [PreserveSig] int Slot19();
            [PreserveSig] int Slot20();
            [PreserveSig] int Slot21();
            [PreserveSig] int Slot22();
            [PreserveSig] int Slot23();
            [PreserveSig] int Slot24();
            [PreserveSig] int Slot25();
            [PreserveSig] int Slot26();
            [PreserveSig] int Slot27();
            [PreserveSig] int Slot28();
            [PreserveSig] int Slot29();
            [PreserveSig] int GetSampleFlags(out int flags);
            [PreserveSig] int SetSampleFlags(int flags);
            [PreserveSig] int GetSampleTime(out long time);
            [PreserveSig] int SetSampleTime(long time);
            [PreserveSig] int GetSampleDuration(out long duration);
            [PreserveSig] int SetSampleDuration(long duration);
            [PreserveSig] int GetBufferCount(out int count);
            [PreserveSig] int GetBufferByIndex(int index, out IMFMediaBuffer buffer);
            [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
            [PreserveSig] int AddBuffer(IMFMediaBuffer buffer);
        }

        [ComImport, Guid("045FA593-8799-42B8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMFMediaBuffer
        {
            // 注意：vtable 顺序与文档页的列出顺序不同，必须以 mfidl.h 的声明顺序为准
            [PreserveSig] int Lock(out IntPtr buffer, out int maxLength, out int currentLength);
            [PreserveSig] int Unlock();
            [PreserveSig] int GetCurrentLength(out int currentLength);
            [PreserveSig] int SetCurrentLength(int currentLength);
            [PreserveSig] int GetMaxLength(out int maxLength);
        }

        [ComImport, Guid("3137F1CD-FE5E-4805-A5D8-FB477448CB3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMFSinkWriter
        {
            [PreserveSig] int AddStream(IMFMediaType targetType, out int streamIndex);
            [PreserveSig] int SetInputMediaType(int streamIndex, IMFMediaType inputType, IntPtr encodingParameters);
            [PreserveSig] int BeginWriting();
            [PreserveSig] int WriteSample(int streamIndex, IMFSample sample);
            [PreserveSig] int SendStreamTick(int streamIndex, long timestamp);
            [PreserveSig] int PlaceMarker(int streamIndex, IntPtr context, int markerType);
            [PreserveSig] int NotifyEndOfSegment(int streamIndex);
            [PreserveSig] int Flush(int streamIndex);
            [PreserveSig] int Finish();
        }

        [ComImport, Guid("AD4C1B00-4BF7-422F-9175-756693D9130D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMFByteStream
        {
        }

        [ComImport, Guid("279A808D-AEC7-40C8-9C6B-A6B492C78A66"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMFMediaSink
        {
        }
    }
}
