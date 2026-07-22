# -*- coding: utf-8 -*-
"""Generate 800x800 Steam Workshop covers for the two mods."""
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_BOLD = r"C:/Windows/Fonts/msyhbd.ttc"
FONT_REG = r"C:/Windows/Fonts/msyh.ttc"

GOLD = (216, 178, 106)
DIM_GOLD = (178, 148, 92)
INK = (16, 20, 22)


def square_crop(img: Image.Image) -> Image.Image:
    w, h = img.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    return img.crop((left, top, left + side, top + side)).resize((800, 800), Image.LANCZOS)


def make_cover(bg_path, title, subtitle, footer, out_path, dark=0.38, blur=3):
    bg = Image.open(bg_path).convert("RGB")
    bg = square_crop(bg)
    if blur:
        bg = bg.filter(ImageFilter.GaussianBlur(blur))
    bg = ImageEnhance.Brightness(bg).enhance(dark)

    # vignette: darken edges
    vignette = Image.new("L", (800, 800), 0)
    vd = ImageDraw.Draw(vignette)
    vd.ellipse((-260, -260, 1060, 1060), fill=255)
    vignette = vignette.filter(ImageFilter.GaussianBlur(140))
    black = Image.new("RGB", (800, 800), (0, 0, 0))
    bg = Image.composite(bg, black, vignette)

    # top / bottom accent bars
    d = ImageDraw.Draw(bg)
    d.rectangle((0, 0, 800, 6), fill=(122, 26, 26))
    d.rectangle((0, 794, 800, 800), fill=(122, 26, 26))

    def center_text(y, text, font, fill, stroke=0):
        bbox = d.textbbox((0, 0), text, font=font, stroke_width=stroke)
        tw = bbox[2] - bbox[0]
        d.text(((800 - tw) / 2, y), text, font=font, fill=fill,
               stroke_width=stroke, stroke_fill=(10, 8, 6))

    def fit_font(text, start, max_w, bold=True):
        size = start
        while size > 20:
            f = ImageFont.truetype(FONT_BOLD if bold else FONT_REG, size)
            bbox = d.textbbox((0, 0), text, font=f)
            if bbox[2] - bbox[0] <= max_w:
                return f
            size -= 4
        return f

    f_title = fit_font(title, 118, 700)
    f_sub = fit_font(subtitle, 40, 720, bold=False)
    f_foot = ImageFont.truetype(FONT_REG, 26)

    center_text(300, title, f_title, GOLD, stroke=3)
    # divider line
    d.line((240, 470, 560, 470), fill=DIM_GOLD, width=2)
    center_text(500, subtitle, f_sub, (222, 210, 190), stroke=1)
    if footer:
        center_text(720, footer, f_foot, (150, 142, 128))

    bg.save(out_path, "JPEG", quality=92)
    print("wrote", out_path)


shot = os.path.join(ROOT, "map-skill-finder/.artifacts/combat-holdings-clean-tall.png")
shot2 = os.path.join(ROOT, "taiwu-ui-framework/.artifacts/scrollbar-fix-verified.png")

make_cover(
    shot,
    "太吾寻访",
    "功法书 · 技艺书 · 人物 · 商会",
    "按地域查找秘籍持有人与商队目标",
    os.path.join(ROOT, "scratch/cover-xunfang.jpg"),
    dark=0.34, blur=2,
)
make_cover(
    shot2,
    "Taiwu UI Framework",
    "太吾绘卷 · 声明式原生风格 UI 框架",
    "面向 MOD 开发者的前置框架",
    os.path.join(ROOT, "scratch/cover-framework.jpg"),
    dark=0.30, blur=4,
)
