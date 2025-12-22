using Godot;

public partial class HUD : Control
{
	// 拖到这里：heart.tscn（单个心形的场景）
	[Export] public PackedScene HeartScene { get; set; }

	// 拖到这里：HUD 场景里的 HFlowContainer（命名为 "Hearts"）
	[Export] public NodePath HeartsPath { get; set; }

	private Control _hearts; // HFlowContainer 实际引用

	public override void _Ready()
	{
		_hearts = GetNode<Control>(HeartsPath);
	}

	/// <summary>
	/// 按血量生成对应数量的心形
	/// </summary>
	public void Setup(int hearts)
	{
		if (_hearts is null || HeartScene is null)
			return;

		// 先清空旧的
		foreach (Node child in _hearts.GetChildren())
			child.QueueFree();

		// 生成新的
		for (int i = 0; i < hearts; i++)
		{
			var heart = HeartScene.Instantiate<Control>();
			_hearts.AddChild(heart);
		}
	}
}
