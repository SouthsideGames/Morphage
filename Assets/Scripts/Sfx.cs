using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Synthesized SFX (no audio assets), ported from the prototype's WebAudio Sfx class.
    /// Clips are baked once at init and played via PlayOneShot. Slides/filters are approximated.
    /// </summary>
    public static class Sfx
    {
        static bool _enabled = true;
        public static bool Enabled
        {
            get => _enabled;
            set { _enabled = value; if (!value) _musicSrc?.Stop(); else { ApplyVol(); StartMusic(); } }
        }

        static AudioSource _sfxSrc, _musicSrc;
        const int SR = 44100;

        // mixer
        static float _masterVol = 0.7f, _musicVol = 0.5f, _sfxVol = 0.8f;
        public static float MasterVol => _masterVol;
        public static float MusicVol => _musicVol;
        public static float SfxVol => _sfxVol;
        public static void SetMaster(float v) { _masterVol = v; ApplyVol(); }
        public static void SetMusic(float v) { _musicVol = v; ApplyVol(); }
        public static void SetSfx(float v) { _sfxVol = v; ApplyVol(); }
        static void ApplyVol()
        {
            if (_sfxSrc != null) _sfxSrc.volume = _masterVol * _sfxVol;
            if (_musicSrc != null) _musicSrc.volume = _masterVol * _musicVol;
        }

        // music
        static AudioClip _drone;
        static AudioClip[] _noteClips, _noteLowClips; // pre-baked melody notes (don't allocate per-note)
        static bool _musicOn;
        static float _noteTimer;
        static readonly float[] _scale = { 220f, 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f };

        static AudioClip _hit, _death, _pickup, _levelup, _mutate, _boss, _hurt, _over, _cast, _dash, _evolve, _win;
        static float _lastHit;

        // Real recorded clips (Kenney sci-fi pack) loaded from Resources/Audio; null => fall back to synth.
        // Arrays hold variants that are randomised per play so repeated hits/deaths don't sound identical.
        static AudioClip[] _hitV, _deathV, _castV;
        static AudioClip _hurtClip, _dashClip, _bossClip, _mutateClip;

        static AudioClip LoadClip(string path) => Resources.Load<AudioClip>(path);
        static AudioClip[] LoadClips(params string[] paths)
        {
            var list = new System.Collections.Generic.List<AudioClip>();
            foreach (var p in paths) { var c = Resources.Load<AudioClip>(p); if (c != null) list.Add(c); }
            return list.Count > 0 ? list.ToArray() : null;
        }
        static AudioClip Pick(AudioClip[] a) => a != null && a.Length > 0 ? a[Random.Range(0, a.Length)] : null;

        enum Wave { Sine, Square, Saw, Tri }

        public static void Init(AudioSource sfx, AudioSource music)
        {
            _sfxSrc = sfx; _sfxSrc.spatialBlend = 0f; _sfxSrc.playOnAwake = false;
            _musicSrc = music; _musicSrc.spatialBlend = 0f; _musicSrc.playOnAwake = false;
            _drone = Clip(Drone(110f, 2f, 0.5f)); // looping low sine bed
            _noteClips = new AudioClip[_scale.Length];
            _noteLowClips = new AudioClip[_scale.Length];
            for (int i = 0; i < _scale.Length; i++)
            {
                _noteClips[i] = Clip(NoteBuf(_scale[i], 2.2f, 0.14f));
                _noteLowClips[i] = Clip(NoteBuf(_scale[i] * 0.5f, 2.6f, 0.10f));
            }

            _hit     = Clip(NoiseBuf(0.045f, 0.10f, 2800f));
            _death   = Clip(Mix(NoiseBuf(0.16f, 0.26f, 1400f), ToneBuf(150f, 0.16f, Wave.Saw, 0.10f, 55f)));
            _pickup  = Clip(ToneBuf(680f, 0.07f, Wave.Tri, 0.10f, 1020f));
            _levelup = Clip(Seq(0.07f, 0.16f, Wave.Tri, 0.14f, 523f, 659f, 784f, 1047f));
            _mutate  = Clip(Seq(0.06f, 0.18f, Wave.Sine, 0.13f, 784f, 1047f, 1319f));
            _boss    = Clip(Mix(NoiseBuf(0.5f, 0.4f, 650f), ToneBuf(68f, 0.6f, Wave.Saw, 0.22f, 42f)));
            _hurt    = Clip(Mix(NoiseBuf(0.10f, 0.26f, 900f), ToneBuf(190f, 0.1f, Wave.Square, 0.13f, 90f)));
            _over    = Clip(Seq(0.12f, 0.26f, Wave.Saw, 0.15f, 440f, 330f, 247f, 165f));
            _cast    = Clip(ToneBuf(520f, 0.09f, Wave.Saw, 0.10f, 760f));
            _dash    = Clip(ToneBuf(300f, 0.12f, Wave.Sine, 0.09f, 820f));
            _evolve  = Clip(Seq(0.08f, 0.2f, Wave.Sine, 0.15f, 659f, 880f, 1175f, 1568f));
            _win     = Clip(Seq(0.11f, 0.22f, Wave.Tri, 0.16f, 523f, 659f, 784f, 1047f, 1319f));

            // Swap in the Kenney sci-fi recordings where they fit; musical cues (levelup/evolve/win/
            // over/pickup) keep the synth arpeggios since the pack has no melodic equivalent.
            _hitV       = LoadClips("Audio/impactMetal_000", "Audio/impactMetal_001", "Audio/impactMetal_002");
            _deathV     = LoadClips("Audio/explosionCrunch_000", "Audio/explosionCrunch_001", "Audio/explosionCrunch_002");
            _castV      = LoadClips("Audio/laserSmall_000", "Audio/laserSmall_001");
            _hurtClip   = LoadClip("Audio/forceField_000");
            _dashClip   = LoadClip("Audio/thrusterFire_000");
            _bossClip   = LoadClip("Audio/lowFrequency_explosion_000");
            _mutateClip = LoadClip("Audio/slime_000");

            ApplyVol();
            StartMusic();
        }

        static void Play(AudioClip c)
        {
            if (!_enabled || _sfxSrc == null || c == null) return;
            _sfxSrc.PlayOneShot(c);
        }

        // ---- ambient music: looping drone + sparse scheduled notes ----
        public static void StartMusic()
        {
            if (!_enabled || _musicSrc == null || _drone == null || _musicOn) return;
            _musicSrc.clip = _drone; _musicSrc.loop = true; _musicSrc.Play();
            _musicOn = true; _noteTimer = 1f;
        }

        /// <summary>Call once per frame (real dt) to schedule the ambient melody.</summary>
        public static void Tick(float dt)
        {
            if (!_enabled || !_musicOn || _musicSrc == null) return;
            _noteTimer -= dt;
            if (_noteTimer <= 0f)
            {
                _noteTimer = 0.9f + Random.value * 0.8f;
                int idx = Random.Range(0, _scale.Length);
                _musicSrc.PlayOneShot(_noteClips[idx], 1f);
                if (Random.value < 0.5f) _musicSrc.PlayOneShot(_noteLowClips[idx], 1f);
            }
        }

        public static void Hit()
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastHit < 0.032f) return; // prototype rate-limits hits
            _lastHit = now; Play(Pick(_hitV) ?? _hit);
        }
        public static void Death()   => Play(Pick(_deathV) ?? _death);
        public static void Pickup()  => Play(_pickup);
        public static void LevelUp() => Play(_levelup);
        public static void Mutate()  => Play(_mutateClip != null ? _mutateClip : _mutate);
        public static void Boss()    => Play(_bossClip != null ? _bossClip : _boss);
        public static void Hurt()    => Play(_hurtClip != null ? _hurtClip : _hurt);
        public static void Over()    => Play(_over);
        public static void Cast()    => Play(Pick(_castV) ?? _cast);
        public static void Dash()    => Play(_dashClip != null ? _dashClip : _dash);
        public static void Evolve()  => Play(_evolve);
        public static void Win()     => Play(_win);

        // ---- tiny synth ----
        static float Osc(Wave w, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Saw:    return 2f * p - 1f;
                case Wave.Tri:    return 4f * Mathf.Abs(p - 0.5f) - 1f;
                default:          return Mathf.Sin(p * Mathf.PI * 2f);
            }
        }

        static float[] ToneBuf(float freq, float dur, Wave w, float vol, float slideTo = -1f)
        {
            int n = Mathf.Max(1, (int)(SR * dur));
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float f = slideTo > 0f ? Mathf.Lerp(freq, slideTo, k) : freq;
                phase += f / SR;
                buf[i] = Osc(w, phase) * vol * Mathf.Pow(0.0008f, k);
            }
            return buf;
        }

        static float[] NoiseBuf(float dur, float vol, float filterFreq)
        {
            int n = Mathf.Max(1, (int)(SR * dur));
            var buf = new float[n];
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * filterFreq / SR);
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = Random.value * 2f - 1f;
                y += a * (white - y);
                buf[i] = y * vol * (1f - (float)i / n);
            }
            return buf;
        }

        static float[] Seq(float gap, float dur, Wave w, float vol, params float[] freqs)
        {
            int total = (int)(SR * (gap * (freqs.Length - 1) + dur)) + 1;
            var buf = new float[total];
            for (int k = 0; k < freqs.Length; k++)
                AddInto(buf, ToneBuf(freqs[k], dur, w, vol), (int)(SR * gap * k));
            return buf;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var buf = new float[Mathf.Max(a.Length, b.Length)];
            AddInto(buf, a, 0);
            AddInto(buf, b, 0);
            return buf;
        }

        static void AddInto(float[] dst, float[] src, int offset)
        {
            for (int i = 0; i < src.Length && offset + i < dst.Length; i++) dst[offset + i] += src[i];
        }

        static float[] Drone(float freq, float dur, float vol)
        {
            int n = Mathf.Max(1, (int)(SR * dur));
            var buf = new float[n];
            for (int i = 0; i < n; i++) buf[i] = Mathf.Sin(2f * Mathf.PI * freq * i / SR) * vol;
            return buf;
        }

        static float[] NoteBuf(float freq, float dur, float vol)
        {
            int n = Mathf.Max(1, (int)(SR * dur));
            var buf = new float[n];
            const float atk = 0.4f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float env = t < atk ? t / atk : Mathf.Exp(-(t - atk) * 2.2f);
                buf[i] = Mathf.Sin(2f * Mathf.PI * freq * i / SR) * vol * env;
            }
            return buf;
        }

        static AudioClip Clip(float[] samples)
        {
            var c = AudioClip.Create("sfx", samples.Length, 1, SR, false);
            c.SetData(samples, 0);
            return c;
        }
    }
}
