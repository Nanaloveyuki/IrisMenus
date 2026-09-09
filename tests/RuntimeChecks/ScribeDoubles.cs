using System.Collections.Generic;

namespace Verse
{
    public class ModSettings { public virtual void ExposeData() { } }
    public static class Translation { public static string Translate(this string text) => text; }
    public enum LookMode { Value }
    public static class Scribe_Values
    {
        public static bool Loading;
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
        public static void Look<T>(ref T value, string key, T defaultValue = default)
        {
            if (Loading) value = Data.TryGetValue(key, out object stored) ? (T)stored : defaultValue;
            else Data[key] = value;
        }
    }
    public static class Scribe_Collections
    {
        public static void Look<K, V>(ref Dictionary<K, V> values, string key, LookMode keyMode, LookMode valueMode)
        {
            if (Scribe_Values.Loading)
                values = Scribe_Values.Data.TryGetValue(key, out object stored) && stored != null ? new Dictionary<K, V>((Dictionary<K, V>)stored) : null;
            else Scribe_Values.Data[key] = values == null ? null : new Dictionary<K, V>(values);
        }
        public static void Look<T>(ref List<T> values, string key, LookMode mode)
        {
            if (Scribe_Values.Loading)
                values = Scribe_Values.Data.TryGetValue(key, out object stored) && stored != null ? new List<T>((List<T>)stored) : null;
            else Scribe_Values.Data[key] = values == null ? null : new List<T>(values);
        }
    }
}

namespace RimWorld
{
    public class Dialog_ModSettings : Verse.Window
    {
        private Verse.Mod mod;
        public Dialog_ModSettings(Verse.Mod mod) { this.mod = mod; }
        public Verse.Mod Owner => mod;
    }
    public abstract class MainButtonWorker { public abstract void Activate(); }
}
