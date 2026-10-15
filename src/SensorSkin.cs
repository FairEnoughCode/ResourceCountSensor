using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ResourceSensor
{
    /// <summary>
    /// Sensor look: the germ sensor model without germ spots, with a slower beam.
    /// Colors change through per-part multipliers (SetSymbolTint): gold parts get a
    /// "to silver" multiplier, green parts a "to blue" one. Multipliers are computed once from the texture.
    /// </summary>
    public static class SensorSkin
    {
        public const float ANIM_SPEED = 1f / 3f;

        // Initial model when the building is created (always present);
        // the real one is swapped in later, in ApplyToDef, once all game buildings exist
        public static string PickAnim() { return "wattage_sensor_kanim"; }

        private static KAnimFile resolved;

        /// <summary>The model of the Germ Sensor building (recolored at runtime).</summary>
        public static KAnimFile ResolveAnim()
        {
            if (resolved != null) return resolved;
            KAnimFile f = GermSensorAnim();
            resolved = f;
            if (f == null) Debug.LogWarning("[ResourceSensor] model: germ sensor model not found, keeping the old one");
            return f;
        }

        public static KAnimFile GermSensorAnim()
        {
            try
            {
                BuildingDef d = Assets.GetBuildingDef(LogicDiseaseSensorConfig.ID);
                if (d != null && d.AnimFiles != null && d.AnimFiles.Length > 0) return d.AnimFiles[0];
            }
            catch (Exception e) { Debug.LogWarning("[ResourceSensor] germ sensor def: " + e.Message); }
            return null;
        }

        /// <summary>Swaps the building model: menu icon, placement preview, construction, finished building.</summary>
        public static void ApplyToDef()
        {
            try
            {
                KAnimFile anim = ResolveAnim();
                if (anim == null) return;
                BuildingDef def = Assets.GetBuildingDef(ResourceCountSensorConfig.ID);
                if (def == null) return;
                KAnimFile[] files = new KAnimFile[] { anim };
                def.AnimFiles = files;
                GameObject[] gos = { def.BuildingComplete, def.BuildingPreview, def.BuildingUnderConstruction };
                for (int i = 0; i < gos.Length; i++)
                {
                    if (gos[i] == null) continue;
                    KBatchedAnimController kbac = gos[i].GetComponent<KBatchedAnimController>();
                    if (kbac != null) kbac.AnimFiles = files;
                }
                Sprite icon = MenuIcon();
                if (icon != null) def.UISprite = icon;
            }
            catch (Exception e) { Debug.LogWarning("[ResourceSensor] ApplyToDef: " + e); }
        }

        // ---------------- What to hide and how to animate ----------------

        // Names from the germ sensor's own code (LogicDiseaseSensor: TINT_SYMBOL = "germs",
        // ON_ANIMS = on_pre, on_loop; OFF_ANIMS = on_pst, off). They are private there, so
        // they are written out here rather than read with reflection. If Klei rename them in
        // the model, the sensor would show a still frame and the germ layer: easy to spot.
        private static readonly List<KAnimHashedString> germSymbols = new List<KAnimHashedString>
        {
            new KAnimHashedString("germs"),
            new KAnimHashedString("germ")
        };
        private static readonly HashedString[] onAnims = { "on_pre", "on_loop" };
        private static readonly HashedString[] offAnims = { "on_pst", "off" };

        public static List<KAnimHashedString> GermSymbols() { return germSymbols; }
        public static HashedString[] OnAnims() { return onAnims; }
        public static HashedString[] OffAnims() { return offAnims; }

        // ---------------- Build menu icon ----------------

        private static Sprite menuIcon;
        private static bool menuIconTried;

        /// <summary>The germ sensor icon, recolored with the same multipliers as the building.</summary>
        public static Sprite MenuIcon()
        {
            if (menuIconTried) return menuIcon;
            menuIconTried = true;
            try
            {
                KAnimFile anim = ResolveAnim();
                if (anim == null) return null;
                Dictionary<int, Color> tints = SymbolTints(anim);
                Color tint, ringTint;
                if (!tints.TryGetValue(new KAnimHashedString("ui").HashValue, out tint))
                {
                    Debug.Log("[ResourceSensor] icon: no multiplier for the ui part, leaving it as is");
                    return null;
                }
                if (!tints.TryGetValue(new KAnimHashedString("screenbg").HashValue, out ringTint)) ringTint = tint;
                Sprite src = Def.GetUISpriteFromMultiObjectAnim(anim, "ui", false, "");
                if (src == null || src.texture == null) return null;

                Texture2D full = ReadableCopy(src.texture);
                Rect r = src.textureRect;
                int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
                int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
                Color[] px = full.GetPixels(x0, y0, w, h);
                UnityEngine.Object.Destroy(full);
                // The icon is one flat picture, so each pixel gets the multiplier of the part it
                // belongs to on the building: the same colors as the building itself
                for (int i = 0; i < px.Length; i++)
                {
                    Color c = px[i];
                    float hue, sat, val;
                    Color.RGBToHSV(c, out hue, out sat, out val);
                    hue *= 360f;
                    bool green = hue > 75f && hue <= 170f && sat > 0.10f;
                    // The radar beam (pure saturated green, hue ~115; the rings are ~130) and the
                    // signal lamp (top left corner; it shows the green/red logic signal) stay green,
                    // as on the building. Pixel rows go from the bottom up.
                    bool beam = sat >= 0.8f && hue < 124f;
                    bool lampCorner = (i % w) < w * 0.5f && (i / w) > h * 0.7f;
                    Color m;
                    if (green && (beam || lampCorner)) continue;
                    else if (green && sat > 0.5f) m = ringTint;  // screen rings
                    else m = tint;                               // body and glass
                    px[i] = new Color(Mathf.Clamp01(c.r * m.r), Mathf.Clamp01(c.g * m.g), Mathf.Clamp01(c.b * m.b), c.a);
                }
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                tex.Apply();
                Vector2 pivot = new Vector2(src.pivot.x / Mathf.Max(1f, src.rect.width), src.pivot.y / Mathf.Max(1f, src.rect.height));
                menuIcon = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit);
                menuIcon.name = "resourcesensor_ui";
            }
            catch (Exception e) { Debug.LogWarning("[ResourceSensor] icon: " + e); menuIcon = null; }
            return menuIcon;
        }

        // ---------------- Recoloring parts with multipliers ----------------

        private static Dictionary<int, Color> symbolTints;
        // Overall brightness of the body's "silver" multiplier: lower means darker glass,
        // but also a darker rim (closer to steel). 1 = light silver
        private const float SILVER_BRIGHTNESS = 0.8f;
        // Screen rings (the "screenbg" part: thin rings over the glass, the glass itself is in
        // the body): how much brighter than the original green, so blue rings on the dark blue
        // glass stand out as much as the green ones do on the germ sensor
        private const float RING_BRIGHTNESS = 1.2f;
        // Signal lamps (light, light_bloom); the radar beam (radartrail_bloom) is not a lamp
        private static readonly string[] LAMP_WORDS = { "light", "bulb", "led", "lamp", "indicator", "signal" };

        /// <summary>Color multiplier for each model part (computed once).</summary>
        public static Dictionary<int, Color> SymbolTints(KAnimFile anim)
        {
            if (symbolTints != null) return symbolTints;
            symbolTints = new Dictionary<int, Color>();
            StringBuilder log = new StringBuilder();
            try
            {
                if (anim == null || anim.GetData() == null || anim.GetData().build == null) { log.AppendLine("no build"); return symbolTints; }
                KAnim.Build build = anim.GetData().build;
                Texture2D src = build.GetTexture(0);
                if (src == null) { log.AppendLine("no texture"); return symbolTints; }
                Texture2D tex = ReadableCopy(src);
                Color32[] px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                UnityEngine.Object.Destroy(tex);

                HashSet<int> germ = new HashSet<int>();
                List<KAnimHashedString> gs = GermSymbols();
                for (int i = 0; i < gs.Count; i++) germ.Add(gs[i].HashValue);

                // UVs may count from the top or the bottom: pick the variant where parts land better on opaque pixels
                bool flip = Coverage(px, w, h, build, true) > Coverage(px, w, h, build, false);
                log.AppendLine("texture " + w + "x" + h + " flip=" + flip);

                for (int i = 0; i < build.symbols.Length; i++)
                {
                    KAnim.Build.Symbol s = build.symbols[i];
                    string name = HashCache.Get().Get(s.hash.HashValue);
                    if (germ.Contains(s.hash.HashValue)) { log.AppendLine(name + ": germs, hidden"); continue; }
                    bool lamp = IsLampName(name);
                    if (lamp) { log.AppendLine(name + ": lamp, untouched (flags=" + s.flags + ")"); continue; }

                    if (name != null && name.ToLowerInvariant().Contains("radartrail"))
                    {
                        log.AppendLine(name + ": beam, kept as on the germ sensor");
                        continue;
                    }

                    // Collect the pixels of all frames of the part
                    double gr = 0, gg = 0, gb = 0, er = 0, eg = 0, eb = 0; int nGold = 0, nGreen = 0, nOpaque = 0;
                    double ar = 0, ag = 0, ab = 0; float vMin = 1f, vMax = 0f;
                    for (int f = 0; f < s.numFrames; f++)
                    {
                        KAnim.Build.SymbolFrameInstance fr = s.GetFrame(f);
                        int x0, y0, x1, y1;
                        Rect(fr, w, h, flip, out x0, out y0, out x1, out y1);
                        for (int y = y0; y < y1; y++)
                            for (int x = x0; x < x1; x++)
                            {
                                Color32 c = px[y * w + x];
                                if (c.a < 40) continue;
                                nOpaque++;
                                ar += c.r; ag += c.g; ab += c.b;
                                float hue, sat, val;
                                Color.RGBToHSV(c, out hue, out sat, out val);
                                if (val < vMin) vMin = val;
                                if (val > vMax) vMax = val;
                                hue *= 360f;
                                if (hue >= 35f && hue <= 75f && sat > 0.38f && val > 0.25f) { nGold++; gr += c.r; gg += c.g; gb += c.b; }
                                else if (hue > 75f && hue <= 170f && sat > 0.10f) { nGreen++; er += c.r; eg += c.g; eb += c.b; }
                            }
                    }
                    if (nOpaque == 0) { log.AppendLine(name + ": empty"); continue; }
                    Color avgAll = new Color((float)(ar / nOpaque / 255), (float)(ag / nOpaque / 255), (float)(ab / nOpaque / 255));
                    log.AppendLine(name + ": average " + avgAll + ", brightness " + vMin.ToString("0.00") + "–" + vMax.ToString("0.00"));


                    Color tint;
                    string what;
                    if (nGold > nOpaque * 0.25f)
                    {
                        // Gold -> silver: same brightness, no yellow, slightly cooler
                        Color avg = new Color((float)(gr / nGold / 255), (float)(gg / nGold / 255), (float)(gb / nGold / 255));
                        float lum = 0.30f * avg.r + 0.59f * avg.g + 0.11f * avg.b;
                        float t = Mathf.Clamp01(lum * 1.05f + 0.05f);
                        tint = Ratio(new Color(t * 0.95f * SILVER_BRIGHTNESS, t * 0.98f * SILVER_BRIGHTNESS, t * 1.04f * SILVER_BRIGHTNESS), avg);
                        what = "gold -> silver";
                    }
                    else if (nGreen > nOpaque * 0.15f)
                    {
                        // Green -> blue: same brightness, hue turned to ~210°
                        Color avg = new Color((float)(er / nGreen / 255), (float)(eg / nGreen / 255), (float)(eb / nGreen / 255));
                        float hue, sat, val;
                        Color.RGBToHSV(avg, out hue, out sat, out val);
                        // (the radar beam never gets here: it is skipped above)
                        string lname = name != null ? name.ToLowerInvariant() : "";
                        if (lname.Contains("screenbg"))
                        {
                            // Screen rings: light blue
                            tint = Ratio(Color.HSVToRGB(205f / 360f, sat * 0.85f, Mathf.Min(1f, val * RING_BRIGHTNESS)), avg);
                            what = "green -> light blue";
                        }
                        else
                        {
                            tint = Ratio(Color.HSVToRGB(210f / 360f, sat, val), avg);
                            what = "green -> blue";
                        }
                    }
                    else { log.AppendLine(name + ": untouched (gold " + nGold + ", green " + nGreen + " of " + nOpaque + ")"); continue; }

                    symbolTints[s.hash.HashValue] = tint;
                    log.AppendLine(name + ": " + what + " (gold " + nGold + ", green " + nGreen + " of " + nOpaque + ") tint=" + tint);
                }
            }
            catch (Exception e) { log.AppendLine("error: " + e); }
            if (log.ToString().Contains("error:")) Debug.LogWarning("[ResourceSensor] recolor:\n" + log);
            return symbolTints;
        }

        private static Color Ratio(Color target, Color avg)
        {
            return new Color(
                Mathf.Clamp(target.r / Mathf.Max(avg.r, 0.02f), 0f, 6f),
                Mathf.Clamp(target.g / Mathf.Max(avg.g, 0.02f), 0f, 6f),
                Mathf.Clamp(target.b / Mathf.Max(avg.b, 0.02f), 0f, 6f), 1f);
        }

        private static bool IsLampName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string l = name.ToLowerInvariant();
            for (int i = 0; i < LAMP_WORDS.Length; i++) if (l.Contains(LAMP_WORDS[i])) return true;
            return false;
        }

        private static Texture2D ReadableCopy(Texture2D src)
        {
            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private static void Rect(KAnim.Build.SymbolFrameInstance fr, int w, int h, bool flip, out int x0, out int y0, out int x1, out int y1)
        {
            float u0 = Mathf.Min(fr.uvMin.x, fr.uvMax.x), u1 = Mathf.Max(fr.uvMin.x, fr.uvMax.x);
            float v0 = Mathf.Min(fr.uvMin.y, fr.uvMax.y), v1 = Mathf.Max(fr.uvMin.y, fr.uvMax.y);
            if (flip) { float a = 1f - v1, b = 1f - v0; v0 = a; v1 = b; }
            x0 = Mathf.Clamp(Mathf.FloorToInt(u0 * w), 0, w); x1 = Mathf.Clamp(Mathf.CeilToInt(u1 * w), 0, w);
            y0 = Mathf.Clamp(Mathf.FloorToInt(v0 * h), 0, h); y1 = Mathf.Clamp(Mathf.CeilToInt(v1 * h), 0, h);
        }

        private static double Coverage(Color32[] px, int w, int h, KAnim.Build build, bool flip)
        {
            long hit = 0, total = 0;
            for (int i = 0; i < build.symbols.Length; i++)
                for (int f = 0; f < build.symbols[i].numFrames; f++)
                {
                    int x0, y0, x1, y1;
                    Rect(build.symbols[i].GetFrame(f), w, h, flip, out x0, out y0, out x1, out y1);
                    for (int y = y0; y < y1; y += 2)
                        for (int x = x0; x < x1; x += 2) { total++; if (px[y * w + x].a > 20) hit++; }
                }
            return total > 0 ? (double)hit / total : 0;
        }
    }
}
