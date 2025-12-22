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
	[Export] public float Deceleration = 4f;

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
	private const float AttackMaxDuration = 0.55f;
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
	private float _squashAndStretch = 1.0f;
	public float SquashAndStretch
	{
		get => _squashAndStretch;
		set
		{
			_squashAndStretch = value;
			if (_skin != null)
			{
				float negative = 1.0f + (1.0f - _squashAndStretch);
				_skin.Scale = new Vector3(negative, _squashAndStretch, negative);
			}
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












	public override void _Ready()
	{
		// ★ 让 Player 在暂停时仍然接收 _Input
		ProcessMode = ProcessModeEnum.Always;

		// -- 相机 / 模型 --
		if (CameraPath != null && !CameraPath.IsEmpty)
			_camera = GetNodeOrNull<Camera3D>(CameraPath);

		if (SkinPath != null && !SkinPath.IsEmpty)
			_skin = GetNodeOrNull<Node3D>(SkinPath);

		// -- 动画树 --
		if (AnimationTreePath != null && !AnimationTreePath.IsEmpty)
		{
			_animTree = GetNodeOrNull<AnimationTree>(AnimationTreePath);
			if (_animTree != null)
			{
				_animTree.Active = true;

				var statePlaybackVar = _animTree.Get("parameters/StateMachine/playback");
				if (statePlaybackVar.VariantType != Variant.Type.Nil)
					_moveState = statePlaybackVar.As<AnimationNodeStateMachinePlayback>();

				var attackAnimVar = _animTree.Get("parameters/AttackAnimation");
				if (attackAnimVar.VariantType != Variant.Type.Nil)
					_attackAnimation = attackAnimVar.As<AnimationNodeAnimation>();

				_animTree.AnimationFinished += OnAnimTreeAnimationFinished;
			}
			else
			{
				GD.PrintErr($"❌ 找不到 AnimationTree: {AnimationTreePath}");
			}
		}

		// -- AnimationPlayer --
		if (AnimationPlayerPath != null && !AnimationPlayerPath.IsEmpty)
		{
			_animPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
			_animPlayer?.Stop();
		}

		// -- 重力 --
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

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
			_weaponHolder = GetOrCreateWeaponHolder(_rightHand);

		// -- 盾牌：找到左手的 ShieldSlot，清空旧盾牌，只留空 Slot --
		_leftHand = FindLeftHand();
		if (_leftHand != null)
			_shieldHolder = GetOrCreateShieldHolder(_leftHand);

		// -- 头部挂点（现在只是记录一下位置，并清空旧帽子） --
		_headSlot = FindHeadSlot();
		if (_headSlot != null)
		{
			foreach (Node child in _headSlot.GetChildren())
				child.QueueFree();
		}

		// ★ 创建自己的被打定时器
		_hitTimer = new Timer
		{
			OneShot = true,
			WaitTime = 0.35f
		};
		AddChild(_hitTimer);

		// ===== 获取 HUD 并初始化心形 =====
		_hud = null;
		if (HudPath != null && !HudPath.IsEmpty)
			_hud = GetNodeOrNull<HUD>(HudPath);
		if (_hud == null)
			_hud = GetTree().GetFirstNodeInGroup("HUD") as HUD;

		if (_hud == null)
			GD.PrintErr("❌ 没找到 HUD：请给 Player 的 HudPath 指向 HUD，或把 HUD 根节点加入 Group: \"HUD\"。");
		else
			_hud.Setup(Health);
	}













	public override void _PhysicsProcess(double delta)
	{
		// ★ 暂停时不再更新移动 / 动画，只保留输入
		if (GetTree().Paused)
			return;

		HandleMove((float)delta);
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
	public override void _Input(InputEvent @event)
	{
		// 先拿到 inventory：在当前场景根节点下面找名叫 "inventory" 的控件
		Control inventoryControl = null;
		if (GetTree().CurrentScene != null)
			inventoryControl = GetTree().CurrentScene.GetNodeOrNull<Control>("inventory");

		bool inventoryOpen = inventoryControl != null && inventoryControl.Visible;

		if (Input.IsActionJustPressed("ui_exit_to_title"))
		{
			GD.Print("Pressed exit to title!");

			GetTree().ChangeSceneToFile("res://title_screen.tscn");
		}


		// ★ ESC：只负责开关背包 / 暂停，不再退出游戏
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (inventoryControl != null)
			{
				// 如果有背包，用 ESC 开关背包
				ToggleInventory(inventoryControl);
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
	private void ToggleInventory(Control inventory)
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
	private void HandleMove(float delta)
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



	private void PlayCurrentWeaponSound()
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
	}




















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
