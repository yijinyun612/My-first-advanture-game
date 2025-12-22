using Godot;
using System;

/// <summary>
/// 通用角色基类：移动、输入、防御基础逻辑
/// </summary>
public partial class Character : CharacterBody3D
{
	[Export] public float BaseSpeed = 4f;
	protected Vector2 MovementInput = Vector2.Zero;

	private AnimationTree _animTree;
	private bool _defending = false; // ✅ 新增防御状态


	

	public override void _Ready()
	{
		// 如果子类（Player）有动画树，可以在那边赋值给 _animTree
		// 比如在 Player.cs 的 _Ready() 里写：
		// _animTree = GetNode<AnimationTree>(AnimationTreePath);
	}

	public override void _PhysicsProcess(double delta)
	{
		MoveLogic(delta);
		AbilityLogic(); // ✅ 调用防御逻辑
	}

	public virtual void MoveLogic(double delta)
	{
		// 子类（Player）实现移动逻辑
	}

	/// <summary>
	/// 通用防御逻辑（按下 defend 键时）
	/// </summary>
	protected virtual void AbilityLogic()
	{
		_defending = Input.IsActionPressed("defend");

		if (_defending)
		{
			GD.Print("🛡 正在防御中...");
			// 可选播放动画（如果角色有动画树）
			_animTree?.Set("parameters/DefendOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}
	}

	public bool IsDefending()
	{
		return _defending;
	}
}
