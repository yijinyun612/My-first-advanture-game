using Godot;
using System;

public partial class TitleScreen : Control
{
	[Export]
	public string GameScenePath { get; set; } = "res://Game.tscn";

	public override void _Ready()
	{
		// Start 按钮
		var startButton = GetNode<Button>("VBoxContainer/StartButton");
		startButton.Pressed += OnStartButtonPressed;

		// Quit 按钮
		var quitButton = GetNode<Button>("VBoxContainer/QuitButton");
		quitButton.Pressed += OnQuitButtonPressed;
	}

	private void OnStartButtonPressed()
	{
		GD.Print("Start clicked!");
		GetTree().ChangeSceneToFile(GameScenePath);
	}

	private void OnQuitButtonPressed()
	{
		GD.Print("Quit clicked!");
		GetTree().Quit();   // ← 关闭运行中的游戏窗口
	}
}
