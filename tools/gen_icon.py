"""Generate VocabApp icon: stacked flashcards + English/Japanese + checkmark."""

from PIL import Image, ImageDraw, ImageFont

JP_FONT = "/usr/share/fonts/truetype/fonts-japanese-gothic.ttf"
EN_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

# Background
BG_TOP = (32, 64, 128)        # #204080
BG_BOTTOM = (61, 90, 128)     # #3D5A80

# Cards
CARD_FILL = (255, 255, 255)
CARD_SHADOW = (0, 0, 0, 110)
CARD_BORDER = (220, 220, 220)

# Text
TEXT_DARK = (40, 40, 40)
DIVIDER = (200, 200, 200)
CHECK_GREEN = (46, 125, 50)   # #2E7D32 — same green used for mastery

SIZES = [16, 24, 32, 48, 64, 128, 256]


def make_rounded_bg(size: int, radius: int):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    grad = Image.new("RGB", (1, size), color=0)
    for y in range(size):
        ratio = y / max(1, size - 1)
        r = int(BG_TOP[0] * (1 - ratio) + BG_BOTTOM[0] * ratio)
        g = int(BG_TOP[1] * (1 - ratio) + BG_BOTTOM[1] * ratio)
        b = int(BG_TOP[2] * (1 - ratio) + BG_BOTTOM[2] * ratio)
        grad.putpixel((0, y), (r, g, b))
    grad = grad.resize((size, size))
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    img.paste(grad, (0, 0), mask)
    return img


def draw_text_in_box(draw, text, font, box, fill):
    """Center text within the given box (l, t, r, b)."""
    bb = draw.textbbox((0, 0), text, font=font)
    tw = bb[2] - bb[0]
    th = bb[3] - bb[1]
    x = box[0] + (box[2] - box[0] - tw) / 2 - bb[0]
    y = box[1] + (box[3] - box[1] - th) / 2 - bb[1]
    draw.text((x, y), text, font=font, fill=fill)


