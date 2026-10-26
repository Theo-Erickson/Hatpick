# Draws hatpick.ico.
#   python tools/make-icon.py hatpick.ico hatpick.png
# Needs Pillow 10.1+ (pip install pillow).
#
# Large sizes (64px and up) show the "hat trick": a branch splitting three ways,
# with a hard hat (work), a top hat and a beanie (personal) on the tips.
# Small sizes (48px and down, which covers the Start menu and taskbar) show just
# the two-way fork with the hard hat and the beanie, so each hat stays readable.
import sys
from PIL import Image, ImageDraw, ImageFilter

S = 1024            # draw big, then downscale
K = S / 256.0
BADGE_TOP, BADGE_BOTTOM = (60, 70, 104), (34, 40, 66)
INK = (255, 255, 255, 255)
HOLE = (44, 52, 82, 255)
YELLOW = (250, 194, 38, 255)
RED = (214, 64, 64, 255)
CHARCOAL = (40, 40, 50, 255)
ORANGE = (236, 122, 48, 255)
RIM = (24, 28, 46, 255)          # dark outline that separates hats from the white branch

def P(x, y): return (x * K, y * K)

def shade(c, f): return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (255,)

def bezier(p0, p1, p2, p3, n=60):
    pts = []
    for i in range(n + 1):
        t = i / n; u = 1 - t
        pts.append((u**3*p0[0] + 3*u*u*t*p1[0] + 3*u*t*t*p2[0] + t**3*p3[0],
                    u**3*p0[1] + 3*u*u*t*p1[1] + 3*u*t*t*p2[1] + t**3*p3[1]))
    return pts

def stroke(d, pts, w, fill=INK):
    pts = [P(*p) for p in pts]
    r = w * K / 2
    d.line(pts, fill=fill, width=int(w * K))
    for x, y in pts:   # round joins and caps
        d.ellipse((x - r, y - r, x + r, y + r), fill=fill)

def dot(d, c, r, fill, hole=None, hr=0):
    x, y = P(*c); R = r * K
    d.ellipse((x - R, y - R, x + R, y + R), fill=fill)
    if hole:
        h = hr * K
        d.ellipse((x - h, y - h, x + h, y + h), fill=hole)

# ---------- hats (side view, brim/bottom edge at y=base, w = width in 256 units) ----------

def hard_hat(d, cx, base, w, color=YELLOW):
    h = w * 0.62
    d.chord((*P(cx - w / 2, base - h), *P(cx + w / 2, base + h)), 180, 360, fill=color)
    d.chord((*P(cx - w * 0.2, base - h - w * 0.07), *P(cx + w * 0.2, base - h + w * 0.14)), 180, 360, fill=shade(color, 1.1))
    d.arc((*P(cx - w * 0.36, base - h * 0.86), *P(cx + w * 0.36, base + h * 0.7)), 200, 250, fill=shade(color, 1.15), width=int(3 * K))
    d.rounded_rectangle((*P(cx - w * 0.62, base - w * 0.06), *P(cx + w * 0.7, base + w * 0.08)),
                        radius=int(w * 0.06 * K), fill=shade(color, 0.88))

def top_hat(d, cx, base, w, h, body=CHARCOAL, band=ORANGE):
    top_w = w * 1.08
    d.polygon([P(cx - w / 2, base), P(cx - top_w / 2, base - h), P(cx + top_w / 2, base - h), P(cx + w / 2, base)], fill=body)
    d.ellipse((*P(cx - top_w / 2, base - h - w * 0.12), *P(cx + top_w / 2, base - h + w * 0.12)), fill=shade(body, 1.7))
    d.rectangle((*P(cx - w / 2 - 0.5, base - h * 0.26), *P(cx + w / 2 + 0.5, base - 1)), fill=band)
    d.ellipse((*P(cx - w * 0.78, base - w * 0.13), *P(cx + w * 0.78, base + w * 0.13)), fill=body)

