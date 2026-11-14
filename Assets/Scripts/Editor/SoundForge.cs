using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Synthesizes every sound effect and the music loops from scratch (no downloads, no licences),
/// writes them as WAV files into Assets/Audio and fills Assets/Resources/SoundBank.asset.
/// Menu: Jackpot Derby/Build Sounds. Re-run after tweaking a recipe below.
/// </summary>
public static class SoundForge
{
    const int SR = 44100;
    const string SfxDir = "Assets/Audio/Sfx";
    const string MusicDir = "Assets/Audio/Music";
    const string BankPath = "Assets/Resources/SoundBank.asset";

    static System.Random rng = new System.Random(7);
    static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

    // =====================================================================================
    // Build
    // =====================================================================================

    [MenuItem("Jackpot Derby/Build Sounds")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(SfxDir);
        Directory.CreateDirectory(MusicDir);
        Directory.CreateDirectory("Assets/Resources");
        rng = new System.Random(7);

        var sfx = new Dictionary<string, float[]>
        {
            ["shot_small"] = ShotSmall(),
            ["shot_jackpot"] = ShotJackpot(),
            ["hit"] = Hit(),
            ["ram"] = Ram(),
            ["explosion"] = Explosion(),
            ["coins"] = Coins(12, 0.65f),
            ["chest_open"] = ChestOpen(),
            ["chest_spawn"] = ChestSpawn(),
            ["chest_bonk"] = ChestBonk(),
            ["lock"] = LockClick(),
            ["unlock"] = UnlockClick(),
            ["reel_tick"] = ReelTick(),
            ["hint_lucky"] = HintLucky(),
            ["hint_close"] = HintClose(),
            ["hint_high"] = HintTwoNote(76, 69),
            ["hint_low"] = HintTwoNote(69, 76),
            ["clover"] = Clover(),
            ["dice"] = Dice(),
            ["cat"] = Cat(),
            ["turbo"] = Turbo(),
            ["beep"] = Beep(),
            ["go"] = Go(),
            ["ui_click"] = UiClick(),
            ["ui_hover"] = UiHover(),
            ["win"] = Win(),
            ["lose"] = Lose(),
            ["thunder"] = Thunder(),
            ["storm_loop"] = StormLoop(),
            ["engine_loop"] = EngineLoop(),
            ["takedown"] = Takedown(),
        };
        foreach (var kv in sfx) WriteWav($"{SfxDir}/{kv.Key}.wav", kv.Value, null);

        var (ml, mr) = Music(true);
        WriteWav($"{MusicDir}/music_match.wav", ml, mr);
        var (nl, nr) = Music(false);
        WriteWav($"{MusicDir}/music_menu.wav", nl, nr);

        AssetDatabase.Refresh();
        ConfigureImports();
        BuildBank();
        Debug.Log($"SoundForge: built {sfx.Count} sound effects and 2 music loops.");
    }

