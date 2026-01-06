using Godot;
using System;
using System.Collections.Generic; // 为了用 List<T> / C# Dictionary

public partial class Player : CharacterBody3D
{
	// ===== 移动参数 =====
	[Export] public float BaseSpeed = 4f;
	[Export] public float RunSpeed = 6f;
	[Export] public float DefendSpeed = 2f;
	[Export] public float Acceleration = 8f;
	[Export] public float Deceleration = 4f;//减速

	// ★ 血量
	[Export] public int Health = 5;

	// ===== 相机 / 模型 =====
	[Export] public NodePath CameraPath;
	[Export] public NodePath SkinPath;

	// ===== 动画 =====
	[Export] public NodePath AnimationTreePath;
	[Export] public NodePath AnimationPlayerPath;

	private AnimationTree _animTree;
	private AnimationNodeStateMachinePlayback _moveState;
	private AnimationPlayer _animPlayer;
	private AnimationNodeAnimation _attackAnimation;

	// ===== 攻击 =====
	private bool _attacking = false;
	private float _attackTimer = 0f;
	private const float AttackMaxDuration = 0.55f;//攻击最大持续时间
	private const string AttackOneShotPath = "parameters/AttackOneShot/request";

	// ===== 防御 =====
	private bool _defending = false;
	private const string DefendBlendPath = "parameters/DefendBlend/blend_amount";

	// ===== 重力 / 跳 =====
	[Export] public float JumpSpeed = 6.0f;
	private float _gravity;

	// ===== 武器 / 盾牌 / 外观挂点（只作为 Holder，用于找当前装备） =====
	[Export] public NodePath RightHandPath;  // 指向 WeaponSlot（或 RightHand）
	[Export] public NodePath LeftHandPath;   // 指向 ShieldSlot（或 LeftHand）

	private Node3D _rightHand;
	private Node3D _leftHand;

	private Node3D _weaponHolder;            // WeaponSlot（子节点里是当前武器）
	private Node3D _shieldHolder;            // ShieldSlot（子节点里是当前盾牌）
	private Node3D _headSlot;                // 用来找当前帽子（目前只给你留着找挂点用）

	private const string WeaponHolderName = "WeaponSlot";
	private const string ShieldHolderName = "ShieldSlot";

	// ===== 其它 =====
	private Camera3D _camera;
	private Node3D _skin;
	private Vector2 _movementInput = Vector2.Zero;

	// ★ 玩家已经拥有的道具（给拾取系统用）
	public List<System.Collections.Generic.Dictionary<string, object>> playerWeapons = new();
	public List<System.Collections.Generic.Dictionary<string, object>> playerShields = new();
	public List<System.Collections.Generic.Dictionary<string, object>> playerStyles = new();

