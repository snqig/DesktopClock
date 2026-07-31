using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using DesktopClock.Models;

namespace DesktopClock.Services;

/// <summary>
/// 全局指针样式管理器:加载、查询、保存、复制、删除指针方案。
/// 方案持久化到 settings.json 的 PointerSets 数组,程序启动自动加载。
/// </summary>
public class PointerStyleManager
{
    private readonly List<PointerSet> _sets = new();
    private string _dataDir = string.Empty;

    /// <summary>所有方案(只读视图)</summary>
    public ReadOnlyObservableCollection<PointerSet> Sets { get; private set; } = new(new());

    /// <summary>方案变更事件(增删改后触发,UI 刷新用)</summary>
    public event Action? Changed;

    /// <summary>数据目录(AppSettings.PointerSetsPath)</summary>
    public string DataDir
    {
        get => _dataDir;
        set
        {
            _dataDir = value;
            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
        }
    }

    /// <summary>方案 JSON 文件路径</summary>
    private string SetsJsonPath => Path.Combine(_dataDir, "pointer_sets.json");

    /// <summary>用户导入的 PNG 存放目录</summary>
    private string UserAssetsDir => Path.Combine(_dataDir, "PointerSets");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    public PointerStyleManager() { }

    /// <summary>从 JSON 文件加载全部方案</summary>
    public void Load()
    {
        _sets.Clear();
        try
        {
            if (File.Exists(SetsJsonPath))
            {
                var json = File.ReadAllText(SetsJsonPath);
                var loaded = JsonSerializer.Deserialize<List<PointerSet>>(json, JsonOpts);
                if (loaded != null) _sets.AddRange(loaded);
            }
        }
        catch { /* 加载失败不崩溃,用内置默认 */ }

        // 首次运行无方案 → 写入 5 套内置预置
        if (_sets.Count == 0)
        {
            EnsureBuiltinSets();
            Save();
        }

        RefreshObservable();
    }

    /// <summary>保存全部方案到 JSON</summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_sets, JsonOpts);
            File.WriteAllText(SetsJsonPath, json);
        }
        catch { /* 保存失败不崩溃 */ }
    }

    /// <summary>获取所有方案列表</summary>
    public IEnumerable<PointerSet> GetAll() => _sets;

    /// <summary>按 ID 获取方案</summary>
    public PointerSet? GetById(string id) => _sets.FirstOrDefault(s => s.Id == id);

    /// <summary>按分类筛选</summary>
    public IEnumerable<PointerSet> GetByCategory(string category) =>
        _sets.Where(s => string.IsNullOrEmpty(category) || s.Category == category);

    /// <summary>所有分类(去重)</summary>
    public IEnumerable<string> GetCategories() =>
        _sets.Select(s => s.Category).Distinct().OrderBy(c => c);

    /// <summary>添加新方案</summary>
    public PointerSet Add(PointerSet set)
    {
        _sets.Add(set);
        Save();
        RefreshObservable();
        return set;
    }

    /// <summary>新建空白方案</summary>
    public PointerSet CreateNew(string name = "新方案", string category = "自制")
    {
        var set = new PointerSet { Name = name, Category = category };
        return Add(set);
    }

    /// <summary>复制现有方案</summary>
    public PointerSet Duplicate(string id)
    {
        var src = GetById(id);
        if (src == null) return null!;
        var copy = src.Clone();
        return Add(copy);
    }

    /// <summary>更新方案</summary>
    public void Update(PointerSet set)
    {
        var idx = _sets.FindIndex(s => s.Id == set.Id);
        if (idx >= 0)
        {
            _sets[idx] = set;
            Save();
            RefreshObservable();
        }
    }

    /// <summary>删除方案</summary>
    public bool Delete(string id)
    {
        var set = GetById(id);
        if (set == null) return false;
        _sets.Remove(set);
        Save();
        RefreshObservable();
        return true;
    }

    /// <summary>重命名</summary>
    public void Rename(string id, string newName)
    {
        var set = GetById(id);
        if (set == null) return;
        set.Name = newName;
        Save();
        RefreshObservable();
    }

    /// <summary>收藏/取消收藏</summary>
    public void ToggleFavorite(string id)
    {
        var set = GetById(id);
        if (set == null) return;
        set.Favorite = !set.Favorite;
        Save();
        RefreshObservable();
    }

    /// <summary>混搭创建新方案:A时针 + B分针 + C秒针</summary>
    public PointerSet CreateMix(
        string name, string category,
        SinglePointerStyle hour, SinglePointerStyle minute, SinglePointerStyle second)
    {
        var set = PointerSet.FromMix(name, category, hour, minute, second);
        return Add(set);
    }

    /// <summary>导入 PNG 到用户素材目录</summary>
    public string ImportPng(string sourcePath, string setName)
    {
        var dir = Path.Combine(UserAssetsDir, setName);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var fileName = Path.GetFileName(sourcePath);
        var dest = Path.Combine(dir, fileName);
        File.Copy(sourcePath, dest, true);
        return dest;
    }

    /// <summary>将绝对路径转为 pack URI(用于 WPF Image Source)</summary>
    public static string ToPackUri(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        if (relativePath.StartsWith("pack:") || relativePath.StartsWith("http"))
            return relativePath;
        // 统一为正斜杠
        var normalized = relativePath.Replace('\\', '/');
        return $"pack://application:,,,/{normalized}";
    }

    // === 内置 5 套预置方案 ===
    private void EnsureBuiltinSets()
    {
        _sets.Add(Builtin("Cyberpunk", "夜光", "赛博朋克霓虹发光指针"));
        _sets.Add(Builtin("GlowTech", "夜光", "科技夜光指针"));
        _sets.Add(Builtin("Vintage", "复古", "复古经典指针"));
        _sets.Add(Builtin("Minimal", "极简", "极简细针"));
        _sets.Add(Builtin("GhostBlue", "自制", "幽灵蓝指针"));
    }

    private static PointerSet Builtin(string dir, string category, string name) => new()
    {
        Name = name,
        Category = category,
        HourStyle = StyleFor(dir, "hour"),
        MinuteStyle = StyleFor(dir, "minute"),
        SecondStyle = StyleFor(dir, "second")
    };

    private static SinglePointerStyle StyleFor(string dir, string hand) => new()
    {
        ImagePath = $"Assets/PointerSets/{dir}/{hand}.png",
        RotationCenterX = 0.5,
        RotationCenterY = 1.0,
        Scale = 1.0
    };

    private void RefreshObservable()
    {
        var oc = new ObservableCollection<PointerSet>();
        // 收藏的置顶,然后按名称排序
        foreach (var s in _sets.OrderByDescending(x => x.Favorite).ThenBy(x => x.Name))
            oc.Add(s);
        Sets = new(oc);
        Changed?.Invoke();
    }
}
