# python generate_audio.py [out_dir] [names...]
# sfx mono, music stereo, 16-bit 44.1k. seeds are fixed, output is stable
import math
import os
import random
import struct
import sys
import wave
from array import array

SR = 44100
TAU = 2.0 * math.pi



def buf(sec):
    return array('d', bytes(8 * int(sec * SR)))


def add(dst, src, at=0.0, gain=1.0):
    o = int(at * SR)
    n = min(len(src), len(dst) - o)
    for i in range(max(n, 0)):
        dst[o + i] += src[i] * gain
    return dst


def tone(freq, sec, partials=((1.0, 1.0, 1.0),), attack=0.003, decay=0.5, glide=None):
    # partials: (ratio, amp, decay_scale), glide: (f_end, seconds)
    out = buf(sec)
    n = len(out)
    att = max(int(attack * SR), 1)
    for ratio, amp, dscale in partials:
        phase = 0.0
        d = max(decay * dscale, 1e-4)
        for i in range(n):
            t = i / SR
            f = freq
            if glide:
                k = min(t / glide[1], 1.0)
                f = freq * (glide[0] / freq) ** k
            phase += TAU * f * ratio / SR
            env = math.exp(-t / d) * (min(i / att, 1.0))
            out[i] += math.sin(phase) * amp * env
    return out


GLASS = ((1.0, 1.0, 1.0), (2.32, 0.45, 0.55), (4.25, 0.22, 0.32), (6.63, 0.1, 0.2))
BELL = ((1.0, 1.0, 1.0), (2.0, 0.35, 0.7), (3.01, 0.18, 0.45), (4.17, 0.1, 0.3))
KALIMBA = ((1.0, 1.0, 1.0), (5.9, 0.12, 0.12), (2.0, 0.08, 0.5))


def noise(sec, seed=1):
    r = random.Random(seed)
    out = buf(sec)
    for i in range(len(out)):
        out[i] = r.uniform(-1.0, 1.0)
    return out


def lowpass(x, fc):
    a = 1.0 - math.exp(-TAU * fc / SR)
    y = 0.0
    out = array('d', x)
    for i in range(len(x)):
        y += a * (x[i] - y)
        out[i] = y
    return out


def highpass(x, fc):
    lp = lowpass(x, fc)
    return array('d', (x[i] - lp[i] for i in range(len(x))))


def bandpass_sweep(x, f_start, f_end, q=2.0, curve=1.0):
    out = array('d', x)
    n = len(x)
    x1 = x2 = y1 = y2 = 0.0
    b0 = b2 = a1 = a2 = 0.0
    for i in range(n):
        if i % 32 == 0:
            k = (i / max(n - 1, 1)) ** curve
            f = f_start * (f_end / f_start) ** k
            w0 = TAU * f / SR
            alpha = math.sin(w0) / (2.0 * q)
            a0 = 1.0 + alpha
            b0 = alpha / a0
            b2 = -alpha / a0
            a1 = -2.0 * math.cos(w0) / a0
            a2 = (1.0 - alpha) / a0
        xi = x[i]
        yi = b0 * xi + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, xi
        y2, y1 = y1, yi
        out[i] = yi
    return out


def envelope(x, attack, release, total=None):
    n = len(x)
    a = max(int(attack * SR), 1)
    r = max(int(release * SR), 1)
    out = array('d', x)
    for i in range(n):
        g = min(i / a, 1.0)
        rem = n - i
        if rem < r:
            g *= rem / r
        out[i] *= g
    return out


def exp_env(x, decay):
    out = array('d', x)
    for i in range(len(x)):
        out[i] *= math.exp(-(i / SR) / decay)
    return out


