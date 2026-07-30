using System.Collections.Generic;

namespace DesktopClock.Models;

public class LayoutConfig
{
    public string Mode { get; set; } = "stack";
    public List<string> ActiveComponents { get; set; } = new() { "digital_clock" };
    public List<string> ZOrder { get; set; } = new() { "date", "lunar", "digital_clock", "world_clock" };
    public Dictionary<string, ComponentPosition> Positions { get; set; } = new();
    public string DatePosition { get; set; } = "top";
}
