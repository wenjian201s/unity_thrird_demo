# 玩家角色控制器文档

本文档基于 Unity MCP 对当前场景 `Assets/Scenes/GameScene.unity` 中 `玩家` 对象的读取结果整理，用于在另一个 Unity 项目中复现同一套第三人称 ARPG 角色控制器。

读取到的玩家对象：

| 项 | 当前值 |
| --- | --- |
| GameObject | `玩家` |
| Tag | `Player` |
| Layer | `Player`，层号 `8` |
| Transform | Position `(132.43, 0.76, -44.64)`，Rotation `(0, 270, 0)`，Scale `(1, 1, 1)` |
| 当前场景 | `GameScene` |
| Animator Controller | `Assets/Animator/月下誓约N.controller` |
| Avatar | `Assets/Art/Models/Characters/月下誓约/月下誓约N.fbx` |

## 一、整体架构

这套玩家控制器不是单个脚本完成的，而是由多个模块挂在同一个玩家根对象上协作：

```text
玩家 Root
├─ ThirdPersonController      移动、姿态、跳跃、Root Motion、脚步声
├─ PlayerCombatController     玩家攻击输入、连招切换、受击、无敌帧、完美闪避
├─ CombatControllerBase       连招播放、动画时间轴事件、特效、音效、攻击检测窗口
├─ AttackCheckGizmos          武器攻击点射线检测，命中敌人后调用 EnemyCombatController.OnHit
├─ SwapWeapon                 武器切换、背部/手部武器显示、IK 约束切换、连招表切换
├─ PlayerViewDetection        锁敌检测、Cinemachine TargetGroup 更新、锁敌移动动画参数
├─ PlayerExtraActController   额外动作，例如打招呼、同意
├─ PlayerSoundController      脚步、跳跃、拔刀、攻击语音等音效
├─ PlayerAudioController      Greet / Agree 等语音
├─ PlayerInput                Unity Input System 事件分发
├─ Animator                   Root Motion 驱动移动和攻击动画
├─ CharacterController        角色碰撞与位移
├─ CapsuleCollider            角色触发碰撞体
└─ RigBuilder                 武器 IK
```

核心运行链路：

```text
输入系统 PlayerInput
    ↓
ThirdPersonController / SwapWeapon / PlayerCombatController / PlayerViewDetection
    ↓
Animator 参数或动画状态变化
    ↓
Root Motion + CharacterController.Move 移动角色
    ↓
CombatControllerBase 根据动画 normalizedTime 触发攻击判定、特效、音效
    ↓
AttackCheckGizmos 使用武器检测点做连续射线检测
    ↓
EnemyCombatController.OnHit 处理敌人受击
```

## 二、必要包和项目设置

当前项目关键依赖：

| 包 | 当前版本 | 用途 |
| --- | --- | --- |
| `com.unity.inputsystem` | `1.6.3` | 玩家输入 |
| `com.unity.cinemachine` | `2.9.7` | 第三人称相机和锁敌相机 |
| `com.unity.animation.rigging` | `1.2.1` | 武器 IK |
| `com.unity.render-pipelines.universal` | `14.0.8` | 当前渲染管线 |

需要创建的 Tags：

| Tag | 用途 |
| --- | --- |
| `Ground` | 地面检测、攀爬/翻越检测 |
| `Enemy` | 敌人识别 |
| `EnemyWeapon` | 敌人武器检测完美闪避 |
| `PerfectDodgeCollider` | 玩家完美闪避触发器 |

需要创建的 Layers：

| Layer | 层号 | 用途 |
| --- | ---: | --- |
| `Ground` | 6 | 地面检测 |
| `Enemy` | 7 | 攻击检测目标 |
| `Player` | 8 | 玩家根对象 |
| `SubPlayer` | 9 | 玩家子碰撞/子模型 |
| `PerfectDodgeCollider` | 10 | 完美闪避触发器 |
| `SubEnemy` | 11 | 敌人子对象 |
| `FX` | 12 | 特效 |

当前玩家攻击检测使用的敌人 LayerMask 是 `128`，即只检测 Layer 7 `Enemy`。

## 三、玩家对象组件配置

玩家根对象需要挂载这些组件：

| 组件 | 当前关键配置 |
| --- | --- |
| `Animator` | Controller：`月下誓约N.controller`；Avatar：`月下誓约NAvatar`；`Apply Root Motion = true`；`Update Mode = Normal` |
| `CharacterController` | Center `(0, 0.8, 0)`；Radius `0.17`；Height `1.58`；Slope Limit `45`；Step Offset `0.1`；Skin Width `0.05` |
| `CapsuleCollider` | `Is Trigger = true`；Center `(0, 0.85, 0)`；Radius `0.78`；Height `1.8`；Direction `Y` |
| `PlayerInput` | Actions：`Assets/Scripts/Player Actions.inputactions`；Default Map：`Player`；Behavior：`Invoke Unity Events` |
| `AudioSource` | Volume `0.5`；Play On Awake `true`；Spatial Blend `0` |
| `RigBuilder` | 两层 Rig：`Right Hand Rig`、`Left Hand Rig` |

