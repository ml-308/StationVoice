using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Forms;
using StationVoice.Audio;
using StationVoice.Synth;

namespace StationVoice.Ui
{
    public class MainForm : Form
    {
        private TextBox _line;
        private TextBox _terminal;
        private TextBox _outDir;
        private ComboBox _mode;
        private ComboBox _format;
        private ComboBox _voice;
        private ComboBox _preset;
        private CheckBox _english;
        private CheckBox _merge;
        private CheckBox _keepWav;
        private RichTextBox _stops;
        private RichTextBox _preview;
        private RichTextBox _profileBox;
        private RichTextBox _log;
        private TrackBar _rateBar;
        private TrackBar _pitchBar;
        private TrackBar _pauseBar;
        private Label _rateVal;
        private Label _pitchVal;
        private Label _pauseVal;
        private Label _fileLabel;
        private Label _status;
        private ProgressBar _progress;
        private Button _btnSynth;
        private Button _btnListen;

        private TimbreProfile _profile = new TimbreProfile();
        private SoundPlayer _player;

        public MainForm()
        {
            BuildUi();
            LoadDefaults();
        }

        // ————————————————— 界面构建 —————————————————

        private void BuildUi()
        {
            Text = "StationVoice · 报站声色模仿器 v1.0.0";
            ClientSize = new Size(940, 620);
            MinimumSize = new Size(880, 580);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            Font = Theme.BodyFont(9.5f);
            DoubleBuffered = true;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 56;
            header.BackColor = Theme.Panel;
            Controls.Add(header);

            Label title = Theme.MakeLabel("StationVoice", 16, 8, 300, Theme.TitleFont(15f), Theme.Fg);
            title.AutoSize = true;
            header.Controls.Add(title);

            Label sub = Theme.MakeLabel("声色分析 → 韵律迁移 → 公交/地铁报站音频（WAV / MP3 / M4A）", 18, 32, 700, Theme.BodyFont(9f), Theme.Muted);
            header.Controls.Add(sub);

            Label badge = Theme.MakeLabel("离线 · 零依赖", 820, 16, 110, Theme.BodyFont(9f), Theme.Accent);
            badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            badge.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(badge);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = Theme.BodyFont(9.5f);
            tabs.Padding = new Point(14, 5);

            tabs.TabPages.Add(BuildScriptTab());
            tabs.TabPages.Add(BuildTimbreTab());
            tabs.TabPages.Add(BuildSynthTab());

            _status = Theme.MakeLabel("就绪", 12, 0, 600, Theme.BodyFont(9f), Theme.Muted);
            Panel statusBar = new Panel();
            statusBar.Dock = DockStyle.Bottom;
            statusBar.Height = 26;
            statusBar.BackColor = Theme.Panel;
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Padding = new Padding(12, 0, 0, 0);
            statusBar.Controls.Add(_status);

            Controls.Add(header);
            Controls.Add(statusBar);
            Controls.Add(tabs);
        }

        private TabPage BuildScriptTab()
        {
            TabPage page = new TabPage("1 · 报站稿");
            page.BackColor = Theme.Bg;
            page.Padding = new Padding(0);

            page.Controls.Add(Theme.MakeLabel("线路名", 12, 12, 56, Theme.BodyFont(9.5f), Theme.Muted));
            _line = Theme.MakeText("地铁2号线", 70, 10, 160);
            page.Controls.Add(_line);

            page.Controls.Add(Theme.MakeLabel("终点/方向", 246, 12, 76, Theme.BodyFont(9.5f), Theme.Muted));
            _terminal = Theme.MakeText("浦东国际机场", 324, 10, 150);
            page.Controls.Add(_terminal);

            _mode = Theme.MakeCombo(492, 10, 110, true);
            _mode.Items.Add("公交");
            _mode.Items.Add("地铁");
            _mode.SelectedIndex = 1;
            page.Controls.Add(_mode);

            _english = Theme.MakeCheck("中英双语", 616, 10, 110, false);
            page.Controls.Add(_english);

            page.Controls.Add(Theme.MakeLabel("站点列表（每行：站名|English|换乘，以 # 开头为注释）", 12, 44, 700, Theme.BodyFont(9f), Theme.Muted));

            _stops = Theme.MakeRichText(12, 68, 440, 330, false);
            _stops.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            page.Controls.Add(_stops);

            Button preview = Theme.MakeButton("生成报站稿预览", 12, 406, 140, 30, false);
            preview.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            preview.Click += delegate { OnPreviewScript(null, EventArgs.Empty); };
            page.Controls.Add(preview);

            Label hint = Theme.MakeLabel("提示：站名支持中文与英文，换乘信息会自动生成换乘提示句。", 12, 444, 430, Theme.BodyFont(8.5f), Theme.Muted);
            hint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            page.Controls.Add(hint);

            page.Controls.Add(Theme.MakeLabel("报站稿预览", 468, 44, 200, Theme.BodyFont(9f), Theme.Muted));
            _preview = Theme.MakeRichText(468, 68, 452, 330, true);
            _preview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(_preview);

            Label tip = Theme.MakeLabel("预览为文本示意；音频会按「声色」画出韵律后再渲染。", 468, 406, 452, Theme.BodyFont(8.5f), Theme.Muted);
            tip.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(tip);

            return page;
        }