	// ====== 挤压伸展当前值 ======
	private float _squashAndStretch = 1.0f;//👉 外部不能直接改，防止乱改数值。_squashAndStretch：👉 真正存数据的“仓库”•	1.0f：👉 默认不变形（Property）控制角色模型缩放的机制：当你“赋值”的那一瞬间，机关启动了
	public float SquashAndStretch//当外界赋值，触发后面的视觉变化
	{
		get => _squashAndStretch;
		set//拦截所有赋值行为，保证任何变化都走同一条逻辑		
		{
			_squashAndStretch = value;
			if (_skin != null)//判空原因：👉 防止游戏还没加载完就报错		“角色模型已经生成了吗？如果还没加载出来，别动它。”
			{
				_skin.Scale = new Vector3(negative, _squashAndStretch, negative);//这一行是打击感的灵魂。上下被拉伸，横向被挤压			}
		}
	}

	// ★ 被打无敌时间
	private Timer _hitTimer;

	// ★ 声音（可在 Inspector 里拖）
	[Export] public NodePath HitSoundPath;
	[Export] public NodePath ShieldHitSoundPath;
	private AudioStreamPlayer3D _hitSound;
	private AudioStreamPlayer3D _shieldHitSound;

	// ===== HUD（血量显示，按教程） =====
	[Export] public NodePath HudPath;   // Inspector 可直接拖 HUD 根节点
	private HUD _hud;                    // 运行时引用






	public override void _Ready()//场景加载完成的那一刻，只执行一次
	{
		// ★ 让 Player 在暂停时仍然接收 _Input不管游戏现在是不是“暂停状态”这个节点的 _Process / _PhysicsProcess 都要继续执行		
		ProcessMode = ProcessModeEnum.Always;

		// -- 相机 / 模型 --
		if (CameraPath != null && !CameraPath.IsEmpty)
			_camera = GetNodeOrNull<Camera3D>(CameraPath);

		if (SkinPath != null && !SkinPath.IsEmpty)
			_skin = GetNodeOrNull<Node3D>(SkinPath);//GetNode<T>() → 找不到直接崩，GetNodeOrNull<T>() → 工程安全写法
		// -- 动画树 --
		if (AnimationTreePath != null && !AnimationTreePath.IsEmpty)
		{
			_animTree = GetNodeOrNull<AnimationTree>(AnimationTreePath);
			if (_animTree != null)
			{
				_animTree.Active = true;

				var statePlaybackVar = _animTree.Get("parameters/StateMachine/playback");//	•	AnimationTree 是 通用数据容器，它不知道你拿的是什么类型，所以只能给你一个 Variant（万能盒子）				if (statePlaybackVar.VariantType != Variant.Type.Nil)
					_moveState = statePlaybackVar.As<AnimationNodeStateMachinePlayback>();

				var attackAnimVar = _animTree.Get("parameters/AttackAnimation");
				if (attackAnimVar.VariantType != Variant.Type.Nil)//nil：根本没东西，在 Godot 里：Variant = 一个“什么都能装的盒子”
					_attackAnimation = attackAnimVar.As<AnimationNodeAnimation>();//_attackAnimation-变量，AnimationNodeAnimation-类型
					//attackAnimVar.As<AnimationNodeAnimation>() 这是 Godot C# 提供的安全类型转换方法，如果内容真的是 AnimationNodeAnimation，那就把你当作这个类型用
					//从 attackAnimVar 这个“动画节点变量”里，尝试取出一个真正的「攻击动画节点」，如果成功，就存到 _attackAnimation 里备用。
				    //先从树里拿一个“泛型节点”，再 As 成具体节点，调用具体 API（如设置动画名）
					//As<T>() = Godot 提供的安全类型转换，转成功得到对象，失败得到 null（Nil 状态）。

				_animTree.AnimationFinished += OnAnimTreeAnimationFinished;//当 AnimationTree 里“某个动画播放结束”时，自动调用OnAnimTreeAnimationFinished 这个方法。
			else
			{
				GD.PrintErr($"❌ 找不到 AnimationTree: {AnimationTreePath}");
			}
		}

		// -- AnimationPlayer --
		if (AnimationPlayerPath != null && !AnimationPlayerPath.IsEmpty)
		{
			_animPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
			_animPlayer?.Stop();//“如果左边不是 null，才调用右边的方法” 这是简写体
		}

		// -- 重力 --
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();//AsSingle/float单精度浮点数 把3d重力值转换成单精度浮点数，ProjectSettings = Godot 提供的全局配置接口，GetSetting() = 按 路径 获取设置项
//	"physics/3d/default_gravity" = 3D 物理默认重力（Godot 编辑器里 Physics → 3D → Default Gravity）

		// -- 被打音效节点 --
		if (HitSoundPath != null && !HitSoundPath.IsEmpty)
			_hitSound = GetNodeOrNull<AudioStreamPlayer3D>(HitSoundPath);
		if (_hitSound == null)
			_hitSound = GetNodeOrNull<AudioStreamPlayer3D>("HitSound");

		if (ShieldHitSoundPath != null && !ShieldHitSoundPath.IsEmpty)
			_shieldHitSound = GetNodeOrNull<AudioStreamPlayer3D>(ShieldHitSoundPath);
		if (_shieldHitSound == null)
			_shieldHitSound = GetNodeOrNull<AudioStreamPlayer3D>("ShieldHitSound");

		// ===================== 挂点：只负责找到 WeaponSlot / ShieldSlot / 头部 =====================

		// -- 武器：找到右手的 WeaponSlot，清空旧武器，只留空 Slot --
		_rightHand = FindRightHand();
		if (_rightHand != null)
			_weaponHolder = GetOrCreateWeaponHolder(_rightHand);//GetOrCreate = 保证我一定能拿到一个可用对象，

		// -- 盾牌：找到左手的 ShieldSlot，清空旧盾牌，只留空 Slot --
		_leftHand = FindLeftHand();
		if (_leftHand != null)
			_shieldHolder = GetOrCreateShieldHolder(_leftHand);

		// -- 头部挂点（现在只是记录一下位置，并清空旧帽子） --
		_headSlot = FindHeadSlot();
		if (_headSlot != null)
		{
			foreach (Node child in _headSlot.GetChildren())
				child.QueueFree();//QueueFree() 的优势：	延迟到安全时机•	允许：•	在 _Process•	在 foreach•	在信号回调中
		}

		// ★ 创建自己的被打定时器
		_hitTimer = new Timer
		{
			OneShot = true,
			WaitTime = 0.35f
		};
		AddChild(_hitTimer);//把一个节点，挂到当前节点下面，成为子节点

		// ===== 获取 HUD 并初始化心形 =====
		_hud = null;
		if (HudPath != null && !HudPath.IsEmpty)
			_hud = GetNodeOrNull<HUD>(HudPath);
		if (_hud == null)
			_hud = GetTree().GetFirstNodeInGroup("HUD") as HUD;//查找第一个被加入到 "HUD" 这个 Group 的节点
//as HUD 这是 C# 的安全类型转换，如果这个节点真的是 HUD 类型，就转成 HUD，如果不是，返回 null，不报错。

		if (_hud == null)
			GD.PrintErr("❌ 没找到 HUD：请给 Player 的 HudPath 指向 HUD，或把 HUD 根节点加入 Group: \"HUD\"。");
		else
			_hud.Setup(Health);//把角色当前的“生命值数据”，交给 HUD，让 HUD 按这个数据完成初始化显示。
	}
//Setup(...) 这是一个初始化方法，而不是 Update / Tick。通常只在：•	角色生成•	场景加载•	HUD 刚绑定角色的时候调用。












	public override void _PhysicsProcess(double delta)//每一帧重新计算角色的功能
	{
		// ★ 暂停时不再更新移动 / 动画，只保留输入
		if (GetTree().Paused)//GetTree() 获取当前 SceneTree（整个游戏的运行树）		
		return;//如果游戏暂停，立刻退出当前函数，后面的代码都不执行。因为前面的代码决定了游戏暂停时，重力，碰撞继续产生 所以这里需要手动暂停

		HandleMove((float)delta);//HandleMove方法名（函数）	“处理移动相关的事情”		delta 原本是 double	速度 / 向量一般用 float 所以做了 显式类型转换		“这一帧过去了多少时间”		
		HandleJump((float)delta);
		HandleRotateSkin((float)delta);
		UpdateAnimationState();

		HandleAttack();
		CheckAttackFinished();
		CheckAttackTimer((float)delta);

		HandleDefend();

		MoveAndSlide();
	}
	










	// ===================== ★ 输入 =====================
	public override void _Input(InputEvent @event)//只判断“发生了什么输入”，不负责角色怎么动
	{
		// 先拿到 inventory：在当前场景根节点下面找名叫 "inventory" 的控件
		Control inventoryControl = null;
		if (GetTree().CurrentScene != null)
			inventoryControl = GetTree().CurrentScene.GetNodeOrNull<Control>("inventory");
                      //获取control类型的叫做inventory的uI控件，control是所有/UI的基类
		bool inventoryOpen = inventoryControl != null && inventoryControl.Visible;//只有在“背包节点存在”并且“当前是可见状态”时，才认为背包是打开的
		//bool inventoryOpen = ... 定义一个布尔变量，用来描述一个状态，这个状态会被后续逻辑使用，这是一个不做任何行为的状态判断
		if (Input.IsActionJustPressed("ui_exit_to_title"))
		{
			GD.Print("Pressed exit to title!");

			GetTree().ChangeSceneToFile("res://title_screen.tscn");//ChangeSceneToFile() → 切换场景的方法
			//整体效果：1.	卸载当前场景  2.	加载新场景  3.	设置新场景为 CurrentScene
			//使用时机  •	游戏开始 → 切到主菜单  •	游戏结束 → 切到结算界面  •	关卡通关 → 切下一个关卡
		}


		// ★ ESC：只负责开关背包 / 暂停，不再退出游戏
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (inventoryControl != null)
			{
				// 如果有背包，用 ESC 开关背包
				ToggleInventory(inventoryControl);//“Toggle” = 切换 / 开关
			}
			else
			{
				// 没有背包，就只切换暂停
				bool newPaused = !GetTree().Paused;
				GetTree().Paused = newPaused;
				Input.MouseMode = newPaused
					? Input.MouseModeEnum.Visible
					: Input.MouseModeEnum.Captured;
			}
			return;
		}

		// ★ M：打开 / 关闭背包（menu 动作）
		if (@event.IsActionPressed("menu"))
		{
			if (inventoryControl != null)
			{
				ToggleInventory(inventoryControl);
			}
			else
			{
				bool newPaused = !GetTree().Paused;
				GetTree().Paused = newPaused;
				Input.MouseMode = newPaused
					? Input.MouseModeEnum.Visible
					: Input.MouseModeEnum.Captured;
				GD.Print("⚠ 没找到 inventory，只做暂停切换。");
			}
			return;
		}

		// ★ 新增：退出游戏快捷键（Input Map 里绑定 quit_game，比如 F10 或 Ctrl+Q）
		if (@event.IsActionPressed("quit_game"))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GetTree().Quit();
			return;
		}

		// ★ 背包打开时：玩家不再处理其他按键
		if (inventoryOpen)
		{
			return;
		}

		// ✅ 不再处理 switch_weapon / switch_shield / switch_style
		//    真正换装交给 Inventory + PlayerEquipment
	}














