using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ResourceSensor
{
    /// <summary>
    /// Mod localization. English defaults live in the STRINGS class; if the mod has
    /// translations/&lt;language code&gt;.po for the current game language, its strings
    /// are written straight into the matching STRINGS fields (key = "Namespace.STRINGS.GROUP.FIELD").
    ///
    /// The game's own helpers (Localization.RegisterForTranslation / LocString.CreateLocStringKeys)
    /// are deliberately not used: in this mod they threw a NullReferenceException and left
    /// everything in English. Applying the strings ourselves needs nothing from the game but
    /// the .po reader. The .po file sits in a subfolder, so the game does not mistake the mod
    /// for a language pack.
    /// </summary>
    internal static class ModLocalization
    {
        public static void Init(Type root, string modPath, string logTag)
        {
            Dictionary<string, string> strings = LoadCurrent(modPath, logTag);
            if (strings == null) return;

            try
            {
                int applied = Apply(root, root.Namespace + "." + root.Name, strings);
                Debug.Log("[" + logTag + "] translated strings: " + applied);
            }
            catch (Exception e)
            {
                // A broken translation must never break the mod: English stays in place.
                Debug.LogWarning("[" + logTag + "] translation not applied: " + e);
            }
        }

        /// <summary>
        /// Reads translations/&lt;current language&gt;.po from the mod folder.
        /// Returns null when the game language has no translation file.
        /// </summary>
        public static Dictionary<string, string> LoadCurrent(string modPath, string logTag)
        {
            try
            {
                Localization.Locale locale = Localization.GetLocale();
                string code = locale != null ? locale.Code : null;
                if (string.IsNullOrEmpty(code))
                {
                    Debug.Log("[" + logTag + "] no game language set, using English");
                    return null;
                }
                if (string.IsNullOrEmpty(modPath))
                {
                    Debug.LogWarning("[" + logTag + "] mod folder unknown, using English");
                    return null;
                }

                string po = Path.Combine(Path.Combine(modPath, "translations"), code + ".po");
                if (!File.Exists(po))
                {
                    Debug.Log("[" + logTag + "] no translation for '" + code + "', using English");
                    return null;
                }

                Dictionary<string, string> strings = Localization.LoadStringsFile(po, false);
                Debug.Log("[" + logTag + "] loaded translation: " + code + " (" + (strings != null ? strings.Count : 0) + " entries)");
                return strings;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[" + logTag + "] translation file not read: " + e);
                return null;
            }
        }

        // Walks STRINGS and its nested classes; every LocString field whose key is in the
        // translation gets the translated text.
        private static int Apply(Type type, string path, Dictionary<string, string> strings)
        {
            int count = 0;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.FieldType != typeof(LocString) || field.IsInitOnly || field.IsLiteral) continue;
                string key = path + "." + field.Name;
                string value;
                if (strings.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                {
                    field.SetValue(null, new LocString(value, key));
                    count++;
                }
            }

            foreach (Type nested in type.GetNestedTypes(flags))
                count += Apply(nested, path + "." + nested.Name, strings);

            return count;
        }
    }
}