    static void ConfigureImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            bool music = path.Contains("/Music/");
            var s = imp.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.7f : 0.85f;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.forceToMono = !music;
            imp.loadInBackground = music;
            imp.SaveAndReimport();
        }
    }

    static void BuildBank()
    {
        var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }
        var so = new SerializedObject(bank);
        var it = so.GetIterator();
        it.Next(true);
        while (it.NextVisible(false))
        {
            if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
            string name = ToSnake(it.name);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/{name}.wav")
                       ?? AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicDir}/{name}.wav");
            if (clip == null) Debug.LogWarning("SoundForge: no clip for SoundBank." + it.name);
            it.objectReferenceValue = clip;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
    }

    static string ToSnake(string camel)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in camel)
        {
            if (char.IsUpper(c)) { sb.Append('_'); sb.Append(char.ToLowerInvariant(c)); }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // =====================================================================================
    // DSP toolkit
    // =====================================================================================

    enum Wave { Sine, Tri, Saw, Square }

    class Osc
    {
        double phase;
        public Osc(double startPhase = 0) { phase = startPhase; }
        public float Next(float freq, Wave w)
        {
            double dt = freq / SR;
            double p = phase;
            float v;
            switch (w)
            {
                case Wave.Sine: v = (float)Math.Sin(2.0 * Math.PI * p); break;
                case Wave.Tri: v = (float)(4.0 * Math.Abs(p - 0.5) - 1.0); break;
                case Wave.Saw: v = (float)(2.0 * p - 1.0 - Blep(p, dt)); break;
                default: v = (p < 0.5 ? 1f : -1f) + Blep(p, dt) - Blep((p + 0.5) % 1.0, dt); break;
            }
            phase += dt;
            if (phase >= 1.0) phase -= Math.Floor(phase);
            return v;
        }
        static float Blep(double t, double dt)
        {
            if (dt <= 0) return 0f;
            if (t < dt) { t /= dt; return (float)(t + t - t * t - 1.0); }
            if (t > 1.0 - dt) { t = (t - 1.0) / dt; return (float)(t * t + t + t + 1.0); }
            return 0f;
        }
    }

    /// <summary>Zero-delay-feedback state variable filter (low/band/high at once).</summary>
    class Svf
    {
        float ic1, ic2;
        public float Low, Band, High;
        public void Run(float x, float fc, float q)
        {
            fc = Mathf.Clamp(fc, 20f, SR * 0.45f);
            float g = Mathf.Tan(Mathf.PI * fc / SR);
            float k = 1f / Mathf.Max(0.05f, q);
            float a1 = 1f / (1f + g * (g + k)), a2 = g * a1, a3 = g * a2;
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            Low = v2; Band = v1; High = x - k * v1 - v2;
        }
    }

    enum Filt { None, Low, Band, High }

    static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];

    /// <summary>Linear attack, exponential decay.</summary>
    static float AD(float t, float attack, float decay)
    {
        if (t < 0f) return 0f;
        if (t < attack) return t / attack;
        return Mathf.Exp(-(t - attack) / decay);
    }

    /// <summary>Attack, hold at 1, then release; zero after.</summary>
    static float AHR(float t, float attack, float hold, float release)
    {
        if (t < 0f) return 0f;
        if (t < attack) return t / attack;
        if (t < attack + hold) return 1f;
        float r = (t - attack - hold) / release;
        return r >= 1f ? 0f : (1f - r) * (1f - r);
    }

    /// <summary>Exponential glide from f0 to f1 over dur seconds.</summary>
    static float Glide(float t, float dur, float f0, float f1) => f0 * Mathf.Pow(f1 / f0, Mathf.Clamp01(t / dur));

    static void Tone(float[] b, float start, float dur, Func<float, float> freq, Wave w, Func<float, float> amp,
                     float gain = 1f, Filt filt = Filt.None, Func<float, float> cutoff = null, float q = 0.7f)
    {
        var o = new Osc(Rand(0f, 1f));
        var f = new Svf();
        int s0 = Mathf.RoundToInt(start * SR), n = Mathf.RoundToInt(dur * SR);
        for (int i = 0; i < n; i++)
        {
            int idx = s0 + i;
            if (idx < 0) continue;
            if (idx >= b.Length) break;
            float t = i / (float)SR;
            float v = o.Next(freq(t), w);
            if (filt != Filt.None) { f.Run(v, cutoff(t), q); v = filt == Filt.Low ? f.Low : filt == Filt.Band ? f.Band : f.High; }
            b[idx] += v * amp(t) * gain;
        }
    }

    static void NoiseBurst(float[] b, float start, float dur, Func<float, float> amp, Filt filt, Func<float, float> cutoff,
                           float q = 0.7f, float gain = 1f)
    {
        var f = new Svf();
        int s0 = Mathf.RoundToInt(start * SR), n = Mathf.RoundToInt(dur * SR);
        for (int i = 0; i < n; i++)
        {
            int idx = s0 + i;
            if (idx < 0) continue;
            if (idx >= b.Length) break;
            float t = i / (float)SR;
            float v = Noise();
            if (filt != Filt.None) { f.Run(v, cutoff(t), q); v = filt == Filt.Low ? f.Low : filt == Filt.Band ? f.Band : f.High; }
            b[idx] += v * amp(t) * gain;
        }
    }

    /// <summary>Two-operator FM bell / mallet. ratio 1 = sweet, 3.5 = glassy, 2.76 = metallic coin.</summary>
    static void Bell(float[] b, float start, float freq, float decay, float gain, float ratio = 3.5f, float index = 2f, float pan = 0f, float[] right = null)
    {
        int s0 = Mathf.RoundToInt(start * SR), n = Mathf.RoundToInt(decay * 7f * SR);
        float fm = freq * ratio;
        for (int i = 0; i < n; i++)
        {
            int idx = s0 + i;
            if (idx < 0) continue;
            if (idx >= b.Length) break;
            float t = i / (float)SR;
            float env = AD(t, 0.002f, decay);
            float idxEnv = index * Mathf.Exp(-t / (decay * 0.45f));
            float v = Mathf.Sin(2f * Mathf.PI * freq * t + idxEnv * Mathf.Sin(2f * Mathf.PI * fm * t)) * env * gain;
            if (right == null) b[idx] += v;
            else { b[idx] += v * Mathf.Sqrt(0.5f * (1f - pan)); right[idx] += v * Mathf.Sqrt(0.5f * (1f + pan)); }
        }
    }

    static void Saturate(float[] b, float drive)
    {
        float norm = (float)Math.Tanh(drive);
        for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * drive) / norm;
    }

    /// <summary>Freeverb-style room: 8 damped combs into 4 allpasses. Returns a longer buffer.</summary>
    static float[] Reverb(float[] dry, float room, float damp, float wet, float tail, int spread = 0)
    {
        int[] combLen = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        int[] apLen = { 556, 441, 341, 225 };
        int n = dry.Length + Mathf.RoundToInt(tail * SR);
        var wetBuf = new float[n];
        foreach (int len0 in combLen)
        {
            int len = len0 + spread;
            var cb = new float[len];
            int ci = 0; float store = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = i < dry.Length ? dry[i] : 0f;
                float y = cb[ci];
                store = y * (1f - damp) + store * damp;
                cb[ci] = x * 0.015f + store * room;
                if (++ci >= len) ci = 0;
                wetBuf[i] += y;
            }
        }
        foreach (int len0 in apLen)
        {
            int len = len0 + spread;
            var ab = new float[len];
            int ai = 0;
            for (int i = 0; i < n; i++)
            {
                float x = wetBuf[i];
                float bo = ab[ai];
                wetBuf[i] = -x + bo;
                ab[ai] = x + bo * 0.5f;
                if (++ai >= len) ai = 0;
            }
        }
        var outp = new float[n];
        for (int i = 0; i < n; i++) outp[i] = (i < dry.Length ? dry[i] : 0f) + wetBuf[i] * wet * 3f;
        return outp;
    }

    /// <summary>Removes DC, trims trailing silence, adds tiny fades and normalizes the peak.</summary>
    static float[] Finish(float[] b, float peak = 0.89f, bool trim = true)
    {
        // DC blocker
        float x1 = 0f, y1 = 0f;
        for (int i = 0; i < b.Length; i++) { float y = b[i] - x1 + 0.9985f * y1; x1 = b[i]; y1 = y; b[i] = y; }
        float max = 1e-6f;
        foreach (float v in b) max = Mathf.Max(max, Mathf.Abs(v));
        int end = b.Length;
        if (trim)
        {
            float floor = max * 0.0008f;
            while (end > 1 && Mathf.Abs(b[end - 1]) < floor) end--;
            end = Mathf.Min(b.Length, end + SR / 100);
        }
        var o = new float[end];
        float g = peak / max;
        int fin = Mathf.Min(end, SR / 3000), fout = Mathf.Min(end, SR / 120);
        for (int i = 0; i < end; i++)
        {
            float v = b[i] * g;
            if (i < fin) v *= i / (float)fin;
            if (i > end - fout) v *= (end - i) / (float)fout;
            o[i] = v;
        }
        return o;
    }

    /// <summary>Makes a seamless loop of length L from a buffer rendered longer: the overhang crossfades into the start.</summary>
    static float[] LoopFold(float[] b, int L, int fade)
    {
        var o = new float[L];
        for (int i = 0; i < L; i++) o[i] = b[i];
        for (int i = 0; i < fade && L + i < b.Length; i++)
        {
            float k = i / (float)fade;
            o[i] = b[i] * Mathf.Sqrt(k) + b[L + i] * Mathf.Sqrt(1f - k);
        }
        return o;
    }

    static float[] Mix(params float[][] parts)
    {
        int n = 0;
        foreach (var p in parts) n = Mathf.Max(n, p.Length);
        var o = new float[n];
        foreach (var p in parts) for (int i = 0; i < p.Length; i++) o[i] += p[i];
        return o;
    }

    static void WriteWav(string path, float[] left, float[] right)
    {
        int ch = right == null ? 1 : 2;
        int frames = left.Length;
        int dataLen = frames * ch * 2;
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)ch);
            w.Write(SR);
            w.Write(SR * ch * 2);
            w.Write((short)(ch * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            for (int i = 0; i < frames; i++)
            {
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(left[i] * 32767f), -32768, 32767));
                if (ch == 2) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(right[i] * 32767f), -32768, 32767));
            }
        }
    }

    // =====================================================================================
    // Combat
    // =====================================================================================

    /// <summary>Fat bubbly "bwip" for the teal lucky bolts.</summary>
    static float[] ShotSmall()
    {
        var b = Buf(0.3f);
        Tone(b, 0f, 0.016f, t => Glide(t, 0.016f, 700f, 1300f), Wave.Square, t => AD(t, 0.002f, 0.05f), 0.35f, Filt.Low, t => 5000f);
        Tone(b, 0.014f, 0.2f, t => Glide(t, 0.11f, 1300f, 300f), Wave.Square, t => AD(t, 0.001f, 0.055f), 0.5f, Filt.Low, t => Glide(t, 0.12f, 5200f, 1100f), 1.2f);
        Tone(b, 0f, 0.15f, t => Glide(t, 0.08f, 260f, 110f), Wave.Sine, t => AD(t, 0.002f, 0.04f), 0.8f);
        NoiseBurst(b, 0f, 0.02f, t => AD(t, 0.0005f, 0.004f), Filt.High, t => 3000f, 0.7f, 0.35f);
        Saturate(b, 1.6f);
        return Finish(Reverb(b, 0.7f, 0.4f, 0.06f, 0.25f));
    }

    /// <summary>Deeper boom-pew for the gold Jackpot cannon bursts, with a hint of a gold chime.</summary>
    static float[] ShotJackpot()
    {
        var b = Buf(0.5f);
        Tone(b, 0f, 0.35f, t => Glide(t, 0.22f, 560f, 95f), Wave.Saw, t => AD(t, 0.002f, 0.09f), 0.6f, Filt.Low, t => Glide(t, 0.25f, 3800f, 420f), 1.4f);
        Tone(b, 0f, 0.35f, t => Glide(t, 0.2f, 150f, 45f), Wave.Sine, t => AD(t, 0.003f, 0.11f), 1.0f);
        NoiseBurst(b, 0f, 0.15f, t => AD(t, 0.001f, 0.035f), Filt.Low, t => Glide(t, 0.1f, 4000f, 900f), 0.7f, 0.55f);
        Bell(b, 0.01f, 1568f, 0.09f, 0.12f, 1f, 1.2f);
        Saturate(b, 2.2f);
        return Finish(Reverb(b, 0.75f, 0.4f, 0.1f, 0.35f));
    }

    /// <summary>Chunky cartoon "thwack-pop" when a bolt lands.</summary>
    static float[] Hit()
    {
        var b = Buf(0.3f);
        Tone(b, 0f, 0.2f, t => Glide(t, 0.08f, 280f, 70f), Wave.Sine, t => AD(t, 0.001f, 0.055f), 1.0f);
        NoiseBurst(b, 0f, 0.12f, t => AD(t, 0.0005f, 0.028f), Filt.Band, t => Glide(t, 0.06f, 2600f, 1200f), 0.8f, 1.3f);
        Tone(b, 0f, 0.05f, t => Glide(t, 0.03f, 1100f, 500f), Wave.Tri, t => AD(t, 0.0005f, 0.018f), 0.35f);
        Saturate(b, 2.6f);
        return Finish(Reverb(b, 0.6f, 0.5f, 0.05f, 0.2f));
    }

    /// <summary>Metal crunch + clang + thump for car-on-car rams.</summary>
    static float[] Ram()
    {
        var b = Buf(0.9f);
        Tone(b, 0f, 0.35f, t => Glide(t, 0.12f, 130f, 38f), Wave.Sine, t => AD(t, 0.002f, 0.1f), 1.1f);
        NoiseBurst(b, 0f, 0.5f, t => AD(t, 0.002f, 0.11f), Filt.Low, t => Glide(t, 0.3f, 2400f, 500f), 0.8f, 0.9f);
        // Crunchy grains
        var grain = Buf(0.9f);
        NoiseBurst(grain, 0f, 0.35f, t => AD(t, 0.001f, 0.07f), Filt.Band, t => 3400f, 2f, 1.4f);
        int chunk = SR / 450; float gate = 1f;
        for (int i = 0; i < grain.Length; i++) { if (i % chunk == 0) gate = Rand(0f, 1f) > 0.55f ? 1f : 0.15f; grain[i] *= gate; }
        for (int i = 0; i < b.Length; i++) b[i] += grain[i];
        float[] partials = { 310f, 523f, 787f, 1131f, 1590f };
        for (int k = 0; k < partials.Length; k++)
        {
            float f = partials[k] * Rand(0.98f, 1.02f), d = Rand(0.12f, 0.3f);
            Tone(b, 0.003f, 0.9f, t => f, Wave.Sine, t => AD(t, 0.001f, d), 0.22f / (1f + k * 0.3f));
        }
        Saturate(b, 3f);
        return Finish(Reverb(b, 0.78f, 0.35f, 0.12f, 0.5f));
    }

    /// <summary>Big cartoon kaboom: pop, sub boom, falling roar and crackles.</summary>
    static float[] Explosion()
    {
        var b = Buf(2.6f);
        NoiseBurst(b, 0f, 0.04f, t => AD(t, 0.0005f, 0.01f), Filt.High, t => 1500f, 0.7f, 1.0f);
        Tone(b, 0f, 1.4f, t => Glide(t, 0.6f, 95f, 28f), Wave.Sine, t => AD(t, 0.004f, 0.33f), 1.3f);
        Tone(b, 0f, 0.6f, t => Glide(t, 0.4f, 190f, 55f), Wave.Tri, t => AD(t, 0.003f, 0.15f), 0.5f);
        NoiseBurst(b, 0f, 2.2f, t => AD(t, 0.004f, 0.42f), Filt.Low, t => Glide(t, 1.6f, 5500f, 260f), 0.9f, 1.4f);
        for (int i = 0; i < 46; i++)
        {
            float st = Mathf.Pow(Rand(0f, 1f), 1.8f) * 1.3f;
            float g = Rand(0.15f, 0.5f) * (1f - st / 1.5f);
            float fc = Rand(1800f, 5000f);
            NoiseBurst(b, 0.03f + st, 0.02f, t => AD(t, 0.0003f, 0.003f), Filt.Band, t => fc, 2f, g);
        }
        Saturate(b, 2.2f);
        return Finish(Reverb(b, 0.86f, 0.3f, 0.22f, 1.2f));
    }

    /// <summary>A shower of little coin chimes.</summary>
    static float[] Coins(int count, float spread)
    {
        var b = Buf(spread + 1.2f);
        float[] notes = { 2093f, 2349f, 2637f, 3136f, 3520f, 2794f };
        for (int i = 0; i < count; i++)
        {
            float st = Mathf.Pow(i / (float)count, 1.3f) * spread + Rand(0f, 0.025f);
            float f = notes[rng.Next(notes.Length)] * Rand(0.985f, 1.015f);
            Bell(b, st, f, Rand(0.1f, 0.22f), Rand(0.22f, 0.4f) * (1f - 0.5f * i / count), 2.76f, Rand(0.8f, 1.6f));
        }
        return Finish(Reverb(b, 0.75f, 0.45f, 0.14f, 0.5f));
    }

    static float[] Takedown()
    {
        var b = Buf(1.2f);
        int[] notes = { 79, 84, 88 };
        for (int i = 0; i < notes.Length; i++)
        {
            float f = Midi(notes[i]);
            Tone(b, i * 0.07f, 0.4f, t => f, Wave.Square, t => AD(t, 0.003f, 0.09f), 0.22f, Filt.Low, t => 3200f);
            Bell(b, i * 0.07f, f, 0.25f, 0.4f, 1f, 1.6f);
        }
        var coins = Coins(6, 0.3f);
        for (int i = 0; i < coins.Length && i + SR / 5 < b.Length; i++) b[i + SR / 5] += coins[i] * 0.5f;
        return Finish(Reverb(b, 0.78f, 0.35f, 0.2f, 0.6f));
    }

    // =====================================================================================
    // Chests and the number
    // =====================================================================================

    static float[] ChestOpen()
    {
        var b = Buf(1.8f);
        Tone(b, 0f, 0.2f, t => Glide(t, 0.1f, 200f, 90f), Wave.Sine, t => AD(t, 0.002f, 0.06f), 0.7f);
        NoiseBurst(b, 0f, 0.06f, t => AD(t, 0.001f, 0.015f), Filt.Band, t => 900f, 2.5f, 0.6f);
        int[] arp = { 72, 76, 79, 84, 88 };
        for (int i = 0; i < arp.Length; i++) Bell(b, 0.06f + i * 0.065f, Midi(arp[i]), 0.45f, 0.42f, 1f, 1.5f);
        NoiseBurst(b, 0.05f, 1.2f, t => AD(t, 0.25f, 0.35f), Filt.High, t => 7000f, 0.7f, 0.1f);
        var coins = Coins(10, 0.5f);
        for (int i = 0; i < coins.Length && i + SR / 4 < b.Length; i++) b[i + SR / 4] += coins[i] * 0.55f;
        return Finish(Reverb(b, 0.82f, 0.35f, 0.22f, 0.8f));
    }

    /// <summary>Gentle "a chest appeared" sparkle.</summary>
    static float[] ChestSpawn()
    {
        var b = Buf(1.4f);
        Bell(b, 0f, Midi(84), 0.4f, 0.5f, 1f, 1.2f);
        Bell(b, 0.11f, Midi(91), 0.55f, 0.45f, 1f, 1.2f);
        Tone(b, 0f, 0.8f, t => Glide(t, 0.5f, 900f, 2200f), Wave.Sine, t => AD(t, 0.2f, 0.2f) * (0.6f + 0.4f * Mathf.Sin(t * 60f)), 0.08f);
        return Finish(Reverb(b, 0.84f, 0.4f, 0.25f, 0.8f));
    }

    /// <summary>Cartoon "boing-bonk" when the number is too far off and you bounce off the chest.</summary>
    static float[] ChestBonk()
    {
        var b = Buf(0.6f);
        NoiseBurst(b, 0f, 0.05f, t => AD(t, 0.0005f, 0.012f), Filt.Band, t => 750f, 3f, 1.2f);
        Tone(b, 0f, 0.25f, t => Glide(t, 0.12f, 430f, 200f), Wave.Sine, t => AD(t, 0.002f, 0.07f), 0.7f);
        Tone(b, 0.02f, 0.5f, t => 290f * (1f + 0.18f * Mathf.Sin(2f * Mathf.PI * 17f * t) * Mathf.Exp(-t / 0.18f)), Wave.Tri, t => AD(t, 0.004f, 0.16f), 0.55f);
        return Finish(b);
    }

    static float[] Click(float f1, float g1)
    {
        var b = Buf(0.06f);
        NoiseBurst(b, 0f, 0.03f, t => AD(t, 0.0003f, 0.005f), Filt.Band, t => f1, 4f, g1);
        Tone(b, 0f, 0.03f, t => f1 * 0.75f, Wave.Sine, t => AD(t, 0.0003f, 0.008f), 0.3f * g1);
        return b;
    }

    static void Paste(float[] dst, float[] src, float at, float gain = 1f)
    {
        int s0 = Mathf.RoundToInt(at * SR);
        for (int i = 0; i < src.Length && s0 + i < dst.Length; i++) dst[s0 + i] += src[i] * gain;
    }

    static float[] LockClick()
    {
        var b = Buf(0.25f);
        Paste(b, Click(3400f, 0.8f), 0f);
        Paste(b, Click(2100f, 1.2f), 0.055f);
        Tone(b, 0.055f, 0.08f, t => Glide(t, 0.04f, 200f, 120f), Wave.Sine, t => AD(t, 0.001f, 0.02f), 0.6f);
        return Finish(Reverb(b, 0.5f, 0.6f, 0.05f, 0.1f));
    }

    static float[] UnlockClick()
    {
        var b = Buf(0.25f);
        Paste(b, Click(2000f, 1f), 0f);
        Paste(b, Click(3600f, 0.7f), 0.05f);
        Tone(b, 0.05f, 0.06f, t => Glide(t, 0.05f, 1200f, 2200f), Wave.Square, t => AD(t, 0.001f, 0.015f), 0.12f, Filt.Low, t => 4000f);
        return Finish(Reverb(b, 0.5f, 0.6f, 0.05f, 0.1f));
    }

    static float[] ReelTick()
    {
        var b = Buf(0.05f);
        NoiseBurst(b, 0f, 0.02f, t => AD(t, 0.0002f, 0.0035f), Filt.Band, t => 4800f, 3f, 1f);
        Tone(b, 0f, 0.02f, t => 1900f, Wave.Sine, t => AD(t, 0.0002f, 0.005f), 0.3f);
        return Finish(b, 0.8f);
    }

    /// <summary>Slot-machine jackpot: racing arpeggio, big chord, coins.</summary>
    static float[] HintLucky()
    {
        var b = Buf(2.4f);
        int[] arp = { 72, 76, 79, 84, 88, 91, 96 };
        for (int i = 0; i < arp.Length; i++)
        {
            float f = Midi(arp[i]), st = i * 0.055f;
            Tone(b, st, 0.2f, t => f, Wave.Square, t => AD(t, 0.002f, 0.06f), 0.16f, Filt.Low, t => 3500f);
            Bell(b, st, f, 0.2f, 0.32f, 1f, 1.4f);
        }
        foreach (int n in new[] { 84, 88, 91, 96 })
        {
            float f = Midi(n);
            Bell(b, 0.42f, f, 0.6f, 0.3f, 1f, 1.8f);
            Tone(b, 0.42f, 1.2f, t => f * (1f + 0.004f * Mathf.Sin(t * 33f)), Wave.Square, t => AD(t, 0.01f, 0.35f), 0.07f, Filt.Low, t => 2600f);
        }
        var coins = Coins(16, 0.8f);
        Paste(b, coins, 0.45f, 0.6f);
        return Finish(Reverb(b, 0.84f, 0.35f, 0.24f, 0.9f));
    }

    static float[] HintClose()
    {
        var b = Buf(1.1f);
        Bell(b, 0f, Midi(79), 0.35f, 0.5f, 1f, 1.4f);
        Bell(b, 0.11f, Midi(84), 0.5f, 0.55f, 1f, 1.4f);
        Tone(b, 0f, 0.14f, t => Midi(79), Wave.Square, t => AD(t, 0.002f, 0.05f), 0.14f, Filt.Low, t => 3000f);
        Tone(b, 0.11f, 0.3f, t => Midi(84), Wave.Square, t => AD(t, 0.002f, 0.08f), 0.14f, Filt.Low, t => 3000f);
        return Finish(Reverb(b, 0.8f, 0.4f, 0.2f, 0.6f));
    }

    /// <summary>Two notes: falling = "go lower" (too high), rising = "go higher" (too low).</summary>
    static float[] HintTwoNote(int from, int to)
    {
        var b = Buf(0.9f);
        float f0 = Midi(from), f1 = Midi(to);
        Tone(b, 0f, 0.2f, t => f0, Wave.Tri, t => AD(t, 0.003f, 0.08f), 0.55f);
        Tone(b, 0f, 0.2f, t => f0, Wave.Square, t => AD(t, 0.003f, 0.06f), 0.12f, Filt.Low, t => 2000f);
        Tone(b, 0.13f, 0.5f, t => f1, Wave.Tri, t => AD(t, 0.003f, 0.15f), 0.55f);
        Tone(b, 0.13f, 0.5f, t => f1, Wave.Square, t => AD(t, 0.003f, 0.1f), 0.12f, Filt.Low, t => 2000f);
        return Finish(Reverb(b, 0.75f, 0.45f, 0.15f, 0.5f));
    }

    // =====================================================================================
    // Pickups and superstitions
    // =====================================================================================

    static float[] Clover()
    {
        var b = Buf(1.5f);
        Tone(b, 0f, 0.45f, t => Glide(t, 0.35f, 500f, 1700f), Wave.Sine, t => AD(t, 0.02f, 0.15f) * (0.7f + 0.3f * Mathf.Sin(t * 90f)), 0.25f);
        int[] arp = { 79, 83, 86, 91, 95 };
        for (int i = 0; i < arp.Length; i++) Bell(b, 0.05f + i * 0.055f, Midi(arp[i]), 0.4f, 0.38f, 1f, 1.1f);
        NoiseBurst(b, 0f, 1f, t => AD(t, 0.15f, 0.3f), Filt.High, t => 8000f, 0.7f, 0.08f);
        return Finish(Reverb(b, 0.84f, 0.35f, 0.25f, 0.8f));
    }

    static float[] Dice()
    {
        var b = Buf(1.2f);
        float st = 0f;
        for (int i = 0; i < 8; i++)
        {
            st += Rand(0.03f, 0.08f) * (1f + i * 0.08f);
            float fc = Rand(1600f, 3200f), tf = Rand(500f, 950f), g = Rand(0.5f, 1f);
            NoiseBurst(b, st, 0.03f, t => AD(t, 0.0003f, 0.006f), Filt.Band, t => fc, 5f, g);
            Tone(b, st, 0.03f, t => tf, Wave.Tri, t => AD(t, 0.0003f, 0.008f), 0.3f * g);
        }
        Bell(b, st + 0.12f, Midi(88), 0.3f, 0.55f, 1f, 1.6f);
        Bell(b, st + 0.12f, Midi(81), 0.3f, 0.3f, 1f, 1.6f);
        return Finish(Reverb(b, 0.72f, 0.45f, 0.12f, 0.5f));
    }

    /// <summary>A synthesized "mi-aaa-ow" plus a low, uneasy bad-luck sting.</summary>
    static float[] Cat()
    {
        var b = Buf(1.6f);
        float dur = 0.75f;
        var osc = new Osc();
        var f1 = new Svf(); var f2 = new Svf(); var f3 = new Svf();
        int n = Mathf.RoundToInt(dur * SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, k = t / dur;
            float pitch = k < 0.3f ? Mathf.Lerp(560f, 780f, k / 0.3f) : Mathf.Lerp(780f, 430f, (k - 0.3f) / 0.7f);
            pitch *= 1f + 0.012f * Mathf.Sin(t * 2f * Mathf.PI * 6f);
            float src = osc.Next(pitch, Wave.Saw);
            // Formants glide m -> iaa -> ow.
            float open = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f * 0.8f;
            f1.Run(src, Mathf.Lerp(420f, 950f, open), 5f);
            f2.Run(src, Mathf.Lerp(1100f, 1800f, open) - (k > 0.6f ? 500f * (k - 0.6f) : 0f), 6f);
            f3.Run(src, 2900f, 8f);
            float env = AHR(t, 0.05f, dur - 0.3f, 0.25f);
            b[i] += (f1.Band * 1f + f2.Band * 0.7f + f3.Band * 0.25f) * env * 0.9f;
        }
        Tone(b, 0.3f, 1.2f, t => 110f, Wave.Tri, t => AD(t, 0.4f, 0.4f), 0.3f);
        Tone(b, 0.3f, 1.2f, t => 116.5f, Wave.Tri, t => AD(t, 0.4f, 0.4f), 0.3f);
        Tone(b, 0.3f, 1.2f, t => 155.6f, Wave.Sine, t => AD(t, 0.45f, 0.4f), 0.2f);
        return Finish(Reverb(b, 0.82f, 0.4f, 0.2f, 0.7f));
    }

    static float[] Turbo()
    {
        var b = Buf(1.3f);
        Tone(b, 0f, 0.25f, t => Glide(t, 0.1f, 120f, 50f), Wave.Sine, t => AD(t, 0.002f, 0.08f), 0.8f);
        NoiseBurst(b, 0f, 1.1f, t => AD(t, 0.06f, 0.35f), Filt.Band,
                   t => t < 0.3f ? Glide(t, 0.3f, 350f, 3800f) : Glide(t - 0.3f, 0.8f, 3800f, 900f), 1.4f, 1.6f);
        Tone(b, 0f, 0.9f, t => Glide(t, 0.6f, 180f, 640f), Wave.Saw, t => AD(t, 0.05f, 0.25f), 0.22f, Filt.Low, t => 1800f);
        Saturate(b, 1.5f);
        return Finish(Reverb(b, 0.7f, 0.4f, 0.1f, 0.4f));
    }

    // =====================================================================================
    // Match flow and UI
    // =====================================================================================

    static float[] Beep()
    {
        var b = Buf(0.45f);
        Tone(b, 0f, 0.3f, t => 660f, Wave.Square, t => AHR(t, 0.004f, 0.11f, 0.08f), 0.35f, Filt.Low, t => 3500f);
        Tone(b, 0f, 0.3f, t => 1320f, Wave.Sine, t => AHR(t, 0.004f, 0.08f, 0.1f), 0.25f);
        return Finish(Reverb(b, 0.7f, 0.5f, 0.1f, 0.3f));
    }

    static float[] Go()
    {
        var b = Buf(1.3f);
        foreach (int n in new[] { 72, 76, 79, 84 })
        {
            float f = Midi(n);
            Tone(b, 0f, 0.8f, t => f * (1f + 0.003f * Mathf.Sin(t * 30f)), Wave.Square, t => AHR(t, 0.005f, 0.3f, 0.35f), 0.14f, Filt.Low, t => 3800f);
            Bell(b, 0f, f * 2f, 0.4f, 0.16f, 1f, 1.4f);
        }
        Tone(b, 0f, 0.3f, t => Glide(t, 0.1f, 150f, 50f), Wave.Sine, t => AD(t, 0.002f, 0.09f), 0.6f);
        return Finish(Reverb(b, 0.8f, 0.4f, 0.18f, 0.6f));
    }

    static float[] UiClick()
    {
        var b = Buf(0.12f);
        Tone(b, 0f, 0.08f, t => Glide(t, 0.03f, 1300f, 700f), Wave.Sine, t => AD(t, 0.001f, 0.022f), 0.8f);
        NoiseBurst(b, 0f, 0.01f, t => AD(t, 0.0002f, 0.002f), Filt.High, t => 4000f, 0.7f, 0.4f);
        return Finish(b, 0.8f);
    }

    static float[] UiHover()
    {
        var b = Buf(0.08f);
        Tone(b, 0f, 0.06f, t => 1900f, Wave.Sine, t => AD(t, 0.001f, 0.012f), 0.6f);
        return Finish(b, 0.5f);
    }

    /// <summary>Brassy victory fanfare with coins.</summary>
    static float[] Win()
    {
        var b = Buf(3.6f);
        (float t, int n, float d)[] tune = { (0f, 67, 0.12f), (0.13f, 72, 0.12f), (0.26f, 76, 0.12f), (0.39f, 79, 0.28f), (0.7f, 76, 0.12f), (0.83f, 79, 1.9f) };
        foreach (var (st, n, d) in tune) Brass(b, st, Midi(n), d, 0.3f);
        foreach (int n in new[] { 60, 64, 67, 72 }) Brass(b, 0.83f, Midi(n), 1.9f, 0.16f);
        Bell(b, 0.83f, Midi(91), 0.8f, 0.2f, 1f, 1.5f);
        Bell(b, 0.83f, Midi(96), 0.8f, 0.15f, 1f, 1.5f);
        Paste(b, Coins(18, 1f), 0.85f, 0.5f);
        Tone(b, 0.83f, 0.4f, t => Glide(t, 0.1f, 140f, 50f), Wave.Sine, t => AD(t, 0.002f, 0.12f), 0.6f);
        return Finish(Reverb(b, 0.85f, 0.35f, 0.22f, 1f));
    }

    static void Brass(float[] b, float start, float f, float dur, float gain)
    {
        Func<float, float> amp = t => AHR(t, 0.02f, dur, 0.25f);
        Func<float, float> fc = t => 500f + 2600f * Mathf.Min(1f, t / 0.06f) * Mathf.Exp(-t / (dur + 0.4f));
        Func<float, float> vib = t => f * (1f + 0.005f * Mathf.Sin(t * 2f * Mathf.PI * 5.5f) * Mathf.Min(1f, t / 0.3f));
        Tone(b, start, dur + 0.3f, vib, Wave.Saw, amp, gain, Filt.Low, fc, 1.1f);
        Tone(b, start, dur + 0.3f, t => vib(t) * 1.004f, Wave.Saw, amp, gain * 0.8f, Filt.Low, fc, 1.1f);
    }

    /// <summary>Sad trombone: wah, wah, wah, waaaah.</summary>
    static float[] Lose()
    {
        var b = Buf(3.2f);
        (float t, int n, float d)[] tune = { (0f, 67, 0.36f), (0.42f, 66, 0.36f), (0.84f, 65, 0.36f), (1.26f, 64, 1.2f) };
        foreach (var (st, n, d) in tune)
        {
            float f = Midi(n) * 0.5f, len = d;
            bool last = len > 1f;
            Func<float, float> freq = t => f * (1f + (last ? 0.02f * Mathf.Sin(t * 2f * Mathf.PI * 6f) * Mathf.Min(1f, t / 0.4f) : 0f));
            Func<float, float> fc = t => 350f + 1500f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len)) * (last ? 0.8f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI * 6f) : 1f);
            Tone(b, st, len + 0.15f, freq, Wave.Saw, t => AHR(t, 0.03f, len - 0.05f, 0.15f), 0.5f, Filt.Low, fc, 2f);
            Tone(b, st, len + 0.15f, t => freq(t) * 1.006f, Wave.Saw, t => AHR(t, 0.03f, len - 0.05f, 0.15f), 0.35f, Filt.Low, fc, 2f);
        }
        return Finish(Reverb(b, 0.8f, 0.4f, 0.15f, 0.8f));
    }

    // =====================================================================================
    // Storm and engine
    // =====================================================================================

    static float[] Thunder()
    {
        var b = Buf(3.2f);
        NoiseBurst(b, 0f, 0.09f, t => AD(t, 0.001f, 0.03f), Filt.High, t => 1400f, 0.7f, 0.5f);
        var rumble = Buf(3.2f);
        NoiseBurst(rumble, 0f, 3f, t => AD(t, 0.05f, 0.8f), Filt.Low, t => Glide(t, 2f, 380f, 90f), 0.9f, 2.4f);
        float mod = 1f;
        int chunk = SR / 30;
        for (int i = 0; i < rumble.Length; i++) { if (i % chunk == 0) mod = Mathf.Lerp(mod, Rand(0.4f, 1.2f), 0.6f); rumble[i] *= mod; }
        for (int i = 0; i < b.Length; i++) b[i] += rumble[i];
        Saturate(b, 1.4f);
        return Finish(Reverb(b, 0.88f, 0.5f, 0.3f, 1.2f));
    }

    /// <summary>8-second seamless wind loop.</summary>
    static float[] StormLoop()
    {
        float L = 8f, over = 1f;
        var b = Buf(L + over);
        var f = new Svf(); var low = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SR;
            // LFO periods divide the loop length, so the fold lines up.
            float lfo = 0.5f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / 8f) + 0.2f * Mathf.Sin(2f * Mathf.PI * t / 2f + 1f);
            float n = Noise();
            f.Run(n, 260f + 700f * lfo, 1.6f);
            low.Run(n, 110f, 0.7f);
            b[i] = f.Band * (0.4f + 0.6f * lfo) * 1.2f + low.Low * 0.9f;
        }
        return Finish(LoopFold(b, Mathf.RoundToInt(L * SR), SR / 2), 0.8f, false);
    }

    /// <summary>
    /// One-second chuggy cartoon engine at 50 Hz firing; pitched up at runtime with speed.
    /// All tones complete whole cycles in the loop, so it wraps without a click.
    /// </summary>
    static float[] EngineLoop()
    {
        float L = 1f, over = 0.15f;
        var b = Buf(L + over);
        const float f0 = 50f;
        var air = new Svf(); var body = new Svf();
        float[] ph = new float[9];
        for (int k = 0; k < ph.Length; k++) ph[k] = Rand(0f, 6.28f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SR;
            float cyc = (t * f0) % 1f;
            float firing = Mathf.Exp(-cyc * 5f);                     // a pulse each cylinder firing
            float lope = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 25f * t); // 4-stroke unevenness
            float tone = 0f;
            for (int k = 1; k <= ph.Length; k++) tone += Mathf.Sin(2f * Mathf.PI * f0 * k * t + ph[k - 1]) / Mathf.Pow(k, 0.85f);
            body.Run(tone * firing * lope, 900f, 0.8f);
            air.Run(Noise() * firing, 1400f, 1f);
            b[i] = body.Low * 1.0f + air.Band * 0.35f;
        }
        Saturate(b, 2f);
        return Finish(LoopFold(b, Mathf.RoundToInt(L * SR), Mathf.RoundToInt(0.1f * SR)), 0.85f, false);
    }

    // =====================================================================================
    // Music: 124 BPM casino-disco loop, 8 bars, stereo. full=false is the lighter menu mix.
    // =====================================================================================

    const float Bpm = 124f;
    static float Beat => 60f / Bpm;

    static (float[], float[]) Music(bool full)
    {
        rng = new System.Random(full ? 124 : 125);
        int bars = 8;
        float loopLen = bars * 4 * Beat;
        float tail = 2.5f;
        int total = Mathf.CeilToInt((loopLen + tail) * SR);
        var L = new float[total]; var R = new float[total];
        var revL = new float[total]; var revR = new float[total]; // reverb send

        // C | Am | F | G | C | Am | Dm7 | G7
        int[] bass = { 48, 45, 41, 43, 48, 45, 38, 43 };
        int[][] chords =
        {
            new[] { 64, 67, 72 }, new[] { 64, 69, 72 }, new[] { 65, 69, 72 }, new[] { 62, 67, 71 },
            new[] { 64, 67, 72 }, new[] { 64, 69, 72 }, new[] { 65, 69, 72, 74 }, new[] { 65, 71, 74 },
        };

        for (int bar = 0; bar < bars; bar++)
        {
            float b0 = bar * 4 * Beat;

            // Drums
            for (int beat = 0; beat < 4; beat++)
            {
                float t = b0 + beat * Beat;
                if (full || beat % 2 == 0) Kick(L, R, t, full ? 0.95f : 0.45f);
                if (beat % 2 == 1) Snare(L, R, revL, revR, t, full ? 0.55f : 0.3f);
                for (int s = 0; s < 4; s++)
                {
                    float ht = t + s * Beat * 0.25f;
                    bool off = s == 2;
                    if (off) Hat(L, R, ht, 0.32f, 0.11f, 0.25f);
                    else if (full) Hat(L, R, ht, s == 0 ? 0.16f : 0.09f, 0.025f, -0.2f);
                }
            }
            if (bar == bars - 1 && full)
                for (int s = 0; s < 4; s++) Snare(L, R, revL, revR, b0 + 3 * Beat + s * Beat * 0.25f, 0.18f + s * 0.07f);

            // Bass: octave-bouncing funk eighths.
            int r = bass[bar];
            int[] pat = { 0, 12, 0, 12, 0, 12, 7, 10 };
            for (int e = 0; e < 8; e++)
            {
                if (!full && e % 2 == 1) continue;
                float bt = b0 + e * Beat * 0.5f;
                BassNote(L, R, bt, Midi(r + pat[e]), Beat * (e % 2 == 0 ? 0.42f : 0.3f), e % 2 == 0 ? 0.42f : 0.3f);
            }

            // Electric-piano stabs on the off-beats + a soft pad underneath.
            foreach (int n in chords[bar])
            {
                float f = Midi(n);
                for (int beat = 0; beat < 4; beat++) EPiano(L, R, revL, revR, b0 + (beat + 0.5f) * Beat, f, Beat * 0.3f, 0.07f);
                Pad(L, R, revL, revR, b0, f * 0.5f, 4 * Beat, full ? 0.035f : 0.05f);
            }
        }

        // Lead melody (beat, midi, length in beats), vibraphone voice.
        (float b, int n, float l)[] mel =
        {
            (0, 76, .5f), (.5f, 79, .5f), (1, 81, .75f), (2, 79, .5f), (2.5f, 76, .5f), (3, 79, 1),
            (4.5f, 76, .5f), (5, 74, .5f), (5.5f, 72, .5f), (6, 69, 1.5f),
            (8, 72, .5f), (8.5f, 74, .5f), (9, 77, .75f), (10, 76, .5f), (10.5f, 74, .5f), (11, 72, 1),
            (12.5f, 74, .5f), (13, 79, .5f), (13.5f, 81, .5f), (14, 83, 1.5f),
            (16, 76, .5f), (16.5f, 79, .5f), (17, 81, .75f), (18, 79, .5f), (18.5f, 76, .5f), (19, 79, 1),
            (20.5f, 84, .5f), (21, 83, .5f), (21.5f, 81, .5f), (22, 79, 1.5f),
            (24, 77, .5f), (24.5f, 76, .5f), (25, 74, .5f), (25.5f, 77, .5f), (26, 81, 1), (27, 79, .5f), (27.5f, 77, .5f),
            (28, 79, .5f), (28.5f, 74, .5f), (29, 71, .5f), (29.5f, 74, .5f), (30, 79, 1), (31, 77, .5f), (31.5f, 83, .5f),
        };
        foreach (var (bt, n, l) in mel) Vibes(L, R, revL, revR, bt * Beat, Midi(n), l * Beat, full ? 0.16f : 0.13f, 0.3f);

        // Stereo reverb return.
        var rl = Reverb(revL, 0.84f, 0.4f, 1f, 0f, 0);
        var rr = Reverb(revR, 0.84f, 0.4f, 1f, 0f, 23);
        for (int i = 0; i < total; i++) { L[i] += (rl[i] - revL[i]) * 0.35f; R[i] += (rr[i] - revR[i]) * 0.35f; }

        // Fold the tail into the start for a seamless loop, then glue the mix.
        int loopN = Mathf.RoundToInt(loopLen * SR);
        var outL = new float[loopN]; var outR = new float[loopN];
        for (int i = 0; i < total; i++) { outL[i % loopN] += L[i]; outR[i % loopN] += R[i]; }
        float peak = 1e-6f;
        for (int i = 0; i < loopN; i++) peak = Mathf.Max(peak, Mathf.Abs(outL[i]), Mathf.Abs(outR[i]));
        float drive = 1.3f, g = 1.4f / peak, norm = (float)Math.Tanh(drive * 1.4f);
        for (int i = 0; i < loopN; i++)
        {
            outL[i] = (float)Math.Tanh(outL[i] * g * drive) / norm * 0.85f;
            outR[i] = (float)Math.Tanh(outR[i] * g * drive) / norm * 0.85f;
        }
        return (outL, outR);
    }

    static void AddStereo(float[] L, float[] R, int idx, float v, float pan)
    {
        if (idx < 0 || idx >= L.Length) return;
        L[idx] += v * Mathf.Sqrt(0.5f * (1f - pan));
        R[idx] += v * Mathf.Sqrt(0.5f * (1f + pan));
    }

    static void Kick(float[] L, float[] R, float start, float gain)
    {
        var o = new Osc();
        int s0 = Mathf.RoundToInt(start * SR), n = (int)(0.4f * SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float v = o.Next(Glide(t, 0.07f, 160f, 48f), Wave.Sine) * AD(t, 0.001f, 0.16f);
            v += Noise() * AD(t, 0.0002f, 0.002f) * 0.3f;
            v = (float)Math.Tanh(v * 1.8f);
            AddStereo(L, R, s0 + i, v * gain, 0f);
        }
    }

    static void Snare(float[] L, float[] R, float[] sL, float[] sR, float start, float gain)
    {
        var o = new Osc(); var f = new Svf();
        int s0 = Mathf.RoundToInt(start * SR), n = (int)(0.3f * SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            f.Run(Noise(), 2400f, 0.6f);
            float v = (f.Band * 1.2f + f.High * 0.5f) * AD(t, 0.0005f, 0.07f) + o.Next(Glide(t, 0.03f, 240f, 180f), Wave.Tri) * AD(t, 0.0005f, 0.04f) * 0.6f;
            AddStereo(L, R, s0 + i, v * gain, 0.05f);
            AddStereo(sL, sR, s0 + i, v * gain * 0.5f, 0.05f);
        }
    }

    static void Hat(float[] L, float[] R, float start, float gain, float decay, float pan)
    {
        var f = new Svf();
        int s0 = Mathf.RoundToInt(start * SR), n = (int)((decay * 6f + 0.01f) * SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            f.Run(Noise(), 8500f, 0.8f);
            AddStereo(L, R, s0 + i, f.High * AD(t, 0.0004f, decay) * gain, pan);
        }
    }

    static void BassNote(float[] L, float[] R, float start, float freq, float len, float gain)
    {
        var o1 = new Osc(); var o2 = new Osc(); var f = new Svf();
        int s0 = Mathf.RoundToInt(start * SR), n = Mathf.RoundToInt((len + 0.08f) * SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float src = o1.Next(freq, Wave.Saw) * 0.7f + o2.Next(freq * 0.5f, Wave.Square) * 0.35f;
            f.Run(src, 260f + 1700f * Mathf.Exp(-t / 0.05f), 1.4f);
            float v = (float)Math.Tanh(f.Low * 1.6f) * AHR(t, 0.004f, len, 0.06f);
            AddStereo(L, R, s0 + i, v * gain, 0f);
        }
    }

    static void EPiano(float[] L, float[] R, float[] sL, float[] sR, float start, float f, float len, float gain)
    {
        int s0 = Mathf.RoundToInt(start * SR), n = Mathf.RoundToInt((len + 0.25f) * SR);
        float pan = Rand(-0.4f, 0.4f);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float idx = 1.6f * Mathf.Exp(-t / 0.08f);
            float v = Mathf.Sin(2f * Mathf.PI * f * t + idx * Mathf.Sin(2f * Mathf.PI * f * t)) * AHR(t, 0.002f, len, 0.18f) * Mathf.Exp(-t / 0.5f);
            AddStereo(L, R, s0 + i, v * gain, pan);
            AddStereo(sL, sR, s0 + i, v * gain * 0.6f, pan);
        }
    }

    static void Pad(float[] L, float[] R, float[] sL, float[] sR, float start, float f, float len, float gain)
    {
        var a = new Osc(Rand(0f, 1f)); var c = new Osc(Rand(0f, 1f));
        var fl = new Svf(); var fr = new Svf();
        int s0 = Mathf.RoundToInt(start * SR), n = Mathf.RoundToInt((len + 0.3f) * SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float env = AHR(t, 0.25f, len - 0.25f, 0.3f);
            fl.Run(a.Next(f * 0.997f, Wave.Saw), 900f, 0.7f);
            fr.Run(c.Next(f * 1.003f, Wave.Saw), 900f, 0.7f);
            int idx = s0 + i;
            if (idx >= L.Length) break;
            L[idx] += fl.Low * env * gain; R[idx] += fr.Low * env * gain;
            sL[idx] += fl.Low * env * gain * 0.5f; sR[idx] += fr.Low * env * gain * 0.5f;
        }
    }

    static void Vibes(float[] L, float[] R, float[] sL, float[] sR, float start, float f, float len, float gain, float pan)
    {
        int s0 = Mathf.RoundToInt(start * SR), n = Mathf.RoundToInt((len + 0.6f) * SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float trem = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 5f * t);
            float env = AD(t, 0.002f, 0.55f) * (t > len ? Mathf.Exp(-(t - len) / 0.08f) : 1f);
            float v = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 4f * t) * Mathf.Exp(-t / 0.08f)
                    + 0.12f * Mathf.Sin(2f * Mathf.PI * f * 2f * t);
            v *= env * trem * gain;
            AddStereo(L, R, s0 + i, v, pan);
            AddStereo(sL, sR, s0 + i, v * 0.8f, pan);
        }
    }
}
