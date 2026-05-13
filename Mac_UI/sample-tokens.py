#!/usr/bin/env python3
"""从 Mac_UI/images/*.png 采样关键 anchor 像素，生成 sampled-tokens.json。

anchor 位置由肉眼挑选（标定特征区域中心点），output 为 token 名 → "#RRGGBB"。
后续如需调整，改 ANCHORS 字典即可。
"""
from __future__ import annotations
import json
import sys
from pathlib import Path
from PIL import Image

# (image_name, (x, y), token_name)
ANCHORS = [
    # 01 Welcome
    ("01-welcome-project-selector.png", (40, 40),   "AccentBase"),       # 天命 logo 青色
    ("01-welcome-project-selector.png", (300, 300), "SurfaceCanvas"),    # 页面背景
    ("01-welcome-project-selector.png", (480, 180), "SurfaceBase"),      # 卡片底
    # 02 Main workspace
    ("02-main-workspace-three-column.png", (520, 220), "TextPrimary"),   # dashboard 标题黑色
    ("02-main-workspace-three-column.png", (520, 240), "TextSecondary"), # 副文字
    # ...实施时按肉眼挑选位置补全所有 token，先有结构再迭代
]

def sample(image_path: Path, xy: tuple[int, int]) -> str:
    img = Image.open(image_path).convert("RGB")
    r, g, b = img.getpixel(xy)
    return f"#{r:02X}{g:02X}{b:02X}"

def main() -> int:
    images_dir = Path(__file__).parent / "images"
    out: dict[str, str] = {}
    for image_name, xy, token in ANCHORS:
        path = images_dir / image_name
        if not path.exists():
            print(f"WARN: {path} not found, skipping", file=sys.stderr)
            continue
        try:
            color = sample(path, xy)
            out[token] = color
            print(f"  {token:20s} = {color}  ({image_name} @ {xy})")
        except Exception as exc:
            print(f"WARN: sampling {token} failed: {exc}", file=sys.stderr)

    out_path = Path(__file__).parent / "sampled-tokens.json"
    out_path.write_text(json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"\nwrote {out_path}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
