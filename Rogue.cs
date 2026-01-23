using Godot;
using System;

public partial class Rogue : Node3D
{
	// ====== 自动旋转速度（度/秒）======
	[Export] public float AutoRotateSpeedDeg = 20f;

	// ====== 按键旋转速度（度/秒）======
	[Export] public float KeyRotateSpeedDeg = 90f;

	// ====== 鼠标拖拽旋转速度（每像素多少度）======
	[Export] public float DragRotateSpeedDeg = 0.3f;

	// 是否自动旋转
	[Export] public bool EnableAutoRotate = true;

	// 鼠标拖拽相关
	private bool _dragging = false;
	private Vector2 _lastMousePos = Vector2.Zero;

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// -------- 自动旋转（没有在拖拽、也没按方向键时）--------
		if (EnableAutoRotate 
			&& !_dragging 
			&& !Input.IsActionPressed("ui_left") 
			&& !Input.IsActionPressed("ui_right"))
		{
			RotateY(Mathf.DegToRad(AutoRotateSpeedDeg * dt));
			//	Unity / Godot 的方法，把角度（degree）转为弧度（radian）
	        //•	因为底层旋转函数通常使用弧度而不是度数。
		}

		// -------- 键盘左右旋转 --------
		if (Input.IsActionPressed("ui_left"))
		{
			RotateY(Mathf.DegToRad(-KeyRotateSpeedDeg * dt));
		}
		if (Input.IsActionPressed("ui_right"))
		{
			RotateY(Mathf.DegToRad(KeyRotateSpeedDeg * dt));
		}

		// （真正的鼠标拖拽在 _UnhandledInput 里处理）
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// 鼠标按下 / 抬起
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_dragging = true;
				_lastMousePos = mb.Position;
			}
			else
			{
				_dragging = false;
			}
		}

		// 鼠标移动：拖拽旋转
		if (@event is InputEventMouseMotion mm && _dragging)
		{
			Vector2 delta = mm.Position - _lastMousePos;
			_lastMousePos = mm.Position;

			// 根据鼠标 X 方向旋转角色（取反是为了方向顺手一点）
			RotateY(Mathf.DegToRad(-delta.X * DragRotateSpeedDeg));
		}
	}

	// 外部想要把角色角度重置，可以调用这个
	public void ResetRotation()
	{
		Rotation = Vector3.Zero;
	}
}