`ThirdPersonController` 当前配置：

| 字段 | 当前值 |
| --- | --- |
| `gravity` | `-15` |
| `maxHeight` | `1.3` |
| `fallMultiplier` | `1.3` |
| `longJumpMultiplier` | `1.6` |
| `weaponOnBack` | 大剑背部对象 |
| `weaponInHand` | 大剑手部对象 |

`AttackCheckGizmos` 当前配置：

| 字段 | 当前值 |
| --- | --- |
| `enemyLayer` | LayerMask `128`，即 `Enemy` |
| `weaponType` | 初始 `Empty` |
| `timeBetweenCheck` | `0.01` |
| `katanaCheckPoints` | 12 个检测点：`KatanaAttackCheckPoint0` 到 `KatanaAttackCheckPoint11` |
| `greatSwordCheckPoints` | 13 个检测点：`GSAttackPosition0` 到 `GSAttackPosition12` |

`PlayerCombatController` 当前配置：

| 字段 | 当前值 |
| --- | --- |
| `attack` | `1` |
| `weaponType` | 初始 `Empty` |
| `comboDictStructs[0]` | `Katana -> Assets/Scripts/Player/SO/Katana/Katana.asset` |
| `comboDictStructs[1]` | `GreatSword -> Assets/Scripts/Player/SO/GreatSword/GreatSword.asset` |
| `currentComboList` | `Katana.asset` |
| `multiplier` | `2`，用于连招断连窗口 |
| `hitCoolDown` | `0.2` |
| `hitFXList[0]` | `Assets/Scripts/SO/HitConfig/PlayerOnHitFXConfig.asset` |
| `hitTransform` | 子对象 `HitTransform` |
| `hitFXScale` | `(0.2, 0.2, 0.2)` |
| `invincibleFrame` | `14` |
| `perfectDodgeTime` | `0.25` |
| `canPerfectDodgeTime` | `0.5` |
| `perfectDodgeTimeScale` | `0.2` |
| `perfectDodgeAudioClipPath` | `Audio/PerfectDodge` |

`SwapWeapon` 当前配置：

| 字段 | 当前值 |
| --- | --- |
| `effectTransform` | 大剑手部对象下的 `EffectPosition` |
| `rightHandIKConstraints[0]` | `Right Hand Rig/EmptyIKConstraint` |
| `rightHandIKConstraints[1]` | `Right Hand Constraint Katana` |
| `rightHandIKConstraints[2]` | `Right Hand Constraint GS` |
| `rightHandIKConstraints[3]` | `null`，Bow 暂未配置 |
| `leftHandIKConstraints[0]` | `Left Hand Rig/EmptyIKConstraint` |
| `leftHandIKConstraints[1]` | `Left Hand Constraint Katana` |
| `leftHandIKConstraints[2]` | `Left Hand Rig/EmptyIKConstraint` |
| `leftHandIKConstraints[3]` | `null` |
| `weaponOnBack[1]` | 太刀背部对象，初始 active |
| `weaponOnBack[2]` | 大剑背部对象，初始 active |
| `weaponInHand[1]` | 太刀手部对象，初始 inactive |
| `weaponInHand[2]` | 大剑手部对象，初始 inactive |

`PlayerViewDetection` 当前配置：

| 字段 | 当前值 |
| --- | --- |
| `mainCamera` | `//Camera/Main Camera` |
| `cinemachineTargetGroup` | `//Camera/Target Group` |
| `maxLockOnDistance` | `30` |
| `offset` | `(15, 15, 10)` |
| `size` | `(25, 20, 25)` |
| `rotateEuler` | `(0, 0, 0)` |
| `enemyLayer` | `128`，即 `Enemy` |
| `ignoreLayer` | `7936`，即 Layer 8、9、10、11、12 |
| `viewTransform` | 子对象 `ViewTransform` |
| `viewAngle` | `50` |
| `targetWeight` | `0.3` |
| `lockRotationSpeed` | `7` |
| `offsetAngle` | `10` |
| `stopFaceDis` | `3` |

`PlayerSoundController` 当前配置：

| 字段 | 当前值 |
| --- | --- |
| `footSteps` | 9 个石头脚步音效，路径在 `Assets/Audio/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Walk/` |
| `jumpEfforts` | 3 个跳跃语音，`Assets/Audio/JumpEfforts/` |
| `landing` | 空数组 |
| `equip` | `Assets/Audio/Weapon/拔剑2.MP3` |
| `unEquip` | `Assets/Audio/Weapon/拔剑1.MP3` |
| `commonAttack` | 4 个武器挥砍音效 |
| `playerCommonAttack` | 11 个玩家攻击语音 |

