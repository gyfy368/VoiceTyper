---
name: advanced-interaction-design
description: >-
  Chooses one of 10 advanced UI interaction patterns and writes an English
  copyable implementation prompt. Use when the user describes an app/mini-program
  interaction, asks for motion/animation quality, or an existing desktop app
  (including VoiceTyper) needs interaction polish without restating every effect.
---

# Advanced Interaction Design

根据 APP 或小程序的界面需求，从10种高级交互模式中判断合适类型，并生成可直接交给 AI 的英文实现提示词。

## 作用

当用户描述一个APP或小程序的界面交互时，先判断这个需求真正适合哪一种交互模式，再把需求整理成可以直接交给AI的实现指令。

这不是一个视频制作Skill，也不是让 AI 随便给界面加动画。重点是判断：

- 变化从哪里发生；
- 用户的操作最后落在哪里；
- 哪些元素需要跟着变化；
- 周围内容是否需要主动让位；
- 动画结束后，用户应该看到什么结果。

已有产品（例如 VoiceTyper）且用户不愿逐条重述动效时：自己扫一遍界面，把每个会动的点映射到下面一种模式再改，不要问用户「要不要加淡入淡出」。

## 使用规则

1．先理解用户要完成的任务，再选择交互类型，不要看到"动画"两个字就直接添加通用淡入淡出。
2．默认选择一个最匹配的主交互。如果一个需求确实包含多个交互，拆成主交互和辅助交互，并说明两者的关系。
3．保留用户已有的视觉风格、组件结构和技术栈，不要改写无关页面。
4．描述交互时必须写清楚触发方式、开始状态、变化过程、结束状态和失败或取消状态。
5．需要实现代码时，再根据用户指定的技术栈输出代码；用户只要提示词时，不要擅自输出完整项目代码。
6．所有 Copyable prompt 必须使用英文，方便用户直接复制给其他AI。不要在英文提示词中混入中文。
7．不要把"更高级""更有质感""更顺滑"当作完整需求，必须翻译成具体的状态变化、位置变化、尺寸变化或反馈方式。
8．如果关键信息缺失但仍能合理判断，先做一个明确假设并继续，不要连续追问。

## 判断方法

先用下面四个问题定位需求：

变化应该从哪里开始？
对应需要关注的交互：Radial Theme Transition、Drag-to-Reorder
操作最后应该停在哪里？
对应需要关注的交互：Staggered Bulk Selection、Velocity-Based Slider Snap
这次变化需要谁跟着回应？
对应需要关注的交互：Spring Stepper Progress、Ripple Feedback for Related Switches
周围内容要不要一起让位？
对应需要关注的交互：Curved Card Deletion、Stacked Card Scroll、Expanding Tag Selection
如果是内容本身从隐藏变为显示，优先检查 Animated Text Disclosure。

完整 10 种模式英文 prompt 与必须保留项见 [patterns.md](patterns.md)。

## 输出格式

当用户描述一个具体界面需求时，按下面格式回答：

Selected interaction
写出最匹配的英文名称和中文名称。

Why this fits
用简短中文说明它解决的是"从哪里发生""落在哪里""谁跟着变化"还是"周围是否让位"。

Interaction behavior
用中文说明触发方式、开始状态、变化过程、最终状态、取消或失败状态，以及移动端需要注意的触控区域。

Copyable prompt
只输出一段英文提示词。提示词必须包含：

- 具体组件或界面对象；
- 触发方式；
- 开始状态和结束状态；
- 位移、尺寸、透明度、裁切、弹簧或过冲等具体变化；
- 数据状态和视觉状态之间的关系；
- 响应式、键盘、触控和reduced-motion要求；
- 不要改写无关组件。

Use the selected interaction pattern in my existing interface. Preserve the current visual style, layout language, and component structure. Do not rewrite unrelated components. Implement the trigger, state changes, start and end states, motion behavior, responsive behavior, keyboard accessibility, touch support, and reduced-motion fallback described below:

[Insert the selected English interaction prompt here]

## 不要这样回答

- 不要只说"加一个高级动画"。
- 不要把 10种模式全部混在一起输出。
- 不要把视觉反馈误写成真实功能变化，例如让邻近开关跟着改变状态。
- 不要用固定时长掩盖没有定义开始状态和结束状态的问题。
- 不要输出视频剪辑、配音、字幕或视频制作流程。
- 不要生成中英文双栏提示词图；需要图片时，只提供适合做信息浓缩图的结构和文案。