def reverb(x, mix=0.25, room=0.82, damp=0.35, tail=1.2, spread=0):
    # cut-down freeverb
    combs = [1116, 1188, 1277, 1356]
    alls = [556, 441]
    combs = [c + spread for c in combs]
    alls = [a + spread // 2 for a in alls]
    n = len(x) + int(tail * SR)
    src = array('d', x) + array('d', bytes(8 * (n - len(x))))
    wet = array('d', bytes(8 * n))
    for L in combs:
        line = [0.0] * L
        idx = 0
        store = 0.0
        for i in range(n):
            o = line[idx]
            store = o * (1.0 - damp) + store * damp
            line[idx] = src[i] * 0.015 + store * room
            idx += 1
            if idx == L:
                idx = 0
            wet[i] += o
    for L in alls:
        line = [0.0] * L
        idx = 0
        for i in range(n):
            b = line[idx]
            inp = wet[i]
            wet[i] = -inp + b
            line[idx] = inp + b * 0.5
            idx += 1
            if idx == L:
                idx = 0
    return array('d', (src[i] * (1.0 - mix) + wet[i] * mix * 3.0 for i in range(n)))


def normalize(x, peak_db=-1.0):
    m = max(max(x), -min(x), 1e-9)
    g = 10 ** (peak_db / 20.0) / m
    return array('d', (v * g for v in x))


def fade(x, fin=0.002, fout=0.01):
    return envelope(x, fin, fout)


def write_wav(path, chans):
    n = min(len(c) for c in chans)
    with wave.open(path, 'wb') as w:
        w.setnchannels(len(chans))
        w.setsampwidth(2)
        w.setframerate(SR)
        frames = bytearray()
        for i in range(n):
            for c in chans:
                v = max(-1.0, min(1.0, c[i]))
                frames += struct.pack('<h', int(v * 32767))
        w.writeframes(bytes(frames))


def note(name):
    names = {'C': 0, 'C#': 1, 'D': 2, 'D#': 3, 'E': 4, 'F': 5, 'F#': 6, 'G': 7, 'G#': 8, 'A': 9, 'A#': 10, 'B': 11}
    pitch, octave = name[:-1], int(name[-1])
    midi = 12 * (octave + 1) + names[pitch]
    return 440.0 * 2 ** ((midi - 69) / 12.0)



WOOD = ((1.0, 1.0, 1.0), (2.57, 0.4, 0.4), (4.21, 0.15, 0.25))
METAL = ((1.0, 1.0, 1.0), (1.007, 0.8, 1.0), (2.21, 0.5, 0.7), (3.63, 0.35, 0.5), (5.18, 0.2, 0.35))
GLOCK = ((1.0, 1.0, 1.0), (2.76, 0.32, 0.4), (5.40, 0.12, 0.22))
MARIMBA = ((1.0, 1.0, 1.0), (3.93, 0.22, 0.18), (9.2, 0.05, 0.07))
TUBE = ((1.0, 1.0, 1.0), (2.76, 0.5, 0.55), (5.40, 0.25, 0.35), (8.93, 0.1, 0.2))


def click(sec, seed, hp=2500, decay=0.003):
    return exp_env(highpass(noise(sec, seed), hp), decay)


def thud(freq, sec=0.12, decay=0.04, seed=9):
    out = tone(freq, sec, ((1.0, 1.0, 1.0),), attack=0.001, decay=decay, glide=(freq * 0.7, sec))
    add(out, exp_env(lowpass(noise(sec, seed), 900), decay * 0.5), 0, 0.6)
    return out


def bubble(f0, amp=1.0, xi=0.12, life=0.5):
    # minnaert bubble, damping after van den Doel; life stretches it so small ones stay audible
    d = (0.13 * f0 + 0.0072 * f0 ** 1.5) * life
    sec = min(6.0 / d, 0.09)
    out = buf(sec)
    phase = 0.0
    for i in range(len(out)):
        t = i / SR
        phase += TAU * f0 * (1.0 + xi * d * t) / SR
        out[i] = math.sin(phase) * math.exp(-d * t) * amp
    return out


def gurgle(sec, seed, rate, r_min, r_max, f_bias=1.0):
    r = random.Random(seed)
    out = buf(sec)
    t = 0.0
    while t < sec - 0.1:
        radius = r_min * (r_max / r_min) ** (r.random() ** 1.6)
        f0 = 3.26 / radius * f_bias
        add(out, bubble(f0, (radius / r_max) ** 0.8 * r.uniform(0.5, 1.0)), t)
        t += r.expovariate(rate)
    return out


def flutter(sec, seed, rate=28.0):
    r = random.Random(seed)
    out = buf(sec)
    target = 0.0
    g = 0.0
    step = int(SR / rate)
    for i in range(len(out)):
        if i % step == 0:
            target = r.random() ** 2
        g += (target - g) * 0.004
        out[i] = g
    return out


def sfx_tap():
    out = tone(2300, 0.05, WOOD, attack=0.0005, decay=0.007)
    add(out, click(0.01, 3, 3500, 0.0015), 0, 0.5)
    return normalize(fade(out, 0.0005, 0.005), -10)


def sfx_button():
    # water drop: one big bubble with a fast upward chirp
    out = tone(520, 0.14, ((1.0, 1.0, 1.0),), attack=0.001, decay=0.035, glide=(1250, 0.05))
    add(out, click(0.006, 4, 4000, 0.001), 0, 0.15)
    return normalize(reverb(fade(out), 0.12, tail=0.25), -7)


def sfx_popup():
    swish = envelope(bandpass_sweep(noise(0.18, 81), 900, 2600, q=1.4), 0.06, 0.08)
    out = buf(0.4)
    add(out, swish, 0, 0.5)
    add(out, tone(680, 0.14, ((1.0, 1.0, 1.0),), attack=0.001, decay=0.03, glide=(1500, 0.045)), 0.1, 0.9)
    return normalize(reverb(fade(out), 0.15, tail=0.3), -6)


def sfx_whoosh():
    a = envelope(bandpass_sweep(noise(0.5, 41), 300, 2400, q=1.2, curve=0.7), 0.18, 0.25)
    b = envelope(bandpass_sweep(noise(0.5, 42), 500, 4200, q=2.5, curve=0.9), 0.2, 0.22)
    out = buf(0.5)
    add(out, a, 0, 0.8)
    add(out, b, 0.02, 0.35)
    return normalize(fade(out), -8)


def sfx_select():
    # fingernail tap on a glass bottle
    out = tone(2650, 0.35, GLASS, attack=0.0005, decay=0.09)
    add(out, click(0.008, 5, 5000, 0.0012), 0, 0.6)
    return normalize(reverb(fade(out, 0.0005), 0.1, tail=0.25), -5)


def sfx_deselect():
    # bottle put back on the shelf
    out = thud(150, 0.16, 0.035)
    add(out, tone(980, 0.2, GLASS, attack=0.001, decay=0.05), 0.004, 0.25)
    add(out, click(0.01, 6, 1500, 0.002), 0, 0.3)
    return normalize(fade(out), -7)


def sfx_pour(sec=1.15, seed=11):
    splash = lowpass(highpass(noise(sec, seed), 350), 5500)
    mod = flutter(sec, seed + 7, 9.0)
    for i in range(len(splash)):
        splash[i] *= 0.35 + 0.65 * mod[i]
    cavity = bandpass_sweep(noise(sec, seed + 2), 430, 1500, q=7.0, curve=1.15)
    bubbles = gurgle(sec, seed + 1, 140.0, 0.0011, 0.0055)
    glugs = gurgle(sec, seed + 3, 9.0, 0.004, 0.009, f_bias=0.9)
    out = buf(sec)
    add(out, envelope(splash, 0.05, 0.2), 0, 0.22)
    add(out, envelope(cavity, 0.12, 0.2), 0, 1.6)
    add(out, bubbles, 0, 0.55)
    add(out, glugs, 0, 0.5)
    return normalize(reverb(fade(out, 0.02, 0.08), 0.08, tail=0.15), -4)


def sfx_complete():
    # cork pushed in, then the bottle rings a little
    out = buf(1.2)
    pop = exp_env(bandpass_sweep(noise(0.05, 91), 1400, 520, q=3.0), 0.01)
    air = envelope(highpass(noise(0.09, 92), 3500), 0.004, 0.06)
    add(out, pop, 0.0, 1.0)
    add(out, air, 0.0, 0.25)
    add(out, thud(210, 0.1, 0.02, 93), 0.0, 0.35)
    add(out, tone(1180, 0.8, GLASS, attack=0.002, decay=0.28), 0.01, 0.22)
    r = random.Random(94)
    for k in range(4):
        add(out, tone(note(['G6', 'C7', 'E7', 'G7'][k]), 0.4, GLOCK, attack=0.001, decay=0.16), 0.12 + 0.06 * k, 0.13)
    return normalize(reverb(fade(out), 0.18, tail=0.6), -2)


def sfx_wrong():
    out = buf(0.4)
    add(out, tone(190, 0.12, WOOD, attack=0.0008, decay=0.028), 0.0, 1.0)
    add(out, lowpass(click(0.02, 7, 300, 0.004), 2500), 0.0, 0.5)
    add(out, tone(160, 0.14, WOOD, attack=0.0008, decay=0.032), 0.13, 0.9)
    add(out, lowpass(click(0.02, 8, 300, 0.004), 2500), 0.13, 0.45)
    return normalize(fade(out), -8)


def sfx_coin():
    out = buf(0.9)
    for k, (t, a, f) in enumerate([(0.0, 1.0, 2050), (0.075, 0.55, 2110), (0.125, 0.3, 2080)]):
        add(out, tone(f, 0.75 - t, METAL, attack=0.0004, decay=0.22 - 0.05 * k), t, a)
        add(out, click(0.005, 50 + k, 5000, 0.0008), t, 0.4 * a)
    return normalize(reverb(fade(out), 0.12, tail=0.3), -3)


def sfx_star(n):
    f = note(['E6', 'G6', 'C7'][n])
    out = tone(f, 1.3, GLOCK, attack=0.0005, decay=0.55)
    add(out, click(0.006, 60 + n, 4500, 0.001), 0, 0.35)
    add(out, tone(f * 2, 0.6, GLOCK, attack=0.0005, decay=0.2), 0.05, 0.18)
    return normalize(reverb(fade(out), 0.2, tail=0.6), -3)


def sfx_win():
    out = buf(3.4)
    # party popper
    add(out, exp_env(bandpass_sweep(noise(0.06, 70), 1800, 900, q=1.2), 0.012), 0.0, 1.0)
    paper = highpass(noise(1.2, 71), 2200)
    fl = flutter(1.2, 72, 40.0)
    for i in range(len(paper)):
        paper[i] *= fl[i]
    add(out, envelope(paper, 0.01, 0.6), 0.03, 0.35)
    melody = [('C5', 0.18), ('E5', 0.30), ('G5', 0.42), ('C6', 0.54), ('G5', 0.72), ('C6', 0.84)]
    for n, t in melody:
        add(out, tone(note(n), 0.9, MARIMBA, attack=0.001, decay=0.32), t, 0.7)
    for n in ['C5', 'E5', 'G5']:
        add(out, tone(note(n), 1.6, MARIMBA, attack=0.001, decay=0.5), 1.02, 0.45)
    for n in ['C6', 'E6', 'G6', 'C7']:
        add(out, tone(note(n), 1.8, GLOCK, attack=0.0005, decay=0.7), 1.02, 0.22)
    return normalize(reverb(fade(out, 0.001, 0.3), 0.22, tail=1.2), -1.5)


def sfx_reward():
    # wind chimes
    out = buf(2.2)
    r = random.Random(33)
    scale = ['C6', 'D6', 'E6', 'G6', 'A6', 'C7', 'D7']
    t = 0.0
    for k in range(11):
        add(out, tone(note(r.choice(scale)), 1.4, TUBE, attack=0.0008, decay=r.uniform(0.5, 0.9)), t, r.uniform(0.35, 0.7))
        t += r.uniform(0.04, 0.13)
    return normalize(reverb(fade(out), 0.28, tail=1.0), -3)


def sfx_hint():
    out = buf(0.9)
    swell = envelope(bandpass_sweep(noise(0.6, 101), 1200, 5200, q=4.0), 0.25, 0.25)
    add(out, swell, 0, 0.5)
    add(out, tone(note('E6'), 0.7, ((1.0, 1.0, 1.0), (2.0, 0.2, 0.6)), attack=0.08, decay=0.35, glide=(note('B6'), 0.35)), 0.08, 0.45)
    add(out, tone(note('B6'), 0.5, GLOCK, attack=0.0005, decay=0.25), 0.36, 0.3)
    return normalize(reverb(fade(out), 0.25, tail=0.6), -5)


def sfx_undo():
    # quick tape rewind
    sec = 0.32
    out = buf(sec)
    phase = 0.0
    for i in range(len(out)):
        t = i / SR
        f = 1400 * (380 / 1400) ** (t / sec) * (1 + 0.05 * math.sin(TAU * 38 * t))
        phase += TAU * f / SR
        out[i] = (math.sin(phase) + 0.3 * math.sin(2 * phase)) * min(t / 0.03, 1.0) * (1 - t / sec)
    hiss = envelope(bandpass_sweep(noise(sec, 111), 3000, 900, q=1.5), 0.04, 0.1)
    add(out, hiss, 0, 0.6)
    out = lowpass(out, 4000)
    return normalize(fade(out), -8)


def sfx_ice():
    out = buf(0.9)
    r = random.Random(51)
    t = 0.0
    for _ in range(14):
        add(out, click(0.03, r.randint(0, 9999), r.uniform(2000, 5000), r.uniform(0.0015, 0.005)), t, r.uniform(0.35, 1.0))
        t += r.expovariate(55)
    add(out, thud(95, 0.25, 0.06, 52), 0.0, 0.6)
    add(out, tone(note('C#7'), 0.6, GLASS, attack=0.001, decay=0.2), 0.04, 0.12)
    return normalize(reverb(fade(out), 0.15, tail=0.5), -3)


def sfx_unlock():
    out = buf(0.9)
    add(out, exp_env(bandpass_sweep(noise(0.03, 61), 4200, 4200, q=6), 0.004), 0.0, 0.8)
    add(out, tone(3100, 0.15, METAL, attack=0.0004, decay=0.03), 0.0, 0.25)
    add(out, exp_env(bandpass_sweep(noise(0.03, 62), 3300, 3300, q=6), 0.004), 0.09, 0.7)
    add(out, thud(130, 0.15, 0.03, 63), 0.2, 0.9)
    add(out, exp_env(bandpass_sweep(noise(0.04, 64), 2200, 2200, q=4), 0.006), 0.2, 0.6)
    add(out, tone(1650, 0.6, METAL, attack=0.0005, decay=0.18), 0.21, 0.18)
    return normalize(reverb(fade(out), 0.12, tail=0.4), -3)


def sfx_reveal():
    out = buf(0.8)
    air = envelope(bandpass_sweep(noise(0.55, 71), 2500, 7000, q=1.8), 0.3, 0.2)
    add(out, air, 0, 0.55)
    add(out, tone(note('A6'), 0.6, GLOCK, attack=0.0005, decay=0.28), 0.3, 0.45)
    return normalize(reverb(fade(out), 0.25, tail=0.5), -5)


def sfx_wheel_tick():
    # пластиковый язычок стрелки цепляет штырёк
    out = tone(2900, 0.06, WOOD, attack=0.0003, decay=0.006)
    add(out, click(0.008, 121, 2500, 0.0012), 0, 0.7)
    add(out, tone(950, 0.04, ((1.0, 1.0, 1.0),), attack=0.0005, decay=0.008), 0, 0.35)
    return normalize(fade(out, 0.0003, 0.004), -9)


def sfx_wheel_stop():
    out = buf(0.9)
    add(out, thud(170, 0.14, 0.03, 131), 0, 0.8)
    add(out, click(0.01, 132, 1800, 0.002), 0, 0.4)
    add(out, tone(note('E7'), 0.7, GLOCK, attack=0.0005, decay=0.3), 0.05, 0.3)
    return normalize(reverb(fade(out), 0.15, tail=0.4), -6)


def music_loop(chords, bpm, bars_per_chord, seed, melody_density, pad_gain, root_oct, arp_prob=0.82, arp_gain=0.22):
    beat = 60.0 / bpm
    bar = beat * 4
    length = bar * bars_per_chord * len(chords)
    r = random.Random(seed)
    voice = buf(length * 2 + 2.0)
    for rep in range(2):
        base_t = rep * length
        for ci, chord in enumerate(chords):
            t0 = base_t + ci * bar * bars_per_chord
            dur = bar * bars_per_chord
            for nn in chord:
                f = note(nn[:-1] + str(int(nn[-1]) + root_oct - 4))
                pad = tone(f, dur + 0.6, ((1, 1, 1), (2, 0.18, 1), (3.0, 0.06, 1)), attack=0.8, decay=20.0)
                pad = envelope(pad, 0.9, 0.9)
                add(voice, pad, t0, pad_gain)
            # arp in 8ths
            arp = chord + [chord[1][:-1] + str(int(chord[1][-1]) + 1)]
            steps = int(dur / (beat / 2))
            for s in range(steps):
                if r.random() < arp_prob:
                    nn = arp[(s * 2 + (s // 4)) % len(arp)]
                    f = note(nn[:-1] + str(int(nn[-1]) + 1))
                    add(voice, tone(f, 1.0, KALIMBA, decay=0.45), t0 + s * beat / 2, arp_gain + 0.04 * r.random())
            scale = ['C', 'D', 'E', 'G', 'A']
            for s in range(int(dur / beat)):
                if r.random() < melody_density:
                    nn = r.choice(scale) + str(r.choice([5, 6]))
                    add(voice, tone(note(nn), 1.4, GLASS, decay=0.5), t0 + s * beat, 0.16)
    left = reverb(voice, 0.35, room=0.86, damp=0.4, tail=0.0, spread=0)
    right = reverb(voice, 0.35, room=0.86, damp=0.4, tail=0.0, spread=23)
    s = int(length * SR)
    left = left[s:2 * s]
    right = right[s:2 * s]
    m = max(max(left), -min(left), max(right), -min(right), 1e-9)
    g = 10 ** (-4.0 / 20.0) / m
    return array('d', (v * g for v in left)), array('d', (v * g for v in right))



def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
    sfx_dir = os.path.join(out_dir, 'SFX')
    mus_dir = os.path.join(out_dir, 'Music')
    os.makedirs(sfx_dir, exist_ok=True)
    os.makedirs(mus_dir, exist_ok=True)
    jobs = {
        'ui_tap': sfx_tap,
        'ui_button': sfx_button,
        'ui_popup': sfx_popup,
        'ui_whoosh': sfx_whoosh,
        'bottle_select': sfx_select,
        'bottle_deselect': sfx_deselect,
        'pour': sfx_pour,
        'pour_short': lambda: sfx_pour(0.55, 13),
        'bottle_complete': sfx_complete,
        'wrong_move': sfx_wrong,
        'level_win': sfx_win,
        'star_1': lambda: sfx_star(0),
        'star_2': lambda: sfx_star(1),
        'star_3': lambda: sfx_star(2),
        'coin': sfx_coin,
        'reward': sfx_reward,
        'booster_hint': sfx_hint,
        'booster_undo': sfx_undo,
        'ice_crack': sfx_ice,
        'unlock': sfx_unlock,
        'reveal': sfx_reveal,
        'wheel_tick': sfx_wheel_tick,
        'wheel_stop': sfx_wheel_stop,
    }
    only = set(sys.argv[2:])
    for name, fn in jobs.items():
        if only and name not in only:
            continue
        write_wav(os.path.join(sfx_dir, name + '.wav'), [fn()])
        print('sfx', name, flush=True)
    if not only or 'music' in only:
        menu = music_loop([['C4', 'E4', 'G4', 'B4'], ['A3', 'C4', 'E4', 'G4'], ['F3', 'A3', 'C4', 'E4'], ['G3', 'B3', 'D4', 'E4']],
                          bpm=66, bars_per_chord=2, seed=101, melody_density=0.12, pad_gain=0.14, root_oct=4, arp_prob=0.5, arp_gain=0.16)
        write_wav(os.path.join(mus_dir, 'music_menu_loop.wav'), list(menu))
        print('music menu', flush=True)
        game = music_loop([['D4', 'F#4', 'A4', 'C#5'], ['B3', 'D4', 'F#4', 'A4'], ['G3', 'B3', 'D4', 'F#4'], ['A3', 'C#4', 'E4', 'G4']],
                          bpm=58, bars_per_chord=2, seed=202, melody_density=0.05, pad_gain=0.13, root_oct=4, arp_prob=0.32, arp_gain=0.13)
        write_wav(os.path.join(mus_dir, 'music_game_loop.wav'), list(game))
        print('music game', flush=True)


if __name__ == '__main__':
    main()
