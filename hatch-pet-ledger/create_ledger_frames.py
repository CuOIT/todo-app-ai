from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
FRAMES = ROOT / "frames"
CELL_W = 192
CELL_H = 208
SCALE = 4

ROWS = {
    "idle": 6,
    "running-right": 8,
    "running-left": 8,
    "waving": 4,
    "jumping": 5,
    "failed": 8,
    "waiting": 6,
    "running": 6,
    "review": 6,
}


def sc(v: float) -> int:
    return round(v * SCALE)


def rounded(draw: ImageDraw.ImageDraw, box, radius, fill, outline=None, width=1):
    box = tuple(sc(v) for v in box)
    draw.rounded_rectangle(box, radius=sc(radius), fill=fill, outline=outline, width=sc(width))


def ellipse(draw: ImageDraw.ImageDraw, box, fill, outline=None, width=1):
    box = tuple(sc(v) for v in box)
    draw.ellipse(box, fill=fill, outline=outline, width=sc(width))


def line(draw: ImageDraw.ImageDraw, points, fill, width=1):
    draw.line([(sc(x), sc(y)) for x, y in points], fill=fill, width=sc(width), joint="curve")


def frame_base(
    *,
    bob=0,
    lean=0,
    face_shift=0,
    arm_left=0,
    arm_right=0,
    crest=0,
    eye_mode="calm",
    focus=0,
    x_offset=0,
    y_offset=0,
    facing=1,
):
    img = Image.new("RGBA", (CELL_W * SCALE, CELL_H * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx = 96 + x_offset
    cy = 105 + bob + y_offset
    lean_px = lean * facing

    charcoal = (40, 48, 54, 255)
    charcoal_dark = (24, 29, 34, 255)
    teal = (31, 164, 157, 255)
    teal_dark = (18, 114, 115, 255)
    amber = (248, 184, 74, 255)
    amber_dark = (166, 112, 37, 255)
    cream = (255, 230, 155, 255)
    ink = (18, 23, 27, 255)
    pale = (169, 235, 224, 255)

    # Back tab crest, shaped like a checklist marker without readable text.
    rounded(draw, (76 + lean_px, cy - 81 + crest, 116 + lean_px, cy - 55 + crest), 8, teal_dark, charcoal_dark, 2)
    rounded(draw, (84 + lean_px, cy - 75 + crest, 108 + lean_px, cy - 61 + crest), 4, pale)
    line(draw, [(90 + lean_px, cy - 68 + crest), (95 + lean_px, cy - 64 + crest), (103 + lean_px, cy - 72 + crest)], amber_dark, 3)

    # Side ears/buttons.
    ellipse(draw, (36 + lean_px, cy - 33, 60 + lean_px, cy - 7), teal_dark, charcoal_dark, 2)
    ellipse(draw, (132 + lean_px, cy - 33, 156 + lean_px, cy - 7), teal_dark, charcoal_dark, 2)

    # Body shell.
    rounded(draw, (49 + lean_px, cy - 55, 143 + lean_px, cy + 61), 34, charcoal, charcoal_dark, 3)
    rounded(draw, (57 + lean_px, cy - 48, 76 + lean_px, cy + 45), 10, teal_dark)
    rounded(draw, (116 + lean_px, cy - 48, 135 + lean_px, cy + 45), 10, teal)

    # Amber face plate.
    rounded(draw, (64 + lean_px + face_shift * facing, cy - 35, 128 + lean_px + face_shift * facing, cy + 17), 19, amber, amber_dark, 2)
    if eye_mode == "blink":
        line(draw, [(78 + lean_px, cy - 10), (88 + lean_px, cy - 10)], ink, 3)
        line(draw, [(104 + lean_px, cy - 10), (114 + lean_px, cy - 10)], ink, 3)
    elif eye_mode == "failed":
        line(draw, [(78 + lean_px, cy - 15), (88 + lean_px, cy - 5)], ink, 3)
        line(draw, [(88 + lean_px, cy - 15), (78 + lean_px, cy - 5)], ink, 3)
        line(draw, [(104 + lean_px, cy - 15), (114 + lean_px, cy - 5)], ink, 3)
        line(draw, [(114 + lean_px, cy - 15), (104 + lean_px, cy - 5)], ink, 3)
    else:
        ellipse(draw, (78 + lean_px, cy - 16 + focus, 89 + lean_px, cy - 4 + focus), ink)
        ellipse(draw, (103 + lean_px, cy - 16 + focus, 114 + lean_px, cy - 4 + focus), ink)
        ellipse(draw, (82 + lean_px, cy - 13 + focus, 85 + lean_px, cy - 10 + focus), cream)
        ellipse(draw, (107 + lean_px, cy - 13 + focus, 110 + lean_px, cy - 10 + focus), cream)

    if eye_mode == "failed":
        line(draw, [(84 + lean_px, cy + 8), (96 + lean_px, cy + 3), (108 + lean_px, cy + 8)], ink, 3)
    else:
        line(draw, [(84 + lean_px, cy + 5), (94 + lean_px, cy + 10), (108 + lean_px, cy + 5)], ink, 2)

    # Arms and feet.
    left_raise = arm_left
    right_raise = arm_right
    line(draw, [(51 + lean_px, cy + 4), (31 + lean_px, cy + 18 - left_raise), (23 + lean_px, cy + 34 - left_raise)], teal, 7)
    line(draw, [(141 + lean_px, cy + 4), (161 + lean_px, cy + 18 - right_raise), (169 + lean_px, cy + 34 - right_raise)], teal, 7)
    ellipse(draw, (17 + lean_px, cy + 29 - left_raise, 31 + lean_px, cy + 43 - left_raise), amber)
    ellipse(draw, (161 + lean_px, cy + 29 - right_raise, 175 + lean_px, cy + 43 - right_raise), amber)

    ellipse(draw, (60 + lean_px, cy + 52, 86 + lean_px, cy + 72), charcoal_dark)
    ellipse(draw, (106 + lean_px, cy + 52, 132 + lean_px, cy + 72), charcoal_dark)
    ellipse(draw, (65 + lean_px, cy + 53, 84 + lean_px, cy + 66), teal_dark)
    ellipse(draw, (108 + lean_px, cy + 53, 127 + lean_px, cy + 66), teal)

    return img.resize((CELL_W, CELL_H), Image.Resampling.LANCZOS)


def save_state(state: str, frames):
    out_dir = FRAMES / state
    out_dir.mkdir(parents=True, exist_ok=True)
    for index, img in enumerate(frames):
        img.save(out_dir / f"{index:02d}.png")


def make_frames():
    save_state("idle", [
        frame_base(bob=math.sin(i / 6 * math.tau) * 2, crest=math.sin(i / 6 * math.tau) * 1, eye_mode="blink" if i == 3 else "calm")
        for i in range(6)
    ])
    save_state("running-right", [
        frame_base(x_offset=(i - 3.5) * 2, bob=-abs(math.sin(i / 8 * math.tau)) * 5, lean=4, arm_left=(i % 2) * 8, arm_right=((i + 1) % 2) * 8, face_shift=2, facing=1)
        for i in range(8)
    ])
    save_state("running-left", [
        frame_base(x_offset=(3.5 - i) * 2, bob=-abs(math.sin(i / 8 * math.tau)) * 5, lean=4, arm_left=((i + 1) % 2) * 8, arm_right=(i % 2) * 8, face_shift=2, facing=-1)
        for i in range(8)
    ])
    save_state("waving", [
        frame_base(arm_right=raise_amt, bob=-1 if i % 2 else 0)
        for i, raise_amt in enumerate([18, 38, 26, 42])
    ])
    save_state("jumping", [
        frame_base(y_offset=y, bob=-2 if i in (1, 2, 3) else 0, arm_left=8, arm_right=8)
        for i, y in enumerate([10, -8, -16, -8, 8])
    ])
    save_state("failed", [
        frame_base(bob=(i % 2) * 2, lean=(-3 if i % 2 else 3), eye_mode="failed", crest=2, arm_left=4, arm_right=4)
        for i in range(8)
    ])
    save_state("waiting", [
        frame_base(bob=math.sin(i / 6 * math.tau) * 1, arm_left=14 + (i % 2) * 4, arm_right=14 + ((i + 1) % 2) * 4, face_shift=0, focus=1)
        for i in range(6)
    ])
    save_state("running", [
        frame_base(bob=math.sin(i / 6 * math.tau) * 2, lean=(-2 if i % 2 else 2), crest=-2 if i % 2 else 1, arm_left=8, arm_right=8, focus=-1)
        for i in range(6)
    ])
    save_state("review", [
        frame_base(bob=0, lean=3, face_shift=1, focus=2 if i in (1, 2, 3) else 0, eye_mode="blink" if i == 4 else "calm")
        for i in range(6)
    ])


def main():
    make_frames()
    manifest = {
        "cell_width": CELL_W,
        "cell_height": CELL_H,
        "states": [{"name": name, "frames": count} for name, count in ROWS.items()],
        "note": "Deterministic local fallback frames for Ledger, a compact workflow sentinel pet.",
    }
    (FRAMES / "frames-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
