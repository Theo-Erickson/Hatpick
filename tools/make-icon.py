# Draws gitpicker.ico: a white branch graph on a green badge.
#   python tools/make-icon.py gitpicker.ico gitpicker.png
# Needs Pillow 10.1+ (pip install pillow).
import sys
from PIL import Image, ImageDraw

def draw(S=1024):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    k = S / 256
    # badge with a vertical gradient
    top, bot = (64, 150, 110), (30, 100, 72)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).ellipse((4*k, 4*k, 252*k, 252*k), fill=255)
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        gd.line((0, y, S, y), fill=tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)) + (255,))
    img.paste(grad, (0, 0), mask)

    white = (255, 255, 255, 240)
    w = int(14 * k)
    # trunk and a branch curving off to the right
    d.line((96*k, 64*k, 96*k, 192*k), fill=white, width=w)
    d.arc((28*k, 24*k, 164*k, 160*k), start=0, end=90, fill=white, width=w)
    d.line((164*k, 72*k, 164*k, 93*k), fill=white, width=w)
    r = 20 * k
    for (x, y) in ((96, 64), (96, 192), (164, 72)):
        d.ellipse((x*k - r, y*k - r, x*k + r, y*k + r), fill=white)
        d.ellipse((x*k - r*0.45, y*k - r*0.45, x*k + r*0.45, y*k + r*0.45), fill=(40, 120, 88, 255))
    return img

master = draw().resize((256, 256), Image.LANCZOS)
master.save(sys.argv[2])
# BMP frames load everywhere, including .NET Framework's Icon class.
master.save(sys.argv[1], sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (256, 256)], bitmap_format="bmp")
print("wrote", sys.argv[1])