def beanie(d, cx, base, w, color=RED):
    h = w * 0.66
    cuff = w * 0.26
    # knit dome
    d.chord((*P(cx - w / 2, base - cuff - h), *P(cx + w / 2, base - cuff + h)), 180, 360, fill=color)
    # a couple of knit ribs on the dome
    for f in (-0.22, 0.0, 0.22):
        x = cx + w * f
        d.line((*P(x, base - cuff - h * (0.9 - abs(f))), *P(x, base - cuff)), fill=shade(color, 0.84), width=int(max(1.5, w * 0.04) * K))
    # folded cuff, slightly wider than the dome
    d.rounded_rectangle((*P(cx - w * 0.54, base - cuff), *P(cx + w * 0.54, base)), radius=int(w * 0.08 * K), fill=shade(color, 0.8))
    # pom-pom
    dot(d, (cx, base - cuff - h - w * 0.08), w * 0.17, (245, 240, 232, 255))

def outlined(draw_fn):
    """Draw a hat on its own layer with a dark rim so it separates from the white branch."""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(layer))
    rim = layer.split()[3].filter(ImageFilter.MaxFilter(int(3 * K) | 1))
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (S, S), RIM), (0, 0), rim)
    out.alpha_composite(layer)
    return out

def badge():
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).ellipse((*P(4, 4), *P(252, 252)), fill=255)
    grad = Image.new("RGBA", (S, S)); gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        gd.line((0, y, S, y), fill=tuple(int(BADGE_TOP[i] + (BADGE_BOTTOM[i] - BADGE_TOP[i]) * t) for i in range(3)) + (255,))
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img.paste(grad, (0, 0), mask)
    return img

# ---------- the two layouts ----------

def hat_trick():
    img = badge(); d = ImageDraw.Draw(img)
    root = (128, 222)
    tips = [(62, 152), (128, 138), (194, 152)]
    stroke(d, [root, tips[1]], 14)
    stroke(d, bezier((128, 204), (128, 178), (62, 186), (62, 160)), 14)
    stroke(d, bezier((128, 204), (128, 178), (194, 186), (194, 160)), 14)
    for c in tips + [root]:
        dot(d, c, 16, INK, HOLE, 6)
    img.alpha_composite(outlined(lambda d: hard_hat(d, 60, tips[0][1] - 13, 72)))
    img.alpha_composite(outlined(lambda d: top_hat(d, 128, tips[1][1] - 13, 52, 70)))
    img.alpha_composite(outlined(lambda d: beanie(d, 196, tips[2][1] - 13, 64)))
    return img

def fork():
    img = badge(); d = ImageDraw.Draw(img)
    root = (128, 214)
    tips = [(76, 136), (180, 136)]
    stroke(d, bezier((128, 196), (128, 168), (76, 172), (76, 144)), 17)
    stroke(d, bezier((128, 196), (128, 168), (180, 172), (180, 144)), 17)
    stroke(d, [root, (128, 194)], 17)
    for c in tips + [root]:
        dot(d, c, 20, INK, HOLE, 8)
    img.alpha_composite(outlined(lambda d: hard_hat(d, 74, tips[0][1] - 17, 82)))
    img.alpha_composite(outlined(lambda d: beanie(d, 182, tips[1][1] - 17, 80)))
    return img

SIZES = [16, 24, 32, 48, 64, 256]

def frames():
    big, small = hat_trick(), fork()
    return {s: (big if s >= 64 else small).resize((s, s), Image.LANCZOS) for s in SIZES}

if __name__ == "__main__":
    f = frames()
    f[256].save(sys.argv[2])
    # BMP frames load everywhere, including .NET Framework's Icon class.
    f[256].save(sys.argv[1], format="ICO", sizes=[(s, s) for s in SIZES],
                append_images=[f[s] for s in SIZES if s != 256], bitmap_format="bmp")
    print("wrote", sys.argv[1])