        private TabPage BuildTimbreTab()
        {
            TabPage page = new TabPage("2 · 声色");
            page.BackColor = Theme.Bg;

            Button load = Theme.MakeButton("载入参考录音…", 12, 12, 150, 30, false);
            load.Click += delegate { OnLoadSample(null, EventArgs.Empty); };
            page.Controls.Add(load);

            _fileLabel = Theme.MakeLabel("尚未载入参考音频（可选：不载入则使用预置风格）", 176, 18, 600, Theme.BodyFont(9f), Theme.Muted);
            page.Controls.Add(_fileLabel);

            Button analyze = Theme.MakeButton("分析声色", 12, 50, 150, 30, true);
            analyze.Click += delegate { OnAnalyze(null, EventArgs.Empty); };
            page.Controls.Add(analyze);

            page.Controls.Add(Theme.MakeLabel("风格预置", 176, 56, 66, Theme.BodyFont(9f), Theme.Muted));
            _preset = Theme.MakeCombo(244, 52, 280, true);
            page.Controls.Add(_preset);

            Button applyPreset = Theme.MakeButton("应用", 534, 50, 78, 30, false);
            applyPreset.Click += delegate { OnApplyPreset(null, EventArgs.Empty); };
            page.Controls.Add(applyPreset);

            _profileBox = Theme.MakeRichText(12, 92, 908, 296, true);
            _profileBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _profileBox.Font = Theme.BodyFont(9.5f);
            page.Controls.Add(_profileBox);

            int y = 402;
            page.Controls.Add(Theme.MakeLabel("语速", 12, y + 4, 40, Theme.BodyFont(9f), Theme.Muted));
            _rateBar = Theme.MakeTrack(54, y, 196, -30, 30, 0);
            _rateBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _rateBar.ValueChanged += delegate { UpdateProfileFromSliders(); };
            page.Controls.Add(_rateBar);
            _rateVal = Theme.MakeLabel("+0%", 254, y + 4, 56, Theme.BodyFont(9f), Theme.Fg);
            _rateVal.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            page.Controls.Add(_rateVal);

            page.Controls.Add(Theme.MakeLabel("音调", 336, y + 4, 40, Theme.BodyFont(9f), Theme.Muted));
            _pitchBar = Theme.MakeTrack(378, y, 196, -30, 30, 0);
            _pitchBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _pitchBar.ValueChanged += delegate { UpdateProfileFromSliders(); };
            page.Controls.Add(_pitchBar);
            _pitchVal = Theme.MakeLabel("+0%", 578, y + 4, 56, Theme.BodyFont(9f), Theme.Fg);
            _pitchVal.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            page.Controls.Add(_pitchVal);

            page.Controls.Add(Theme.MakeLabel("停顿", 660, y + 4, 40, Theme.BodyFont(9f), Theme.Muted));
            _pauseBar = Theme.MakeTrack(702, y, 140, 200, 1200, 500);
            _pauseBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _pauseBar.ValueChanged += delegate { UpdateProfileFromSliders(); };
            page.Controls.Add(_pauseBar);
            _pauseVal = Theme.MakeLabel("500ms", 844, y + 4, 70, Theme.BodyFont(9f), Theme.Fg);
            _pauseVal.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            page.Controls.Add(_pauseVal);

            Button save = Theme.MakeButton("导出画像 JSON", 12, 444, 140, 30, false);
            save.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            save.Click += delegate { OnSaveProfile(null, EventArgs.Empty); };
            page.Controls.Add(save);

            Button explain = Theme.MakeButton("原理说明", 162, 444, 110, 30, false);
            explain.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            explain.Click += delegate { ShowExplain(); };
            page.Controls.Add(explain);

            return page;
        }