	// ★ 小工具：切换 inventory 显隐 + 暂停
	private void ToggleInventory(Control inventory)//装备管理器
	{
		bool newVisible = !inventory.Visible;
		inventory.Visible = newVisible;

		GetTree().Paused = newVisible;
		Input.MouseMode = newVisible
			? Input.MouseModeEnum.Visible
			: Input.MouseModeEnum.Captured;

		// 打开菜单时，把焦点给当前 Tab 的第一个 Item
		if (newVisible && inventory is Inventory inv)
		{
			inv.FocusFirstItem();
		}

		GD.Print($"Inventory 现在: {(newVisible ? "打开" : "关闭")}");
	}
	








	// =============== 找节点 ==================
	private Node3D FindRightHand()
	{
		if (RightHandPath != null && !RightHandPath.IsEmpty)
		{
			var n = GetNodeOrNull<Node3D>(RightHandPath);
			if (n != null) return n;
		}

		string[] candidates =
		{
			"PlayerSkin/Rogue/Rig/Skeleton3D/RightHand/WeaponSlot",
			"PlayerSkin/Rogue/Rig/Skeleton3D/RightHand"
		};
		foreach (var path in candidates)
		{
			var n = GetNodeOrNull<Node3D>(path);
			if (n != null)
				return n;
		}
		return null;
	}
	



