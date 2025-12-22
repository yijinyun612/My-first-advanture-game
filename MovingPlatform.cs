using Godot;

public partial class MovingPlatform : AnimatableBody3D
{
	[Export] public NodePath PointAPath;
	[Export] public NodePath PointBPath;
	[Export] public float Speed = 4.0f;

	private Node3D _pointA;
	private Node3D _pointB;
	private Node3D _target;

	public override void _Ready()
	{
		// 1. 检查有没有在 Inspector 里填路径
		if (PointAPath == null || PointAPath.ToString() == "" ||
			PointBPath == null || PointBPath.ToString() == "")
		{
			GD.PushError("[MovingPlatform] PointA / PointB 没有在 Inspector 里设置，平台停止工作。");
			return;
		}

		// 2. 根据 NodePath 找到节点
		_pointA = GetNodeOrNull<Node3D>(PointAPath);
		_pointB = GetNodeOrNull<Node3D>(PointBPath);

		if (_pointA == null || _pointB == null)
		{
			GD.PushError("[MovingPlatform] PointA / PointB 找不到节点，检查 NodePath 是否正确。");
			return;
		}

		// 3. 初始位置放在 A 点，目标设成 B 点
		GlobalPosition = _pointA.GlobalPosition;
		_target = _pointB;

		GD.Print("[MovingPlatform] Ready: A = " + _pointA.GetPath() +
				 ", B = " + _pointB.GetPath());
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_pointA == null || _pointB == null || _target == null)
			return;

		float step = Speed * (float)delta;

		// 在世界坐标中移动到目标点
		GlobalPosition = GlobalPosition.MoveToward(_target.GlobalPosition, step);

		// 接近目标点时切换方向
		float dist = GlobalPosition.DistanceTo(_target.GlobalPosition);
		if (dist <= 0.05f) // 阈值可以改大一点，比如 0.1f
		{
			// 先记录当前到达的是哪一个
			string reachedName = _target.Name;

			// 切换到另外一个点
			if (_target == _pointA)
				_target = _pointB;
			else
				_target = _pointA;

			string nextName = _target.Name;

			GD.Print("[MovingPlatform] 到达 " + reachedName +
					 " 点，切换目标为: " + nextName);
		}
	}
}