        private TabPage BuildSynthTab()
        {
            TabPage page = new TabPage("3 · 合成导出");
            page.BackColor = Theme.Bg;

            page.Controls.Add(Theme.MakeLabel("音色", 12, 14, 40, Theme.BodyFont(9f), Theme.Muted));
            _voice = Theme.MakeCombo(54, 10, 300, true);
            page.Controls.Add(_voice);

            Button refresh = Theme.MakeButton("刷新", 360, 9, 60, 28, false);
            refresh.Click += delegate { LoadVoices(); };
            page.Controls.Add(refresh);

            page.Controls.Add(Theme.MakeLabel("格式", 440, 14, 40, Theme.BodyFont(9f), Theme.Muted));
            _format = Theme.MakeCombo(482, 10, 130, true);
            _format.Items.Add("MP3");
            _format.Items.Add("WAV");
            _format.Items.Add("M4A");
            _format.SelectedIndex = 0;
            page.Controls.Add(_format);

            _merge = Theme.MakeCheck("生成整线合集", 630, 10, 130, true);
            page.Controls.Add(_merge);

            _keepWav = Theme.MakeCheck("保留 WAV", 762, 10, 110, false);
            page.Controls.Add(_keepWav);

            page.Controls.Add(Theme.MakeLabel("输出目录", 12, 50, 70, Theme.BodyFont(9f), Theme.Muted));
            _outDir = Theme.MakeText("", 84, 48, 500);
            page.Controls.Add(_outDir);

            Button browse = Theme.MakeButton("浏览…", 592, 47, 76, 28, false);
            browse.Click += delegate { OnBrowse(null, EventArgs.Empty); };
            page.Controls.Add(browse);

            Button open = Theme.MakeButton("打开", 676, 47, 62, 28, false);
            open.Click += delegate { OnOpenFolder(null, EventArgs.Empty); };
            page.Controls.Add(open);

            _btnSynth = Theme.MakeButton("开始合成整线", 12, 86, 160, 34, true);
            _btnSynth.Click += delegate { OnSynth(null, EventArgs.Empty); };
            page.Controls.Add(_btnSynth);

            _btnListen = Theme.MakeButton("试听首站", 184, 86, 130, 34, false);
            _btnListen.Click += delegate { OnListen(null, EventArgs.Empty); };
            page.Controls.Add(_btnListen);

            Button clear = Theme.MakeButton("清空日志", 326, 86, 100, 34, false);
            clear.Click += delegate { _log.Clear(); };
            page.Controls.Add(clear);

            _progress = new ProgressBar();
            _progress.Location = new Point(12, 130);
            _progress.Size = new Size(908, 10);
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(_progress);

            _log = Theme.MakeRichText(12, 152, 908, 336, true);
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _log.Font = Theme.BodyFont(9f);
            page.Controls.Add(_log);

            return page;
        }

        // ————————————————— 初始化 —————————————————

