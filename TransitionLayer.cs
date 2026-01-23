using Godot;
using System;

public partial class TransitionLayer : CanvasLayer
{
	private ColorRect _colorRect;

	public override void _Ready()
	{
		_colorRect = GetNode<ColorRect>("ColorRect");
		_colorRect.Modulate = new Color(0, 0, 0, 0);
	}

	public async void ChangeScene(string targetPath)
	{
		var tween = CreateTween();

		// fade-in
		tween.TweenProperty(_colorRect, "modulate", new Color(0, 0, 0, 1), 0.5f);
		await ToSignal(tween, Tween.SignalName.Finished);

		// change scene
		GetTree().ChangeSceneToFile(targetPath);

		// fade-out
		var tween2 = CreateTween();
		tween2.TweenProperty(_colorRect, "modulate", new Color(0, 0, 0, 0), 0.5f);
		//在 0.5 秒内，让 _colorRect 的颜色平滑过渡到透明
	}
}
