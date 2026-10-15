using System.Collections.Generic;
using System.Runtime.Serialization;
using KSerialization;
using UnityEngine;

namespace ResourceSensor
{
    /// <summary>
    /// Resource sensor.
    /// Count modes:
    ///   0 — On Map: same as the resources panel on the right (WorldInventory of the current asteroid);
    ///   1 — Range: in a square of ±N cells around the sensor (N = 1..10);
    ///   2 — Room: in the same enclosed area (up to 256 cells) where the sensor stands.
    /// Substance kind: solid / liquid / gas (changes the list in the filter).
    /// In Range/Room modes, liquids and gases count both what lies/floats in the cells
    /// and bottles/canisters (on the floor and in storage).
    /// Default: below the threshold — green, above — red.
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class ResourceCountSensor : KMonoBehaviour, IThresholdSwitch, ISim1000ms
    {
        public static readonly HashedString PORT_ID = new HashedString("ResourceCountSensorOutput");
        private const float MAX_KG = 1000000f;

        public const int MODE_WORLD = 0;
        public const int MODE_RANGE = 1;
        public const int MODE_ROOM = 2;

        public const int STATE_SOLID = 0;
        public const int STATE_LIQUID = 1;
        public const int STATE_GAS = 2;
        public const int STATE_OTHER = 3; // obsolete: now part of solids

        public const int MAX_ROOM_CELLS = 256;

        public const int MIN_RANGE = 1;
        public const int MAX_RANGE = 10;

        [Serialize] public float threshold = 1000f;
        [Serialize] public bool activateAboveThreshold = false;
        [Serialize] public int countMode = MODE_WORLD;
        [Serialize] public int range = 3;
        [Serialize] public int elementState = STATE_SOLID;
        [Serialize] private bool switchedOn;

        [MyCmpGet] private Filterable filterable;
        [MyCmpGet] private LogicPorts logicPorts;
        [MyCmpGet] private KBatchedAnimController animController;

        private float currentAmount;
        private bool visualInitialized;
        private bool lastVisualState;

        private static readonly EventSystem.IntraObjectHandler<ResourceCountSensor> OnCopySettingsDelegate =
            new EventSystem.IntraObjectHandler<ResourceCountSensor>(delegate (ResourceCountSensor c, object data) { c.OnCopySettings(data); });

        // ---------------- Lifecycle ----------------

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.CopySettings, OnCopySettingsDelegate);
        }

        [OnDeserialized]
        private void OnDeserialized()
        {
            ApplyElementState();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (elementState == STATE_OTHER) elementState = STATE_SOLID;
            ApplyElementState();
            KAnimFile anim = SensorSkin.ResolveAnim();
            if (animController != null && anim != null
                && (animController.AnimFiles == null || animController.AnimFiles.Length == 0 || animController.AnimFiles[0] != anim))
                animController.SwapAnims(new KAnimFile[] { anim });
            ApplyLook();
            if (filterable != null)
                filterable.onFilterChanged += OnFilterChanged;
            KSelectable sel = GetComponent<KSelectable>();
            if (sel != null)
            {
                sel.AddStatusItem(SensorStatus.Amount(), this);
                sel.AddStatusItem(SensorStatus.Mode(), this);
            }
            Refresh();
        }

        // ---------------- Hover tooltip ----------------

        // Short, so it fits the status panel; the threshold condition is in the tooltip
        public string StatusAmountText()
        {
            if (!HasTag()) return STRINGS.STATUS.NO_RESOURCE;
            if (NoData()) return string.Format(STRINGS.STATUS.NO_DATA, filterable.SelectedTag.ProperName());
            return string.Format(STRINGS.STATUS.AMOUNT, filterable.SelectedTag.ProperName(),
                FormatMass(currentAmount, true));
        }

        public string StatusAmountTooltip()
        {
            if (!HasTag()) return STRINGS.STATUS.AMOUNT_TOOLTIP;
            string condition = activateAboveThreshold ? SensorStrings.TOOLTIP_ABOVE : SensorStrings.TOOLTIP_BELOW;
            return condition.Replace("{0}", FormatMass(threshold, true));
        }

        public string StatusModeText()
        {
            if (countMode == MODE_RANGE) return string.Format(STRINGS.STATUS.MODE_RANGE, Mathf.Clamp(range, MIN_RANGE, MAX_RANGE));
            if (countMode == MODE_ROOM)
            {
                int cells = RoomCells();
                return cells < 0 ? (string)STRINGS.STATUS.MODE_ROOM_TOO_BIG : string.Format(STRINGS.STATUS.MODE_ROOM, cells);
            }
            return STRINGS.STATUS.MODE_WORLD;
        }

        protected override void OnCleanUp()
        {
            if (filterable != null)
                filterable.onFilterChanged -= OnFilterChanged;
            base.OnCleanUp();
        }

        public void Sim1000ms(float dt) { Refresh(); }

        // ---------------- Look ----------------

        // Germ sensor model (silver + blue), without germ spots, with a slower beam
        private void ApplyLook()
        {
            if (animController == null) return;
            animController.TintColour = new Color32(255, 255, 255, 255);
            animController.PlaySpeedMultiplier = SensorSkin.ANIM_SPEED;
            List<KAnimHashedString> germs = SensorSkin.GermSymbols();
            for (int i = 0; i < germs.Count; i++)
                animController.SetSymbolVisiblity(germs[i], false);
            Dictionary<int, Color> tints = SensorSkin.SymbolTints(SensorSkin.ResolveAnim());
            foreach (KeyValuePair<int, Color> kv in tints)
                animController.SetSymbolTint(new KAnimHashedString(kv.Key), kv.Value);
        }

        private void OnFilterChanged(Tag tag) { Refresh(); }

        private void ApplyElementState()
        {
            Filterable f = filterable != null ? filterable : GetComponent<Filterable>();
            if (f == null) return;
            if (elementState == STATE_LIQUID) f.filterElementState = Filterable.ElementState.Liquid;
            else if (elementState == STATE_GAS) f.filterElementState = Filterable.ElementState.Gas;
            else f.filterElementState = Filterable.ElementState.Solid;
        }

        private void OnCopySettings(object data)
        {
            GameObject go = data as GameObject;
            if (go == null) return;
            ResourceCountSensor other = go.GetComponent<ResourceCountSensor>();
            if (other == null) return;
            threshold = other.threshold;
            activateAboveThreshold = other.activateAboveThreshold;
            countMode = other.countMode;
            range = other.range;
            elementState = other.elementState;
            ApplyElementState();
            Refresh();
        }

        // ---------------- Controls (called by the side panel buttons) ----------------

        public void SetMode(int mode)
        {
            if (countMode == mode) return;
            countMode = mode;
            // On Map has no data on liquids and gases in the tiles: switch the list to materials
            // (the selected resource stays, as with any change of the kind)
            if (mode == MODE_WORLD && elementState != STATE_SOLID)
            {
                elementState = STATE_SOLID;
                ApplyElementState();
            }
            // Back from On Map: the ticked kind follows the chosen resource again, so a gas
            // chosen before is shown (and highlighted) in the list
            else if (mode != MODE_WORLD && elementState == STATE_SOLID)
            {
                Element fluid = FluidOfTag();
                if (fluid != null)
                {
                    elementState = fluid.IsLiquid ? STATE_LIQUID : STATE_GAS;
                    ApplyElementState();
                }
            }
            Refresh();
            RefreshScreen();
        }

        public void SetElementState(int state)
        {
            if (elementState == state) return;
            elementState = state;
            ApplyElementState();
            // The selected resource stays until another one is picked from the list
            Refresh();
            RefreshScreen();
        }

        public void SetRange(int value)
        {
            value = Mathf.Clamp(value, MIN_RANGE, MAX_RANGE);
            if (range == value) return;
            range = value;
            Refresh();
        }

        private void RefreshScreen()
        {
            // Only while this sensor is the one shown (e.g. Copy Settings changes other sensors too)
            if (DetailsScreen.Instance != null && DetailsScreen.Instance.target == gameObject)
                DetailsScreen.Instance.Refresh(gameObject);
        }

        // ---------------- Counting ----------------

        // The chosen resource is a liquid or a gas (an element in that state), or null if not
        private Element FluidOfTag()
        {
            if (!HasTag()) return null;
            Element e = ElementLoader.GetElement(filterable.SelectedTag);
            return e != null && (e.IsLiquid || e.IsGas) ? e : null;
        }

        /// <summary>
        /// On Map counts what the game's resource panel counts: liquids and gases only in
        /// bottles and storage, not in the tiles. With a liquid or gas chosen the sensor has
        /// no real number there, so it says so instead of showing (and acting on) 0.
        /// </summary>
        public bool NoData()
        {
            return countMode == MODE_WORLD && FluidOfTag() != null;
        }

        private bool HasTag()
        {
            if (filterable == null) return false;
            Tag tag = filterable.SelectedTag;
            return tag.IsValid && tag != GameTags.Void;
        }

        /// <summary>
        /// Cells counted in the Range and Room modes.
        /// Returns null for Asteroid, and an empty list for a room that is too big.
        /// </summary>
        public List<int> GetCountedCells()
        {
            if (countMode == MODE_WORLD) return null;
            List<int> result = new List<int>();
            FillCountedCells(result);
            return result;
        }

        /// <summary>The same, written into a list the caller keeps (no new list every second).</summary>
        private void FillCountedCells(List<int> result)
        {
            result.Clear();
            if (countMode == MODE_WORLD) return;
            int myCell = Grid.PosToCell(this);
            if (!Grid.IsValidCell(myCell)) return;
            int worldIdx = Grid.WorldIdx[myCell];

            if (countMode == MODE_RANGE)
            {
                int cx, cy;
                Grid.CellToXY(myCell, out cx, out cy);
                int r = Mathf.Clamp(range, MIN_RANGE, MAX_RANGE);
                for (int y = cy - r; y <= cy + r; y++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        // Off the map edge (a cell number would wrap to the next row)
                        if (x < 0 || y < 0 || x >= Grid.WidthInCells || y >= Grid.HeightInCells) continue;
                        int cell = Grid.XYToCell(x, y);
                        if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] == worldIdx) result.Add(cell);
                    }
                return;
            }

            // Room
            if (Game.Instance == null || Game.Instance.roomProber == null) return;
            CavityInfo cavity = Game.Instance.roomProber.GetCavityForCell(myCell);
            if (cavity == null || cavity.cells == null || cavity.NumCells > MAX_ROOM_CELLS) return;
            result.AddRange(cavity.cells);
        }

        // Kept between counts (sensors count one after another on the game's main thread)
        private static readonly List<int> cellList = new List<int>();
        private static readonly HashSet<int> cellSet = new HashSet<int>();

        /// <summary>Cells in the sensor's room, or -1 if it is not in a room of up to 256 cells.</summary>
        public int RoomCells()
        {
            int myCell = Grid.PosToCell(this);
            if (!Grid.IsValidCell(myCell) || Game.Instance == null || Game.Instance.roomProber == null) return -1;
            CavityInfo cavity = Game.Instance.roomProber.GetCavityForCell(myCell);
            return cavity == null || cavity.NumCells > MAX_ROOM_CELLS ? -1 : cavity.NumCells;
        }

        private static readonly List<ScenePartitionerEntry> gathered = new List<ScenePartitionerEntry>();
        private static readonly HashSet<Pickupable> visited = new HashSet<Pickupable>();

        private float ReadAmount()
        {
            if (!HasTag()) return 0f;
            Tag tag = filterable.SelectedTag;

            if (countMode == MODE_WORLD)
            {
                if (ClusterManager.Instance == null) return 0f;
                WorldContainer world = ClusterManager.Instance.GetWorld(gameObject.GetMyWorldId());
                if (world == null || world.worldInventory == null) return 0f;
                return world.worldInventory.GetAmount(tag, false);
            }

            List<int> cells = cellList;
            FillCountedCells(cells);
            if (cells.Count == 0) return 0f;
            HashSet<int> set = cellSet;
            set.Clear();
            for (int i = 0; i < cells.Count; i++) set.Add(cells[i]);
            float total = 0f;

            // Substance right in the cells (liquids and gases). Decided by the resource itself,
            // not by the kind ticked in the panel: the chosen resource stays when the kind is
            // switched, so a gas can be counted while "Materials" is ticked.
            Element element = ElementLoader.GetElement(tag);
            if (element != null && (element.IsLiquid || element.IsGas))
                for (int i = 0; i < cells.Count; i++)
                    total += CellMass(cells[i], tag);

            // Items: on the floor, in storage, bottles/canisters —
            // ask the game only for what lies in the bounding rectangle
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                int x, y;
                Grid.CellToXY(cells[i], out x, out y);
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
            int w = maxX - minX + 1, h = maxY - minY + 1;

            GameScenePartitioner gsp = GameScenePartitioner.Instance;
            if (gsp == null) return total;
            gathered.Clear();
            visited.Clear();
            gsp.GatherEntries(minX, minY, w, h, gsp.pickupablesLayer, gathered);
            gsp.GatherEntries(minX, minY, w, h, gsp.storedPickupablesLayer, gathered);
            for (int i = 0; i < gathered.Count; i++)
            {
                Pickupable p = gathered[i].obj as Pickupable;
                if (p == null || !visited.Add(p)) continue;
                if (!set.Contains(Grid.PosToCell(p))) continue;
                total += ItemMass(p, tag);
            }
            gathered.Clear();
            visited.Clear();
            return total;
        }

        private static float CellMass(int cell, Tag tag)
        {
            Element e = Grid.Element[cell];
            if (e == null || e.tag != tag) return 0f;
            return Grid.Mass[cell];
        }

        private static float ItemMass(Pickupable p, Tag tag)
        {
            KPrefabID id = p.KPrefabID;
            if (id == null || !id.HasTag(tag)) return 0f;
            PrimaryElement pe = p.PrimaryElement;
            return pe != null ? pe.Mass : 0f;
        }

        private void Refresh()
        {
            // No data: the signal stays red, as with no resource chosen, instead of acting on a 0
            currentAmount = NoData() ? 0f : ReadAmount();
            bool on = HasTag() && !NoData() && (activateAboveThreshold ? currentAmount > threshold : currentAmount < threshold);
            switchedOn = on;
            if (logicPorts != null)
                logicPorts.SendSignal(PORT_ID, on ? 1 : 0);
            UpdateSignalStatus();
            UpdateVisual();
        }

        // The game's own "Sending a Green/Red Signal" line, as on its sensors
        private bool signalStatusShown;
        private bool signalStatusOn;

        private void UpdateSignalStatus()
        {
            if (signalStatusShown && signalStatusOn == switchedOn) return;
            KSelectable sel = GetComponent<KSelectable>();
            if (sel == null || Db.Get() == null) return;
            StatusItem item = switchedOn
                ? Db.Get().BuildingStatusItems.LogicSensorStatusActive
                : Db.Get().BuildingStatusItems.LogicSensorStatusInactive;
            sel.SetStatusItem(Db.Get().StatusItemCategories.Power, item, null);
            signalStatusShown = true;
            signalStatusOn = switchedOn;
        }

        private void UpdateVisual()
        {
            if (animController == null) return;
            // The colors and the hidden germ layer stay on the model once set (on spawn and
            // after each animation change below), so nothing to do while the signal is the same
            if (visualInitialized && lastVisualState == switchedOn) return;

            HashedString[] anims = switchedOn ? SensorSkin.OnAnims() : SensorSkin.OffAnims();
            HashedString loop = anims[anims.Length - 1];
            if (!visualInitialized || anims.Length < 2)
            {
                animController.Play(loop, KAnim.PlayMode.Loop);
            }
            else
            {
                animController.Play(anims[0], KAnim.PlayMode.Once);
                animController.Queue(loop, KAnim.PlayMode.Loop);
            }
            visualInitialized = true;
            lastVisualState = switchedOn;
            ApplyLook();
        }

        // ---------------- IThresholdSwitch (threshold) ----------------

        public float Threshold
        {
            get { return threshold; }
            set { threshold = value; Refresh(); }
        }

        public bool ActivateAboveThreshold
        {
            get { return activateAboveThreshold; }
            set { activateAboveThreshold = value; Refresh(); }
        }

        public float CurrentValue { get { return currentAmount; } }
        public float RangeMin { get { return 0f; } }
        public float RangeMax { get { return MAX_KG; } }
        public float GetRangeMinInputField() { return 0f; }
        public float GetRangeMaxInputField() { return 100000000f; }

        public LocString Title { get { return SensorStrings.SIDESCREEN_TITLE; } }
        public LocString ThresholdValueName
        {
            get
            {
                string name = HasTag() ? filterable.SelectedTag.ProperName() : (string)STRINGS.THRESHOLD.AMOUNT;
                // The name on a line of its own above the game's "— currently:" (see ThresholdReadingFit)
                SensorStrings.SetThresholdName(name + "\n");
                return SensorStrings.THRESHOLD_NAME;
            }
        }
        public string AboveToolTip { get { return SensorStrings.TOOLTIP_ABOVE; } }
        public string BelowToolTip { get { return SensorStrings.TOOLTIP_BELOW; } }

        public string Format(float value, bool units)
        {
            return FormatMass(value, units);
        }

        public static string FormatMass(float kg, bool units)
        {
            if (!units) return kg.ToString("0.#");
            if (kg > 4999f) return (kg / 1000f).ToString("0.#") + STRINGS.UNITS.TONNES;
            return kg.ToString("0.#") + STRINGS.UNITS.KG;
        }

        public float ProcessedSliderValue(float input) { return Mathf.Round(input); }
        public float ProcessedInputValue(float input) { return input; }
        public LocString ThresholdValueUnits() { return SensorStrings.UNITS_KG; }

        public ThresholdScreenLayoutType LayoutType { get { return ThresholdScreenLayoutType.SliderBar; } }
        public int IncrementScale { get { return 100; } }

        public NonLinearSlider.Range[] GetRanges
        {
            get
            {
                return new NonLinearSlider.Range[]
                {
                    new NonLinearSlider.Range(30f, 1000f),
                    new NonLinearSlider.Range(30f, 10000f),
                    new NonLinearSlider.Range(20f, 100000f),
                    new NonLinearSlider.Range(20f, MAX_KG),
                };
            }
        }
    }

    /// <summary>
    /// Threshold side screen text. The screen reads these by key from the game's string table,
    /// so the (already translated) values from STRINGS are copied there.
    /// </summary>
    public static class SensorStrings
    {
        private const string P = "STRINGS.UI.UISIDESCREENS.RESOURCECOUNTSENSOR.";

        private static LocString title, thresholdName, unitsKg;

        public static LocString SIDESCREEN_TITLE
        {
            get { if (title == null) title = new LocString(STRINGS.THRESHOLD.TITLE, P + "TITLE"); return title; }
        }
        public static LocString THRESHOLD_NAME
        {
            get { if (thresholdName == null) thresholdName = new LocString(STRINGS.THRESHOLD.AMOUNT, P + "THRESHOLD_NAME"); return thresholdName; }
        }
        public static LocString UNITS_KG
        {
            get { if (unitsKg == null) unitsKg = new LocString(STRINGS.UNITS.KG, P + "UNITS"); return unitsKg; }
        }

        public static string TOOLTIP_ABOVE { get { return ((string)STRINGS.THRESHOLD.TOOLTIP_ABOVE).Replace("{signal}", Green()); } }
        public static string TOOLTIP_BELOW { get { return ((string)STRINGS.THRESHOLD.TOOLTIP_BELOW).Replace("{signal}", Green()); } }

        public static string Green()
        {
            return global::STRINGS.UI.FormatAsAutomationState(STRINGS.SENSOR.GREEN_SIGNAL, global::STRINGS.UI.AutomationState.Active);
        }

        public static string Red()
        {
            return global::STRINGS.UI.FormatAsAutomationState(STRINGS.SENSOR.RED_SIGNAL, global::STRINGS.UI.AutomationState.Standby);
        }

        public static void SetThresholdName(string name)
        {
            Strings.Add(P + "THRESHOLD_NAME", name);
        }

        public static void Register()
        {
            Strings.Add(P + "TITLE", STRINGS.THRESHOLD.TITLE);
            Strings.Add(P + "THRESHOLD_NAME", STRINGS.THRESHOLD.AMOUNT);
            Strings.Add(P + "UNITS", STRINGS.UNITS.KG);
        }
    }
}

