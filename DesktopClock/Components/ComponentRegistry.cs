using System;
using System.Collections.Generic;
using System.Windows;

namespace DesktopClock.Components;

public class ComponentRegistry
{
    private readonly Dictionary<string, IClockComponent> _components = new();
    private readonly List<IClockComponent> _ordered = new();
    private readonly Dictionary<string, FrameworkElement> _external = new();

    public void Register(IClockComponent component)
    {
        _components[component.Id] = component;
        _ordered.Add(component);
    }

    public void RegisterExternal(string id, FrameworkElement view)
    {
        _external[id] = view;
    }

    public void Unregister(string id)
    {
        _external.Remove(id);
        if (_components.TryGetValue(id, out var comp))
        {
            _components.Remove(id);
            _ordered.Remove(comp);
        }
    }

    public IClockComponent? Get(string id)
    {
        _components.TryGetValue(id, out var c);
        return c;
    }

    public FrameworkElement? GetExternal(string id)
    {
        _external.TryGetValue(id, out var v);
        return v;
    }

    public IEnumerable<IClockComponent> GetAll() => _ordered;

    public IEnumerable<KeyValuePair<string, FrameworkElement>> GetAllExternal() => _external;

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
