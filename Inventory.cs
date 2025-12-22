using Godot;
using System;
using System.Collections.Generic;

public partial class Inventory : Control
{
	// 只保留这三个导出，其他路径我们写死
	[Export] public PackedScene ItemScene;          // res://item.tscn

	[Export] public NodePath PlayerEquipmentPath;   // 3D 模型身上的 PlayerEquipment
	[Export] public NodePath PreviewEquipmentPath;  // UI 预览里的 PlayerEquipment

	private GridContainer _weaponsGrid;
	private GridContainer _shieldsGrid;
	private GridContainer _styleGrid;
	private TabContainer _tabs;

	private PlayerEquipment _playerEquip;
	private PlayerEquipment _previewEquip;

	private Global _global;

	public override void _Ready()
	{
		// 背包在暂停时也要吃输入
		ProcessMode = ProcessModeEnum.Always;

		GD.Print("=== Inventory._Ready 开始 ===");

		// 1. 用“写死路径”直接拿到三个 Grid 和 Tabs
		_weaponsGrid = GetNodeOrNull<GridContainer>(
			"HBoxContainer/ItemPanelContainer/MarginContainer2/Tabs/WeaponsTab/MarginContainer/WeaponsGrid");
		_shieldsGrid = GetNodeOrNull<GridContainer>(
			"HBoxContainer/ItemPanelContainer/MarginContainer2/Tabs/ShieldsTab/MarginContainer/ShieldsGrid");
		_styleGrid = GetNodeOrNull<GridContainer>(
			"HBoxContainer/ItemPanelContainer/MarginContainer2/Tabs/StyleTab/MarginContainer/StyleGrid");
		_tabs = GetNodeOrNull<TabContainer>(
			"HBoxContainer/ItemPanelContainer/MarginContainer2/Tabs");

		if (_weaponsGrid == null || _shieldsGrid == null || _styleGrid == null || _tabs == null)
		{
			GD.PrintErr("Inventory: 某个 Grid / Tabs 路径没找到！");
			GD.PrintErr($"  weaponsGrid null? {_weaponsGrid == null}");
			GD.PrintErr($"  shieldsGrid null? {_shieldsGrid == null}");
			GD.PrintErr($"  styleGrid   null? {_styleGrid == null}");
			GD.PrintErr($"  tabs        null? {_tabs == null}");
			return;
		}

		if (ItemScene == null)
		{
			GD.PrintErr("Inventory: ItemScene 没在 Inspector 里指定（应该指向 res://item.tscn）");
			return;
		}

		// 2. PlayerEquipment（用 Inspector 的 NodePath）
		if (PlayerEquipmentPath != null && PlayerEquipmentPath.ToString() != "")
			_playerEquip = GetNodeOrNull<PlayerEquipment>(PlayerEquipmentPath);
		if (PreviewEquipmentPath != null && PreviewEquipmentPath.ToString() != "")
			_previewEquip = GetNodeOrNull<PlayerEquipment>(PreviewEquipmentPath);

		GD.Print($"Inventory: playerEquip={_playerEquip}, previewEquip={_previewEquip}");

		// 3. 拿全局数据
		_global = GetNodeOrNull<Global>("/root/Global");
		if (_global == null)
		{
			GD.PrintErr("Inventory: 找不到 /root/Global（Autoload 没设置？）");
			return;
		}

		GD.Print($"Inventory: Weapons={_global.Weapons.Count}, Shields={_global.Shields.Count}, Style={_global.Style.Count}");

		// 4. 把三类物品生成到格子里
		AddCategory("weapon", _global.Weapons, _weaponsGrid);
		AddCategory("shield", _global.Shields, _shieldsGrid);
		AddCategory("style",  _global.Style,  _styleGrid);

		// 5. 一开始就给角色和预览挂上默认装备（各分类第一个物品）
		EquipDefaultItems();

		// 6. 初始聚焦当前 tab 的第一个按钮（第一次打开时用）
		FocusCurrentTabFirstItem();

		GD.Print("=== Inventory._Ready 结束 ===");

		// ★ 运行时默认隐藏（在编辑器里仍然显示方便摆 UI）
		if (!Engine.IsEditorHint())
			Visible = false;
	}

	// ====== 把某一类物品塞进一个 GridContainer ======
	private void AddCategory(string category,
		Dictionary<string, Dictionary<string, object>> source,
		GridContainer grid)
	{
		GD.Print($"Inventory.AddCategory 调用: {category}, source.Count={source.Count}");

		// 先清空旧的
		foreach (Node child in grid.GetChildren())
			child.QueueFree();

		int count = 0;

		foreach (var kv in source)
		{
			string key = kv.Key;
			Dictionary<string, object> data = kv.Value;

			Node instNode = ItemScene.Instantiate();
			Item item = instNode as Item;
			if (item == null)
			{
				GD.PrintErr("Inventory.AddCategory: ItemScene 不是 Item 场景，检查 item.tscn 根节点是不是 Item(Button)！");
				instNode.QueueFree();
				continue;
			}

			grid.AddChild(item);

			item.SetInventory(this);
			item.Setup(key, category, data);

			count++;
		}

		GD.Print($"Inventory.AddCategory 完成: {category}, 实际生成数量={count}");
	}