namespace ResourceSensor
{
    public static class SensorStatus
    {
        private static StatusItem amount, mode;

        public static StatusItem Amount()
        {
            if (amount == null)
            {
                amount = new StatusItem("ResourceSensorAmount", "ResourceSensorAmount", "", "",
                    StatusItem.IconType.Info, NotificationType.Neutral, false, OverlayModes.None.ID, 129022, false,
                    delegate (string str, object data)
                    {
                        ResourceCountSensor s = data as ResourceCountSensor;
                        return s != null ? s.StatusAmountText() : str;
                    });
                amount.resolveTooltipCallback = delegate (string str, object data)
                {
                    ResourceCountSensor s = data as ResourceCountSensor;
                    return s != null ? s.StatusAmountTooltip() : (string)STRINGS.STATUS.AMOUNT_TOOLTIP;
                };
            }
            return amount;
        }

        public static StatusItem Mode()
        {
            if (mode == null)
            {
                mode = new StatusItem("ResourceSensorMode", "ResourceSensorMode", "", "",
                    StatusItem.IconType.Info, NotificationType.Neutral, false, OverlayModes.None.ID, 129022, false,
                    delegate (string str, object data)
                    {
                        ResourceCountSensor s = data as ResourceCountSensor;
                        return s != null ? s.StatusModeText() : str;
                    });
                mode.resolveTooltipCallback = delegate (string str, object data)
                {
                    return STRINGS.STATUS.MODE_TOOLTIP;
                };
            }
            return mode;
        }
    }
}
