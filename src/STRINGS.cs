namespace ResourceSensor
{
    /// <summary>
    /// UI text. English is the default; translations live in translations/&lt;language&gt;.po
    /// with keys like "ResourceSensor.STRINGS.SENSOR.NAME".
    /// Placeholders: {0}/{1} are filled by our code, except where noted.
    /// Inside this namespace the game's own strings are reached as global::STRINGS.
    /// </summary>
    public static class STRINGS
    {
        public static class SENSOR
        {
            public static LocString NAME = "Resource Sensor";
            public static LocString DESC = "Keeps track of how much of the selected resource there is on the asteroid, nearby, or in the room.";
            // {0} = green signal, {1} = red signal
            public static LocString EFFECT = "Counts the selected resource across the whole asteroid, within range of the sensor, or in its room, and sends a {0} or a {1} depending on the threshold.";

            public static LocString GREEN_SIGNAL = "Green Signal";
            public static LocString RED_SIGNAL = "Red Signal";

            public static LocString LOGIC_PORT = "Resource Sensor Output";
            // {0} = green signal
            public static LocString LOGIC_PORT_ACTIVE = "Sends a {0} when the threshold condition is met";
            // {0} = red signal
            public static LocString LOGIC_PORT_INACTIVE = "Otherwise sends a {0}";
        }

        public static class THRESHOLD
        {
            public static LocString TITLE = "Stock Threshold";
            public static LocString AMOUNT = "Amount";
            // {signal} = green signal; {0} is filled by the game with the threshold value
            public static LocString TOOLTIP_ABOVE = "Send a {signal} if there is more than <b>{0}</b> of the resource";
            public static LocString TOOLTIP_BELOW = "Send a {signal} if there is less than <b>{0}</b> of the resource";
        }

        public static class UNITS
        {
            public static LocString KG = " kg";
            public static LocString TONNES = " t";
        }

        public static class SIDESCREEN
        {
            public static LocString RESOURCE_LIST_TITLE = "Resource to Count";
            public static LocString BUTTONS_TITLE = "Sensor Settings";

            public static LocString MODE_WORLD = "On Map";
            public static LocString MODE_WORLD_TOOLTIP = "Count across the whole asteroid — the same number as in the resources panel on the right.";
            public static LocString MODE_RANGE = "Range";
            public static LocString MODE_RANGE_TOOLTIP = "Count in a square around the sensor. The range is set with the slider below.";
            public static LocString MODE_ROOM = "Room";
            public static LocString MODE_ROOM_TOOLTIP = "Count in the same enclosed area (up to 256 tiles) where the sensor stands.";

            public static LocString KIND_TITLE = "What to Count";
            public static LocString STATE_SOLID = "Materials";
            public static LocString STATE_LIQUID = "Liquids";
            public static LocString STATE_GAS = "Gases";
            public static LocString STATE_TOOLTIP = "Which resources to show in the selection list. The selected resource stays until you pick another one.";

            // Range panel (the game's capacity panel): the text before the number
            public static LocString RANGE_LABEL = "Range in tiles:";
        }

        public static class STATUS
        {
            public static LocString NO_RESOURCE = "No resource selected";
            // {0} = resource, {1} = current amount; the threshold condition is in the tooltip
            public static LocString AMOUNT = "{0}: {1}";
            // On Map with a liquid or gas chosen; {0} = resource
            public static LocString NO_DATA = "{0}: no data";
            // Tooltip while no resource is chosen
            public static LocString AMOUNT_TOOLTIP = "Pick the resource to count in the list on the left";

            public static LocString MODE_WORLD = "Mode: on map";
            // {0} = range in tiles
            public static LocString MODE_RANGE = "Mode: range, tiles {0}";
            // {0} = tiles in the room
            public static LocString MODE_ROOM = "Mode: room, tiles {0}";
            public static LocString MODE_ROOM_TOO_BIG = "Mode: room, tiles >256 (no data)";
            public static LocString MODE_TOOLTIP = "Where the sensor counts the resource: across the whole asteroid, within range, or in a room";
        }
    }
}