`PlayerAudioController` 当前配置：

| 字段 | 当前值 |
| --- | --- |
| `greetAudio` | 3 个问候语音 |
| `agreeAudio` | 2 个同意语音 |
| `linesAudio` | 空数组 |

## 四、关键子对象

玩家根对象下至少需要这些功能子对象：

| 子对象 | 用途 | 当前配置 |
| --- | --- | --- |
| `HitTransform` | 受击特效播放点 | Local Position `(0, 1.186, 0)` |
| `ViewTransform` | 锁敌视野检测参考点 | Local Position `(0, 1.2, 0)` |
| `完美闪避碰撞体` | 完美闪避触发器 | Tag `PerfectDodgeCollider`；Layer `PerfectDodgeCollider`；CapsuleCollider Trigger |
| `Right Hand Rig` | 右手 IK Rig | RigBuilder 第 0 层 |
| `Left Hand Rig` | 左手 IK Rig | RigBuilder 第 1 层 |
| `KatanaAttackCheckPoints` | 太刀攻击检测点集合 | 12 个点，沿太刀刃分布 |
| `GSAttackPoints` | 大剑攻击检测点集合 | 13 个点，沿大剑刃分布 |
| `EffectPosition` | 大剑手部特效位置 | 挂在大剑手部对象下 |

完美闪避子对象当前配置：

| 项 | 当前值 |
| --- | --- |
| GameObject | `玩家/完美闪避碰撞体` |
| Tag | `PerfectDodgeCollider` |
| Layer | `PerfectDodgeCollider`，层号 `10` |
| `CapsuleCollider` | `Is Trigger = true`，Center `(0, 0.16, 0)`，Radius `1`，Height `1`，Direction `Z` |
| `PerfectDodge.playerCombatController` | 指向玩家根对象上的 `PlayerCombatController` |

## 五、输入系统配置

输入资源：

```text
Assets/Scripts/Player Actions.inputactions
Action Map: Player
Default Action Map: Player
PlayerInput Behavior: Invoke Unity Events
```

Action 和按键绑定：

| Action | 类型 | 按键 |
| --- | --- | --- |
| `PlayerMovement` | `Value / Vector2` | `WASD`，2DVector |
| `Look` | `Value / Vector2` | `Mouse delta` |
| `Crouch` | `Button` | `C` |
| `Run/Slide` | `Button` | `Left Shift`，Interactions：`Hold(duration=1), Tap` |
| `Jump` | `Button` | `Space` |
| `KatanaEquip` | `Button` | `1` |
| `GSEquip` | `Button` | `2` |
| `BowEquip` | `Button` | `3` |
| `Attack` | `Button` | `Mouse press` |
| `Greet` | `Button` | `F1` |
| `Agree` | `Button` | `F2` |
| `StopTime` | `Button` | Backquote |
| `LockTarget` | `Button` | `Q` |
| `AttackTest` | `Button` | Right Mouse Button |

PlayerInput 事件回调应这样绑定：

| Action | 回调 |
| --- | --- |
| `PlayerMovement` | `ThirdPersonController.GetMoveInput(InputAction.CallbackContext)` |
| `Crouch` | `ThirdPersonController.GetCrouchInput(InputAction.CallbackContext)` |
| `Run/Slide` | `ThirdPersonController.GetRunInput(InputAction.CallbackContext)` |
| `Run/Slide` | `PlayerCombatController.GetSlideInput(InputAction.CallbackContext)` |
| `Jump` | `ThirdPersonController.GetJumpInput(InputAction.CallbackContext)` |
| `KatanaEquip` | `SwapWeapon.GetKatanaInput(InputAction.CallbackContext)` |
| `GSEquip` | `SwapWeapon.GetGreatSwordInput(InputAction.CallbackContext)` |
| `BowEquip` | `SwapWeapon.GetBowInput(InputAction.CallbackContext)` |
| `Attack` | `PlayerCombatController.GetAttackInput(InputAction.CallbackContext)` |
| `Greet` | `PlayerExtraActController.GetGreetInput(InputAction.CallbackContext)` |
| `Agree` | `PlayerExtraActController.GetAgreeInput(InputAction.CallbackContext)` |
| `StopTime` | `SwapWeapon.StopTime(InputAction.CallbackContext)` |
| `LockTarget` | `PlayerViewDetection.GetLockTargetInput(InputAction.CallbackContext)` |

注意：当前 Inspector 里的 `AttackTest` 事件还保留了一个指向 `PlayerCombatController.GetInput` 的旧回调名，但 `PlayerCombatController` 代码里没有这个方法。迁移时建议删除这个事件，或补上调试方法。

## 六、Animator 配置

