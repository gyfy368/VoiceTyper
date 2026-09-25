# v1.1.2 发布说明（创建 GitHub Release 时可粘贴）

Windows 10/11 x64，解压即用。模型已经打进压缩包，仓库里没有 onnx。

请上传资源：`dist/VoiceTyper-v1.1.2-win-x64.zip`（约 225 MB）。不要把 zip 或 `models/*.onnx` 推进 Git。

修复：展开后的纸窗可从空白区域与外壳拖动；按钮、转写框与右下角缩放柄仍不会误触发拖动。胶囊点击展开/拖动逻辑不变。

「已复制」和「撤回刚才继续说的」改到文字框下方那行小字上，小字先淡出再让位。文字框不再被多占的一行挤短。

---

Unzip and double-click `VoiceTyper.exe`. No .NET install needed.

- Fix: drag the expanded paper from empty chrome / blank paper area
- Buttons, transcript box, and resize grip still do not start a window drag
- Capsule click-to-expand and capsule drag unchanged
- Copy toast and undo sit on the hint line under the transcript; the hint fades out to make room, and the text well does not lose a row
- SenseVoice (zh/en/ja/ko/yue) + Silero VAD, ~228 MB, bundled in this zip
- Microphone permission: Settings → Privacy → Microphone → allow desktop apps
