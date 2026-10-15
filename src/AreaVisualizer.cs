using System.Collections.Generic;
using UnityEngine;

namespace ResourceSensor
{
    /// <summary>
    /// Shows the counted area in Range and Room modes with the game's own range highlight
    /// (the same white rounded tiles as the Duplicant Motion Sensor or a critter trap).
    /// The game draws it for the selected building from its RangeVisualizer component;
    /// here that component is kept in sync with the sensor: the rectangle around the counted
    /// tiles, and every tile outside the counted area marked as blocked so it is not lit.
    /// In On Map mode there is no area, so the component is removed.
    /// </summary>
    public class AreaVisualizer : KMonoBehaviour
    {
        private const float REFRESH_SECONDS = 0.5f;

        [MyCmpGet] private ResourceCountSensor sensor;
        [MyCmpGet] private KSelectable selectable;

        private readonly HashSet<int> counted = new HashSet<int>();
        private float nextRefresh;

        private bool Selected()
        {
            if (selectable != null) return selectable.IsSelected;
            return SelectTool.Instance != null && SelectTool.Instance.selected != null
                && SelectTool.Instance.selected.gameObject == gameObject;
        }

        private void Update()
        {
            // Only matters while selected: the game draws the range for the selected building only
            if (sensor == null || !Selected()) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + REFRESH_SECONDS;

            RangeVisualizer rv = GetComponent<RangeVisualizer>();
            if (sensor.countMode == ResourceCountSensor.MODE_WORLD)
            {
                if (rv != null) Destroy(rv);
                return;
            }
            if (rv == null) rv = gameObject.AddComponent<RangeVisualizer>();
            Sync(rv);
        }

        private void Sync(RangeVisualizer rv)
        {
            int myCell = Grid.PosToCell(this);
            List<int> cells = sensor.GetCountedCells();
            counted.Clear();
            if (cells != null)
                for (int i = 0; i < cells.Count; i++) counted.Add(cells[i]);

            int mx, my;
            Grid.CellToXY(myCell, out mx, out my);
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (int cell in counted)
            {
                int x, y;
                Grid.CellToXY(cell, out x, out y);
                minX = Mathf.Min(minX, x - mx); maxX = Mathf.Max(maxX, x - mx);
                minY = Mathf.Min(minY, y - my); maxY = Mathf.Max(maxY, y - my);
            }

            rv.OriginOffset = new Vector2I(0, 0);
            rv.RangeMin = new Vector2I(minX, minY);
            rv.RangeMax = new Vector2I(maxX, maxY);
            // A room can be long in one direction: make the game's texture big enough
            rv.TexSize = new Vector2I(Mathf.Max(64, maxX - minX + 2), Mathf.Max(64, maxY - minY + 2));
            rv.TestLineOfSight = false;
            // Tiles outside the counted area are "blocked", so they stay unlit;
            // with nothing counted (room too big), nothing is lit
            rv.BlockingCb = IsNotCounted;
        }

        private bool IsNotCounted(int cell)
        {
            return !counted.Contains(cell);
        }
    }
}
