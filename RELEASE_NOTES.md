# v1.2.0 — 发现新版本提示（应用内更新检查）

报站声色模仿器 1.2.0 新增应用内「发现新版本」提示：日常使用中不再错过重要更新，也不会被检查打扰。

## 新增

* **发现新版本提示**：程序启动后在后台检查 GitHub Releases 的最新版本（最多每 24 小时一次）。
  发现比当前版本更新的版本时弹窗提示，显示最新版本号、发布时间与该版本的更新内容，
  并提供「立即下载」「稍后提醒」「跳过此版本」三个选择；选择跳过的那一版不再打扰。
* **检查更新入口**：菜单 / 工具栏里的「检查更新…」可随时手动检查，已是最新时会明确告知。
* **无人值守检查**：--update-probe <文件> 把检查结果写成 JSON（脚本 / CI 用），
  --check-update 立即检查，--no-update-check 本次启动跳过检查。

## 说明

* 检查失败（断网、代理、GitHub 限流等）只会提示「暂时无法检查更新」，绝不影响程序正常使用；
* 只请求 api.github.com/repos/ml-308/StationVoice/releases/latest，不收集任何信息；
* 版本比较按「主.次.修订」三段数字，忽略 v 前缀。

## 本版其它改动

* 版本 1.1.0 → 1.2.0；SAPI 合成与音色拟合流程不变。

> 安装包：StationVoice-Setup-1.2.0.exe（Inno Setup 6，每用户安装，无需管理员权限）

---

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
