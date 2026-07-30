using System.Collections.Generic;
using DesktopClock.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesktopClock.Tests;

[TestClass]
public class LayoutConfigTests
{
    [TestMethod]
    public void Default_Mode_IsStack()
    {
        var layout = new LayoutConfig();
        Assert.AreEqual("stack", layout.Mode);
    }

    [TestMethod]
    public void Default_DatePosition_IsTop()
    {
        var layout = new LayoutConfig();
        Assert.AreEqual("top", layout.DatePosition);
    }

    [TestMethod]
    public void Default_ActiveComponents_ContainsDigitalClock()
    {
        var layout = new LayoutConfig();
        CollectionAssert.Contains(layout.ActiveComponents, "digital_clock");
    }

    [TestMethod]
    public void Default_ZOrder_ContainsExpectedEntries()
    {
        var layout = new LayoutConfig();
        CollectionAssert.Contains(layout.ZOrder, "date");
        CollectionAssert.Contains(layout.ZOrder, "lunar");
        CollectionAssert.Contains(layout.ZOrder, "digital_clock");
    }

    [TestMethod]
    public void Default_Positions_IsEmpty()
    {
        var layout = new LayoutConfig();
        Assert.IsNotNull(layout.Positions);
        Assert.AreEqual(0, layout.Positions.Count);
    }

    [TestMethod]
    public void ActiveComponents_CanBeSetAndReadBack()
    {
        var layout = new LayoutConfig();
        var expected = new List<string> { "date", "lunar", "digital_clock", "world_clock" };

        layout.ActiveComponents = expected;

        CollectionAssert.AreEqual(expected, layout.ActiveComponents);
    }

    [TestMethod]
    public void ZOrder_CanBeSetAndReadBack()
    {
        var layout = new LayoutConfig();
        var expected = new List<string> { "alpha", "beta", "gamma" };

        layout.ZOrder = expected;

        CollectionAssert.AreEqual(expected, layout.ZOrder);
    }

    [TestMethod]
    public void Mode_CanBeSetAndReadBack()
    {
        var layout = new LayoutConfig();
        layout.Mode = "grid";
        Assert.AreEqual("grid", layout.Mode);
    }

    [TestMethod]
    public void DatePosition_CanBeSetAndReadBack()
    {
        var layout = new LayoutConfig();
        layout.DatePosition = "bottom";
        Assert.AreEqual("bottom", layout.DatePosition);
    }
}