	private Node3D FindLeftHand()
	{
		if (LeftHandPath != null && !LeftHandPath.IsEmpty)
		{
			var n = GetNodeOrNull<Node3D>(LeftHandPath);
			if (n != null) return n;
		}

		string[] candidates =
		{
			"PlayerSkin/Rogue/Rig/Skeleton3D/LeftHand/ShieldSlot",
			"PlayerSkin/Rogue/Rig/Skeleton3D/LeftHand",
			"PlayerSkin/Rogue/Rig/Skeleton3D/LeftHand/WeaponSlot2"
		};
		foreach (var path in candidates)
		{
			var n = GetNodeOrNull<Node3D>(path);
			if (n != null)
				return n;
		}
		return null;
	}
	



	private Node3D FindHeadSlot()
	{
		string[] candidates =
		{
			"Head/HatOffset",
			"Head",
			"head",
			"PlayerSkin/Rogue/Rig/Skeleton3D/Head",
			"PlayerSkin/Rogue/Rig/Skeleton3D/HeadSlot",
			"PlayerSkin/Rogue/Rig/Skeleton3D/Rogue_Head"
		};

		foreach (var path in candidates)
		{
			var n = GetNodeOrNull<Node3D>(path);
			if (n != null)
			{
				GD.Print($"✅ 找到头部挂点: {path}");
				return n;
			}
		}

		GD.Print("⚠ 没找到头部挂点，style 切换不会显示。");
		return null;
	}

















