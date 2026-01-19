using Godot;
using System.Collections.Generic;

public partial class Global : Node
{
	public Dictionary<string, Dictionary<string, object>> Weapons =
		new Dictionary<string, Dictionary<string, object>>
		{
			{
				"dagger",
				new Dictionary<string, object>
				{
					{ "type", "weapon" },
					{ "damage", 1 },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/dagger.png") },
					//GD 是 Godot 提供的一个 全局静态类（类似工具类），里面有很多 静态方法，用来做通用操作。
					//Load<T>(path) 是 GD 的一个静态泛型方法，用来 加载资源（Texture、Scene、Script 等等）。
					{ "scene", GD.Load<PackedScene>("res://scenes/weapons/dagger.tscn") },
					{ "animation", "1H_Melee_Attack_Stab" },
					{ "range", 1.2f },
					{ "audio", GD.Load<AudioStream>("res://audio/dagger_sound.wav") }
				}
			},
			{
				"sword",
				new Dictionary<string, object>
				{
					{ "type", "weapon" },
					{ "damage", 2 },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/sword.png") },
					{ "scene", GD.Load<PackedScene>("res://scenes/weapons/sword.tscn") },
					{ "animation", "1H_Melee_Attack_Slice_Horizontal" },
					{ "range", 1.5f },
					{ "audio", GD.Load<AudioStream>("res://audio/sword_sound.wav") }
				}
			},
			{
				"axe",
				new Dictionary<string, object>
				{
					{ "type", "weapon" },
					{ "damage", 3 },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/axe.png") },
					{ "scene", GD.Load<PackedScene>("res://scenes/weapons/axe.tscn") },
					{ "animation", "2H_Melee_Attack_Spin" },
					{ "range", 1.3f },
					{ "audio", GD.Load<AudioStream>("res://audio/axe_sound.wav") }
				}
			},
			{
				"staff",
				new Dictionary<string, object>
				{
					{ "type", "weapon" },
					{ "damage", 1 },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/staff.png") },
					{ "scene", GD.Load<PackedScene>("res://scenes/weapons/staff.tscn") },
					{ "animation", "2H_Melee_Attack_Slice" },
					{ "range", 2.1f },
					{ "audio", GD.Load<AudioStream>("res://audio/staff_sound.wav") }
				}
			}
		};

	public Dictionary<string, Dictionary<string, object>> Shields =
		new Dictionary<string, Dictionary<string, object>>
		{
			{
				"square",
				new Dictionary<string, object>
				{
					{ "type", "shield" },
					{ "defense", 0.8f },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/square.png") },
					{ "scene", GD.Load<PackedScene>("res://scenes/shields/square_shield.tscn") }
				}
			},
			{
				"round",
				new Dictionary<string, object>
				{
					{ "type", "shield" },
					{ "defense", 0.9f },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/round.png") },
					{ "scene", GD.Load<PackedScene>("res://scenes/shields/round_shield.tscn") }
				}
			},
			{
				"spike",
				new Dictionary<string, object>
				{
					{ "type", "shield" },
					{ "defense", 0.6f },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/spike.png") },
					{ "scene", GD.Load<PackedScene>("res://scenes/shields/spike_shield.tscn") }
				}
			}
		};

	public Dictionary<string, Dictionary<string, object>> Style =
		new Dictionary<string, Dictionary<string, object>>
		{
			{
				"sunglasses",
				new Dictionary<string, object>
				{
					{ "type", "style" },
					{ "scene", GD.Load<PackedScene>("res://scenes/style/sunglasses.tscn") },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/sun_glasses.png") }
				}
			},
			{
				"starglasses",
				new Dictionary<string, object>
				{
					{ "type", "style" },
					{ "scene", GD.Load<PackedScene>("res://scenes/style/starglasses.tscn") },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/star_glasses.png") }
				}
			},
			{
				"duckhat",
				new Dictionary<string, object>
				{
					{ "type", "style" },
					{ "scene", GD.Load<PackedScene>("res://scenes/style/duck_hat.tscn") },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/duck.png") }
				}
			},
			{
				"tophat",
				new Dictionary<string, object>
				{
					{ "type", "style" },
					{ "scene", GD.Load<PackedScene>("res://scenes/style/tophat.tscn") },
					{ "thumbnail", GD.Load<Texture2D>("res://graphics/ui/thumbnails/top_hat.png") }
				}
			}
		};
}
