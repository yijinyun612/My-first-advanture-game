using Godot;
using System;
using System.Collections.Generic;

public partial class TransitionGate : Area3D
{
	[Export(PropertyHint.Enum, "Castle,Game")]
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
		}
	}
}
