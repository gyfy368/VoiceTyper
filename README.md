# VoiceTyper

贴边胶囊，点开是一张纸。对着麦克说话，字写在纸上：可以改，也可以复制。录音时的脉冲和展开收回都不抢戏。

音频不出本机。Windows 10/11 x64。

**中文** · [English](#english)

---

## 中文

### 怎么用（解压即用）

模型（约 228 MB 的 onnx）在 **Release 压缩包**里，不在 Git 仓库。clone 下来不能直接转写。

1. 在 GitHub Releases 下载 `VoiceTyper-v1.0.0-win-x64.zip`
2. 解压到任意文件夹
3. 双击 `VoiceTyper.exe`
4. 点贴边胶囊展开；点麦克开始说。停大约一秒会自动结束，也可以再点麦克停
5. 字出现后纸默认还开着，方便改。剪贴板是默认开启的选项，不是唯一用法：聊天、笔记、改稿、自己看都行

右键胶囊或托盘图标 → 设置 / 显示 / 退出。展开后顶栏齿轮也能进设置。

解压后大致是这样：

```
VoiceTyper.exe
README.md
models/
  silero_vad.onnx
  sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/
    model.int8.onnx
    tokens.txt
```

### 设置（写在 exe 旁的 `settings.json`）

| 选项 | 默认 |
|------|------|
| 关闭窗口：退出 / 仅藏托盘 | 退出 |
| 显示任务栏图标 | 开 |
| 始终置顶 | 开 |
| 失焦自动收成胶囊 | 关 |
| 静音自动停录 | 开 |
| 转完自动进剪贴板 | 开 |
| 转完后收成胶囊 | 关（方便改字） |
| 配色：墨黑 / 暖纸 / 冷灰 / 林绿 | 墨黑 |
| 胶囊贴左 / 右 | 右 |
| 纸张大小 | 中（320×428） |

展开后的纸可以拖走；胶囊仍贴边。纸上有字之后，麦克会分成「新开一句」和「继续说」。

### 从源码编译

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。Release 压缩包是自包含的，最终用户不用装 .NET。

```powershell
dotnet build VoiceTyper.sln -c Release
dotnet test VoiceTyper.sln -c Release
.\scripts\publish-release.ps1
```

产物：`dist\VoiceTyper-v1.0.0-win-x64.zip`（exe + models + README）。

仓库里没有 onnx。第一次自己跑，程序会尝试下载模型；下失败就把 Release 里的 `models/` 整个拷到仓库根目录。不要 `git add` 那些 onnx。

麦克风：Windows 设置 → 隐私和安全性 → 麦克风 → 允许桌面应用。

### 许可

MIT。转写走 [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) / SenseVoice，静音检测走 Silero VAD。

---

## English

A docked capsule that opens into a paper window. You talk; the words land on the paper. Edit them, copy them, or leave them there. The recording pulse and the expand/collapse motion are meant to stay quiet.

Audio never leaves the machine. Windows 10/11 x64.

### Run from the Release zip

The models (~228 MB onnx) are **in the zip**, not in git. Cloning the repo is not enough to transcribe.

1. Download `VoiceTyper-v1.0.0-win-x64.zip` from GitHub Releases
2. Unzip anywhere
3. Double-click `VoiceTyper.exe`
4. Click the capsule, then the mic. Silence for about a second stops recording; clicking the mic also stops
5. The paper stays open so you can edit. Clipboard copy is on by default, but it is not the only use: chat, notes, drafts, or just reading the paper

Right-click the capsule or tray icon for Settings / Show / Exit.

### Settings (saved as `settings.json` next to the exe)

| Option | Default |
|--------|---------|
| Close: exit / tray | Exit |
| Taskbar icon | On |
| Always on top | On |
| Collapse when unfocused | Off |
| Auto-stop on silence | On |
| Copy when done | On |
| Collapse when done | Off (easier to edit) |
| Theme: ink / warm paper / cool gray / forest | Ink |
| Dock left / right | Right |
| Paper size | Medium (320×428) |

You can drag the paper; the capsule stays docked. After there is text, the mic splits into “new utterance” and “continue”.

### Build from source

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). The Release zip is self-contained; end users do not install .NET.

```powershell
dotnet build VoiceTyper.sln -c Release
dotnet test VoiceTyper.sln -c Release
.\scripts\publish-release.ps1
```

Output: `dist\VoiceTyper-v1.0.0-win-x64.zip`.

Onnx files are gitignored. On first run the app may download models; if that fails, copy `models/` from the Release zip. Do not force-add onnx into git.

Microphone: Windows Settings → Privacy → Microphone → allow desktop apps.

### License

MIT. Recognition via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) / SenseVoice; silence detection via Silero VAD.
