using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ResourceSensor
{
    /// <summary>
    /// The sensor's own side panel for the substance kind: three of the game's square check
    /// boxes, "Materials / Liquids / Gases", working as one choice (exactly one is ticked).
    ///
    /// The panel is a copy of the Critter Sensor's panel ("Count Critters / Count Eggs"), so
    /// the check boxes, fonts and spacing are Klei's own. Its parts are taken from the
    /// critter panel's open fields; the second row is copied once more for the third choice.
    /// In On Map mode the mod has data on solids only, so only "Materials" is shown there.
    /// </summary>
    public class SensorKindScreen : SideScreenContent
    {
        // Filled in when the panel is made (see SensorKindScreenSetup); kept in the copies the
        // game makes, because Unity copies public fields along with the objects they point to
        public GameObject[] rows;
        public KToggle[] toggles;
        public KImage[] checks;
        public VerticalLayoutGroup list;

        private static readonly int[] STATES = { ResourceCountSensor.STATE_SOLID, ResourceCountSensor.STATE_LIQUID, ResourceCountSensor.STATE_GAS };

        private ResourceCountSensor sensor;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            if (toggles == null) return;
            for (int i = 0; i < toggles.Length && i < STATES.Length; i++)
            {
                if (toggles[i] == null) continue;
                int state = STATES[i];
                toggles[i].onClick += () => Choose(state);
            }
        }

        public override bool IsValidForTarget(GameObject target)
        {
            return target != null && target.GetComponent<ResourceCountSensor>() != null;
        }

        public override void SetTarget(GameObject target)
        {
            sensor = target != null ? target.GetComponent<ResourceCountSensor>() : null;
            Refresh();
        }

        public override void ClearTarget()
        {
            sensor = null;
        }

        public override string GetTitle()
        {
            return STRINGS.SIDESCREEN.KIND_TITLE;
        }

        // Right under the count mode buttons (see SideScreenContent_GetSideScreenSortOrder_Patch)
        public override int GetSideScreenSortOrder()
        {
            return 3;
        }

        private void Choose(int state)
        {
            // Clicking the ticked box keeps it ticked: one kind is always chosen
            if (sensor != null) sensor.SetElementState(state);
            Refresh();
        }

        private void Refresh()
        {
            if (sensor == null || checks == null) return;
            bool onMap = sensor.countMode == ResourceCountSensor.MODE_WORLD;
            for (int i = 0; i < checks.Length && i < STATES.Length; i++)
            {
                if (checks[i] != null) checks[i].enabled = sensor.elementState == STATES[i];
                // On Map: materials only
                bool show = !onMap || STATES[i] == ResourceCountSensor.STATE_SOLID;
                if (rows != null && i < rows.Length && rows[i] != null && rows[i].activeSelf != show)
                    rows[i].SetActive(show);
            }
            // Space under the last row: more in Range mode, where the range block follows
            bool range = sensor.countMode == ResourceCountSensor.MODE_RANGE;
            ThresholdReadingFit.SetRangeSpace(true, range);
            int bottom = range ? SensorKindScreenSetup.BOTTOM_SPACE_RANGE : SensorKindScreenSetup.BOTTOM_SPACE;
            if (list != null && list.padding.bottom != bottom)
            {
                list.padding = new RectOffset(list.padding.left, list.padding.right, list.padding.top, bottom);
                LayoutRebuilder.MarkLayoutForRebuild(list.transform as RectTransform);
            }
        }
    }

    /// <summary>
    /// Makes the sensor's kind panel and adds it to the game's list of side panels.
    ///
    /// REFLECTION, one field (the only one in the mod): the game keeps its list of side
    /// panels in DetailsScreen's private field "sideScreens", and offers no open way to add a
    /// panel to it. The same read also finds the resource list for the second column. Mods that
    /// add their own panels all do it this way (PLib does too). If Klei rename that field,
    /// the kind panel just does not appear: the sensor keeps counting the kind already chosen
    /// and the rest of its panel works as before. Everything else here uses open fields only.
    /// </summary>
    [HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
    public static class SensorKindScreenSetup
    {
        private const string NAME = "ResourceSensorKindScreen";
        // Measured on a screenshot: the critter panel's boxes start 5 px left of the buttons
        private const int ALIGN_SHIFT = 5;
        // Space under the last row, tuned on screenshots. On Map and Room: the last box ->
        // 22 px -> the threshold reading. Range: the last box -> 22 px -> the range block's
        // frame line.
        // A little more above the first row than the critter panel has: the line above ->
        // 15 px -> the first box, as the range block's line -> its label
        private const int TOP_EXTRA = 3;
        public const int BOTTOM_SPACE = 8;
        public const int BOTTOM_SPACE_RANGE = 18;

        /// <summary>The game's resource list panel (prefab), for the second column.</summary>
        public static FilterSideScreen FilterPrefab;

        // Made once and kept: the game makes a new list of side panels for every game it loads
        private static SensorKindScreen made;

        public static void Postfix(DetailsScreen __instance)
        {
            // A new details panel means a new game load: the values the mod kept about the
            // old game panels (to put them back for other buildings) are no longer needed
            CapacityControlSideScreen_SetTarget_Patch.Forget();
            ThresholdReadingFit.Forget();
            OutputHider.Forget();
            SelectedLook.Forget();

            try
            {
                FieldInfo field = AccessTools.Field(typeof(DetailsScreen), "sideScreens");
                List<DetailsScreen.SideScreenRef> list = field != null ? field.GetValue(__instance) as List<DetailsScreen.SideScreenRef> : null;
                if (list == null)
                {
                    Debug.LogWarning("[ResourceSensor] kind panel: the game's side panel list was not found, no kind panel");
                    return;
                }
                // The game's resource list (the logic filter one), opened by the mod in the
                // second column; taken from the same list, so its field is read only here
                FilterPrefab = null;
                foreach (DetailsScreen.SideScreenRef r in list)
                {
                    FilterSideScreen fs = r != null ? r.screenPrefab as FilterSideScreen : null;
                    if (fs == null) continue;
                    if (fs.isLogicFilter) { FilterPrefab = fs; break; }
                    if (FilterPrefab == null) FilterPrefab = fs;
                }

                foreach (DetailsScreen.SideScreenRef r in list)
                    if (r != null && r.name == NAME) return;

                DetailsScreen.SideScreenRef critterRef = null;
                foreach (DetailsScreen.SideScreenRef r in list)
                    if (r != null && r.screenPrefab is CritterSensorSideScreen) { critterRef = r; break; }
                if (critterRef == null)
                {
                    Debug.LogWarning("[ResourceSensor] kind panel: the Critter Sensor panel was not found, no kind panel");
                    return;
                }

                if (made == null) made = Make((CritterSensorSideScreen)critterRef.screenPrefab);
                SensorKindScreen prefab = made;
                if (prefab == null) return;
                list.Add(new DetailsScreen.SideScreenRef
                {
                    name = NAME,
                    screenPrefab = prefab,
                    offset = critterRef.offset,
                    tab = critterRef.tab
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResourceSensor] kind panel not added: " + e);
            }
        }

        private static SensorKindScreen Make(CritterSensorSideScreen critter)
        {
            KToggle firstToggle = critter.countCrittersToggle, secondToggle = critter.countEggsToggle;
            if (firstToggle == null || secondToggle == null)
            {
                Debug.LogWarning("[ResourceSensor] kind panel: the critter panel has no check boxes, no kind panel");
                return null;
            }

            // The copy is made inside a switched-off holder, so nothing in it starts running
            // before the critter panel's own code is swapped for ours
            GameObject holder = new GameObject(NAME + "Holder");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            GameObject go = UnityEngine.Object.Instantiate(critter.gameObject, holder.transform, false);
            go.name = NAME;
            CritterSensorSideScreen copy = go.GetComponent<CritterSensorSideScreen>();

            // The same parts in the copy
            KToggle[] toggles = { copy.countCrittersToggle, copy.countEggsToggle, null };
            KImage[] checks = { copy.crittersCheckmark, copy.eggsCheckmark, null };
            GameObject[] rows = { RowOf(toggles[0], go.transform), RowOf(toggles[1], go.transform), null };
            UnityEngine.Object.DestroyImmediate(copy);

            if (rows[0] == null || rows[1] == null || rows[0] == rows[1])
            {
                Debug.LogWarning("[ResourceSensor] kind panel: the check box rows are laid out differently than expected, no kind panel\n" + PanelLog.Describe(go.transform, 0));
                UnityEngine.Object.Destroy(holder);
                return null;
            }

            // The third row: one more copy of the second, right under it
            GameObject third = UnityEngine.Object.Instantiate(rows[1], rows[1].transform.parent, false);
            third.transform.SetSiblingIndex(rows[1].transform.GetSiblingIndex() + 1);
            rows[2] = third;
            toggles[2] = third.GetComponentInChildren<KToggle>(true);
            checks[2] = FindCheck(third, toggles[2], checks[1]);

            string[] labels = { STRINGS.SIDESCREEN.STATE_SOLID, STRINGS.SIDESCREEN.STATE_LIQUID, STRINGS.SIDESCREEN.STATE_GAS };
            for (int i = 0; i < 3; i++)
            {
                rows[i].name = "KindRow" + i;
                SetLabel(rows[i], toggles[i], labels[i]);
                SetTooltip(rows[i], STRINGS.SIDESCREEN.STATE_TOOLTIP);
            }
            // Any other text the critter panel has (a heading, if any) must not be switched
            // back to the critter wording by the game
            foreach (LocText t in go.GetComponentsInChildren<LocText>(true))
                if (!string.IsNullOrEmpty(t.key) && t.key.Contains("CRITTER")) { t.key = ""; t.text = ""; }

            // Look: under the last row the game's usual gap between blocks, and the boxes moved
            // right so their left edge is in line with the count mode buttons above
            VerticalLayoutGroup list = rows[0].transform.parent.GetComponent<VerticalLayoutGroup>();
            if (list != null) list.padding = new RectOffset(list.padding.left, list.padding.right, list.padding.top + TOP_EXTRA, BOTTOM_SPACE);
            // The critter panel keeps at least 100 high (two rows); with one row (On Map) that
            // left an empty strip, so the height follows the rows
            LayoutElement listLe = rows[0].transform.parent.GetComponent<LayoutElement>();
            if (listLe != null) listLe.minHeight = -1f;
            foreach (GameObject row in rows)
            {
                HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
                if (h != null) h.padding = new RectOffset(h.padding.left + ALIGN_SHIFT, h.padding.right, h.padding.top, h.padding.bottom);
            }

            SensorKindScreen screen = go.AddComponent<SensorKindScreen>();
            screen.rows = rows;
            screen.toggles = toggles;
            screen.checks = checks;
            screen.list = list;

            return screen;
        }

        // The child of the panel's row container that holds this check box with its text:
        // the closest parent of the box that also holds a text
        private static GameObject RowOf(KToggle toggle, Transform root)
        {
            if (toggle == null) return null;
            for (Transform t = toggle.transform; t != null && t != root; t = t.parent)
            {
                foreach (LocText text in t.GetComponentsInChildren<LocText>(true))
                    if (!text.transform.IsChildOf(toggle.transform)) return t.gameObject;
            }
            return null;
        }

        // The tick of the copied row: the image at the same place as the tick of the original
        private static KImage FindCheck(GameObject row, KToggle toggle, KImage originalCheck)
        {
            if (originalCheck != null)
            {
                string name = originalCheck.name;
                foreach (KImage img in row.GetComponentsInChildren<KImage>(true))
                    if (img.name == name) return img;
            }
            return toggle != null ? toggle.GetComponentInChildren<KImage>(true) : null;
        }

        // The row's text (not inside the box itself)
        private static void SetLabel(GameObject row, KToggle toggle, string text)
        {
            foreach (LocText t in row.GetComponentsInChildren<LocText>(true))
            {
                if (toggle != null && t.transform.IsChildOf(toggle.transform)) continue;
                t.key = "";
                t.text = text;
                return;
            }
        }

        private static void SetTooltip(GameObject row, string text)
        {
            foreach (ToolTip tip in row.GetComponentsInChildren<ToolTip>(true))
                tip.SetSimpleTooltip(text);
        }

    }
}

namespace ResourceSensor
{
    // How a panel is built, written to the log if the kind panel cannot be made
    internal static class PanelLog
    {
        public static string Describe(Transform t, int depth)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)");
            RectTransform rt = t as RectTransform;
            if (rt != null) sb.Append(" size=").Append(rt.sizeDelta);
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                sb.Append(" [").Append(c.GetType().Name);
                LayoutElement le = c as LayoutElement;
                if (le != null) sb.Append(" min=").Append(le.minHeight).Append(" pref=").Append(le.preferredHeight);
                HorizontalOrVerticalLayoutGroup lg = c as HorizontalOrVerticalLayoutGroup;
                if (lg != null) sb.Append(" pad=").Append(lg.padding).Append(" spacing=").Append(lg.spacing);
                LocText text = c as LocText;
                if (text != null) sb.Append(" \"").Append(text.text).Append("\" key=").Append(text.key);
                sb.Append(']');
            }
            sb.Append('\n');
            foreach (Transform child in t) sb.Append(Describe(child, depth + 1));
            return sb.ToString();
        }
    }
}
