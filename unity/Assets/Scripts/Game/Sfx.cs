// Effets sonores (fichiers WAV synthétisés, dans Resources/Sounds).
using System.Collections.Generic;
using UnityEngine;

namespace Menteur.Game
{
    public static class Sfx
    {
        static AudioSource source, ambience;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly List<KeyValuePair<float, string>> queue = new List<KeyValuePair<float, string>>();
        static bool ambienceWanted;

        public static bool On
        {
            get => PlayerPrefs.GetInt("sound", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("sound", value ? 1 : 0);
                if (ambience == null) return;
                if (!value) ambience.Stop();
                else if (ambienceWanted) StartAmbience();
            }
        }

        public static void Init(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            ambience = host.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            ambience.loop = true;
            ambience.volume = 0.3f;
        }

        static AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out var c)) clips[name] = c = Resources.Load<AudioClip>("Sounds/" + name);
            return c;
        }

        public static void Play(string name, float volume = 1f)
        {
            if (!On || source == null) return;
            var c = Clip(name);
            if (c != null) source.PlayOneShot(c, volume);
        }

        /// <summary>Joue un son après un délai (calé sur une animation).</summary>
        public static void Later(float delay, string name) =>
            queue.Add(new KeyValuePair<float, string>(Time.time + delay, name));

        public static void Tick()
        {
            for (var i = queue.Count - 1; i >= 0; i--)
            {
                if (Time.time < queue[i].Key) continue;
                Play(queue[i].Value);
                queue.RemoveAt(i);
            }
        }

        public static void StartAmbience()
        {
            ambienceWanted = true;
            if (!On || ambience == null || ambience.isPlaying) return;
            var c = Clip("ambience");
            if (c == null) return;
            ambience.clip = c;
            ambience.Play();
        }

        public static void StopAmbience()
        {
            ambienceWanted = false;
            if (ambience != null) ambience.Stop();
        }
    }
}
