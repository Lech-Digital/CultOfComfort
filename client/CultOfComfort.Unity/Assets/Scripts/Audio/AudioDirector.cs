using System.Collections.Generic;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Plays the synthesised kit: effects through a small pool of 2D sources (the same clip at most every
    /// <see cref="RepeatGap"/> s, so a wave of defeats doesn't become a drill), and two music loops that crossfade
    /// between day and Night. Sound and music switch off separately (remembered on the device).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        private const float RepeatGap = 0.07f;
        private const float MusicVolume = 0.32f;
        private const float FadeSeconds = 1.5f;
        private const string SoundKey = "audio.sound";
        private const string MusicKey = "audio.music";

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private AudioSource _day, _night;
        private bool _nightMusic;

        public bool SoundOn { get; private set; } = true;

        public bool MusicOn { get; private set; } = true;

        private void Awake()
        {
            SoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            MusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            foreach (var name in SoundBook.All)
            {
                var clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip != null)
                {
                    _clips[name] = clip;
                }
            }

            for (var i = 0; i < 8; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _pool.Add(source);
            }

            _day = Loop(SoundBook.DayMusic);
            _night = Loop(SoundBook.NightMusic);
            if (_day != null)
            {
                _day.volume = MusicOn ? MusicVolume : 0f;
                _day.Play();
            }
        }

        private AudioSource Loop(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null)
            {
                return null;
            }

            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>One effect, now (ignored when sound is off, the clip is missing, or it just played).</summary>
        public void Play(string name, float volume = 1f)
        {
            if (!SoundOn || string.IsNullOrEmpty(name) || !_clips.TryGetValue(name, out var clip))
            {
                return;
            }

            var now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(name, out var last) && now - last < RepeatGap)
            {
                return;
            }

            _lastPlayed[name] = now;
            var source = _pool.Find(s => !s.isPlaying) ?? _pool[0];
            source.pitch = 1f + Random.Range(-0.04f, 0.04f); // a little life: no two clonks quite alike
            source.PlayOneShot(clip, volume);
        }

        /// <summary>Day or Night music (crossfades in <see cref="Update"/>).</summary>
        public void SetNight(bool night)
        {
            _nightMusic = night;
            var target = night ? _night : _day;
            if (target != null && !target.isPlaying)
            {
                target.Play();
            }
        }

        public void SetSound(bool on)
        {
            SoundOn = on;
            PlayerPrefs.SetInt(SoundKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetMusic(bool on)
        {
            MusicOn = on;
            PlayerPrefs.SetInt(MusicKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            var step = Time.unscaledDeltaTime * MusicVolume / FadeSeconds;
            Fade(_day, MusicOn && !_nightMusic ? MusicVolume : 0f, step);
            Fade(_night, MusicOn && _nightMusic ? MusicVolume : 0f, step);
        }

        private static void Fade(AudioSource source, float target, float step)
        {
            if (source == null)
            {
                return;
            }

            source.volume = Mathf.MoveTowards(source.volume, target, step);
            if (source.volume <= 0f && target <= 0f && source.isPlaying)
            {
                source.Pause();
            }
            else if (target > 0f && !source.isPlaying)
            {
                source.UnPause();
                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
        }
    }
}
