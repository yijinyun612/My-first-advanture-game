using Godot;
using System;
using System.Collections.Generic;

public partial class TransitionGate : Area3D
{
	[Export(PropertyHint.Enum, "Castle,Game")]
	//方括号表示“特性（Attribute）”
	//特性就是 给类、字段、方法、属性附加额外信息•	编译器和运行时可以读取这些信息来做额外处理
	//PropertyHint.Enum → 用下拉菜单显示固定选项
	//	"Castle,Game" → 下拉菜单的两个选项
	public string Target = "Game";

	private Dictionary<string, string> _targets = new Dictionary<string, string>()
	{
		{ "Castle", "res://Castle.tscn" },
		{ "Game", "res://Game.tscn" }
	};

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_targets.ContainsKey(Target))
		{
			var transitionLayer = GetNode<TransitionLayer>("/root/TransitionLayer");
		transitionLayer.ChangeScene(_targets[Target]);
		//_targets 是一个 场景列表/数组（或 Dictionary）
		//Target 是索引（int 或 key），表示要切换到哪一个场景
		//_targets[Target] → 得到具体的场景路径或对象
		}
	}
}
