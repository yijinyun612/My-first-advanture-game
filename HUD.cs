using Godot;

public partial class HUD : Control
//HUD 本身 不关心血量逻辑，它只负责 “把血量数字可视化成心形 UI”。
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
	//这里就是 HUD 和 Player 配合的关键点
	//Player 管理血量数据，HUD 管理显示。Player 在游戏开始时，把自己的血量交给 HUD，让它显示出来。
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
			//Instantiate<T>() 的作用：把 PackedScene 复制一份出来，生成真正的 Node 对象。
			//<Control> 表示生成的对象类型，你这里 HeartScene 的根节点是 Control 类型。
			//返回的 heart 就是 一个可以加入 HUD 场景树的控件。
			_hearts.AddChild(heart);
		}
	}
}