Animator 必须启用 Root Motion，因为移动主要依赖 `OnAnimatorMove()` 中的 `animator.deltaPosition`：

```csharp
Vector3 playerDeltaMovement = animator.deltaPosition;
playerDeltaMovement.y = verticalVelocity * Time.deltaTime;
characterController.Move(playerDeltaMovement);
```

Animator 参数：

| 参数 | 类型 | 用途 |
| --- | --- | --- |
| `Posture` | Float | 站立、蹲伏、滞空、落地过渡 |
| `MoveSpeed` | Float | 基础移动 BlendTree 速度 |
| `TurnSpeed` | Float | 转向 BlendTree / 转向幅度 |
| `VerticalSpeed` | Float | 跳跃、下落动画 |
| `XSpeed` | Float | 锁敌状态横向移动 |
| `YSpeed` | Float | 锁敌状态前后移动 |
| `JumpType` | Float | 左脚/右脚跳跃动画 |
| `Roll` | Trigger | 翻滚/闪避 |
| `WeaponType` | Int | `0 Empty`、`1 Katana`、`2 GreatSword`、`3 Bow` |
| `AttackCount` | Float | 旧攻击计数参数，当前代码未直接使用 |
| `Right Hand Weight` | Float | 动画驱动右手 IK 权重 |
| `Left Hand Weight` | Float | 动画驱动左手 IK 权重 |
| `StopLeft` | Trigger | 急停左脚动画 |
| `StopRight` | Trigger | 急停右脚动画 |
| `ExtraAct` | Float | 额外动作 BlendTree |
| `LockOn` | Float | 普通相机/锁敌相机切换 |
| `XInput` | Float | 锁敌移动输入 X |
| `YInput` | Float | 锁敌移动输入 Y |

关键状态机：

| 层/子状态机 | 关键状态 |
| --- | --- |
| Base | `BaseMotion`、`NormalStopLeft`、`NormalStopRight` |
| GreatSword / Combo | `GSCombo01` 到 `GSCombo05`，Tag 为 `GSAttack` |
| Katana / Combo | `KatanaCombo01` 到 `KatanaCombo06`，Tag 为 `KatanaAttack` |
| GreatSword / GSRoll | `FreeSlide`、`LockSlide` 下的 `F/B/L/R/FL/FR/BL/BR` |
| Katana / KatanaRoll | `FreeSlide`、`LockSlide` 下的 `F/B/L/R/FL/FR/BL/BR` |
| OnHit | `Hit_Front_Empty`、`Hit_Back_Empty`、`Hit_Left_Empty`、`Hit_Right_Empty` |
| Katana / OnHit | `Hit_Front_Katana`、`Hit_Back_Katana`、`Hit_Left_Katana`、`Hit_Right_Katana` |
| GreatSword / OnHit | `Hit_Front_GreatSword`、`Hit_Left_GreatSword`、`Hit_Right_GreatSword` |
| ExtraLayer | `ExtraActs`、`Empty` |
| StateDrivenCamera | `Normal`、`LockOn` |

关键约束：

1. `ComboConfig.comboName` 必须和 Animator State 名完全一致。
2. 受击动画名由代码拼接：`Hit_Front_` + `weaponType.ToString()`，所以 Animator 状态名必须按这个规则命名。
3. 武器攻击动画需要有正确 Tag：太刀攻击用 `KatanaAttack`，大剑攻击用 `GSAttack`。
4. 装备/收武器动画需要 Tag `Equip`，装备移动状态需要 Tag `EquipMotion`。

## 七、连招 ScriptableObject 配置

数据结构：

```text
ComboList
└─ ComboConfig[]
   ├─ comboName
   ├─ coolDownTime
   ├─ ComboInteractionConfig[]
   ├─ FXConfig[]
   ├─ ClipConfig[]
   ├─ AttackFeedbackConfig[]
   ├─ SelfMoveOffsetConfig[]
   └─ TargetMoveOffsetConfig[]
```

枚举：

| 枚举 | 值 |
| --- | --- |
| `E_WeaponType` | `Empty`、`Katana`、`GreatSword`、`Bow` |
| `E_AttackType` | `Common`、`Skill`、`Ultimate` |
| `E_AttackForce` | `Easy`、`Mid`、`Hard` |
| `E_MoveOffsetDirection` | `Forward`、`Up` |

太刀连招表：`Assets/Scripts/Player/SO/Katana/Katana.asset`

