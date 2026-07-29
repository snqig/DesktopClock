using System.Collections.Generic;

namespace DesktopClock.Models;

public class ComponentConfig
{
    public bool Enabled { get; set; } = false;
    public string Position { get; set; } = "center";
    public Dictionary<string, object> Settings { get; set; } = new();
}
