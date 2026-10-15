using UnityEngine;

namespace ResourceSensor
{
    /// <summary>
    /// The game sorts the side panels by GetSideScreenSortOrder, higher first. The range
    /// panel and the threshold panel both say 0, so their order changed from one
    /// refresh to the next. For the sensor they get a fixed order under the mode buttons:
    /// the substance kind (its own panel says 3), then the range (Range mode only), then
    /// the threshold.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(SideScreenContent), "GetSideScreenSortOrder")]
    public static class SideScreenContent_GetSideScreenSortOrder_Patch
    {
        public static void Postfix(SideScreenContent __instance, ref int __result)
        {
            try
            {
                if (__result != 0 || __instance == null) return;
                DetailsScreen ds = DetailsScreen.Instance;
                GameObject target = ds != null ? ds.target : null;
                if (target == null || target.GetComponent<ResourceCountSensor>() == null) return;
                if (__instance is CapacityControlSideScreen) __result = 2;
                else if (__instance is ThresholdSwitchSideScreen) __result = 1;
            }
            catch (System.Exception e) { Debug.LogWarning("[ResourceSensor] panel order: " + e.Message); }
        }
    }
}