| Combo | Cooldown | 判定时间 | 伤害/精力 | 特效 | 音效 | 反馈 |
| --- | ---: | --- | --- | --- | --- | --- |
| `KatanaCombo01` | `0.4` | `0.1 - 0.3` | `78 / 37` | `FX/FX01`，rot `(0,180,60)` | Normal Swing 4 03 | velocity `(-0.02,0.02,0)` |
| `KatanaCombo02` | `0.4` | `0.1 - 0.3` | `83 / 40` | `FX/FX01`，rot `(180,0,-45)` | Normal Swing 1 02 | velocity `(0.02,0.02,0)` |
| `KatanaCombo03` | `0.4` | `0.1 - 0.3` | `80 / 39` | `FX/FX01`，rot `(0,180,-90)` | Normal Swing 3 01 | velocity `(0,-0.03,0)` |
| `KatanaCombo04` | `0.4` | `0.1 - 0.3` | `80 / 39` | `FX/FX01`，rot `(0,180,-120)` | Normal Swing 1 11 | velocity `(-0.02,-0.02,0)` |
| `KatanaCombo05` | `0.5` | `0.1 - 0.3` | `102 / 45` | `FX/FX_hitStab01`，pos `(-0.1,1,2)`，scale `(1,1,1.5)` | Normal Swing 3 03 | velocity `(0,0,0.02)` |
| `KatanaCombo06` | `1.1` | `0.2 - 0.3` | `133 / 50` | `FX/FX01`，pos `(0,0.8,0)` | Normal Swing 2 01 | animatorSpeed `0.05`，stopFrameTime `0.25` |

大剑连招表：`Assets/Scripts/Player/SO/GreatSword/GreatSword.asset`

| Combo | Cooldown | 判定时间 | 伤害/精力 | 特效 | 音效 | 反馈 |
| --- | ---: | --- | --- | --- | --- | --- |
| `GSCombo01` | `0.65` | `0.3 - 0.43` | `101 / 48` | `FX/FX_hit03`，rot `(0,180,-50)` | Large Swing 7 01 | velocity `(-0.03,-0.03,0)` |
| `GSCombo02` | `0.8` | `0.32 - 0.5` | `117 / 46` | `FX/FX_hit03`，scale `(-1,1,1)` | Large Swing 7 02 | velocity `(0.04,0,0)` |
| `GSCombo03` | `1.2` | 两段：`0.15 - 0.25`、`0.38 - 0.48` | 每段 `130 / 50` | 两段 `FX/FX_hit03` | Large Swing 7 03、04 | 两段 feedback |
| `GSCombo04` | `1.0` | `0.3 - 0.4` | `180 / 65` | `FX/FX_hit03`，pos `(0,1,0.4)`，rot `(0,180,-90)` | Large Swing 7 05 | velocity `(0,-0.05,0)` |
| `GSCombo05` | `2.5` | `0.3 - 0.4` | `196 / 65` | `FX/FX_hit03`，rot `(0,200,0)`，scale `(-1,1,1)` | Large Swing 7 08 | animatorSpeed `0.05`，stopFrameTime `0.3` |

受击特效配置：

| 资源 | 内容 |
| --- | --- |
| `Assets/Scripts/SO/HitConfig/PlayerOnHitFXConfig.asset` | `FX/FX_hit02`，Prefab：`Assets/Resources/FX/FX_hit02.prefab` |
| `Assets/Scripts/SO/HitConfig/EnemyOnHitFXConfig.asset` | `FX/FX_hit01`，Prefab：`Assets/Resources/FX/FX_hit01.prefab` |

所有 `FXName` 都是 `Resources.Load` / 缓存池 key，必须保证对应 Prefab 放在 `Assets/Resources/` 下。例如 `FX/FX01` 对应：

```text
Assets/Resources/FX/FX01.prefab
```

## 八、主要脚本职责

必须复制或重建的玩家侧脚本：

| 文件 | 作用 |
| --- | --- |
| `Assets/Scripts/Player/ThirdPersonController.cs` | 姿态机、移动输入、跳跃、重力、Root Motion 移动、脚步声、急停 |
| `Assets/Scripts/Player/CombatControllerBase.cs` | 连招基类，负责播放攻击动画和执行动画时间轴事件 |
| `Assets/Scripts/Player/PlayerCombatController.cs` | 玩家战斗控制，负责攻击输入、切换连招表、玩家受击、无敌帧、完美闪避 |
| `Assets/Scripts/Player/AttackCheckGizmos.cs` | 武器攻击检测点连续射线检测 |
| `Assets/Scripts/Player/SwapWeapon.cs` | 武器切换、IK 切换、武器显示隐藏、连招表切换 |
| `Assets/Scripts/Player/PlayerViewDetection.cs` | 锁敌检测和锁敌相机目标更新 |
| `Assets/Scripts/Player/PlayerExtraActController.cs` | 打招呼、同意等额外动作 |
| `Assets/Scripts/Player/PerfectDodge.cs` | 完美闪避触发器入口 |
| `Assets/Scripts/Audio/PlayerSoundController.cs` | 玩家通用音效 |
| `Assets/Scripts/Audio/PlayerAudioController.cs` | 玩家语音 |
| `Assets/Scripts/FX/FXManager.cs` | 特效播放入口 |
| `Assets/Scripts/Player/CombatSOScript/ComboConfig.cs` | 连招单段配置结构 |
| `Assets/Scripts/Player/CombatSOScript/ComboList.cs` | 连招列表配置结构 |
| `Assets/Scripts/Player/CombatSOScript/HitFXConfig.cs` | 受击特效随机配置 |
| `Assets/Scripts/Player/Enum/CombatEnum.cs` | 武器、攻击类型、攻击力度、位移补偿方向枚举 |