        private void LoadDefaults()
        {
            LoadVoices();

            _preset.Items.Clear();
            _preset.Items.Add("自动匹配");
            for (int i = 0; i < TimbrePresets.All.Length; i++) _preset.Items.Add(TimbrePresets.All[i].Name);
            _preset.SelectedIndex = 0;

            _stops.Text =
                "人民广场|People's Square|1号线\r\n" +
                "南京东路|Nanjing East Road\r\n" +
                "陆家嘴|Lujiazui|2号线\r\n" +
                "世纪大道|Century Avenue|4号线\r\n" +
                "龙阳路|Longyang Road|7号线\r\n" +
                "浦东国际机场|Pudong International Airport";

            _outDir.Text = Path.Combine(Application.StartupPath, "output");

            TimbreProfile baseline = new TimbreProfile();
            TimbrePresets.ApplyPreset(baseline, TimbrePresets.Find("中性播报（基准）"));
            baseline.MatchedPreset = "未分析（使用基准风格）";
            _profile = baseline;
            _profileBox.Text = baseline.Describe();
            SyncSlidersFromProfile();

            AppendLog("就绪。先在第 1 页填写线路与站点，第 2 页可载入参考录音分析声色，再到第 3 页合成。");
        }

        private void LoadVoices()
        {
            _voice.Items.Clear();
            List<string> voices;
            try
            {
                voices = SapiEngine.InstalledVoices();
            }
            catch (Exception ex)
            {
                AppendLog("读取系统音色失败：" + ex.Message);
                voices = new List<string>();
            }
            for (int i = 0; i < voices.Count; i++) _voice.Items.Add(voices[i]);
            if (_voice.Items.Count > 0)
            {
                int chineseIndex = 0;
                for (int i = 0; i < _voice.Items.Count; i++)
                {
                    if (StationSynthesizer.IsChineseVoice(_voice.Items[i].ToString())) { chineseIndex = i; break; }
                }
                _voice.SelectedIndex = chineseIndex;
            }
            else
            {
                _voice.Items.Add("(未检测到系统音色)");
                _voice.SelectedIndex = 0;
            }
        }

        // ————————————————— 事件 —————————————————

