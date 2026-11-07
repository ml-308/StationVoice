## StationVoice v1.0.0

用一段参考录音的「声色」驱动系统语音，生成公交 / 地铁报站音频。

### 主要能力

- **声色分析**：基频（自相关 + 抛物线插值）、谱质心、五频带能量占比、包络语速、谐噪清晰度、响度
- **风格匹配**：7 种报站风格原型，加权对数距离 + 高斯软融合，输出置信度与 Top-3 候选
- **韵律迁移**：自动推导 SSML 的 `rate` / `pitch` / `volume` / 句间停顿，也可手动微调
- **报站模板**：公交 / 地铁两套，站名重读、换乘提示、中英双语、首末站差异
- **批量导出**：逐站一个文件 + 整线合集，输出 WAV / MP3 / M4A（码率可调）
- **命令行**：`--analyze` / `--text` / `--stops` / `--voices` / `--presets` / `--selftest`
- **零第三方依赖**：语音走 SAPI 5 COM，MP3/M4A 走 Windows Media Foundation，不需要 ffmpeg / LAME

### 交付物

| 文件 | 说明 |
|---|---|
| `StationVoice.exe` | 单文件便携版（约 90 KB），.NET Framework 4.0+，免安装 |
| `templates/` | 地铁 / 公交示例站点文件 |
| `README.md` | 使用说明、原理说明与边界说明 |

### 已验证

`StationVoice.exe --selftest` 共 10 项：WAV 回环、FFT 频率定位、基频检测、重采样保音高、男女声分类、
SSML 结构合法性、SAPI 渲染、MP3 转码、M4A 转码、批量合成与合集。

构建机上实测 **9 通过 / 0 失败 / 1 跳过**：第 07 项（SAPI 渲染）因该机器的系统语音引擎加载失败
（`SPERR_INVALID_REGISTRY_KEY 0x80045040`）而跳过；此时第 10 项自动切到「管道自检模式」，
依然真实跑通 去静音 → 拼接 → MP3/AAC 编码 → 合集 的全链路。

### 修过的坑（记录在此，便于后续维护）

- `System.Speech` 在无音频输出设备的机器上初始化即 `NullReferenceException` → 改用 SAPI COM + `SpFileStream` 直接写文件
- `IMFMediaBuffer` 的 vtable 顺序是 Lock/Unlock/GetCurrentLength/SetCurrentLength/GetMaxLength（不是文档页列出的顺序）
- `IMFSample` 的 IID 是 `c40a00f2-b93a-4d80-ae8c-5a1c634f58e4`
- EXE 缺清单会被当成 Win8 进程（`OSVersion` 报 6.2）→ 补 `app.manifest`
- `.cmd` 必须 CRLF 且纯 ASCII，否则 cmd 解析中文与 LF 会报语法错误

> 音色克隆的边界已写在 README：这是「声学特征 → 韵律参数」的迁移，不是神经音色克隆。
