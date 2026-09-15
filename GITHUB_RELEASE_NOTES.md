# v1.1.0 发布说明（创建 GitHub Release 时可粘贴）

Windows 10/11 x64，解压即用。模型已经打进压缩包，仓库里没有 onnx。

请上传资源：`dist/VoiceTyper-v1.1.0-win-x64.zip`（约 225 MB）。不要把 zip 或 `models/*.onnx` 推进 Git。

这次主要是录音后续写：光标处插入、只复制新加的内容、复制提示、撤回继续说、胶囊先淡出再换面。

---

Unzip and double-click `VoiceTyper.exe`. No .NET install needed.

- Continue-say inserts at the caret; optional copy of the new segment only
- Copy toast, window opacity, and undo for the last continue utterance
- Capsule and paper fade in sequence instead of morphing while both are visible
- SenseVoice (zh/en/ja/ko/yue) + Silero VAD, ~228 MB, bundled in this zip
- Microphone permission: Settings → Privacy → Microphone → allow desktop apps
