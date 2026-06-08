"""程序化生成像素贴图到 MauiApp/Resources/Raw/art/。
terrain 16x16、单位 sprite 16x16、武将头像 32x32 PNG RGBA。
仅用 Python 标准库。运行：python tools/gen_art.py"""
import json, os, struct, zlib, hashlib

ROOT = os.path.join(os.path.dirname(__file__), "..", "MauiApp", "Resources", "Raw", "art")
GENERALS = os.path.join(os.path.dirname(__file__), "..", "MauiApp", "Resources", "Raw", "data", "generals.json")

TRANSPARENT = (0, 0, 0, 0)


def write_png(path, w, h, pixels):
    """pixels: list of (r,g,b,a) row-major."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            r, g, b, a = pixels[y * w + x]
            raw.extend((r, g, b, a))
    comp = zlib.compress(bytes(raw), 9)

    def chunk(tag, data):
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", comp) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)
    print("wrote", os.path.relpath(path))


def fill(w, h, color):
    return [color] * (w * h)


def rect(pixels, w, x, y, rw, rh, color):
    for dy in range(rh):
        for dx in range(rw):
            px, py = x + dx, y + dy
            if 0 <= px < w and 0 <= py < len(pixels) // w:
                pixels[py * w + px] = color


def tile_grass(variant=0):
    w = h = 16
    base = (0x3b, 0x4a, 0x2a, 255) if variant == 0 else (0x44, 0x55, 0x30, 255)
    alt = (0x48, 0x58, 0x32, 255)
    px = fill(w, h, base)
    for y in range(h):
        for x in range(w):
            if (x + y + variant) % 3 == 0:
                px[y * w + x] = alt
    rect(px, w, 0, 15, 16, 1, (0x2c, 0x38, 0x20, 255))
    return px


def tile_forest():
    w = h = 16
    px = fill(w, h, (0x2a, 0x4a, 0x28, 255))
    for cx, cy in [(4, 4), (10, 5), (7, 9), (3, 10), (11, 11)]:
        rect(px, w, cx, cy, 3, 4, (0x1a, 0x32, 0x18, 255))
        rect(px, w, cx + 1, cy - 1, 1, 2, (0x3a, 0x6a, 0x30, 255))
    return px


def tile_water():
    w = h = 16
    px = fill(w, h, (0x2a, 0x4a, 0x7a, 255))
    for y in range(0, h, 4):
        for x in range(0, w, 5):
            rect(px, w, x, y, 3, 1, (0x4a, 0x7a, 0xba, 200))
    return px


def tile_mountain():
    w = h = 16
    px = fill(w, h, (0x5a, 0x52, 0x48, 255))
    for i in range(5):
        rect(px, w, 2 + i, 10 - i, 12 - 2 * i, 1, (0x72, 0x6a, 0x5e, 255))
    rect(px, w, 6, 4, 4, 3, (0x90, 0x88, 0x80, 255))
    return px


def tile_road():
    w = h = 16
    px = fill(w, h, (0x3b, 0x4a, 0x2a, 255))
    rect(px, w, 6, 0, 4, 16, (0x6a, 0x5a, 0x3a, 255))
    return px


def tile_fort():
    w = h = 16
    px = fill(w, h, (0x4a, 0x48, 0x42, 255))
    rect(px, w, 3, 6, 10, 8, (0x38, 0x36, 0x32, 255))
    for x in (3, 6, 9, 12):
        rect(px, w, x, 4, 2, 3, (0x58, 0x54, 0x4c, 255))
    return px


def sprite_unit(side_attacker):
    w = h = 16
    px = fill(w, h, TRANSPARENT)
    body = (0x3f, 0x8f, 0x4f, 255) if side_attacker else (0xc0, 0x44, 0x40, 255)
    dark = (0x28, 0x63, 0x36, 255) if side_attacker else (0x86, 0x2c, 0x2a, 255)
    rect(px, w, 5, 7, 6, 6, body)
    rect(px, w, 5, 12, 6, 2, dark)
    rect(px, w, 6, 4, 4, 3, (0xe8, 0xc4, 0x9a, 255))
    rect(px, w, 5, 3, 6, 2, dark)
    spear = (0xd9, 0xd9, 0xd9, 255)
    rect(px, w, 11, 2, 1, 11, spear)
    rect(px, w, 11, 2, 2, 2, (0xff, 0xe6, 0x9a, 255))
    return px


def sprite_general(side_attacker):
    w = h = 16
    px = fill(w, h, TRANSPARENT)
    body = (0x4c, 0xa8, 0xff, 255) if side_attacker else (0xc6, 0x52, 0xc0, 255)
    dark = (0x2f, 0x6f, 0xed, 255) if side_attacker else (0x8a, 0x32, 0x86, 255)
    rect(px, w, 4, 7, 8, 7, body)
    rect(px, w, 4, 13, 8, 2, dark)
    rect(px, w, 6, 5, 4, 3, (0xe8, 0xc4, 0x9a, 255))
    rect(px, w, 5, 3, 6, 2, (0xe8, 0xb9, 0x48, 255))
    flag = (0xe8, 0xb9, 0x48, 255) if side_attacker else (0xff, 0xd1, 0x40, 255)
    rect(px, w, 2, 4, 1, 8, (0x6a, 0x50, 0x30, 255))
    rect(px, w, 3, 4, 4, 3, flag)
    return px


def portrait_general(gen_id, name):
    w = h = 32
    px = fill(w, h, TRANSPARENT)
    hsh = int(hashlib.md5(gen_id.encode()).hexdigest()[:6], 16)
    robe = ((hsh >> 16) & 0x7F) + 80, ((hsh >> 8) & 0x5F) + 60, (hsh & 0x3F) + 100, 255
    skin = (0xe8, 0xc4, 0x9a, 255)
    hair = (0x2a, 0x1e, 0x14, 255)

    # 背景圆角框
    rect(px, w, 2, 2, 28, 28, (0x24, 0x1b, 0x14, 255))
    rect(px, w, 4, 4, 24, 24, (0x38, 0x2e, 0x24, 255))

    # 身体
    rect(px, w, 8, 18, 16, 12, robe)
    rect(px, w, 10, 20, 12, 8, tuple(min(255, c + 20) for c in robe[:3]) + (255,))

    # 脸
    rect(px, w, 11, 10, 10, 9, skin)
    rect(px, w, 10, 9, 12, 3, hair)
    rect(px, w, 12, 13, 2, 2, (0x20, 0x18, 0x10, 255))
    rect(px, w, 18, 13, 2, 2, (0x20, 0x18, 0x10, 255))
    rect(px, w, 14, 16, 4, 1, (0xc0, 0x70, 0x60, 255))

    # 特征
    if "guan" in gen_id or "关羽" in name:
        rect(px, w, 11, 17, 10, 6, (0x8a, 0x30, 0x28, 255))
    elif "liu" in gen_id or "刘备" in name:
        rect(px, w, 10, 8, 12, 2, (0x3a, 0x28, 0x18, 255))
    elif "rebel" in gen_id:
        rect(px, w, 9, 8, 14, 4, (0x4a, 0x38, 0x28, 255))
        rect(px, w, 11, 12, 8, 1, (0x60, 0x20, 0x18, 255))
    elif "strategist" in gen_id:
        rect(px, w, 9, 7, 14, 5, hair)
        rect(px, w, 8, 9, 3, 8, hair)

    # 金边
    for x in range(3, 29):
        px[3 * w + x] = (0xe8, 0xb9, 0x48, 255)
        px[28 * w + x] = (0xe8, 0xb9, 0x48, 255)
    for y in range(3, 29):
        px[y * w + 3] = (0xe8, 0xb9, 0x48, 255)
        px[y * w + 28] = (0xe8, 0xb9, 0x48, 255)

    return px


def main():
    tiles = {
        "tiles/grass_a.png": tile_grass(0),
        "tiles/grass_b.png": tile_grass(1),
        "tiles/forest.png": tile_forest(),
        "tiles/water.png": tile_water(),
        "tiles/mountain.png": tile_mountain(),
        "tiles/road.png": tile_road(),
        "tiles/fort.png": tile_fort(),
    }
    units = {
        "units/soldier_atk.png": sprite_unit(True),
        "units/soldier_def.png": sprite_unit(False),
        "units/general_atk.png": sprite_general(True),
        "units/general_def.png": sprite_general(False),
    }

    for rel, px in {**tiles, **units}.items():
        write_png(os.path.join(ROOT, rel), 16, 16, px)

    with open(GENERALS, encoding="utf-8") as f:
        generals = json.load(f)
    for g in generals:
        gid = g["id"]
        px = portrait_general(gid, g.get("name", gid))
        write_png(os.path.join(ROOT, f"portraits/{gid}.png"), 32, 32, px)

    print("done", len(tiles) + len(units) + len(generals), "images")


if __name__ == "__main__":
    main()