def render_small(size: int) -> Image.Image:
    """16-24px: simplified single card with 'A/あ' compressed."""
    radius = max(2, size // 8)
    img = make_rounded_bg(size, radius)
    draw = ImageDraw.Draw(img)

    pad = max(2, size // 8)
    card_box = (pad, pad, size - pad - 1, size - pad - 1)
    draw.rounded_rectangle(card_box, radius=max(1, size // 16), fill=CARD_FILL)

    # Single line with both characters
    inner_w = card_box[2] - card_box[0]
    en_font = ImageFont.truetype(EN_FONT, max(6, int(size * 0.45)))
    jp_font = ImageFont.truetype(JP_FONT, max(6, int(size * 0.45)))
    en_bb = draw.textbbox((0, 0), "A", font=en_font)
    jp_bb = draw.textbbox((0, 0), "あ", font=jp_font)
    en_w = en_bb[2] - en_bb[0]
    jp_w = jp_bb[2] - jp_bb[0]
    gap = max(1, size // 16)
    total = en_w + gap + jp_w
    sx = card_box[0] + (inner_w - total) // 2
    cy = (card_box[1] + card_box[3]) // 2
    draw.text((sx - en_bb[0], cy - (en_bb[3] - en_bb[1]) // 2 - en_bb[1]),
              "A", font=en_font, fill=TEXT_DARK)
    draw.text((sx + en_w + gap - jp_bb[0], cy - (jp_bb[3] - jp_bb[1]) // 2 - jp_bb[1]),
              "あ", font=jp_font, fill=TEXT_DARK)
    return img


def render_large(size: int) -> Image.Image:
    """32+: stacked flashcards with A/あ + checkmark."""
    radius = max(4, size // 8)
    img = make_rounded_bg(size, radius)
    draw = ImageDraw.Draw(img)

    pad = max(4, int(size * 0.12))
    card_w = int(size * 0.68)
    card_h = int(size * 0.72)
    card_radius = max(2, int(size * 0.05))

    front_l = (size - card_w) // 2
    front_t = (size - card_h) // 2 + int(size * 0.02)
    back_offset = max(2, int(size * 0.05))

    # Back card (slightly behind & lower-right)
    back_box = (front_l + back_offset, front_t + back_offset,
                front_l + back_offset + card_w - 1, front_t + back_offset + card_h - 1)
    draw.rounded_rectangle(back_box, radius=card_radius,
                           fill=(245, 245, 245), outline=CARD_BORDER, width=max(1, size // 80))

    # Front card
    front_box = (front_l, front_t, front_l + card_w - 1, front_t + card_h - 1)
    # Shadow
    shadow_box = (front_box[0] + max(1, size // 80), front_box[1] + max(1, size // 60),
                  front_box[2] + max(1, size // 80), front_box[3] + max(1, size // 60))
    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(shadow_box, radius=card_radius, fill=(0, 0, 0, 70))
    shadow_blur = shadow.filter(__import__("PIL.ImageFilter", fromlist=["GaussianBlur"]).GaussianBlur(radius=max(1, size // 40)))
    img.alpha_composite(shadow_blur)

    draw = ImageDraw.Draw(img)
    draw.rounded_rectangle(front_box, radius=card_radius,
                           fill=CARD_FILL, outline=CARD_BORDER, width=max(1, size // 80))

    # Divider line in middle
    mid_y = (front_box[1] + front_box[3]) // 2
    inset = max(3, int(card_w * 0.12))
    draw.line((front_box[0] + inset, mid_y, front_box[2] - inset, mid_y),
              fill=DIVIDER, width=max(1, size // 100))

    # Top half: A
    top_box = (front_box[0], front_box[1], front_box[2], mid_y)
    en_font = ImageFont.truetype(EN_FONT, int(card_h * 0.38))
    draw_text_in_box(draw, "A", en_font, top_box, TEXT_DARK)

    # Bottom half: あ
    bot_box = (front_box[0], mid_y, front_box[2], front_box[3])
    jp_font = ImageFont.truetype(JP_FONT, int(card_h * 0.36))
    draw_text_in_box(draw, "あ", jp_font, bot_box, TEXT_DARK)

    # Checkmark badge in bottom-right corner (overlapping card)
    if size >= 48:
        badge_r = max(6, int(size * 0.13))
        bcx = front_box[2] - int(badge_r * 0.3)
        bcy = front_box[3] - int(badge_r * 0.3)
        # White ring (so it doesn't blend with the BG)
        ring = max(1, size // 60)
        draw.ellipse((bcx - badge_r - ring, bcy - badge_r - ring,
                      bcx + badge_r + ring, bcy + badge_r + ring), fill=CARD_FILL)
        draw.ellipse((bcx - badge_r, bcy - badge_r, bcx + badge_r, bcy + badge_r),
                     fill=CHECK_GREEN)
        # checkmark stroke
        cw = max(1, int(badge_r * 0.45))
        p1 = (bcx - badge_r * 0.45, bcy + badge_r * 0.05)
        p2 = (bcx - badge_r * 0.05, bcy + badge_r * 0.40)
        p3 = (bcx + badge_r * 0.55, bcy - badge_r * 0.30)
        draw.line([p1, p2], fill=CARD_FILL, width=cw)
        draw.line([p2, p3], fill=CARD_FILL, width=cw)

    return img


def render_icon(size: int) -> Image.Image:
    return render_small(size) if size <= 24 else render_large(size)


def main():
    images = [render_icon(s) for s in SIZES]
    out_ico = "src/VocabApp.Wpf/Resources/app.ico"
    images[-1].save(out_ico, format="ICO",
                    sizes=[(s, s) for s in SIZES],
                    append_images=images[:-1])
    images[-1].save("src/VocabApp.Wpf/Resources/app.png", format="PNG")
    print(f"Generated {out_ico} with sizes {SIZES}")


if __name__ == "__main__":
    main()