	// ====== 默认装备：各分类取第一个格子 ======
	private void EquipDefaultItems()
	{
		EquipFirstItemInGrid(_weaponsGrid);
		EquipFirstItemInGrid(_shieldsGrid);
		EquipFirstItemInGrid(_styleGrid);
	}

	private void EquipFirstItemInGrid(GridContainer grid)
	{
		if (grid == null || grid.GetChildCount() == 0)
			return;

		if (grid.GetChild(0) is Item item)
		{
			OnItemPressed(item); // 直接走统一逻辑：换装 + 高亮
		}
	}

	// ====== 高亮当前分类中被选中的 Item ======
	private void HighlightSelectedItem(string category, Item selected)
	{
		GridContainer targetGrid = null;

		switch (category)
		{
			case "weapon":
				targetGrid = _weaponsGrid;
				break;
			case "shield":
				targetGrid = _shieldsGrid;
				break;
			case "style":
				targetGrid = _styleGrid;
				break;
		}

		if (targetGrid == null)
			return;

		foreach (Node child in targetGrid.GetChildren())
		{
			if (child is Item item)
			{
				// 只有当前选中的保持高亮，其它全部取消
				item.SetEquipped(item == selected);
			}
		}
	}

	// ====== Item 按钮点击时调用 ======
	public void OnItemPressed(Item item)
	{
		if (item == null)
			return;

		string category = item.GetCategory();
		string key = item.GetKey();
		Dictionary<string, object> data = item.GetData();
		PackedScene scene = item.SceneResource;

		GD.Print($"Inventory.OnItemPressed: key={key}, category={category}, scene={(scene != null ? scene.ResourcePath : "null")}");

		if (scene == null)
			return;

		switch (category)
		{
			case "weapon":
				if (_playerEquip == null || _previewEquip == null)
					GD.PrintErr("Inventory: PlayerEquipment 没连好，武器无法装备到角色 / 预览。");
				_playerEquip?.EquipWeapon(scene, data);
				_previewEquip?.EquipWeapon(scene, data);
				break;
			case "shield":
				if (_playerEquip == null || _previewEquip == null)
					GD.PrintErr("Inventory: PlayerEquipment 没连好，盾牌无法装备到角色 / 预览。");
				_playerEquip?.EquipShield(scene, data);
				_previewEquip?.EquipShield(scene, data);
				break;
			case "style":
				if (_playerEquip == null || _previewEquip == null)
					GD.PrintErr("Inventory: PlayerEquipment 没连好，外观无法装备到角色 / 预览。");
				_playerEquip?.EquipStyle(scene, data);
				_previewEquip?.EquipStyle(scene, data);
				break;
		}

		// 更新格子高亮状态
		HighlightSelectedItem(category, item);
	}

	// ================== Tab 切换与焦点 ==================

	// 切换 tab：dir=+1 下一类，dir=-1 上一类
	private void SwitchTab(int dir)
	{
		if (_tabs == null)
			return;

		int tabCount = _tabs.GetTabCount();
		if (tabCount <= 0)
			return;

		int current = _tabs.CurrentTab;
		int next = Mathf.PosMod(current + dir, tabCount);
		_tabs.CurrentTab = next;

		// 切完后，把焦点给当前分类的第一个格子
		FocusCurrentTabFirstItem();
	}

	// 把焦点给当前 tab 里的第一个 Item（用于手柄 / 键盘操作）
	private void FocusCurrentTabFirstItem()
	{
		if (_tabs == null)
			return;

		GridContainer grid = null;

		switch (_tabs.CurrentTab)
		{
			case 0: grid = _weaponsGrid; break;
			case 1: grid = _shieldsGrid; break;
			case 2: grid = _styleGrid;   break;
		}

		if (grid == null || grid.GetChildCount() == 0)
			return;

		if (grid.GetChild(0) is Control c)
			c.GrabFocus();
	}

	// ★ 给 Player 调用的版本：打开菜单时用这个
	public void FocusFirstItem()
	{
		FocusCurrentTabFirstItem();
	}

	// 背包打开时：把 switch_weapon / switch_shield 用来切 tab
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible)
			return;

		if (@event.IsActionPressed("switch_weapon"))
		{
			SwitchTab(+1);
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("switch_shield"))
		{
			SwitchTab(-1);
			GetViewport().SetInputAsHandled();
		}
	}

	// ================== 打开 / 关闭背包（监听 M 键） ==================
	public override void _Process(double delta)
	{
		// 这里的 "menu" 必须和 Input Map 里的动作名一致
		if (Input.IsActionJustPressed("menu"))
		{
			Visible = !Visible;

			// 打开时把焦点给当前 Tab 的第一个格子
			if (Visible)
			{
				FocusFirstItem();
			}

			// 如果想打开菜单时暂停游戏，可以解除注释：
			// GetTree().Paused = Visible;
		}
	}
}