必要的公共框架脚本：

| 文件 | 作用 |
| --- | --- |
| `Assets/MyFramework/Base/SingletonPatternBase.cs` | 非 MonoBehaviour 单例基类 |
| `Assets/MyFramework/CachePool/CachePoolManager.cs` | `Resources.Load` + 对象池 |

敌人侧接口依赖：

| 依赖 | 用途 |
| --- | --- |
| `EnemyCombatController.OnHit(ComboInteractionConfig, AttackFeedbackConfig, Transform)` | 玩家武器命中敌人时调用 |
| `EnemyAttackDetectionConfig` | 玩家受击时读取敌人攻击伤害 |
| `EnemyLockOn.lockOnTransform` | 锁敌时加入 CinemachineTargetGroup |

## 九、实现流程

### 1. 建项目和安装包

1. 使用 Unity 2022.3 LTS 或兼容版本。
2. 安装 Input System、Cinemachine、Animation Rigging。
3. 在 Project Settings 中启用新输入系统，必要时选择 `Both`。
4. 创建上文列出的 Tags 和 Layers。

### 2. 准备玩家模型和 Animator

1. 导入 Humanoid 角色模型。
2. 创建 Animator Controller。
3. 添加本文档第六节列出的全部 Animator 参数。
4. 创建基础移动 BlendTree，使用 `Posture`、`MoveSpeed`、`TurnSpeed`、`VerticalSpeed` 驱动。
5. 创建武器子状态机：`Katana`、`GreatSword`、`Bow`。
6. 在 `Katana/Combo` 中创建 `KatanaCombo01` 到 `KatanaCombo06`。
7. 在 `GreatSword/Combo` 中创建 `GSCombo01` 到 `GSCombo05`。
8. 确保 Combo 状态名与 `ComboConfig.comboName` 完全一致。
9. 开启玩家 Animator 的 `Apply Root Motion`。

### 3. 搭建玩家根对象

1. 创建玩家根对象，Tag 设为 `Player`，Layer 设为 `Player`。
2. 添加 `Animator`、`CharacterController`、`CapsuleCollider`、`AudioSource`、`PlayerInput`。
3. 添加玩家脚本：`ThirdPersonController`、`PlayerSoundController`、`PlayerExtraActController`、`PlayerAudioController`、`SwapWeapon`、`AttackCheckGizmos`、`PlayerViewDetection`、`PlayerCombatController`。
4. 添加 `RigBuilder`，配置右手 Rig 和左手 Rig。

### 4. 搭建功能子对象

1. 在玩家根对象下创建 `HitTransform`，放在胸口附近，用于受击特效。
2. 创建 `ViewTransform`，放在头部或胸口高度，用于锁敌检测。
3. 创建 `完美闪避碰撞体`，加 `CapsuleCollider`，设为 Trigger，Tag 和 Layer 都用 `PerfectDodgeCollider`，挂 `PerfectDodge`。
4. 在武器模型上沿刀刃/剑刃摆放攻击检测点。
5. 太刀检测点拖入 `AttackCheckGizmos.katanaCheckPoints`。
6. 大剑检测点拖入 `AttackCheckGizmos.greatSwordCheckPoints`。

### 5. 配置输入

1. 创建 `Player Actions.inputactions`。
2. 添加 `Player` Action Map。
3. 按第五节创建所有 Action 和绑定。
4. 在 PlayerInput 中设置：

```text
Actions = Player Actions
Default Map = Player
Behavior = Invoke Unity Events
```

5. 在 PlayerInput 的 Events 中逐个绑定回调方法。

### 6. 配置武器切换

1. 在角色骨骼上挂背部武器对象和手部武器对象。
2. 背部武器初始显示，手部武器初始隐藏。
3. `SwapWeapon.weaponOnBack` 数组按 `E_WeaponType` 枚举下标填写：

```text
[0] Empty      = null
[1] Katana     = 太刀背部对象
[2] GreatSword = 大剑背部对象
[3] Bow        = null 或弓背部对象
```

