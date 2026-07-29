using System;
using System.Collections.Generic;

namespace DesktopClock.Components;

public class ComponentRegistry
{
    private readonly Dictionary<string, IClockComponent> _components = new();
    private readonly List<IClockComponent> _ordered = new();

    public void Register(IClockComponent component)
    {
        _components[component.Id] = component;
        _ordered.Add(component);
    }

    public IClockComponent? Get(string id)
    {
        _components.TryGetValue(id, out var c);
        return c;
    }

    public IEnumerable<IClockComponent> GetAll() => _ordered;

    public void UpdateAll(DateTime now)
    {
        foreach (var c in _ordered)
            c.Update(now);
    }

    public void ApplyAllConfig()
    {
        foreach (var c in _ordered)
            c.ApplyConfig();
    }
}
