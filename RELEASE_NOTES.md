## StationVoice v1.0.0

用一段参考录音的「声色」驱动系统语音，生成公交 / 地铁报站音频。

### 主要能力

- **声色分析**：基频（自相关 + 抛物线插值）、谱质心、五频带能量占比、包络语速、谐噪清晰度、响度
- **风格匹配**：7 种报站风格原型，加权对数距离 + 高斯软融合，输出置信度与 Top-3 候选
- **韵律迁移**：自动推导 SSML 的 `rate` / `pitch` / `volume` / 句间停顿，也可手动微调
- **报站模板**：公交 / 地铁两套，站名重读、换乘提示、中英双语、首末站差异
- **批量导出**：逐站一个文件 + 整线合集，输出 WAV / MP3 / M4A（码率可调）
- **命令行**：`--analyze` / `--text` / `--stops` / `--presets` / `--selftest`
- **零第三方依赖**：MP3/M4A 通过 Windows Media Foundation 直接编码，不依赖 ffmpeg / LAME

### 交付物

| 文件 | 说明 |
|---|---|
| `src/` | 全部 C# 源码（15 个文件，约 3,700 行，无外部包） |
| `build.cmd` | 一键编译脚本，使用系统自带 `csc.exe`（.NET Framework 4.x） |
| `templates/` | 地铁 / 公交示例站点文件 |
| `ci/build.yml` | 完整 CI 流水线：编译 → 自测 → 三种格式冒烟 → 打包 → 自动 Release |
| `README.md` | 使用说明、原理说明与边界说明 |

### 如何得到可执行文件

```bat
build.cmd
```

产物为单文件 `StationVoice.exe`（约 100 KB，需 .NET Framework 4.0+）。
补上 `workflow` 权限后把 `ci/build.yml` 移到 `.github/workflows/`，即可由 Actions 自动编译并发布便携包。

### 自测覆盖

`StationVoice.exe --selftest` 共 10 项：WAV 回环、FFT 频率定位、基频检测、重采样保音高、男女声分类、
SSML 结构合法性、SAPI 渲染、MP3 转码、M4A 转码、批量合成与合集。

> 音色克隆的边界写在 README 里：这是「声学特征 → 韵律参数」的迁移，不是神经音色克隆。
> 输出音频使用 Windows Media Foundation 编码，全程离线。
