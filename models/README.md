# models/

转写模型放这里。Release 压缩包里已经带上，解压就能离线用。

这些文件**不要**推进 Git（`.gitignore` 已排除 `*.onnx`）。自己从源码跑的话，把 Release 里的 `models/` 拷过来，或第一次启动让程序下载。

This folder holds the offline ASR models. They ship **inside the Release zip**, not in git.

```
models/
  README.md                          ← 可以进 Git
  silero_vad.onnx                    ← 不进 Git（约 0.6 MB）
  sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/
    model.int8.onnx                  ← 不进 Git（约 228 MB）
    tokens.txt                       ← 词表，体积小，可以进 Git
```

| 文件 | 用途 |
|------|------|
| `model.int8.onnx` | SenseVoice 多语言识别（中/英/日/韩/粤，int8） |
| `tokens.txt` | 配套词表 |
| `silero_vad.onnx` | 静音检测，说完约一秒自动停录 |

路径不要改。程序从 exe 旁边的 `models/` 加载。
