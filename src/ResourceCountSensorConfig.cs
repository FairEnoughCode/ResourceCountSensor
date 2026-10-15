using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace ResourceSensor
{
    public class ResourceCountSensorConfig : IBuildingConfig
    {
        public const string ID = "ResourceCountSensor";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID, 1, 1,
                SensorSkin.PickAnim(),
                30, 30f,
                BUILDINGS.CONSTRUCTION_MASS_KG.TIER0,
                MATERIALS.REFINED_METALS,
                1600f,
                BuildLocationRule.Anywhere,
                BUILDINGS.DECOR.PENALTY.TIER0,
                NOISE_POLLUTION.NONE);

            def.Overheatable = false;
            def.Floodable = false;
            def.Entombable = false;
            def.AlwaysOperational = true;
            def.ViewMode = OverlayModes.Logic.ID;
            def.AudioCategory = "Metal";
            def.SceneLayer = Grid.SceneLayer.Building;

            def.LogicOutputPorts = new List<LogicPorts.Port>
            {
                LogicPorts.Port.OutputPort(
                    ResourceCountSensor.PORT_ID,
                    new CellOffset(0, 0),
                    STRINGS.SENSOR.LOGIC_PORT,
                    string.Format(STRINGS.SENSOR.LOGIC_PORT_ACTIVE, SensorStrings.Green()),
                    string.Format(STRINGS.SENSOR.LOGIC_PORT_INACTIVE, SensorStrings.Red()),
                    true)
            };

            GeneratedBuildings.RegisterWithOverlay(OverlayModes.Logic.HighlightItemIDs, ID);
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<CopyBuildingSettings>();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            Filterable filterable = go.AddOrGet<Filterable>();
            filterable.filterElementState = Filterable.ElementState.Solid;
            go.AddOrGet<ResourceCountSensor>();

            // Side panel: count mode buttons and the game's capacity panel for the range.
            // The substance kind has its own panel (SensorKindScreen), shown for any sensor.
            go.AddOrGet<ModeWorldButton>();
            go.AddOrGet<ModeRangeButton>();
            go.AddOrGet<ModeRoomButton>();
            go.AddOrGet<SensorRange>();

            go.AddOrGet<AreaVisualizer>();
        }
    }
}
