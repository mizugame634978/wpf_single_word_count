"""Generate VocabApp icon at multiple sizes and bundle into an .ico file."""

from PIL import Image, ImageDraw, ImageFont

JP_FONT = "/usr/share/fonts/truetype/fonts-japanese-gothic.ttf"
EN_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

# Color palette (matches the existing app accents)
BG_TOP = (32, 64, 128)      # #204080 - matches info color in ErrorDialog
BG_BOTTOM = (61, 90, 128)   # #3D5A80 - slightly lighter
FG = (255, 255, 255)
SHADOW = (0, 0, 0, 60)

SIZES = [16, 24, 32, 48, 64, 128, 256]


def make_rounded_square(size: int, radius: int, fill_top, fill_bottom):
    """Create a vertical gradient rounded square."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    # Vertical gradient
    grad = Image.new("RGB", (1, size), color=0)
    for y in range(size):
        ratio = y / max(1, size - 1)
        r = int(fill_top[0] * (1 - ratio) + fill_bottom[0] * ratio)
        g = int(fill_top[1] * (1 - ratio) + fill_bottom[1] * ratio)
        b = int(fill_top[2] * (1 - ratio) + fill_bottom[2] * ratio)
        grad.putpixel((0, y), (r, g, b))
    grad = grad.resize((size, size))

    # Rounded mask
    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    img.paste(grad, (0, 0), mask)
    return img


def draw_text_centered(img, text, font, fill, dy=0, dx=0):
    draw = ImageDraw.Draw(img)
    bbox = draw.textbbox((0, 0), text, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    x = (img.width - tw) / 2 - bbox[0] + dx
    y = (img.height - th) / 2 - bbox[1] + dy
    draw.text((x, y), text, font=font, fill=fill)


def render_icon(size: int) -> Image.Image:
    radius = max(2, size // 8)
    img = make_rounded_square(size, radius, BG_TOP, BG_BOTTOM)

    # For very small icons, use a single character so it stays readable.
    if size <= 24:
        font = ImageFont.truetype(EN_FONT, int(size * 0.65))
        draw_text_centered(img, "V", font, FG, dy=-int(size * 0.02))
        return img

    # Larger: "A" with あ next to it, slightly overlapped.
    en_font = ImageFont.truetype(EN_FONT, int(size * 0.55))
    jp_font = ImageFont.truetype(JP_FONT, int(size * 0.5))
    draw = ImageDraw.Draw(img)

    # Compute combined width
    en_bb = draw.textbbox((0, 0), "A", font=en_font)
    jp_bb = draw.textbbox((0, 0), "あ", font=jp_font)
    en_w = en_bb[2] - en_bb[0]
    jp_w = jp_bb[2] - jp_bb[0]
    gap = max(2, size // 20)
    total_w = en_w + gap + jp_w
    start_x = (img.width - total_w) // 2

    # Vertical baseline alignment
    en_y = (img.height - (en_bb[3] - en_bb[1])) // 2 - en_bb[1] - int(size * 0.02)
    jp_y = (img.height - (jp_bb[3] - jp_bb[1])) // 2 - jp_bb[1] + int(size * 0.01)

    draw.text((start_x - en_bb[0], en_y), "A", font=en_font, fill=FG)
    draw.text((start_x + en_w + gap - jp_bb[0], jp_y), "あ", font=jp_font, fill=FG)

    return img


def main():
    images = [render_icon(s) for s in SIZES]
    out_ico = "src/VocabApp.Wpf/Resources/app.ico"
    images[-1].save(
        out_ico,
        format="ICO",
        sizes=[(s, s) for s in SIZES],
        append_images=images[:-1],
    )
    images[-1].save("src/VocabApp.Wpf/Resources/app.png", format="PNG")
    print(f"Generated {out_ico} with sizes {SIZES}")


if __name__ == "__main__":
    main()
