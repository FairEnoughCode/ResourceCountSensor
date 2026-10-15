using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ResourceSensor
{
    /// <summary>
    /// The range is set with the game's own capacity panel, the one storage bins and
    /// refrigerators use for "Max: ... kg": a number field and a slider in a frame. The game
    /// shows that panel for any building with an IUserControlledCapacity component, and only
    /// while ControlEnabled() says so, which keeps it to Range mode with no extra patch.
    /// </summary>
    public class SensorRange : KMonoBehaviour, IUserControlledCapacity
    {
        [MyCmpGet] private ResourceCountSensor sensor;

        // No words after the number: the label in front says "Range in tiles:"
        private static readonly LocString NO_UNITS = new LocString("");

        private int Value { get { return sensor != null ? Mathf.Clamp(sensor.range, ResourceCountSensor.MIN_RANGE, ResourceCountSensor.MAX_RANGE) : ResourceCountSensor.MIN_RANGE; } }

        public float UserMaxCapacity
        {
            get { return Value; }
            set { if (sensor != null) sensor.SetRange(Mathf.RoundToInt(value)); }
        }
        public float AmountStored { get { return Value; } }
        public float MinCapacity { get { return ResourceCountSensor.MIN_RANGE; } }
        public float MaxCapacity { get { return ResourceCountSensor.MAX_RANGE; } }
        public bool WholeValues { get { return true; } }
        public LocString CapacityUnits { get { return NO_UNITS; } }
        public bool ControlEnabled() { return sensor != null && sensor.countMode == ResourceCountSensor.MODE_RANGE; }
    }

    /// <summary>
    /// Small touches to the capacity panel when it shows the sensor (it is shared with all
    /// storages, so for any other building the game's own values are put back):
    /// - its heading ("Storage Capacity Control") is hidden;
    /// - "Max:" reads "Range in tiles:", with a little space before the number field;
    /// - some space above the label and a little more under the slider;
    /// - the slider moves in whole steps and the number field takes whole numbers.
    /// Only open parts of the panel are used (no reflection). If Klei ever change the panel
    /// and the "Max:" label is not found, the panel simply keeps saying "Max:".
    /// </summary>
    [HarmonyPatch(typeof(CapacityControlSideScreen), "SetTarget")]
    public static class CapacityControlSideScreen_SetTarget_Patch
    {
        private const string MAX_LABEL_KEY = "STRINGS.UI.UISIDESCREENS.CAPACITY_CONTROL_SIDE_SCREEN.MAX_LABEL";
        private const string TITLE_KEY = "STRINGS.UI.UISIDESCREENS.CAPACITY_CONTROL_SIDE_SCREEN.TITLE";
        // About two spaces between "Range in tiles:" and the number field
        private const float LABEL_GAP = 8f;
        // Space above the label (the heading is hidden), from the block's top frame line,
        // tuned on screenshots to the rhythm of the rest of the side panel: a line -> 13-14 px
        // -> the label (about 15 px from the line). Under the slider the game's own space plus
        // a little, so the slider sits 15 px above the bottom line, as the label sits below the
        // top one (the game's heading is hidden, so its spacing no longer fits the block).
        private const int TOP_SPACE = 10;
        private const int EXTRA_BOTTOM = 5;
        private const int GAME_DECIMALS = 1; // what the game sets on the number field

        private static readonly Dictionary<LocText, string> labelKeys = new Dictionary<LocText, string>();
        private static readonly Dictionary<HorizontalOrVerticalLayoutGroup, Vector3> gameLayout = new Dictionary<HorizontalOrVerticalLayoutGroup, Vector3>();

        public static void Postfix(CapacityControlSideScreen __instance, GameObject __0)
        {
            try
            {
                if (__instance == null) return;
                bool ours = __0 != null && __0.GetComponent<SensorRange>() != null;

                LocText label = FindMaxLabel(__instance);
                if (label != null)
                {
                    string key = labelKeys[label];
                    if (ours)
                    {
                        // Without its key the label is not switched back to "Max:" by the game
                        if (label.key != "") label.key = "";
                        label.text = STRINGS.SIDESCREEN.RANGE_LABEL;
                    }
                    else if (label.key != key)
                    {
                        label.key = key;
                        label.text = Strings.Get(key);
                    }
                    // The gap after the label: the spacing of the row that holds it with the field
                    HorizontalLayoutGroup row = label.transform.parent != null ? label.transform.parent.GetComponent<HorizontalLayoutGroup>() : null;
                    if (row != null)
                    {
                        Vector3 game = Remember(row);
                        float spacing = ours ? game.x + LABEL_GAP : game.x;
                        if (row.spacing != spacing) row.spacing = spacing;
                    }
                }

                // Block spacing: the closest stack of lines around the slider
                KSlider sliderForBlock = __instance.GetComponentInChildren<KSlider>(true);
                VerticalLayoutGroup block = sliderForBlock != null ? sliderForBlock.GetComponentInParent<VerticalLayoutGroup>() : null;
                if (block != null && block.transform.IsChildOf(__instance.transform))
                {
                    Vector3 game = Remember(block);
                    int top = ours ? TOP_SPACE : (int)game.y;
                    int bottom = ours ? (int)game.z + EXTRA_BOTTOM : (int)game.z;
                    if (block.padding.top != top || block.padding.bottom != bottom)
                        block.padding = new RectOffset(block.padding.left, block.padding.right, top, bottom);
                }

                GameObject heading = FindHeading(__instance);
                if (heading != null && heading.activeSelf == ours) heading.SetActive(!ours);

                KSlider slider = __instance.GetComponentInChildren<KSlider>(true);
                if (slider != null && slider.wholeNumbers != ours) slider.wholeNumbers = ours;

                KNumberInputField input = __instance.GetComponentInChildren<KNumberInputField>(true);
                if (input != null)
                {
                    int decimals = ours ? 0 : GAME_DECIMALS;
                    if (input.decimalPlaces != decimals) input.decimalPlaces = decimals;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[ResourceSensor] range panel: " + e.Message); }
        }

        // The game's spacing, top and bottom padding of a layout group, kept to put back
        private static Vector3 Remember(HorizontalOrVerticalLayoutGroup g)
        {
            Vector3 v;
            if (!gameLayout.TryGetValue(g, out v))
            {
                v = new Vector3(g.spacing, g.padding.top, g.padding.bottom);
                gameLayout[g] = v;
            }
            return v;
        }

        private static GameObject heading;

        // A new game load makes new panels: forget the values kept for the old ones
        public static void Forget()
        {
            labelKeys.Clear();
            gameLayout.Clear();
            heading = null;
        }

        /// <summary>
        /// The panel's heading bar: the text with the title's key, together with the bar
        /// around it (its parents up to just under the panel, as long as they hold no slider
        /// or number field).
        /// </summary>
        private static GameObject FindHeading(CapacityControlSideScreen screen)
        {
            if (heading != null && heading.transform.IsChildOf(screen.transform)) return heading;
            heading = null;
            string title = Strings.Get(TITLE_KEY);
            LocText text = null;
            foreach (LocText t in screen.GetComponentsInChildren<LocText>(true))
                if (t.key == TITLE_KEY || (!string.IsNullOrEmpty(title) && t.text == title)) { text = t; break; }
            if (text == null) return null;
            Transform bar = text.transform;
            while (bar.parent != null && bar.parent != screen.transform
                   && bar.parent.GetComponentInChildren<KSlider>(true) == null
                   && bar.parent.GetComponentInChildren<KNumberInputField>(true) == null)
                bar = bar.parent;
            heading = bar.gameObject;
            return heading;
        }

        // The "Max:" text: found by its string key (or, once we took the key off, from memory)
        private static LocText FindMaxLabel(CapacityControlSideScreen screen)
        {
            foreach (KeyValuePair<LocText, string> kv in labelKeys)
                if (kv.Key != null && kv.Key.transform.IsChildOf(screen.transform)) return kv.Key;

            string max = Strings.Get(MAX_LABEL_KEY);
            foreach (LocText t in screen.GetComponentsInChildren<LocText>(true))
            {
                if (t.key == MAX_LABEL_KEY || (!string.IsNullOrEmpty(max) && t.text == max))
                {
                    labelKeys[t] = string.IsNullOrEmpty(t.key) ? MAX_LABEL_KEY : t.key;
                    return t;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The threshold panel's reading. For the sensor it has three lines: the resource name
    /// on a line of its own (so the eye finds it at once), then "— currently:" and the
    /// amount. The name with its line break comes from the sensor (ThresholdValueName); the
    /// rest is the game's own wording. Klei made the reading's box for two lines, so for the
    /// sensor the box is one line taller; for any other building the game's height is put
    /// back. Set once when a building is selected, nothing is measured on the way. Only open
    /// parts of the panel are used; if the box is not found, nothing changes.
    /// </summary>
    public static class ThresholdReadingFit
    {
        private static LayoutElement box;
        private static VerticalLayoutGroup stack;
        private static float gameMin = -1f, lineHeight = -1f;
        private static int gameTop = -1;
        // Range mode only: the range block's bottom frame line -> 19 px -> the reading
        // (the game alone gives 12 there; in On Map and Room the gap is fine as it is)
        private const int RANGE_EXTRA_TOP = 7;

        public static void Forget() { box = null; stack = null; }

        /// <summary>The extra space above the reading, for the sensor in Range mode only.</summary>
        public static void SetRangeSpace(bool ours, bool range)
        {
            try
            {
                if (stack == null || gameTop < 0) return;
                int top = ours && range ? gameTop + RANGE_EXTRA_TOP : gameTop;
                if (stack.padding.top == top) return;
                stack.padding = new RectOffset(stack.padding.left, stack.padding.right, top, stack.padding.bottom);
                LayoutRebuilder.MarkLayoutForRebuild(stack.transform as RectTransform);
            }
            catch (System.Exception e) { Debug.LogWarning("[ResourceSensor] threshold top space: " + e.Message); }
        }

        public static void Apply(ThresholdSwitchSideScreen screen, bool ours)
        {
            try
            {
                if (screen == null) return;
                if (box == null || !box.transform.IsChildOf(screen.transform))
                {
                    box = null;
                    // The reading: the panel's first text of more than one line
                    LocText reading = null;
                    foreach (LocText t in screen.GetComponentsInChildren<LocText>(true))
                        if (!string.IsNullOrEmpty(t.text) && t.text.IndexOf('\n') >= 0) { reading = t; break; }
                    if (reading == null || reading.transform.parent == null) return;
                    box = reading.transform.parent.GetComponent<LayoutElement>();
                    if (box == null) return;
                    gameMin = box.minHeight;
                    // The stack the reading's box sits in: its top space is the gap above the reading
                    stack = box.transform.parent != null ? box.transform.parent.GetComponent<VerticalLayoutGroup>() : null;
                    gameTop = stack != null ? stack.padding.top : -1;
                    // One line of this text: two lines minus one, measured without wrapping
                    lineHeight = Mathf.Round(reading.GetPreferredValues("A\nA", 10000f, 0f).y - reading.GetPreferredValues("A", 10000f, 0f).y);
                }
                if (gameMin <= 0f || lineHeight <= 0f) return;

                float min = ours ? gameMin + lineHeight : gameMin;
                if (Mathf.Abs(box.minHeight - min) < 0.5f) return;
                box.minHeight = min;
                LayoutRebuilder.MarkLayoutForRebuild(box.transform as RectTransform);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ResourceSensor] threshold reading height: " + e);
                box = null;
            }
        }
    }

    [HarmonyPatch(typeof(ThresholdSwitchSideScreen), "SetTarget")]
    public static class ThresholdSwitchSideScreen_SetTarget_Patch
    {
        public static void Postfix(ThresholdSwitchSideScreen __instance, GameObject __0)
        {
            ResourceCountSensor sensor = __0 != null ? __0.GetComponent<ResourceCountSensor>() : null;
            ThresholdReadingFit.Apply(__instance, sensor != null && SensorStrings.NameFirstInReading());
            ThresholdReadingFit.SetRangeSpace(sensor != null, sensor != null && sensor.countMode == ResourceCountSensor.MODE_RANGE);
        }
    }
}