        private void OnLoadSample(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择一段参考录音（WAV，建议 3~15 秒的报站或朗读）";
                dialog.Filter = "WAV 音频|*.wav|所有文件|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string path = dialog.FileName;
                _fileLabel.Text = Path.GetFileName(path) + "　（" + new FileInfo(path).Length / 1024 + " KB）";
                _fileLabel.Tag = path;

                try
                {
                    TimbreProfile p = TimbreAnalyzer.AnalyzeFile(path);
                    _profile = p;
                    _profileBox.Text = p.Describe();
                    SyncSlidersFromProfile();
                    AppendLog("已分析声色：" + p.MatchedPreset + "　置信度 " + (p.Confidence * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
                    _status.Text = "声色分析完成";
                }
                catch (Exception ex)
                {
                    AppendLog("分析失败：" + ex.Message);
                    MessageBox.Show(this, ex.Message, "分析失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OnAnalyze(object sender, EventArgs e)
        {
            string path = _fileLabel.Tag as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "请先载入一段参考录音。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Cursor = Cursors.WaitCursor;
                TimbreProfile p = TimbreAnalyzer.AnalyzeFile(path);
                _profile = p;
                _profileBox.Text = p.Describe();
                SyncSlidersFromProfile();
                AppendLog("重新分析完成：" + p.MatchedPreset);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void OnApplyPreset(object sender, EventArgs e)
        {
            if (_preset.SelectedIndex <= 0)
            {
                AppendLog("当前为「自动匹配」，合成时使用分析得到的画像。");
                return;
            }
            string name = _preset.SelectedItem.ToString();
            VoicePreset preset = TimbrePresets.Find(name);
            if (preset == null) return;

            TimbrePresets.ApplyPreset(_profile, preset);
            _profileBox.Text = _profile.Describe();
            SyncSlidersFromProfile();
            AppendLog("已应用风格预置：" + name);
        }

        private void OnSaveProfile(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON|*.json";
                dialog.FileName = "timbre-profile.json";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, _profile.ToJson(), new UTF8Encoding(false));
                AppendLog("画像已导出：" + dialog.FileName);
            }
        }

        private void OnPreviewScript(object sender, EventArgs e)
        {
            List<Stop> stops = ParseStops();
            if (stops.Count == 0)
            {
                _preview.Text = "请先填写站点列表（每行一个站名）。";
                return;
            }
            AnnounceMode mode = _mode.SelectedIndex == 0 ? AnnounceMode.Bus : AnnounceMode.Metro;
            _preview.Text = Announcement.Preview(mode, _line.Text.Trim(), _terminal.Text.Trim(), stops, _english.Checked);
            _status.Text = "已生成报站稿预览（" + stops.Count + " 站）";
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择输出目录";
                if (Directory.Exists(_outDir.Text)) dialog.SelectedPath = _outDir.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) _outDir.Text = dialog.SelectedPath;
            }
        }

        private void OnOpenFolder(object sender, EventArgs e)
        {
            try
            {
                string dir = _outDir.Text.Trim();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                AppendLog("打开目录失败：" + ex.Message);
            }
        }

        private void OnListen(object sender, EventArgs e)
        {
            List<Stop> stops = ParseStops();
            if (stops.Count == 0)
            {
                MessageBox.Show(this, "请先填写站点列表。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                _btnListen.Enabled = false;
                Cursor = Cursors.WaitCursor;
                SetStatus("正在渲染试听片段…");

                string temp = Path.Combine(Path.GetTempPath(), "stationvoice_preview.wav");
                using (SapiEngine engine = new SapiEngine())
                {
                    string voice = engine.SelectVoice(_voice.SelectedItem == null ? "" : _voice.SelectedItem.ToString());
                    AnnounceMode mode = _mode.SelectedIndex == 0 ? AnnounceMode.Bus : AnnounceMode.Metro;
                    List<ScriptLine> lines = Announcement.Build(mode, _line.Text.Trim(), _terminal.Text.Trim(), stops[0],
                        stops.Count > 1 ? stops[1] : null, _english.Checked, true);
                    string ssml = SsmlBuilder.Build(_profile, "", lines);
                    engine.Render(ssml, Announcement.PlainText(lines), temp, 44100, _profile.RatePct);
                    AppendLog("试听音色：" + voice);
                }

                if (_player != null) { _player.Stop(); _player.Dispose(); }
                _player = new SoundPlayer(temp);
                _player.Play();
                SetStatus("正在试听…");
            }
            catch (Exception ex)
            {
                AppendLog("试听失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "试听失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnListen.Enabled = true;
            }
        }

        private void OnSynth(object sender, EventArgs e)
        {
            List<Stop> stops = ParseStops();
            if (stops.Count == 0)
            {
                MessageBox.Show(this, "请先在第 1 页填写站点列表。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_voice.SelectedItem == null)
            {
                MessageBox.Show(this, "没有可用的系统音色，请安装中文语音包后重试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TimbreProfile profile = _profile;
            SynthOptions options = ReadOptions();
            AnnounceMode mode = _mode.SelectedIndex == 0 ? AnnounceMode.Bus : AnnounceMode.Metro;
            string lineName = _line.Text.Trim();
            string terminal = _terminal.Text.Trim();

            SetBusy(true);
            _log.Clear();
            AppendLog("开始合成：" + stops.Count + " 个站点，格式 " + AudioFormats.Display(options.Format));

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object s, DoWorkEventArgs args)
            {
                try
                {
                    args.Result = StationSynthesizer.RenderStops(profile, mode, lineName, terminal, stops, options, null, new SynthProgress(OnWorkerProgress));
                }
                catch (Exception ex)
                {
                    args.Result = ex;
                }
            };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
            {
                SetBusy(false);
                Exception error = args.Result as Exception;
                if (error != null)
                {
                    AppendLog("合成失败：" + error.Message);
                    MessageBox.Show(this, error.Message, "合成失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SetStatus("合成失败");
                    return;
                }
                SynthResult result = args.Result as SynthResult;
                if (result != null)
                {
                    AppendLog("完成：" + result.Files.Count + " 个分段" + (result.Merged != null ? " + 1 个合集" : ""));
                    SetStatus("合成完成，输出目录：" + options.OutputDir);
                }
            };
            worker.RunWorkerAsync();
        }

        private void OnWorkerProgress(int done, int total, string title)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action<int, int, string>(UpdateProgress), done, total, title);
            }
            catch (Exception)
            {
            }
        }

        private void UpdateProgress(int done, int total, string title)
        {
            if (total <= 0) return;
            _progress.Maximum = total;
            _progress.Value = Math.Min(done, total);
            SetStatus("正在合成 " + done + "/" + total + "：" + title);
        }

        // ————————————————— 辅助 —————————————————

        private List<Stop> ParseStops()
        {
            List<Stop> stops = new List<Stop>();
            string[] lines = _stops.Text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                Stop s = Stop.Parse(lines[i]);
                if (s != null && !string.IsNullOrEmpty(s.Name)) stops.Add(s);
            }
            return stops;
        }

        private SynthOptions ReadOptions()
        {
            SynthOptions options = new SynthOptions();
            if (_format.SelectedIndex == 1) options.Format = AudioFormat.Wav;
            else if (_format.SelectedIndex == 2) options.Format = AudioFormat.M4a;
            else options.Format = AudioFormat.Mp3;

            options.VoiceName = _voice.SelectedItem == null ? "" : _voice.SelectedItem.ToString();
            options.OutputDir = string.IsNullOrEmpty(_outDir.Text.Trim())
                ? Path.Combine(Application.StartupPath, "output")
                : _outDir.Text.Trim();
            options.Merge = _merge.Checked;
            options.KeepWav = _keepWav.Checked;
            options.IncludeEnglish = _english.Checked;
            return options;
        }

        private void SyncSlidersFromProfile()
        {
            _rateBar.Value = Clamp(_profile.RatePct, _rateBar.Minimum, _rateBar.Maximum);
            _pitchBar.Value = Clamp(_profile.PitchPct, _pitchBar.Minimum, _pitchBar.Maximum);
            _pauseBar.Value = Clamp(_profile.PauseMs, _pauseBar.Minimum, _pauseBar.Maximum);
            _rateVal.Text = _profile.RatePct.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "%";
            _pitchVal.Text = _profile.PitchPct.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "%";
            _pauseVal.Text = _profile.PauseMs + "ms";
        }

        private void UpdateProfileFromSliders()
        {
            _profile.RatePct = _rateBar.Value;
            _profile.PitchPct = _pitchBar.Value;
            _profile.PauseMs = _pauseBar.Value;
            _rateVal.Text = _rateBar.Value.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "%";
            _pitchVal.Text = _pitchBar.Value.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "%";
            _pauseVal.Text = _pauseBar.Value + "ms";
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private void SetBusy(bool busy)
        {
            _btnSynth.Enabled = !busy;
            _btnListen.Enabled = !busy;
            _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (!busy)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 0;
            }
            UseWaitCursor = busy;
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.Text = text;
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }
            if (_log.TextLength > 40000) _log.Clear();
            _log.AppendText(message + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void ShowExplain()
        {
            string text =
                "声色识别原理\r\n\r\n" +
                "1. 读取参考录音，重采样到 16kHz 单声道，按 40ms 分帧（10ms 步进）。\r\n" +
                "2. 对每帧做自相关基频估计，得到 F0 中位数与起伏；用 FFT 求谱质心、五个频带的能量占比，刻画「亮/暗、厚/薄」。\r\n" +
                "3. 由能量包络的峰密度估计语速（音节/秒），由峰值清晰度估计谐噪比。\r\n" +
                "4. 把这些特征与内置报站风格原型比较（对数域加权距离 + 高斯软融合），得到风格归属与置信度。\r\n" +
                "5. 由风格基线加上样本相对基线的偏移，推导 SSML 的 rate / pitch / volume 与句间停顿，交给系统 SAPI 音色渲染。\r\n\r\n" +
                "边界说明\r\n" +
                "· 这是「声学特征 → 韵律参数」的迁移，不是神经网络音色克隆，无法复制说话人的音色本质（声道共鸣、口音）。\r\n" +
                "· 想要更接近的音色，请在系统的「语音」设置里安装更多中文音色，或在第 3 页切换音色对比。\r\n" +
                "· 音频输出使用 Windows Media Foundation 编码，全程离线。";
            MessageBox.Show(this, text, "原理说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_player != null)
            {
                try { _player.Stop(); _player.Dispose(); }
                catch (Exception) { }
            }
            base.OnFormClosing(e);
        }
    }
}