	// =============== 移动 / 动画 ===============
	private void HandleMove(float delta)//动画管理器
	{
		_movementInput = Input.GetVector("left", "right", "forward", "backward");

		float targetSpeed = BaseSpeed;
		if (Input.IsActionPressed("run"))
			targetSpeed = RunSpeed;
		if (_defending)
			targetSpeed = DefendSpeed;

		if (_camera != null && _movementInput != Vector2.Zero)
			_movementInput = _movementInput.Rotated(-_camera.GlobalRotation.Y);

		Vector2 vel2D = new Vector2(Velocity.X, Velocity.Z);

		if (_movementInput != Vector2.Zero)
			vel2D = vel2D.MoveToward(_movementInput * targetSpeed, Acceleration * delta);
		else
			vel2D = vel2D.MoveToward(Vector2.Zero, Deceleration * delta);

		Velocity = new Vector3(vel2D.X, Velocity.Y, vel2D.Y);
	}



	private void HandleJump(float delta)
	{
		if (!IsOnFloor())
			Velocity += Vector3.Down * _gravity * delta;
		else if (Input.IsActionJustPressed("jump"))
			Velocity = new Vector3(Velocity.X, JumpSpeed, Velocity.Z);
	}




	private void HandleRotateSkin(float delta)
	{
		if (_skin == null || _movementInput == Vector2.Zero)
			return;

		float targetAngle = -_movementInput.Angle() + Mathf.Pi / 2f;
		float currentY = _skin.Rotation.Y;
		float newY = MoveTowardAngle(currentY, targetAngle, delta * 6f);
		_skin.Rotation = new Vector3(_skin.Rotation.X, newY, _skin.Rotation.Z);
	}





	private void UpdateAnimationState()
	{
		if (_moveState == null)
			return;

		if (!IsOnFloor())
		{
			_moveState.Travel("Jump_Idle");
			return;
		}

		if (_movementInput != Vector2.Zero)
			_moveState.Travel("Running_A");
		else
			_moveState.Travel("Idle");
	}

	private float MoveTowardAngle(float from, float to, float step)
	{
		float diff = Mathf.AngleDifference(from, to);
		if (Mathf.Abs(diff) <= step)
			return to;
		return from + Mathf.Sign(diff) * step;
	}



