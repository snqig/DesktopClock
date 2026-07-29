using System.Collections.Generic;

namespace DesktopClock.Models;

public class LayoutConfig
{
    public List<string> ActiveComponents { get; set; } = new() { "digital_clock" };
    public List<string> ZOrder { get; set; } = new() { "date", "lunar", "digital_clock", "world_clock" };
}
