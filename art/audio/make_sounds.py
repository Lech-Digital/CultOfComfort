#!/usr/bin/env python3
"""Cult of Comfort sound kit, synthesised (no samples, no downloads): python3 art/audio/make_sounds.py

Every sound is made from sine/noise/plucked-string (Karplus-Strong) maths and a fixed random seed, so the files are
reproducible. Output: client/CultOfComfort.Unity/Assets/Resources/Audio/<name>.wav (mono, 22 050 Hz, 16-bit).
Muddy Satire in sound: wooden clonks and a lute for the medieval, crisp electronic bleeps for the 2024 relics.
"""
import os
import wave

import numpy as np

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "client", "CultOfComfort.Unity", "Assets", "Resources", "Audio")
rng = np.random.default_rng(947)


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(n, attack=0.005, release=None, curve=6.0):
    """Fast attack, exponential decay over n samples (release: seconds of linear fade at the end)."""
    a = max(1, int(RATE * attack))
    e = np.exp(-curve * np.linspace(0, 1, n))
    e[:a] *= np.linspace(0, 1, a)
    if release:
        r = min(n, int(RATE * release))
        e[-r:] *= np.linspace(1, 0, r)
    return e


def sine(freq, seconds, phase=0.0):
    x = t(seconds)
    f = np.broadcast_to(freq, x.shape) if np.ndim(freq) else np.full(x.shape, freq)
    return np.sin(2 * np.pi * np.cumsum(f) / RATE + phase)


def noise(seconds):
    return rng.uniform(-1, 1, int(RATE * seconds))


