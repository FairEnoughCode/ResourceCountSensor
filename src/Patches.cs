using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ResourceSensor
{
    public class ResourceSensorMod : KMod.UserMod2
    {
        public static string ModPath;

        /// <summary>
        /// The patches are applied one class at a time, each with its own guard, instead of
        /// all at once (the base OnLoad's PatchAll). If a game update renames a method one
        /// patch needs, only that patch is skipped (with a warning in the log); the rest of
        /// the mod still loads.
        /// </summary>
        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            ModPath = path;
            foreach (Type type in assembly.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyPatch), false)) continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ResourceSensor] patch " + type.Name + " not applied: " + e.Message);
                }
            }
        }
    }

    // UI text: English by default, translations/<language>.po on top
    [HarmonyLib.HarmonyPatch(typeof(Localization), "Initialize")]
    public static class Localization_Initialize_Patch
    {
        public static void Postfix()
        {
            ModLocalization.Init(typeof(STRINGS), ResourceSensorMod.ModPath, "ResourceSensor");
        }
    }

    // When a save loads: use the germ sensor model for our building
    [HarmonyLib.HarmonyPatch(typeof(Game), "OnPrefabInit")]
    public static class Game_OnPrefabInit_SkinPatch
    {
        public static void Prefix()
        {
            SensorSkin.ApplyToDef();
        }
    }

    // ---------------- Strings, build menu, look ----------------

    [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
    public static class GeneratedBuildings_LoadGeneratedBuildings_Patch
    {
        public static void Prefix()
        {
            string key = "STRINGS.BUILDINGS.PREFABS." + ResourceCountSensorConfig.ID.ToUpperInvariant();
            Strings.Add(key + ".NAME", global::STRINGS.UI.FormatAsLink(STRINGS.SENSOR.NAME, ResourceCountSensorConfig.ID));
            Strings.Add(key + ".DESC", STRINGS.SENSOR.DESC);
            Strings.Add(key + ".EFFECT", string.Format(STRINGS.SENSOR.EFFECT, SensorStrings.Green(), SensorStrings.Red()));

            SensorStrings.Register();

            // In the build menu: right after the critter sensor, in the same group
            string relative = LogicCritterCountSensorConfig.ID;
            string sub = "uncategorized";
            if (TUNING.BUILDINGS.PLANSUBCATEGORYSORTING.ContainsKey(relative))
                sub = TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[relative];
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[ResourceCountSensorConfig.ID] = sub;
            ModUtil.AddBuildingToPlanScreen("Automation", ResourceCountSensorConfig.ID, sub, relative, ModUtil.BuildingOrdering.After);
        }
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class Db_Initialize_Patch
    {
        public static void Postfix()
        {
            var tech = Db.Get().Techs.TryGet("GenericSensors");
            if (tech != null && !tech.unlockedItemIDs.Contains(ResourceCountSensorConfig.ID))
                tech.unlockedItemIDs.Add(ResourceCountSensorConfig.ID);
        }
    }

    // ---------------- Resource list: only the selected kind ----------------

    [HarmonyPatch(typeof(Filterable), "GetTagOptions")]
    public static class Filterable_GetTagOptions_Patch
    {
        public static void Postfix(Filterable __instance, ref Dictionary<Tag, HashSet<Tag>> __result)
        {
            try
            {
                if (__result == null || __instance == null) return;
                ResourceCountSensor sensor = __instance.GetComponent<ResourceCountSensor>();
                if (sensor == null) return;

                int state = sensor.elementState;
                Dictionary<Tag, HashSet<Tag>> filtered = new Dictionary<Tag, HashSet<Tag>>();
                foreach (KeyValuePair<Tag, HashSet<Tag>> kv in __result)
                {
                    HashSet<Tag> keep = new HashSet<Tag>();
                    foreach (Tag t in kv.Value)
                        if (Accept(t, state)) keep.Add(t);
                    if (keep.Count > 0 && kv.Key != GameTags.Egg) filtered[kv.Key] = keep;
                }
                __result = filtered;
            }
            catch (System.Exception e) { Debug.LogWarning("[ResourceSensor] resource list filter: " + e.Message); }
        }

        private static bool Accept(Tag tag, int state)
        {
            Element e = ElementLoader.GetElement(tag);
            if (e == null)
            {
                // Not an element: food, seeds and other items count as solids, except eggs
                if (state != ResourceCountSensor.STATE_SOLID && state != ResourceCountSensor.STATE_OTHER) return false;
                GameObject prefab = Assets.TryGetPrefab(tag);
                if (prefab == null) return true;
                KPrefabID id = prefab.GetComponent<KPrefabID>();
                return id == null || !id.HasTag(GameTags.Egg);
            }
            if (state == ResourceCountSensor.STATE_LIQUID) return e.IsLiquid;
            if (state == ResourceCountSensor.STATE_GAS) return e.IsGas;
            return e.IsSolid;
        }
    }

    // ---------------- Resource list in a separate (third) column ----------------

    /// <summary>
    /// The resource picker is not shown in the main column for the sensor;
    /// a copy of it opens in the second side column (further left) instead.
    /// </summary>
    [HarmonyPatch(typeof(DetailsScreen), "Refresh")]
    public static class DetailsScreen_Refresh_Patch
    {
        private static KScreen opened;

        public static void Postfix(DetailsScreen __instance, object[] __args)
        {
            GameObject target = (__args != null && __args.Length > 0) ? __args[0] as GameObject : null;
            Show(__instance, target);
        }

        /// <summary>
        /// The details panel wakes up (OnSpawn) on the first selection after a load, after the
        /// mod has already opened the list, and switches the second column off. So the list
        /// is opened once more right after that.
        /// </summary>
        public static void AfterSpawn(DetailsScreen ds)
        {
            shownTargetId = 0;
            if (ds != null) Show(ds, ds.target);
        }

        private static void Show(DetailsScreen __instance, GameObject target)
        {
            try
            {
                bool ours = target != null && target.GetComponent<ResourceCountSensor>() != null;

                if (!ours)
                {
                    if (IsOpen())
                        __instance.ClearSecondarySideScreen();
                    opened = null;
                    lastTargetId = 0;
                    shownTargetId = 0;
                    return;
                }

                FilterSideScreen prefab = SensorKindScreenSetup.FilterPrefab;
                if (prefab == null) return;

                bool wasOpen = IsOpen();

                // The list is long and rebuilding it is slow: if it is already open for this
                // sensor and the substance kind is the same, there is nothing to rebuild
                // (e.g. after switching the count mode)
                ResourceCountSensor sensor = target.GetComponent<ResourceCountSensor>();
                int state = sensor != null ? sensor.elementState : -1;
                if (wasOpen && target.GetInstanceID() == shownTargetId && state == shownState) return;

                KScreen screen = __instance.SetSecondarySideScreen(prefab, STRINGS.SIDESCREEN.RESOURCE_LIST_TITLE);
                SideScreenContent content = screen as SideScreenContent;
                if (content != null)
                {
                    content.SetTarget(target);
                    CollapseIfNew(content, target, !wasOpen);
                }
                opened = screen;
                shownTargetId = target.GetInstanceID();
                shownState = state;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResourceSensor] could not open the resource list: " + e);
            }
        }

        // What the open list currently shows
        private static int shownTargetId;
        private static int shownState = -1;

        private static int lastTargetId;
        private static int lastState = -1;

        // On first open (or when the substance kind changes) all categories are folded,
        // except the one holding the selected resource. The categories are reached through
        // the list's open method GetCategoryWithItem, one resource of each category.
        private static void CollapseIfNew(SideScreenContent content, GameObject target, bool force)
        {
            ResourceCountSensor sensor = target.GetComponent<ResourceCountSensor>();
            int id = target.GetInstanceID();
            int state = sensor != null ? sensor.elementState : -1;
            if (!force && id == lastTargetId && state == lastState) return;
            lastTargetId = id;
            lastState = state;

            SingleItemSelectionSideScreenBase list = content as SingleItemSelectionSideScreenBase;
            Filterable filterable = target.GetComponent<Filterable>();
            if (list == null || filterable == null) return;
            Dictionary<Tag, HashSet<Tag>> options = filterable.GetTagOptions();
            if (options == null) return;
            Tag selected = filterable.SelectedTag;
            foreach (KeyValuePair<Tag, HashSet<Tag>> kv in options)
            {
                if (kv.Value == null || kv.Value.Count == 0) continue;
                Tag any = Tag.Invalid;
                foreach (Tag t in kv.Value) { any = t; break; }
                SingleItemSelectionSideScreenBase.Category cat = list.GetCategoryWithItem(any, true);
                if (cat == null) continue;
                bool open = selected.IsValid && selected != GameTags.Void && kv.Value.Contains(selected);
                cat.SetUnfoldedState(open
                    ? SingleItemSelectionSideScreenBase.Category.UnfoldedStates.Unfolded
                    : SingleItemSelectionSideScreenBase.Category.UnfoldedStates.Folded);
            }
        }

        // Our list is open while it can be seen: the game switches the list off when it
        // closes it or opens another one there, and switches the whole column off when the
        // panel first wakes up
        private static bool IsOpen()
        {
            return opened != null && opened.gameObject.activeInHierarchy;
        }
    }

    [HarmonyPatch(typeof(DetailsScreen), "OnSpawn")]
    public static class DetailsScreen_OnSpawn_Patch
    {
        public static void Postfix(DetailsScreen __instance)
        {
            DetailsScreen_Refresh_Patch.AfterSpawn(__instance);
        }
    }

    // ---------------- Hide the "Output 1/2" headers ----------------

    [HarmonyPatch(typeof(FilterSideScreen), "SetTarget")]
    public static class FilterSideScreen_SetTarget_Patch
    {
        public static void Postfix(FilterSideScreen __instance, GameObject __0)
        {
            try
            {
                OutputHider.Apply(__instance, __0);
            }
            catch (System.Exception e) { Debug.LogWarning("[ResourceSensor] list headers: " + e.Message); }
        }
    }

    public static class OutputHider
    {
        // What was hidden, per screen instance, so it can be restored for sorters
        private static readonly Dictionary<int, List<GameObject>> hidden = new Dictionary<int, List<GameObject>>();

        public static void Forget() { hidden.Clear(); }

        public static void Apply(FilterSideScreen screen, GameObject target)
        {
            if (screen == null) return;
            int key = screen.GetInstanceID();
            bool ours = target != null && target.GetComponent<ResourceCountSensor>() != null;

            List<GameObject> list;
            if (!hidden.TryGetValue(key, out list))
            {
                list = new List<GameObject>();
                hidden[key] = list;
            }

            if (!ours)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) list[i].SetActive(true);
                list.Clear();
                return;
            }

            Transform root = screen.transform;
            Transform listRoot = screen.elementEntryContainer;
            SearchBar searchBar = screen.GetComponentInChildren<SearchBar>(true);
            Transform search = searchBar != null ? searchBar.transform : null;

            // The headers are open fields of the panel ("Output 2", the gray bar under the
            // search box, is selectElementHeaderLabel)
            Component[] headers =
            {
                screen.outputIcon, screen.outputElementHeaderLabel, screen.selectElementHeaderLabel,
                screen.everythingElseIcon, screen.everythingElseHeaderLabel
            };
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i] == null) continue;
                Transform t = headers[i].transform;

                // Climb up to the largest block that contains neither the list nor the search bar
                Transform best = t;
                Transform p = t.parent;
                while (p != null && p != root)
                {
                    if (Contains(p, listRoot) || Contains(p, search)) break;
                    best = p;
                    p = p.parent;
                }

                if (best.gameObject.activeSelf)
                {
                    best.gameObject.SetActive(false);
                    if (!list.Contains(best.gameObject)) list.Add(best.gameObject);
                }
            }
        }

        private static bool Contains(Transform parent, Transform child)
        {
            if (parent == null || child == null) return false;
            return child == parent || child.IsChildOf(parent);
        }
    }

    // The building icon in the build menu and elsewhere is the recolored one
    [HarmonyLib.HarmonyPatch(typeof(BuildingDef), "GetUISprite")]
    public static class BuildingDef_GetUISprite_Patch
    {
        public static void Postfix(BuildingDef __instance, ref Sprite __result)
        {
            try
            {
                if (__instance != null && __instance.PrefabID == ResourceCountSensorConfig.ID)
                {
                    Sprite icon = SensorSkin.MenuIcon();
                    if (icon != null) __result = icon;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[ResourceSensor] menu icon: " + e.Message); }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Def), "GetUISprite", new System.Type[] { typeof(object), typeof(string), typeof(bool) })]
    public static class Def_GetUISprite_Patch
    {
        public static void Postfix(object item, ref global::Tuple<Sprite, Color> __result)
        {
            try
            {
                string id = null;
                if (item is Tag) id = ((Tag)item).Name;
                else if (item is string) id = (string)item;
                else
                {
                    GameObject go = item as GameObject;
                    if (go == null && item is Component) go = ((Component)item).gameObject;
                    if (go != null) { KPrefabID kp = go.GetComponent<KPrefabID>(); if (kp != null) id = kp.PrefabTag.Name; }
                }
                if (id != ResourceCountSensorConfig.ID) return;
                Sprite icon = SensorSkin.MenuIcon();
                if (icon != null) __result = new global::Tuple<Sprite, Color>(icon, Color.white);
            }
            catch (System.Exception e) { Debug.LogWarning("[ResourceSensor] icon: " + e.Message); }
        }
    }
}
