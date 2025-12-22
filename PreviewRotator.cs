using Godot;

public partial class PreviewRotator : Node3D
{
	[Export] public float AutoSpeedDeg = 20f;
	[Export] public float KeySpeedDeg = 90f;
	[Export] public float DragRotateSpeed = 0.01f;

	private bool _dragging = false;
	private Vector2 _lastMousePos;

	public override void _Process(double delta)
	{
		// 自动旋转（在没拖动、没按键时才转）
		if (!_dragging && !Input.IsActionPressed("ui_left") && !Input.IsActionPressed("ui_right"))
		{
			RotateY(Mathf.DegToRad(AutoSpeedDeg * (float)delta));
		}

		// 键盘旋转
		float speed = KeySpeedDeg * (float)delta;
		if (Input.IsActionPressed("ui_left"))
			RotateY(Mathf.DegToRad(speed));
		if (Input.IsActionPressed("ui_right"))
			RotateY(Mathf.DegToRad(-speed));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				_dragging = mb.Pressed;
				_lastMousePos = mb.Position;
			}
		}

		if (@event is InputEventMouseMotion mm && _dragging)
		{
			float dx = mm.Position.X - _lastMousePos.X;
			RotateY(-dx * DragRotateSpeed);
			_lastMousePos = mm.Position;
		}
	}
}