	// =============== 攻击 / 防御 ===============
	private void HandleAttack()
	{
		if (_animTree == null || _attacking)
			return;

		if (Input.IsActionJustPressed("attack"))
		{
			_animTree.Set(AttackOneShotPath, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			_attacking = true;
			_attackTimer = AttackMaxDuration;

			PlayCurrentWeaponSound();
		}
	}






	private void PlayCurrentWeaponSound()//声音触发器
	{
		// 利用 WeaponSlot 里当前武器的 WeaponSound 来播放音效
		if (_weaponHolder == null || _weaponHolder.GetChildCount() == 0)
			return;

		var weaponNode = _weaponHolder.GetChild(0);

		if (weaponNode is WeaponSound wsRoot)
		{
			wsRoot.PlayAudio();
			return;
		}

		var ws = (weaponNode as Node)?.GetNodeOrNull<WeaponSound>("WeaponSound");
		ws?.PlayAudio();
	}




	private void CheckAttackFinished()
	{
		if (_animTree == null || !_attacking)
			return;

		var activeVar = _animTree.Get("parameters/AttackOneShot/active");
		if (activeVar.VariantType == Variant.Type.Nil)
			return;

		if (!activeVar.As<bool>())
		{
			_attacking = false;
			_attackTimer = 0f;
		}
	}



	private void CheckAttackTimer(float delta)
	{
		if (!_attacking)
			return;

		_attackTimer -= delta;
		if (_attackTimer <= 0f)
		{
			_animTree?.Set(AttackOneShotPath, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			_attacking = false;
		}
	}



	private void OnAnimTreeAnimationFinished(StringName animName)
	{
		if (animName.ToString().Contains("Attack"))
		{
			_attacking = false;
			_attackTimer = 0f;
		}
	}



	private void HandleDefend()
	{
		if (_animTree == null)
			return;

		bool isDefending = Input.IsActionPressed("defend");
		_defending = isDefending;
		_animTree.Set(DefendBlendPath, isDefending ? 1.0f : 0.0f);




	// =============== Weapon / Shield Holder（清理旧武器/盾牌，只保留 Slot） ===============
	private Node3D GetOrCreateWeaponHolder(Node3D rightHand)
	{
		// 先找 RightHand 下面有没有叫 WeaponSlot 的子节点
		Node3D holder = rightHand.GetNodeOrNull<Node3D>(WeaponHolderName);
		if (holder == null)
		{
			// 没有就新建一个空挂点
			holder = new Node3D { Name = WeaponHolderName };
			rightHand.AddChild(holder);
		}

		// ★ 1）清空 WeaponSlot 里面原来挂着的东西
		foreach (Node child in holder.GetChildren())
			child.QueueFree();

		// ★ 2）把 RightHand 下面除了 WeaponSlot 以外的旧节点全部删掉（清理旧武器）
		foreach (Node child in rightHand.GetChildren())
		{
			if (child != holder)
				child.QueueFree();
		}

		holder.Position = Vector3.Zero;
		holder.Rotation = Vector3.Zero;
		holder.Scale = Vector3.One;
		return holder;
	}




	private Node3D GetOrCreateShieldHolder(Node3D leftHand)
	{
		Node3D holder = leftHand.GetNodeOrNull<Node3D>(ShieldHolderName);
		if (holder == null)
		{
			holder = new Node3D { Name = ShieldHolderName };
			leftHand.AddChild(holder);
		}

		// ★ 1）清空 ShieldSlot 里面原来挂着的盾牌
		foreach (Node child in holder.GetChildren())
			child.QueueFree();

		// ★ 2）把 LeftHand 下面除了 ShieldSlot 以外的旧节点全部删掉（清理旧盾牌）
		foreach (Node child in leftHand.GetChildren())
		{
			if (child != holder)
				child.QueueFree();
		}

		holder.Position = Vector3.Zero;
		holder.Rotation = Vector3.Zero;
		holder.Scale = Vector3.One;
		return holder;
	}












	// =============== 被打 ===============
	public void Hit(Node3D weapon)
	{
		if (_hitTimer != null && _hitTimer.TimeLeft > 0f)
			return;

		string from = weapon != null ? weapon.Name.ToString() : "unknown";
		GD.Print($"player 被打，来自: {from}");

		float damage = 1.0f;
		if (weapon != null)
		{
			var damageProp = weapon.Get("Damage");
			if (damageProp.VariantType != Variant.Type.Nil)
				damage = damageProp.AsSingle();
			else if (weapon.HasMeta("damage"))
				damage = (float)weapon.GetMeta("damage");
		}

		Shield currentShield = GetCurrentShield();
		if (_defending && currentShield != null)
		{
			damage *= currentShield.Defense;
			currentShield.Flash();
			_shieldHitSound?.Play();
		}
		else
		{
			DoSquashAndStretch(1.2f, 0.2f);
			_hitSound?.Play();
		}

		Health -= Mathf.CeilToInt(damage);
		GD.Print($"player 剩余血量: {Health}");

		_hud?.Setup(Mathf.Max(Health, 0));

		if (Health <= 0)
			DeathLogic();

		_hitTimer?.Start();
	}



	public void Hit()
	{
		Hit(null);
	}




	private void DoSquashAndStretch(float value, float duration)
	{
		var tween = CreateTween();
		tween.TweenProperty(this, nameof(SquashAndStretch), value, duration);
		tween.TweenProperty(this, nameof(SquashAndStretch), 1.0f, duration * 1.8f)
			.SetEase(Tween.EaseType.Out);
	}




	private Shield GetCurrentShield()
	{
		if (_shieldHolder == null || _shieldHolder.GetChildCount() == 0)
			return null;

		var root = _shieldHolder.GetChild(0);

		if (root is Shield sh)
			return sh;

		foreach (Node child in root.GetChildren())
		{
			if (child is Shield s)
				return s;
		}
		return null;
	}




	private void DeathLogic()
	{
		GD.Print("player 死亡，退出游戏");
		GetTree().Quit();
	}
}
