using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ResourceSensor
{
    /// <summary>
    /// Fixes for the game's button side panel (ButtonMenuSideScreen):
    /// 1. The game shows every button of the building and ignores SidescreenEnabled,
    ///    so our buttons that are off are hidden here, and so is a row left with no
    ///    visible buttons.
    /// 2. Within one row the game lays buttons out in an unpredictable order, so ours
    ///    are reordered by descending ButtonSideScreenSortOrder.
    /// Our buttons are found on screen by their text (see Collect), without reading the
    /// panel's private lists.
    /// </summary>
    public static class ButtonOrder
    {

        public static void Apply(ButtonMenuSideScreen screen)
        {
            try
            {
                List<KeyValuePair<SensorButton, GameObject>> ours = Collect(screen);
                if (ours.Count == 0) return;

                // 1. Hide the buttons that are off, then rows with nothing left in them
                HashSet<Transform> rowsToCheck = new HashSet<Transform>();
                for (int i = 0; i < ours.Count; i++)
                {
                    GameObject go = ours[i].Value;
                    bool on = ours[i].Key.SidescreenEnabled();
                    if (go.activeSelf != on) go.SetActive(on);
                    if (go.transform.parent != null) rowsToCheck.Add(go.transform.parent);
                }
                foreach (Transform row in rowsToCheck)
                {
                    if (row == screen.buttonContainer) continue; // the column itself, not a row
                    bool any = false;
                    for (int c = 0; c < row.childCount; c++)
                        if (row.GetChild(c).gameObject.activeSelf) { any = true; break; }
                    if (row.gameObject.activeSelf != any) row.gameObject.SetActive(any);
                }

                // The chosen mode is drawn in the "selected" color
                for (int i = 0; i < ours.Count; i++)
                    SelectedLook.Apply(ours[i].Value, ours[i].Key.IsSelected);

                // 2. Order within each row
                Dictionary<int, List<KeyValuePair<SensorButton, GameObject>>> rows =
                    new Dictionary<int, List<KeyValuePair<SensorButton, GameObject>>>();
                for (int i = 0; i < ours.Count; i++)
                {
                    int g = ours[i].Key.HorizontalGroupID();
                    List<KeyValuePair<SensorButton, GameObject>> row;
                    if (!rows.TryGetValue(g, out row))
                    {
                        row = new List<KeyValuePair<SensorButton, GameObject>>();
                        rows[g] = row;
                    }
                    row.Add(ours[i]);
                }

                foreach (List<KeyValuePair<SensorButton, GameObject>> row in rows.Values)
                {
                    if (row.Count < 2) continue;
                    Transform parent = row[0].Value.transform.parent;
                    bool sameParent = true;
                    int first = int.MaxValue;
                    for (int i = 0; i < row.Count; i++)
                    {
                        Transform t = row[i].Value.transform;
                        if (t.parent != parent) { sameParent = false; break; }
                        if (t.GetSiblingIndex() < first) first = t.GetSiblingIndex();
                    }
                    if (!sameParent) continue;

                    row.Sort(delegate (KeyValuePair<SensorButton, GameObject> a, KeyValuePair<SensorButton, GameObject> b)
                    {
                        return b.Key.ButtonSideScreenSortOrder().CompareTo(a.Key.ButtonSideScreenSortOrder());
                    });
                    for (int i = 0; i < row.Count; i++)
                        row[i].Value.transform.SetSiblingIndex(first + i);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResourceSensor] button panel: " + e.Message);
            }
        }

        // Pairs "our button -> its object on screen". Each of our buttons has its own text,
        // so its object is the button in the panel showing that text (the panel's button
        // column is an open field; no reflection).
        private static List<KeyValuePair<SensorButton, GameObject>> Collect(ButtonMenuSideScreen screen)
        {
            List<KeyValuePair<SensorButton, GameObject>> result = new List<KeyValuePair<SensorButton, GameObject>>();
            DetailsScreen ds = DetailsScreen.Instance;
            GameObject target = ds != null ? ds.target : null;
            if (target == null || screen.buttonContainer == null) return result;
            SensorButton[] buttons = target.GetComponents<SensorButton>();
            if (buttons.Length == 0) return result;

            LocText[] texts = screen.buttonContainer.GetComponentsInChildren<LocText>(true);
            foreach (SensorButton b in buttons)
            {
                string label = Plain(b.SidescreenButtonText);
                if (label.Length == 0) continue;
                foreach (LocText t in texts)
                {
                    if (Plain(t.text) != label) continue;
                    GameObject go = ButtonObject(t.transform, screen.buttonContainer);
                    if (go != null) result.Add(new KeyValuePair<SensorButton, GameObject>(b, go));
                    break;
                }
            }
            return result;
        }

        // The button's own object: our buttons share one row, so it is the part right under
        // the row (the row itself is right under the column)
        private static GameObject ButtonObject(Transform t, Transform column)
        {
            while (t != null && t.parent != null && t.parent.parent != null)
            {
                if (t.parent.parent == column) return t.gameObject;
                t = t.parent;
            }
            return null;
        }

        // Text without its formatting tags (<nobr> and the like)
        private static string Plain(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : Regex.Replace(s, "<[^>]*>", "").Trim();
        }
    }

    // The game builds the buttons in SetTarget (its private Refresh is only called from there)
    [HarmonyPatch(typeof(ButtonMenuSideScreen), "SetTarget")]
    public static class ButtonMenuSideScreen_SetTarget_Patch
    {
        public static void Postfix(ButtonMenuSideScreen __instance)
        {
            ButtonOrder.Apply(__instance);
        }
    }

    /// <summary>
    /// The chosen button of a row (the count mode) cannot be pressed again, so the game would draw
    /// it gray, exactly like a button that is unavailable. To tell them apart, the chosen one
    /// gets the light blue of the game's own selected toggles (like "Below" in the threshold panel).
    /// </summary>
    public static class SelectedLook
    {
        // Color of a selected toggle in the game's threshold panel
        private static readonly Color SELECTED = new Color32(128, 139, 178, 255);

        private static ColorStyleSetting selectedStyle;
        // The button's own style, kept so it can be put back when the button is no longer chosen
        private static readonly Dictionary<KImage, ColorStyleSetting> original = new Dictionary<KImage, ColorStyleSetting>();

        public static void Forget() { original.Clear(); }

        public static void Apply(GameObject go, bool selected)
        {
            if (go == null) return;
            KButton button = go.GetComponentInChildren<KButton>(true);
            if (button == null || button.bgImage == null) return;
            KImage bg = button.bgImage;

            ColorStyleSetting own;
            if (!original.TryGetValue(bg, out own))
            {
                if (bg.colorStyleSetting == null) return;
                own = bg.colorStyleSetting;
                original[bg] = own;
            }

            ColorStyleSetting want = own;
            if (selected)
            {
                if (selectedStyle == null)
                {
                    selectedStyle = UnityEngine.Object.Instantiate(own);
                    selectedStyle.name = "ResourceSensorSelected";
                    selectedStyle.disabledColor = SELECTED;
                    selectedStyle.disabledActiveColor = SELECTED;
                    selectedStyle.disabledhoverColor = SELECTED;
                }
                want = selectedStyle;
            }
            if (bg.colorStyleSetting != want)
            {
                bg.colorStyleSetting = want;
                bg.ApplyColorStyleSetting();
            }
        }
    }
}
