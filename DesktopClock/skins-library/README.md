# DesktopClock 官方皮肤素材库

本目录是 DesktopClock 表盘底图的官方素材仓库，提供可直接使用的矢量表盘底图（SVG 源）。
每个底图一个子文件夹，内含矢量源、元数据与预览说明。

## 简介

这里收录的素材用于「指针表盘」（AnalogClockSkin）的自定义底图。
主程序通过 `SkinBackgroundConfig`（路径 / 透明度 / 模糊 / 拉伸）加载 PNG 底图；
本仓库以 **SVG 矢量源**形式发布，可经工具转换为 1024×1024 PNG 后填入设置面板使用。

> 命名说明：因 Windows 文件系统（NTFS）不区分大小写，`skins/` 与代码目录 `Skins/` 会指向同一目录而冲突。
> 故官方素材仓库使用 `skins-library/`，与代码层 `Skins/`（`IClockSkin` 接口及各皮肤实现）严格区分。

## 目录结构

```
skins-library/
├── README.md                  # 本说明文档
├── LICENSE                    # MIT 许可证
├── minimal-white/             # 极简白
│   ├── dial.svg               # 表盘矢量源
│   └── meta.json              # 元数据
├── dark-neon/                 # 暗色霓虹
│   ├── dial.svg
│   └── meta.json
└── classic-roman/             # 经典罗马
    ├── dial.svg
    └── meta.json
```

每个底图子文件夹包含：

| 文件 | 说明 |
| --- | --- |
| `dial.svg` | 表盘矢量源（viewBox `0 0 400 400`），浏览器可直接打开预览 |
| `meta.json` | 底图元数据（名称、作者、描述、标签等） |
| `preview.png` | （可选）预览图，由 SVG 导出 |

## meta.json 格式

```json
{
  "name": "极简白",
  "author": "DesktopClock",
  "description": "纯白矢量表盘，黑色刻度",
  "preview": "preview.png",
  "dial": "dial.png",
  "tags": ["minimal", "white"]
}
```

字段说明：

- `name`：底图显示名称
- `author`：作者
- `description`：简短描述
- `preview`：预览图文件名（由 SVG 导出）
- `dial`：实际加载的 PNG 文件名（由 SVG 转换得到）
- `tags`：标签数组，便于检索

## 安装方法

1. 将 `dial.svg` 转换为 `dial.png`（1024×1024，透明背景）。推荐命令：
   ```bash
   # 使用 Inkscape
   inkscape dial.svg --export-type=png --export-filename=dial.png -w 1024 -h 1024
   # 或使用 rsvg-convert
   rsvg-convert -w 1024 -h 1024 dial.svg -o dial.png
   ```
2. 打开 DesktopClock 设置面板。
3. 将 `dial.png` 的绝对路径填入：
   - 「相册背景」路径，或
   - 「指针表盘底图」路径
4. 按需调整透明度、模糊、拉伸等参数（`SkinBackgroundConfig`）。

## 贡献指南

欢迎通过 PR 提交新底图，请遵循以下要求：

- **格式**：PNG 透明背景，分辨率 1024×1024，体积 < 500KB
- **矢量源**：同时提交 SVG（viewBox `0 0 400 400`），便于后续维护
- **元数据**：每个底图一个子文件夹，必须包含 `meta.json`
- **预览**：附 `preview.png` 预览图
- **许可**：提交即视为在 MIT 许可下发布
- 命名使用小写英文加连字符，如 `minimal-white`、`dark-neon`

## 许可证

MIT，详见 [LICENSE](./LICENSE)。
