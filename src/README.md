# 概要

- [概要](#概要)
  - [📦 数据来源](#-数据来源)
  - [✏️ 校对格式](#️-校对格式)
  - [🧮 管线设计](#-管线设计)

## 📦 数据来源

| 数据 | 文件 | 说明 |
|------|------|------|
| 英文原文 | `assets/dialoguebundle_*` | Unity AssetBundle，PixelCrushers Dialogue System |
| 官方中译 | `assets/lockits/chinese_0` | Unity AssetBundle，I2 Localization，游戏分发原版，**永远不改** |
| 校对文件 | `data/<area>.tsv` | TSV 格式，按对话树区域前缀拆分，git 追踪翻译版本演进 |

## ✏️ 校对格式

`data/<area>.tsv`，制表符分隔，每行对应 I2 Localization 的一个 term：

| 列 | 说明 |
|----|------|
| `ArticyId` | 对话节点唯一标识 |
| `TermType` | I2 term 类型前缀：`Dialogue Text`（主对白）、`Alternate1`-`Alternate4`（备选变体）、`tooltip1`-`tooltip10`（检定提示） |
| `ConvTitle` | 所属对话树 |
| `Actor` | 说话人 |
| `En` | 对应 term 类型的英文原文 |
| `Zh` | 中文译文 |

> TSV 而非 CSV：英文含大量逗号，TSV 避免转义，便于 `grep` 和 `git diff`。

## 🧮 管线设计

```
dialoguebundle ──┐
                 ├── Extract ──► data/<area>.tsv (tsv_base)
chinese_0 ───────┘
                                   │ 人工校对
                                   ▼
                              data/<area>.tsv (tsv_edit)
                                   │
                 chinese_0 ────────┤
                                   │
                                   ▼ Build
                              build/chinese

tsv_base           = Extract(chinese_0, dialoguebundle)                raw 提取，全部 term 类型
chinese_0          ≈ Build(tsv_base, chinese_0)                        传输层：字节不同（压缩格式差异）
dec(chinese_0)     = dec(Build(tsv_base, chinese_0))                   数据层：解压后逐字节相同
build/chinese      = Build(tsv_edit, chinese_0)                        分发翻译
```

- **全部 term 类型覆盖**：Dialogue Text（主对白）+ Alternate1-4（备选变体）+ tooltip1-10（检定提示），一行对应 I2 字典一条 term
- **翻译数据完全保真**：extract → 编辑 → build → extract 回到编辑内容，未编辑部分不变
- **chinese_0 不可变**：所有构建以它为模板，不依赖任何中间产出
- **压缩格式逐字节一致**：不同 LZMA 实现产出不同压缩字节，不影响游戏引擎读取