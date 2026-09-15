# VoiceTyper 图标设计任务书

给设计 AI / 设计师用。不要沿用现在的托盘简笔画麦克风。交付要能直接进 Windows 托盘、任务栏和 exe。

## 产品一句话

VoiceTyper 是一张贴在桌面边缘的**纸片**：本地离线语音转文字，占内存小，界面安静。不是录音棚 App，也不是聊天机器人。

## 必须传达

1. **纸 / 胶囊**：可收成一条、可展开成纸。轮廓宁方可识别成「一张纸」或「一粒胶囊」，不要做成播放器或播客 logo。
2. **听**：有麦克风语义，但麦克风只能是配角（不超过画面 1/3）。
3. **本地、安静**：无云、无 Wi‑Fi 符号、无声波彩虹、无渐变紫、无大字 V。
4. **好认小尺寸**：16×16 和 20×20 托盘仍能看出形状，不能糊成一团。

## 禁止

- Material / iOS 默认麦克风剪影直接套圆
- 聊天气泡、机器人头、波形 EQ
- 斜体科技字、霓虹描边、玻璃拟态堆滤镜
- 和 Windows 系统「声音设置」图标撞车的纯扬声器

## 建议形态（任选其一做深，不要拼三个）

- **折角纸片 + 极简麦杆**：深色暖纸（约 `#221C17`）上浅象牙色折角，麦只用两根线和一个圆点。
- **胶囊剖面**：圆角胶囊里一条细缝像纸边，中间一个点表示「在听」。
- **印章**：方印里一个负形麦，边比芯更重要。

## 色彩

主色跟应用墨黑纸：底 `#1A1612`～`#221C17`，金属点缀最多一处 `#C8A56A`，前景 `#F3EBE0`。浅色任务栏要另出一版深色前景，保证对比。

## 交付规格

| 文件 | 用途 |
|------|------|
| `app.ico` | 含 16 / 20 / 24 / 32 / 48 / 64 / 128 / 256 PNG 层，Windows 多尺寸 ico |
| `icon-256.png` | 透明底，GitHub / README |
| `icon-1024.png` | 源文件备份 |
| 可选 `icon.svg` | 纯几何，少滤镜，可缩到 16px |

格式：ico 必须真·多分辨率，不要单张 256 硬缩。背景透明。不要 JPG。

## 验收

1. 把 16px 放在浅灰任务栏和深色任务栏各看一眼，仍能说出「纸或胶囊」，不是「一个圆」。
2. 和资源管理器里普通文件夹图标并排，不能完全淹没。
3. 不靠颜色盲以外的人才能认（形状先成立，金色只是点缀）。

## 给设计 AI 的短指令（可整段复制）

Design a Windows app icon for VoiceTyper, a local offline speech-to-text floating paper that collapses into an edge capsule. Quiet, small-memory utility. Motif: paper sheet or capsule first, microphone as a minor mark (under one third of the mark). Palette: ink paper #221C17, ivory #F3EBE0, one brass accent #C8A56A. No Material mic, no chatbot, no neon, no purple gradient, no Wi-Fi, no EQ bars. Must stay identifiable at 16px. Deliver a multi-size .ico (16 through 256) plus 256 and 1024 PNG with transparency, optional geometric SVG.
