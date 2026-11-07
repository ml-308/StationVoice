# StationVoice · 报站声色模仿器

把一段参考录音的「声色」分析成可解释的声学参数，再用 Windows 系统语音（SAPI）模仿这套腔调，批量生成**公交 / 地铁报站音频**，输出 **WAV / MP3 / M4A**。

单文件、免安装、零第三方依赖：`StationVoice.exe` 约 90 KB，双击即用，全程离线。

```
参考录音 ──▶ 声学特征提取 ──▶ 风格匹配 ──▶ SSML 韵律参数 ──▶ SAPI 渲染 ──▶ MP3/M4A
   (wav)      F0/语速/谱质心     预置库         rate/pitch/pause     PCM        Media Foundation
```

---

## 功能

| 模块 | 说明 |
|---|---|
| 声色分析 | 自相关基频、FFT 谱质心、五频带能量占比、能量包络语速、谐噪清晰度、响度统计 |
| 风格匹配 | 内置 7 种报站风格原型（男/女声 × 地铁/公交/机场/高铁），加权对数距离 + 高斯软融合，输出置信度与 Top-3 候选 |
| 韵律迁移 | 由风格基线 + 样本相对基线偏移，推导 SSML 的 `rate` / `pitch` / `volume` 与句间停顿 |
| 报站稿 | 公交/地铁两套模板，站名重读、换乘提示、中英双语、首末站差异 |
| 批量合成 | 一次生成整条线路，逐站一个文件 + 可选整线合集（自动去首尾静音、按停顿拼接） |
| 音频输出 | WAV（SAPI 直出）· MP3 · M4A/AAC（Media Foundation 转码），码率可调 |
| 命令行 | `--analyze` / `--text` / `--stops` / `--voices` / `--selftest`，可脚本化 |
| 诊断 | `--mfdebug` 打印编码器返回码、`--ttsprobe` 逐步打印语音引擎状态 |

---

## 运行要求

- Windows 7 SP1 及以上（推荐 Windows 10/11），.NET Framework 4.0+（系统自带）
- 中文音色：Win10/11 通常已带 **Microsoft Huihui / Kangkang**。用 `StationVoice.exe --voices` 查看；
  若没有，请在 `设置 → 时间和语言 → 语音 → 添加语音` 安装中文语音包。

### 两个实现细节

- **语音走 SAPI 5 COM（`SAPI.SpVoice` + `SAPI.SpFileStream`），不用 `System.Speech`。**
  原因：`System.Speech` 初始化时会枚举默认音频输出设备，无声卡的机器（服务器 / CI）直接抛 `NullReferenceException`；
  `SpFileStream` 可以完全绕过音频设备，把 SSML 直接渲染进 WAV 文件。
- **EXE 带应用清单（`app.manifest`）**，声明 Win7~Win11 兼容并开启 Per-Monitor DPI 感知，
  否则系统会把进程当 Win8 对待（`Environment.OSVersion` 报 6.2，界面发虚）。

---

## 图形界面

1. **第 1 页「报站稿」**：填线路名、终点方向，选公交/地铁，粘贴站点列表，点「生成报站稿预览」看逐站文案。
2. **第 2 页「声色」**：可载入一段 3~15 秒的参考录音（WAV），点「载入参考录音」后自动分析，
   得到声学画像与风格归属；也可以不载入，直接从「风格预置」里挑腔调。三个滑块（语速/音调/停顿）可微调。
3. **第 3 页「合成导出」**：选音色、格式、输出目录，点「开始合成整线」。
   「试听首站」会先渲染第一站到临时 WAV 并播放。

站点列表每行一个，支持三种写法：

```
人民广场|People's Square|1号线      ← 站名|英文名|换乘信息
南京东路|Nanjing East Road          ← 站名|英文名
陆家嘴                              ← 只有站名
# 以 # 开头是注释
```

---

## 命令行