4. `SwapWeapon.weaponInHand` 同样按枚举下标填写。
5. 配置 `rightHandIKConstraints` 和 `leftHandIKConstraints`，下标也要对应武器枚举。
6. 动画中通过 `Right Hand Weight` 和 `Left Hand Weight` 两个 Float 曲线控制 IK 权重。
7. 装备/收武器动画事件调用 `SwapWeapon.PutGrabWeapon(int weaponType)`，切换背部和手部武器显示。

### 7. 配置连招数据

1. 创建 `ComboConfig` 资源，每个资源代表一段攻击。
2. `comboName` 填 Animator State 名。
3. `coolDownTime` 填本段攻击后摇。
4. `comboInteractionConfigs` 填判定窗口：

```text
startTime = 动画 normalizedTime 开始判定
endTime   = 动画 normalizedTime 结束判定
weaponType / attackForce / healthDamage / enduranceDamage
```

5. `fxConfigs` 填攻击特效时间点、Resources key、位置、旋转、缩放。
6. `clipConfigs` 填挥砍音效时间点、音量。
7. `attackFeedbackConfigs` 填命中反馈、顿帧速度、顿帧时间。
8. 创建 `ComboList` 资源，将同一武器的所有 `ComboConfig` 按连招顺序放进去。
9. 在 `PlayerCombatController.comboDictStructs` 中配置武器类型到 `ComboList` 的映射。

### 8. 配置相机和锁敌

1. 创建 `Main Camera`，Tag 设为 `MainCamera`。
2. 添加 `CinemachineBrain`。
3. 创建 `CinemachineTargetGroup`，第一个成员固定是玩家 Transform，Weight `1`，Radius `1`。
4. 创建普通 FreeLook 相机：Follow 玩家，LookAt 玩家。
5. 创建锁敌 FreeLook 相机：Follow 玩家，LookAt TargetGroup。
6. 如果使用 State Driven Camera，需要让 Animator 的 `LockOn` 参数驱动普通相机和锁敌相机切换。
7. `PlayerViewDetection` 中绑定 `mainCamera`、`cinemachineTargetGroup`、`viewTransform`。

### 9. 配置特效池

1. 将攻击特效和受击特效 Prefab 放在 `Assets/Resources/FX/`。
2. `FXConfig.FXName` 使用 Resources 路径，例如 `FX/FX01`。
3. `HitFXConfig.hitFXName` 也使用同样规则，例如 `FX/FX_hit02`。
4. 项目中需要 `CachePoolManager`，`FXManager` 通过它获取对象。

## 十、核心运行逻辑

### 移动

`ThirdPersonController` 先读取输入，再把输入方向转换成相机朝向下的本地移动方向：

```text
moveInput
    ↓
cameraTransform.forward/right
    ↓
playerMovement = 玩家本地移动方向
    ↓
Animator 参数 MoveSpeed / TurnSpeed
    ↓
OnAnimatorMove 使用 Root Motion 移动 CharacterController
```

移动速度来自脚本内部常量：

```text
crouchSpeed = 0.8
walkSpeed   = 1.27
runSpeed    = 4.2
```

### 跳跃和落地

跳跃初速度：

```csharp
verticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);
```

当前配置为：

```text
gravity = -15
maxHeight = 1.3
fallMultiplier = 1.3
longJumpMultiplier = 1.6
```

地面检测依赖：

```text
Physics.SphereCast(...)
命中的 Collider 必须 CompareTag("Ground")
```

### 武器切换

按键触发 `SwapWeapon`：

```text
1 -> Katana
2 -> GreatSword
3 -> Bow
```

武器切换时同步修改：

```text
SwapWeapon.weaponType
ThirdPersonController.isEquip
AttackCheckGizmos.weaponType
PlayerCombatController.weaponType
PlayerCombatController.currentComboList
```

### 攻击连招

攻击输入进入 `PlayerCombatController.GetAttackInput()`：

```text
如果 weaponType != Empty
    ↓
ExecuteCombo()
    ↓
播放 currentComboList[currentComboIndex].comboName 对应的 Animator State
    ↓
RunEvent() 每帧根据 normalizedTime 触发攻击判定、特效、音效
```

连招冷却：

```text
coolDownTime 控制下一次攻击什么时候可以输入
coolDownTime * multiplier 控制多久不输入后重置连招
当前 multiplier = 2
```

### 攻击检测

攻击检测由 `AttackCheckGizmos` 完成：

```text
StartAttacking()
    ↓
isAttacking = true
    ↓
FixedUpdate 中每 0.01 秒检测一次
    ↓
从上一次检测点位置向当前检测点位置发射 Raycast
    ↓
命中 Enemy Layer 后调用 EnemyCombatController.OnHit
```

这种做法适合高速挥刀，因为它不是只检测当前点，而是检测上一帧到当前帧之间的线段。

### 玩家受击和完美闪避

玩家被敌人打中时调用：

```text
PlayerCombatController.PlayerOnHit(EnemyAttackDetectionConfig, attackerTransform)
```

流程：

