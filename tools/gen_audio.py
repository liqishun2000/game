"""程序化生成占位音频（SFX + 简单循环 BGM）到 MauiApp/Resources/Raw/audio。
仅用 Python 标准库（wave/struct/math/random），16-bit PCM 单声道 22050Hz。
非商用占位，后续可替换为真实素材。"""
import wave, struct, math, random, os

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "MauiApp", "Resources", "Raw", "audio")
os.makedirs(OUT, exist_ok=True)

def write(name, samples):
    path = os.path.join(OUT, name)
    with wave.open(path, "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        frames = bytearray()
        for s in samples:
            v = int(max(-1.0, min(1.0, s)) * 32767)
            frames += struct.pack("<h", v)
        w.writeframes(bytes(frames))
    print("wrote", name, len(samples)/SR, "s")

def env(i, n, atk=0.01, rel=0.2):
    t = i / SR; dur = n / SR
    a = min(1.0, t/atk) if atk > 0 else 1.0
    r = min(1.0, (dur - t)/rel) if rel > 0 else 1.0
    return max(0.0, min(a, r))

def tone(freq, dur, vol=0.5, atk=0.005, rel=0.05, kind="sine"):
    n = int(SR*dur); out = []
    for i in range(n):
        t = i/SR
        if kind == "sine": s = math.sin(2*math.pi*freq*t)
        elif kind == "square": s = 1.0 if math.sin(2*math.pi*freq*t) >= 0 else -1.0
        elif kind == "tri": s = 2/math.pi*math.asin(math.sin(2*math.pi*freq*t))
        else: s = math.sin(2*math.pi*freq*t)
        out.append(s*vol*env(i, n, atk, rel))
    return out

def sweep(f0, f1, dur, vol=0.5, kind="sine"):
    n = int(SR*dur); out = []
    for i in range(n):
        t = i/SR; f = f0 + (f1-f0)*(i/n)
        s = math.sin(2*math.pi*f*t) if kind=="sine" else (1.0 if math.sin(2*math.pi*f*t)>=0 else -1.0)
        out.append(s*vol*env(i, n, 0.005, 0.05))
    return out

def noise(dur, vol=0.5, atk=0.002, rel=0.08, lp=0.0):
    n = int(SR*dur); out = []; prev = 0.0
    for i in range(n):
        r = random.uniform(-1, 1)
        if lp > 0: prev = prev + lp*(r-prev); r = prev
        out.append(r*vol*env(i, n, atk, rel))
    return out

def mix(*tracks):
    n = max(len(t) for t in tracks); out = [0.0]*n
    for t in tracks:
        for i, s in enumerate(t): out[i] += s
    return [max(-1, min(1, s)) for s in out]

def seq(*parts):
    out = []
    for p in parts: out += p
    return out

# ---------- SFX ----------
write("sfx_click.wav", tone(1200, 0.05, 0.4, kind="square", rel=0.04))
write("sfx_confirm.wav", seq(tone(660, 0.07, 0.4, kind="tri"), tone(990, 0.10, 0.4, kind="tri")))
write("sfx_cancel.wav", seq(tone(700, 0.07, 0.35, kind="tri"), tone(420, 0.10, 0.35, kind="tri")))
write("sfx_move.wav", tone(320, 0.06, 0.3, kind="sine", rel=0.05))
write("sfx_hit.wav", mix(noise(0.12, 0.5, lp=0.5), tone(140, 0.12, 0.4, kind="sine")))
write("sfx_arrow.wav", sweep(1500, 600, 0.15, 0.35))
write("sfx_down.wav", mix(noise(0.28, 0.5, lp=0.3), tone(90, 0.28, 0.4, rel=0.2)))
write("sfx_build.wav", seq(noise(0.06, 0.5, lp=0.4), [0.0]*int(SR*0.05), noise(0.06, 0.5, lp=0.4)))
write("sfx_coin.wav", seq(tone(940, 0.06, 0.35, kind="tri"), tone(1320, 0.10, 0.35, kind="tri")))
# 胜利/失败号角
C,E,G,C2 = 392.0, 494.0, 587.0, 784.0
write("sfx_victory.wav", seq(tone(C,0.14,0.45,kind="tri"), tone(E,0.14,0.45,kind="tri"), tone(G,0.14,0.45,kind="tri"), tone(C2,0.30,0.5,kind="tri")))
write("sfx_defeat.wav", seq(tone(440,0.18,0.4,kind="tri"), tone(370,0.18,0.4,kind="tri"), tone(294,0.40,0.45,kind="tri")))

# ---------- BGM（五声音阶循环） ----------
def bgm(notes, beat, dur, bass_freq, vol=0.22, drum=False):
    total = int(SR*dur); out = [0.0]*total
    step = int(SR*beat); idx = 0; ni = 0
    while idx < total:
        f = notes[ni % len(notes)]
        t = tone(f, beat*0.95, vol, atk=0.02, rel=beat*0.4, kind="tri")
        for i, s in enumerate(t):
            if idx+i < total: out[idx+i] += s
        # 低音每两拍
        if ni % 2 == 0:
            b = tone(bass_freq, beat*1.8, vol*0.6, atk=0.02, rel=beat*0.6, kind="sine")
            for i, s in enumerate(b):
                if idx+i < total: out[idx+i] += s
        if drum and ni % 2 == 1:
            d = noise(0.08, vol*0.8, lp=0.3)
            for i, s in enumerate(d):
                if idx+i < total: out[idx+i] += s
        idx += step; ni += 1
    return [max(-1, min(1, s)) for s in out]

penta = [261.63, 293.66, 329.63, 392.00, 440.00, 392.00, 329.63, 293.66]
penta_hi = [392.00, 440.00, 523.25, 587.33, 659.25, 587.33, 523.25, 440.00]
write("bgm_menu.wav", bgm(penta, 0.5, 16.0, 130.81, vol=0.20))
write("bgm_world.wav", bgm(penta, 0.42, 16.0, 146.83, vol=0.20))
write("bgm_battle.wav", bgm(penta_hi, 0.34, 16.0, 130.81, vol=0.20, drum=True))

print("done")
