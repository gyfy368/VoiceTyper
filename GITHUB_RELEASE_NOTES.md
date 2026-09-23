# v1.1.1 发布说明（创建 GitHub Release 时可粘贴）

Windows 10/11 x64，解压即用。模型已经打进压缩包，仓库里没有 onnx。

请上传资源：`dist/VoiceTyper-v1.1.1-win-x64.zip`（约 225 MB）。不要把 zip 或 `models/*.onnx` 推进 Git。

这次把任务栏、资源管理器和托盘图标换成拿纸的鳄鱼。功能与 1.1.0 相同。

---

Unzip and double-click `VoiceTyper.exe`. No .NET install needed.

- Taskbar, Explorer, and tray use the crocodile-with-paper icon
- Continue-say inserts at the caret; optional copy of the new segment only
- Copy toast, window opacity, and undo for the last continue utterance
- Capsule and paper fade in sequence instead of morphing while both are visible
- SenseVoice (zh/en/ja/ko/yue) + Silero VAD, ~228 MB, bundled in this zip
- Microphone permission: Settings → Privacy → Microphone → allow desktop apps