1. 检查 `canBeHit`。
2. 关闭玩家当前攻击检测。
3. 禁用移动输入和攻击输入。
4. 计算伤害。
5. 根据攻击者方位播放 `Hit_Front_WeaponType` 等受击动画。
6. 播放 `PlayerOnHitFXConfig` 中的受击特效。
7. `hitCoolDown` 后恢复受击和输入。

完美闪避流程：

```text
Roll 动画事件 StartInvincibleFrame()
    ↓
canBeHit = false
startToCountInvincibleFrame = true
    ↓
完美闪避碰撞体碰到 EnemyWeapon
    ↓
PerfectDodge.PerfectDodgeInterface()
    ↓
PlayerCombatController.PerfectDodge()
    ↓
Time.timeScale = 0.2，持续 0.25 秒
```

## 十一、迁移检查清单

迁移到新项目后，按这个顺序检查：

1. `Player`、`Enemy`、`Ground`、`PerfectDodgeCollider` 的 Tag 和 Layer 都存在。
2. 玩家根对象 Tag 是 `Player`，Layer 是 `Player`。
3. 所有地面对象 Tag 是 `Ground`。
4. 敌人根对象 Layer 是 `Enemy`，并挂能响应 `OnHit` 的脚本。
5. 玩家 Animator 开启 `Apply Root Motion`。
6. Animator 参数名完全一致，尤其是 `Posture`、`MoveSpeed`、`TurnSpeed`、`WeaponType`、`Roll`、`LockOn`。
7. ComboConfig 的 `comboName` 和 Animator State 名完全一致。
8. `PlayerInput` 使用 `Invoke Unity Events`，所有事件回调都绑定。
9. 武器检测点数组不为空。
10. `HitTransform`、`ViewTransform`、相机、TargetGroup 都拖引用。
11. `FXName` 对应的 Prefab 确实放在 `Resources` 目录。
12. `Audio/PerfectDodge` 对应的音频资源能被 `Resources.Load<AudioClip>()` 读到。
13. WeaponType 枚举顺序和所有数组下标一致。
14. 大剑、太刀的手部/背部对象初始 active 状态正确。
15. IK Constraint 的 target 指向正确的武器握持点。

## 十二、当前工程中值得注意的问题

这些不是复现阻塞点，但迁移时最好顺手修：

1. `CombatControllerBase.OnHit()` 中：

```csharp
Quaternion.LookRotation(-attacker.position, Vector3.up)
```

这里传入的是世界坐标的反方向，不是“从自己指向攻击者”的方向。更合理的是：

```csharp
Vector3 direction = attacker.position - transform.position;
direction.y = 0f;
Quaternion.LookRotation(direction, Vector3.up);
```

2. `CombatControllerBase.RunEvent()` 在判定窗口内会每帧调用 `StartAttacking()`。如果 `StartAttacking()` 以后加入清空命中列表等逻辑，需要加“只启动一次”的保护。

3. 音效事件只有在 `clipConfig.audioClip` 不为空时才递增 `clipIndex`。如果某个 ClipConfig 没配音频，后续音效事件会被卡住。

4. 当前 PlayerInput 里有一个旧事件指向 `PlayerCombatController.GetInput`，但脚本里没有这个方法。迁移时应删除或修正。

5. `PlayerCombatController.canPlayHitAnim` 当前序列化值是 `false`，但代码 `Start()` 会设置为 `true`。如果不是运行时读取，Inspector 上看到的值可能让人误判。

6. `ComboList.TryGet...` 方法只判断了 `comboIndex >= Length`，没有判断负数索引和数组为空。迁移时可以补防御逻辑。

## 十三、最小复现版本建议

如果你只想先在新项目里跑通最小版本，建议按这个范围复制：

```text
ThirdPersonController.cs
CombatControllerBase.cs
PlayerCombatController.cs
AttackCheckGizmos.cs
SwapWeapon.cs
PlayerViewDetection.cs
PerfectDodge.cs
PlayerSoundController.cs
PlayerAudioController.cs
FXManager.cs
ComboConfig.cs
ComboList.cs
HitFXConfig.cs
CombatEnum.cs
SingletonPatternBase.cs
CachePoolManager.cs
Player Actions.inputactions
Katana.asset + KatanaCombo01-06.asset
GreatSword.asset + GSCombo01-05.asset
PlayerOnHitFXConfig.asset
```

然后先只接这几个功能：

1. 移动、跑、蹲、跳。
2. 太刀/大剑装备切换。
3. 攻击动画播放。
4. 攻击检测点命中敌人。
5. 攻击特效和音效。
6. 锁敌相机。
7. 完美闪避。

这样分层复现最稳：先让角色能移动，再让武器能切，再让攻击能播，再让攻击能打到目标，最后才补齐相机、IK、特效、受击和音频。
