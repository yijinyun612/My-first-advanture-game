using Godot;
using System;
using System.Collections.Generic;

public partial class Pickup : Node3D
{
	// Inspector 下拉菜单（跟 PDF 一致）
	[Export(PropertyHint.Enum,
		"sword,staff,dagger,axe,square,round,spike,duckhat,tophat,sunglasses,starglasses")]
	public string PickupType = "sword";

	private Dictionary<string, object> _data;
	private float _time = 0f; // 给浮动动画用

	public override void _Ready()
	{
		// ① 订阅子节点 Area3D 的 BodyEntered 信号
		var area = GetNodeOrNull<Area3D>("Area3D");
		if (area != null)
		{
			area.BodyEntered += OnBodyEntered;
		}
		else
		{
			GD.PrintErr("Pickup: 找不到子节点 Area3D");
		}

		// ② 找 Global，按顺序从 Weapons / Shields / Style 里找对应 key
		var global = GetNodeOrNull<Global>("/root/Global");
		if (global == null)
		{
			GD.PrintErr("Pickup: 找不到 Global");
			return;
		}

		TryLoadFrom(global.Weapons);
		if (_data == null) TryLoadFrom(global.Shields);
		if (_data == null) TryLoadFrom(global.Style);

		if (_data == null)
		{
			GD.PrintErr($"Pickup: 未找到类型 = {PickupType}");
			return;
		}

		// ③ 根据 data["scene"] 实例化 3D 模型
		if (_data.TryGetValue("scene", out var sceneObj) && sceneObj is PackedScene ps)
		{
			var inst = ps.Instantiate<Node3D>();
			AddChild(inst);

			inst.Position = Vector3.Zero;
			inst.Rotation = Vector3.Zero;
			inst.Scale = Vector3.One;
		}
	}

	// 从某个 Global 字典里用 key 拿一份数据
	private void TryLoadFrom(Dictionary<string, Dictionary<string, object>> source)
	{
		if (source != null && source.TryGetValue(PickupType, out var dict))
			_data = dict;
	}

	// ========= 让拾取物浮动 + 旋转（对应 PDF 的动画）=========
	public override void _PhysicsProcess(double delta)
	{
		_time += (float)delta;

		// y 轴旋转
		RotateY(1.2f * (float)delta);

		// 上下浮动（正弦波），在当前 Y 的基础上小幅摆动
		float yOffset = Mathf.Sin(_time * 3.0f) * 0.03f;
		var pos = Position;
		pos.Y = yOffset;
		Position = pos;
	}

	// ========= 玩家碰撞时触发（PDF 最下面那段）=========
	private async void OnBodyEntered(Node3D body)
	{
		if (body is not Player player)
			return; // 不是玩家就忽略

		if (_data == null)
			return;

		// 根据类型找对应列表
		List<Dictionary<string, object>> target = null;
		string type = GetCategory();

		switch (type)
		{
			case "weapon":
				target = player.playerWeapons;
				break;
			case "shield":
				target = player.playerShields;
				break;
			case "style":
				target = player.playerStyles;
				break;
		}

		if (target == null)
			return;

		// 已经有了就不重复添加
		if (!target.Contains(_data))
		{
			target.Add(_data);
			GD.Print($"Player 拾取: {PickupType} ({type})");
		}

		// 消失动画（缩小后删除）
		var tween = CreateTween();
		tween.TweenProperty(this, "scale", new Vector3(0.1f, 0.1f, 0.1f), 0.5f);
		await ToSignal(tween, Tween.SignalName.Finished);

		QueueFree();
	}

	// ========= 小工具 =========
	public Dictionary<string, object> GetData() => _data;

	public string GetCategory()
	{
		if (_data != null && _data.TryGetValue("type", out var t) && t is string s)
			return s;
		return "";
	}

	public string GetKey() => PickupType;
}
