using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// PlayerEquipment
/// - 支持 3D 装备（Node3D）
/// - 支持 UI 预览装备（Control）
/// - 自动清空旧实例
/// - 与 Inventory.cs / Item.cs 对接
/// </summary>
public partial class PlayerEquipment : Node
{
	[Export] public NodePath WeaponSocketPath;
	[Export] public NodePath ShieldSocketPath;
	[Export] public NodePath StyleSocketPath;

	private Node _weaponSocket;
	private Node _shieldSocket;
	private Node _styleSocket;

	private Node _weaponInstance;
	private Node _shieldInstance;
	private Node _styleInstance;

	public override void _Ready()
	{
		// 1⃣ 先按 Inspector 里拖的路径拿一遍（如果有的话）
		if (WeaponSocketPath != null && WeaponSocketPath.ToString() != "")
			_weaponSocket = GetNodeOrNull<Node>(WeaponSocketPath);

		if (ShieldSocketPath != null && ShieldSocketPath.ToString() != "")
			_shieldSocket = GetNodeOrNull<Node>(ShieldSocketPath);

		if (StyleSocketPath != null && StyleSocketPath.ToString() != "")
			_styleSocket = GetNodeOrNull<Node>(StyleSocketPath);

		// 2⃣ 如果上面没拿到，就自动从骨骼里找 RightHand / LeftHand / HatOffset
		AutoFindSocketsFromSkeleton();

		GD.Print($"[PlayerEquipment] Ready -> weapon={_weaponSocket}, shield={_shieldSocket}, style={_styleSocket}");

		// 3⃣ 一上来就把三个插槽下面“所有孩子”全部清空
		//    包括场景里预先摆好的 Dagger / RoundShield / 帽子等等
		ClearSocketChildren(_weaponSocket);
		ClearSocketChildren(_shieldSocket);
		ClearSocketChildren(_styleSocket);

		_weaponInstance = null;
		_shieldInstance = null;
		_styleInstance = null;
	}

	/// <summary>
	/// 从当前节点往上找到 Skeleton3D，再在骨骼下面找 RightHand/WeaponSlot、LeftHand/ShieldSlot、HatOffset/Head
	/// </summary>
	private void AutoFindSocketsFromSkeleton()
	{
		Skeleton3D skeleton = FindParentSkeleton3D();
		if (skeleton == null)
		{
			GD.Print("[PlayerEquipment] AutoFindSocketsFromSkeleton: 没找到 Skeleton3D，跳过自动绑定。");
			return;
		}

		// Weapon：RightHand/WeaponSlot
		if (_weaponSocket == null)
		{
			var rightHand = skeleton.FindChild("RightHand", true, false) as Node3D;
			if (rightHand != null)
			{
				// 优先用 RightHand 下的 WeaponSlot，没有就直接挂在 RightHand 上
				_weaponSocket = rightHand.GetNodeOrNull<Node>("WeaponSlot") ?? rightHand;
			}
		}

		// Shield：LeftHand/ShieldSlot
		if (_shieldSocket == null)
		{
			var leftHand = skeleton.FindChild("LeftHand", true, false) as Node3D;
			if (leftHand != null)
			{
				_shieldSocket = leftHand.GetNodeOrNull<Node>("ShieldSlot") ?? leftHand;
			}
		}

		// Style：HatOffset 或 Head
		if (_styleSocket == null)
		{
			Node hat = skeleton.FindChild("HatOffset", true, false);
			if (hat == null)
				hat = skeleton.FindChild("Head", true, false);

			_styleSocket = hat;
		}
	}

	/// <summary>往上爬节点，找到最近的 Skeleton3D</summary>
	private Skeleton3D FindParentSkeleton3D()
	{
		Node n = this;
		while (n != null)
		{
			if (n is Skeleton3D s)
				return s;
			n = n.GetParent();
		}
		return null;
	}

	// ====== 把插槽下所有孩子都清空（包括场景里预摆的默认模型） ======
	private void ClearSocketChildren(Node socket)
	{
		if (socket == null)
			return;

		foreach (Node child in socket.GetChildren())
		{
			child.QueueFree();
		}
	}

	/// <summary>
	/// 统一装备函数：自动识别 Node3D 或 Control
	/// </summary>
	private void EquipToSocket(ref Node currentInstance, Node socket, PackedScene scene, Dictionary<string, object> data)
	{
		if (socket == null || scene == null)
		{
			GD.PrintErr("[PlayerEquipment] EquipToSocket: socket 或 scene 为 null");
			return;
		}

		// ① 清空当前记录的旧装备实例
		if (IsInstanceValid(currentInstance))
		{
			currentInstance.QueueFree();
			currentInstance = null;
		}

		// ② 再保险：强制把插槽下面所有子节点都清掉
		ClearSocketChildren(socket);

		// ③ 实例化新装备
		var newObj = scene.Instantiate();
		if (newObj == null)
		{
			GD.PrintErr("[PlayerEquipment] 装备实例化失败 scene=" + scene.ResourcePath);
			return;
		}

		socket.AddChild(newObj);
		currentInstance = newObj;

		GD.Print($"[EquipToSocket] 装备 {scene.ResourcePath} 挂到了 {((Node)newObj).GetPath()}");

		// ===== 3D 装备：归零到插槽原点 =====
		if (newObj is Node3D n3 && socket is Node3D)
		{
			// 先统一归零
			n3.Position = Vector3.Zero;
			n3.Rotation = Vector3.Zero;
			n3.Scale = Vector3.One;

			// 如果以后想在 Global 里给每个武器写偏移量，可以从 data 里读：
			if (data != null)
			{
				if (data.TryGetValue("position", out var posObj) && posObj is Vector3 pos)
					n3.Position = pos;

				if (data.TryGetValue("rotation", out var rotObj) && rotObj is Vector3 rot)
					n3.Rotation = rot;

				if (data.TryGetValue("scale", out var scaleObj) && scaleObj is Vector3 scl)
					n3.Scale = scl;
			}
		}

		// ===== UI 预览装备：控件放到插槽左上角 =====
		if (newObj is Control ctrl && socket is Control)
		{
			ctrl.Position = Vector2.Zero;
		}

		// 允许预览模型显示
		if (newObj is CanvasItem ci)
			ci.Visible = true;
	}

	// ------------------------- 装备接口 -------------------------
	public void EquipWeapon(PackedScene scene, Dictionary<string, object> data)
	{
		EquipToSocket(ref _weaponInstance, _weaponSocket, scene, data);
	}

	public void EquipShield(PackedScene scene, Dictionary<string, object> data)
	{
		EquipToSocket(ref _shieldInstance, _shieldSocket, scene, data);
	}

	public void EquipStyle(PackedScene scene, Dictionary<string, object> data)
	{
		EquipToSocket(ref _styleInstance, _styleSocket, scene, data);
	}

	// ------------------------- 卸装备接口 -------------------------
	public void UnequipWeapon()
	{
		if (IsInstanceValid(_weaponInstance))
			_weaponInstance.QueueFree();
		_weaponInstance = null;
	}

	public void UnequipShield()
	{
		if (IsInstanceValid(_shieldInstance))
			_shieldInstance.QueueFree();
		_shieldInstance = null;
	}

	public void UnequipStyle()
	{
		if (IsInstanceValid(_styleInstance))
			_styleInstance.QueueFree();
		_styleInstance = null;
	}
}