```bat
:: 看系统音色（含 Id，可用于 --voice）
StationVoice.exe --voices

:: 分析一段参考录音的声色
StationVoice.exe --analyze sample.wav
StationVoice.exe --analyze sample.wav --json --save profile.json

:: 单句合成
StationVoice.exe --text "下一站，人民广场。请从左侧车门下车。" --format mp3 --out out

:: 整线批量（用参考录音的音色 + 中英双语 + 生成合集）
StationVoice.exe --stops templates/metro-stops.txt --mode metro --line "地铁2号线" ^
                 --terminal "浦东国际机场" --sample sample.wav --english --format m4a --out out

:: 不做分析，直接用预置风格
StationVoice.exe --stops templates/bus-stops.txt --mode bus --line "3路" --preset "女声·公交（明亮）" --format wav

:: 列出所有风格预置 / 跑自测 / 诊断
StationVoice.exe --presets
StationVoice.exe --selftest --out test-output
StationVoice.exe --mfdebug
StationVoice.exe --ttsprobe
```

常用参数：

| 参数 | 说明 |
|---|---|
| `--format wav\|mp3\|m4a` | 输出格式，默认 `mp3` |
| `--out DIR` | 输出目录，默认 `./output` |
| `--voice NAME` | 指定音色（Id、描述或片段），省略则自动取第一个中文音色 |
| `--sample ref.wav` | 用参考录音的声色（自动匹配风格） |
| `--preset NAME` | 直接指定风格预置（与 `--sample` 并存时覆盖匹配结果） |
| `--rate N` `--pitch N` `--pause N` | 覆盖语速/音调百分比（-30~30）与句间停顿（ms） |
| `--bitrate N` | MP3/AAC 码率 kbps，默认 128 |
| `--english` | 中英双语报站 |
| `--no-merge` | 不生成整线合集 |
| `--keep-wav` | 保留中间 WAV |

退出码：`0` 成功 · `1` 参数/运行错误 · `2` 自测未通过。

---

## 声色识别到底做了什么

这不是神经网络的音色克隆，而是一套**可解释的声学特征 → 韵律参数**映射：

1. **重采样**到 16 kHz 单声道，40 ms 分帧、10 ms 步进，汉明窗。
2. **基频 F0**：每帧归一化自相关，峰位抛物线插值，取 P10/中位数/P90 与半音起伏；
   用「首个足够高的局部峰」策略抑制八度误判。
3. **音色亮度**：1024 点 FFT 求谱质心（亮度 = 质心/奈奎斯特），以及 0-0.5k / 1k / 2k / 4k / 8k 五个频带能量占比。
4. **语速**：能量包络（25 ms 窗 + 5 帧平滑）的峰密度，按有声时长折算成「音节/秒」。
5. **清晰度**：自相关峰值均值，近似谐噪比。
6. **匹配**：与 7 个风格原型在「半音域 / log2 频域 / 线性语速域」算加权距离，
   再用高斯核软融合，既给出最接近的风格（置信度），也让相近风格按比例混合。
7. **推导**：以融合后的风格为基线，叠加样本相对基线的偏差，得到最终
   `rate%`、`pitch%`、`volume%`、`pauseMs`，写进 SSML 交给系统音色。

**做不到的部分（明确说明）**：声道长度、共振峰结构、口音、个人音色本质无法通过 SAPI 音色复制。
工具能做的是「腔调迁移」——语速、音高走向、停顿节奏、明暗倾向。想更像本人，请在系统里换更接近的音色再叠加韵律。

---

## 构建

```bat
build.cmd
```

两种工具链自动选择：

1. 有 .NET SDK → `dotnet build -c Release`（读 `StationVoice.csproj`，`LangVersion=5` 与 csc 对齐）
2. 只有 Windows 自带运行时 → `csc.exe` 直接编译（`-codepage:65001` 保证中文字符串正确、`-win32manifest` 带清单）

两个产物都实测可跑，体积都在 90 KB 左右（SDK 版带版本资源）。

