using KSerialization;

namespace ResourceSensor
{
    /// <summary>
    /// Buttons in the side panel (the column left of the building panel).
    /// Each button is a small separate component; the game puts buttons with
    /// the same HorizontalGroupID in one row.
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public abstract class SensorButton : KMonoBehaviour, ISidescreenButtonControl
    {
        public const int GROUP_MODE = 7101;

        private ResourceCountSensor sensor;

        protected ResourceCountSensor Sensor
        {
            get
            {
                if (sensor == null) sensor = GetComponent<ResourceCountSensor>();
                return sensor;
            }
        }

        public virtual string SidescreenTitle { get { return STRINGS.SIDESCREEN.BUTTONS_TITLE; } }
        public abstract string SidescreenButtonText { get; }
        public virtual string SidescreenButtonTooltip { get { return ""; } }
        public virtual bool SidescreenEnabled() { return Sensor != null; }
        public virtual bool SidescreenButtonInteractable() { return true; }
        public abstract void OnSidescreenButtonPressed();
        public void SetButtonTextOverride(ButtonMenuTextOverride text) { }
        public abstract int HorizontalGroupID();
        public abstract int ButtonSideScreenSortOrder();

        /// <summary>The chosen option of its row; drawn in the "selected" color (see SelectedLook).</summary>
        public virtual bool IsSelected { get { return false; } }

        // Without wrapping, so a label never breaks in the middle of a word
        protected static string NoWrap(string text)
        {
            return "<nobr>" + text + "</nobr>";
        }
    }

    // ---------- Count mode ----------

    public abstract class ModeButton : SensorButton
    {
        protected abstract int Mode { get; }
        protected abstract string Label { get; }
        private bool Selected { get { return Sensor != null && Sensor.countMode == Mode; } }
        public override bool IsSelected { get { return Selected; } }

        public override string SidescreenButtonText { get { return NoWrap(Label); } }
        public override bool SidescreenButtonInteractable() { return !Selected; }
        public override void OnSidescreenButtonPressed() { if (Sensor != null) Sensor.SetMode(Mode); }
        public override int HorizontalGroupID() { return GROUP_MODE; }
    }

    public class ModeWorldButton : ModeButton
    {
        protected override int Mode { get { return ResourceCountSensor.MODE_WORLD; } }
        protected override string Label { get { return STRINGS.SIDESCREEN.MODE_WORLD; } }
        public override string SidescreenButtonTooltip { get { return STRINGS.SIDESCREEN.MODE_WORLD_TOOLTIP; } }
        public override int ButtonSideScreenSortOrder() { return 26; }
    }

    public class ModeRangeButton : ModeButton
    {
        protected override int Mode { get { return ResourceCountSensor.MODE_RANGE; } }
        protected override string Label { get { return STRINGS.SIDESCREEN.MODE_RANGE; } }
        public override string SidescreenButtonTooltip { get { return STRINGS.SIDESCREEN.MODE_RANGE_TOOLTIP; } }
        public override int ButtonSideScreenSortOrder() { return 25; }
    }

    public class ModeRoomButton : ModeButton
    {
        protected override int Mode { get { return ResourceCountSensor.MODE_ROOM; } }
        protected override string Label { get { return STRINGS.SIDESCREEN.MODE_ROOM; } }
        public override string SidescreenButtonTooltip { get { return STRINGS.SIDESCREEN.MODE_ROOM_TOOLTIP; } }
        public override int ButtonSideScreenSortOrder() { return 24; }
    }
}