def lowpass(x, cutoff):
    """One-pole low-pass; cutoff may be an array (a sweep)."""
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    alpha = 1 - np.exp(-2 * np.pi * c / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def pluck(freq, seconds, brightness=0.5, decay=0.996):
    """Karplus-Strong plucked string: the lute."""
    n = int(RATE * seconds)
    period = max(2, int(RATE / freq))
    buf = lowpass(rng.uniform(-1, 1, period), 2000 + 6000 * brightness)
    out = np.empty(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = decay * 0.5 * (buf[i % period] + buf[(i + 1) % period])
    return out * env(n, attack=0.001, curve=2.0)


def mix(*parts):
    """Lays (offset seconds, signal) pairs onto one track."""
    length = max(int(RATE * off) + len(sig) for off, sig in parts)
    out = np.zeros(length)
    for off, sig in parts:
        s = int(RATE * off)
        out[s:s + len(sig)] += sig
    return out


def note(name):
    """'A3' → Hz."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[pitch] + 12 * (octave - 4)) / 12)


def save(name, x, gain=0.8):
    peak = np.max(np.abs(x)) or 1.0
    x = np.clip(x / peak * gain, -1, 1)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    print(f"[audio] {name}: {len(x) / RATE:.2f}s")


def wood(freq=320, seconds=0.12):
    """A wooden clonk: a damped low sine plus a click of noise."""
    n = int(RATE * seconds)
    body = sine(freq, seconds) * env(n, curve=22) + 0.5 * sine(freq * 2.7, seconds) * env(n, curve=40)
    click = lowpass(noise(seconds), 3000) * env(n, curve=80)
    return body + 0.6 * click


def bell(freq, seconds=0.8):
    n = int(RATE * seconds)
    return sum(a * sine(freq * r, seconds) * env(n, curve=c) for a, r, c in ((1, 1, 5), (0.5, 2.76, 8), (0.25, 5.4, 12)))


def build_ui():
    save("tap", wood(520, 0.07), 0.5)
    save("error", wood(150, 0.25) + 0.4 * wood(110, 0.25), 0.6)
    hammer = mix((0, wood(260)), (0.16, wood(300)), (0.32, wood(240, 0.18)))
    save("place", mix((0, lowpass(noise(0.25), 500) * env(int(RATE * 0.25), curve=10)), (0.05, hammer)), 0.7)
    save("build_done", mix(*[(i * 0.07, pluck(note(n), 0.8, 0.7)) for i, n in enumerate(("D4", "F#4", "A4", "D5"))]), 0.7)
    coins = mix(*[(i * 0.06 + rng.uniform(0, 0.02), bell(rng.uniform(1800, 2600), 0.35)) for i in range(5)])
    save("collect", coins, 0.55)
    save("claim", mix(*[(i * 0.09, pluck(note(n), 1.2, 0.8)) for i, n in enumerate(("G3", "B3", "D4", "G4"))], (0.36, bell(note("G5"), 1.2))), 0.75)
    # Upgrade finished: a boing (pitch-bent sine) and a poof.
    x = t(0.5)
    boing = sine(180 + 260 * np.exp(-x * 8) * (1 + 0.3 * np.sin(x * 60)), 0.5) * env(len(x), curve=5)
    poof = lowpass(noise(0.5), np.linspace(2500, 300, len(x))) * env(len(x), attack=0.03, curve=7)
    save("upgrade_pop", boing + 0.7 * poof, 0.7)


def build_night():
    # The lamps: a soft gong.
    save("lamps", bell(note("A2"), 2.5) + 0.3 * bell(note("E3"), 2.5), 0.7)
    # A wave: a wonky medieval horn (saw-ish with vibrato), two notes.
    def horn(freq, seconds):
        x = t(seconds)
        vib = freq * (1 + 0.012 * np.sin(2 * np.pi * 5.5 * x))
        tone = sum(sine(vib * k, seconds) / k for k in range(1, 7))
        return lowpass(tone, 1600) * env(len(x), attack=0.06, release=0.15, curve=0.8)
    save("wave_horn", mix((0, horn(note("D3"), 0.45)), (0.5, horn(note("A3"), 0.9))), 0.7)
    x = t(0.35)
    save("hearth_hit", sine(70 * np.exp(-x * 3), 0.35) * env(len(x), curve=7) + 0.4 * lowpass(noise(0.35), 400) * env(len(x), curve=12), 0.8)
    x = t(0.18)
    save("enemy_down", sine(600 * np.exp(-x * 9), 0.18) * env(len(x), curve=8) + 0.3 * wood(200, 0.18), 0.6)
    save("prophet_hit", lowpass(noise(0.12), 1800) * env(int(RATE * 0.12), curve=25) + 0.6 * wood(180, 0.12), 0.6)
    # Swoon: a slide whistle all the way down.
    x = t(1.1)
    save("swoon", sine(1400 * np.exp(-x * 1.6), 1.1) * env(len(x), attack=0.02, release=0.2, curve=0.5), 0.55)
    x = t(0.22)
    save("swish", highpass(noise(0.22), np.linspace(300, 3000, len(x))) * np.sin(np.pi * np.linspace(0, 1, len(x))) ** 2, 0.45)
    x = t(1.6)
    splash = lowpass(noise(1.6), 300 + 2500 * np.exp(-x * 2)) * env(len(x), attack=0.05, curve=2.5)
    save("sluice", splash + 0.3 * lowpass(noise(1.6), 6000) * env(len(x), attack=0.2, curve=4), 0.8)
    # The Voice from the Box: speaker feedback squeal, then a wub.
    x = t(0.9)
    squeal = sine(2200 + 300 * np.sin(x * 20), 0.9) * np.clip((x - 0.0) * 8, 0, 1) * np.exp(-x * 3)
    wub = sine(110, 0.9) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 6 * x))) * env(len(x), attack=0.02, curve=2)
    save("voice", 0.4 * squeal + wub, 0.6)
    x = t(0.9)
    save("stones", lowpass(noise(0.9), 5000) * (0.4 + 0.6 * (rng.uniform(0, 1, len(x)) > 0.97)) * env(len(x), attack=0.01, curve=3), 0.5)
    # Relics: crisp 2024 electronics.
    x = t(0.28)
    save("laser", sine(2400 * np.exp(-x * 9) + 300, 0.28) * env(len(x), curve=6), 0.5)
    ratchet = mix(*[(i * 0.07, lowpass(noise(0.025), 4000) * env(int(RATE * 0.025), curve=30)) for i in range(10)])
    x = t(len(ratchet) / RATE)
    save("torch", ratchet + 0.25 * sine(140 + 40 * np.minimum(1, x * 2), len(ratchet) / RATE) * np.minimum(1, x * 3), 0.6)
    x = t(1.4)
    whoosh = lowpass(noise(1.4), 200 + 3000 * np.minimum(1, x / 0.8)) * np.sin(np.pi * np.minimum(1, x / 1.4)) ** 0.5
    crackle = (rng.uniform(0, 1, len(x)) > 0.985) * rng.uniform(-1, 1, len(x)) * np.minimum(1, x * 2)
    save("sunfire", whoosh + 0.8 * crackle, 0.6)
    save("victory", mix(*[(i * 0.14, pluck(note(n), 1.4, 0.8)) for i, n in enumerate(("D4", "F#4", "A4", "D5", "A4", "D5"))], (0.84, bell(note("D6"), 1.5))), 0.75)
    # Defeat: the sad trombone. Wah, wah, wah, waaaah.
    def trombone(f0, f1, seconds):
        x = t(seconds)
        f = np.linspace(f0, f1, len(x)) * (1 + (0.02 * np.sin(2 * np.pi * 6 * x) if seconds > 0.6 else 0))
        tone = sum(sine(f * k, seconds) / k ** 1.3 for k in range(1, 8))
        return lowpass(tone, 900) * env(len(x), attack=0.03, release=0.08, curve=0.6)
    save("defeat", mix((0, trombone(note("D#3"), note("D3"), 0.42)), (0.48, trombone(note("D3"), note("C#3"), 0.42)),
                       (0.96, trombone(note("C#3"), note("C3"), 0.42)), (1.44, trombone(note("C3"), note("B2"), 1.3))), 0.7)


def build_music():
    # Day: a lute in D dorian over a drone, 96 bpm, 16 bars (a seamless loop).
    beat = 60 / 96
    tune = ["D4", "F4", "A4", "G4", "F4", "E4", "D4", "C4", "D4", "E4", "F4", "A4", "G4", "E4", "C4", "E4",
            "D4", "A4", "C5", "B4", "A4", "G4", "F4", "E4", "F4", "G4", "A4", "G4", "F4", "E4", "D4", "D4"]
    bars = 16
    length = bars * 4 * beat
    parts = []
    for i, n in enumerate(tune * 2):
        at = i * beat
        if at < length:
            parts.append((at, 0.5 * pluck(note(n), beat * 2.2, 0.6, 0.995)))
            if i % 2 == 0:
                parts.append((at + beat / 2, 0.18 * pluck(note(n) * 2, beat * 1.2, 0.9, 0.99)))
    for bar in range(bars):
        root = "D2" if bar % 4 != 3 else "C2"
        parts.append((bar * 4 * beat, 0.45 * pluck(note(root), 4 * beat, 0.3, 0.998)))
        parts.append((bar * 4 * beat + 2 * beat, 0.3 * pluck(note(root) * 1.5, 2 * beat, 0.3, 0.997)))
    day = mix(*parts)[:int(RATE * length)]
    tail = int(RATE * 0.05)
    day[:tail] *= np.linspace(0, 1, tail)
    day[-tail:] *= np.linspace(1, 0, tail)
    save("music_day", day, 0.55)

    # Night: a frame drum and a low drone, 84 bpm, 8 bars, minor and uneasy.
    beat = 60 / 84
    length = 8 * 4 * beat
    parts = []
    for b in range(32):
        hit = wood(85, 0.4) if b % 4 in (0, 2) else 0.5 * wood(140, 0.2)
        parts.append((b * beat, 0.8 * lowpass(hit, 900)))
        if b % 8 == 7:
            parts.append((b * beat + beat / 2, 0.4 * wood(140, 0.2)))
    x = t(length)
    drone = (sine(note("D2"), length) + 0.5 * sine(note("A2") * (1 + 0.003 * np.sin(x)), length) + 0.25 * sine(note("F2"), length))
    parts.append((0, 0.22 * lowpass(drone, 500)))
    for i, n in enumerate(("A3", "C4", "D4", "F4", "E4", "C4", "D4", "A3")):
        parts.append((i * 4 * beat + 2 * beat, 0.25 * pluck(note(n), 3 * beat, 0.35, 0.997)))
    night = mix(*parts)[:int(RATE * length)]
    tail = int(RATE * 0.05)
    night[:tail] *= np.linspace(0, 1, tail)
    night[-tail:] *= np.linspace(1, 0, tail)
    save("music_night", night, 0.55)


if __name__ == "__main__":
    build_ui()
    build_night()
    build_music()