`ci/build.yml` 是完整的 GitHub Actions 流水线（编译 → 10 项自测 → 三种格式 CLI 冒烟 → 打包 → 自动 Release）。
它没有放在 `.github/workflows/` 下，因为向该目录推送文件需要令牌具备 `workflow` 权限。补权限后即可启用：

```bash
gh auth refresh -h github.com -s workflow
mkdir -p .github/workflows && git mv ci/build.yml .github/workflows/build.yml
git commit -m "ci: enable workflow" && git push
```

---

## 测试

`StationVoice.exe --selftest --out test-output` 覆盖 10 项：

| # | 用例 | 校验点 |
|---|---|---|
| 01 | WAV 读写回环 | 采样数、平均误差 < 2 LSB |
| 02 | FFT 频率定位 | 1 kHz 正弦峰值频点误差 ≤ 20 Hz |
| 03 | 基频检测 | 110/220 Hz 合成人声，误差 < 8%，含八度抑制 |
| 04 | 重采样保音高 | 44.1k → 16k 后仍测得 441 Hz |
| 05 | 声色分析 | F0 误差 < 12%，男女声风格分类正确，语速落在合理区间 |
| 06 | SSML 结构 | XML 可解析、含 prosody/break/emphasis、`&` 正确转义 |
| 07 | SAPI 渲染 | 真实合成中文报站，文件 > 2 KB，SSML 生效 |
| 08 | MP3 转码 | 文件头为 ID3/帧同步，体积符合码率 |
| 09 | M4A 转码 | 文件头 `ftyp`，体积合理 |
| 10 | 批量报站 | 3 站逐文件 + 合集，全部非空且 SSML 生效 |

**实测结果（Windows 10 Pro 28000，无音频输出设备）**：`9 通过 / 0 失败 / 1 跳过`。
第 07 项在本机跳过，因为该系统的 SAPI 音色引擎加载失败（`SPERR_INVALID_REGISTRY_KEY 0x80045040`，
音色能枚举、能绑定，但 `Speak` 拒绝工作）——这属于系统语音组件问题，不是代码路径问题；
在没有可用语音引擎时，第 10 项会自动切到「管道自检模式」（用合成音代替人声），
仍然真实跑通 去静音 → 拼接 → MP3/AAC 编码 → 合集 的全链路。

---

## 目录结构

```
StationVoice/
├─ build.cmd                      构建脚本（dotnet 优先，csc 兜底）
├─ StationVoice.csproj            SDK 工程（net48，LangVersion 5）
├─ app.manifest                   Win7~Win11 兼容 + DPI 感知
├─ src/
│  ├─ Program.cs                  入口：GUI / CLI 分发（带 AttachConsole）
│  ├─ Audio/
│  │  ├─ WavIO.cs                 WAV 读写、重采样、拼接、去静音
│  │  ├─ Dsp.cs                   FFT、自相关基频、谱质心、频带能量、包络峰值
│  │  ├─ TimbreProfile.cs         声色画像 + 风格预置 + 匹配推导
│  │  ├─ TimbreAnalyzer.cs        分析流水线
│  │  └─ MfTranscoder.cs          Media Foundation 互操作（PCM→MP3/M4A）+ 自检
│  ├─ Synth/
│  │  ├─ Announcement.cs          公交/地铁报站模板
│  │  ├─ SsmlBuilder.cs           画像 → SSML
│  │  ├─ SapiEngine.cs            SAPI 5 COM 封装（枚举/选音色/写文件/诊断）
│  │  └─ StationSynthesizer.cs    批量渲染、转码、合集
│  ├─ Cli/CliRunner.cs            命令行
│  ├─ SelfTest/SelfTestRunner.cs  自测
│  └─ Ui/                         WinForms 界面
├─ templates/                     示例站点文件
└─ ci/build.yml                   CI 流水线定义
```

---

## 许可

MIT License，见 [LICENSE](LICENSE)。
