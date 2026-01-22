using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Item 按钮（用于 inventory grid）
/// - Setup(key, category, data)：配置物品数据
/// - SetInventory：记录 inventory，用来回调
/// - GetData / GetCategory / GetKey：给 Inventory 用
/// </summary>
public partial class Item : Button
{
	// ================== 信号 ==================
	// 实际信号名为 "ItemSelected"
	[Signal]
	public delegate void ItemSelectedEventHandler(Item item);
	//delegate 真正厉害的地方：函数可以当变量用
	//以后凡是“没有参数、没有返回值的函数”，都可以放进 AttackDelegate 里

	// ================== 可导出路径 ==================
	[Export] public NodePath IconRectPath = "TextureRect";
	[Export] public NodePath EquippedPanelPath = "EquippedPanel";
	[Export] public NodePath LabelPath = "Label";

	// ================== 内部节点 ==================
	private TextureRect _iconRect;
	private Control _equippedPanel;
	private Label _label;

	// ================== 数据字段 ==================
	// 相当于 pdf 里的 equipment_data
	private Dictionary<string, object> _rawData = new();
	private PackedScene _scene = null;
	private string _itemType = "";
	private bool _equipped = false;

	// 新增：key / category / inventory 回调
	private string _key = "";
	private string _category = "";
	private Inventory _inventory;

	// ================== 公共属性 ==================
	public PackedScene SceneResource => _scene;
	public string ItemType => _itemType;
	public Dictionary<string, object> RawData => _rawData;
	public bool IsEquipped => _equipped;

//运行时缓存，初始化
	public override void _Ready()
	{
		_iconRect = GetNodeOrNull<TextureRect>(IconRectPath);
		_equippedPanel = GetNodeOrNull<Control>(EquippedPanelPath);
		_label = GetNodeOrNull<Label>(LabelPath);

		if (_equippedPanel != null)
			_equippedPanel.Visible = false;

		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		// ====== pdf Step 1 & 2: 调试用，打印 equipment_data =========
		GD.Print($"[Item] Pressed. key={_key}, category={GetCategory()}");

		foreach (var kv in _rawData)
		{
			GD.Print($"    data[{kv.Key}] = {kv.Value}");
		}

		// 回调 Inventory（如果有绑定）
		_inventory?.OnItemPressed(this);

		// 发出信号，名字是 "ItemSelected"（如果别的地方想连这个信号也可以）
		EmitSignal("ItemSelected", this);
	}

	// ================================================================
	// =============== Inventory 需要的新接口 ==========================
	// ================================================================
	public void Setup(string key, string category, Dictionary<string, object> data)
	{
		_key = key ?? "";
		_category = category ?? "";
		_rawData = data ?? new Dictionary<string, object>();

		// 类型：优先用 category
		if (!string.IsNullOrEmpty(category))
			_itemType = category;
		else if (_rawData.ContainsKey("type") && _rawData["type"] is string t)
			_itemType = t;

		// ----------------- 缩略图 -----------------
		Texture2D tex = null;

		// 1）data["thumbnail"]：可以是 Texture2D 或 string 路径
		if (_rawData.ContainsKey("thumbnail") && _iconRect != null)
		{
			var texObj = _rawData["thumbnail"];
			if (texObj is Texture2D t2d)
			{
				tex = t2d;//数据里已经给了一个「可以直接用的贴图资源」，那就别折腾了。
			}
			else if (texObj is string path)
			{
				var loaded = GD.Load<Texture2D>(path);
				if (loaded != null)
					tex = loaded;
			//	把加载到的贴图  •	存进本地变量 tex  •	后面统一用 tex 来设置 UI
			}
		}
		// 2）如果上面没拿到贴图，就按 key 自动找：
		//    res://graphics/ui/thumbnails/<key>.png
		if (tex == null && !string.IsNullOrEmpty(_key) && _iconRect != null)
		{
			var autoPath = $"res://graphics/ui/thumbnails/{_key}.png";
			var autoTex = GD.Load<Texture2D>(autoPath);
			if (autoTex != null)
				tex = autoTex;
		}

		if (_iconRect != null)
			_iconRect.Texture = tex;

		// ----------------- scene 字段：PackedScene 或路径 -----------------
		//匹配实例场景
		if (_rawData.ContainsKey("scene"))
		{
			var s = _rawData["scene"];
			if (s is PackedScene ps)
			{
				_scene = ps;
			}
			else if (s is string sp)
			{
				var loaded = GD.Load<PackedScene>(sp);
				if (loaded != null)
					_scene = loaded;
					//_scene  PackedScene  模板/蓝图，用来生成实体

			}
		}

		// ----------------- 名称显示（可选） -----------------
		//匹配名字
		if (_label != null)
		{
			if (_rawData.ContainsKey("name") && _rawData["name"] is string nm)
			{
				Name = nm;
				_label.Text = nm;	//把名称显示在 UI 上
			}
			else if (_rawData.ContainsKey("label") && _rawData["label"] is string lab)
			{
				Name = lab;
				_label.Text = lab;//	把名称显示在 UI 上
				//Node Name：调试、脚本引用
				//UI Label：玩家可见
			}
		}
	}

	// 兼容旧写法：Inventory 里如果还在用 item.Setup(data) 也能跑
	// 这一段是 给外部系统（Inventory / Player）用的接口
	public void Setup(Dictionary<string, object> data)
	{
		Setup("", "", data);
	}

	public void SetInventory(Inventory inv)
	{
		_inventory = inv;
	}

	public Dictionary<string, object> GetData()
	{
		return _rawData;
	}

	public string GetCategory()
	{
		return string.IsNullOrEmpty(_category) ? _itemType : _category;
	}

	public string GetKey()
	{
		return _key;
	}

	// ================================================================
	// ================= 高亮相关 ====================================
	// ================================================================
	public void SetEquipped(bool value)
	{
		_equipped = value;

		if (_equippedPanel != null)
			_equippedPanel.Visible = value;

		// 按 pdf 的意思：被选中的更亮一点
		Modulate = value ? Colors.White : new Color(0.95f, 0.95f, 0.95f);
		//	Modulate 是 Godot Node/Control 的颜色调节属性
		//可以理解为 整体颜色乘法调节
		// •	白色 = 原色显示  •	灰色 = 让控件看起来暗一点  •	value 是布尔值（true/false）
		//条件运算符 ?: 	读作：如果 value 为 true，用 Colors.White，否则用 Color(0.95,0.95,0.95)

	}

	public Texture2D GetThumbnail()
	{
		return _iconRect != null ? _iconRect.Texture : null;
		//三元运算符
		//如果有图标控件，就返回它现在用的贴图；
		//如果没有图标控件，就返回 null。
		//Thumbnail缩略图的英文
	}

	public Node CreatePreviewInstance()
	{
		if (_scene == null)
			return null;

		var inst = _scene.Instantiate();
		return inst as Node;
	}

	public override void _ExitTree()
	{
		Pressed -= OnPressed;
		base._ExitTree();
	}
}
