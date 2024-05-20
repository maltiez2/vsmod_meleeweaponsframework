using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace MeleeWeaponsFramework;

public class DirectionalWeaponParameters
{
    public string IdleAnimationOneHanded { get; set; } = "meleeweaponsframework-empty";
    public string IdleAnimationTwoHanded { get; set; } = "meleeweaponsframework-empty";
    public string ReadyAnimationOneHanded { get; set; } = "meleeweaponsframework-empty";
    public string ReadyAnimationTwoHanded { get; set; } = "meleeweaponsframework-empty";
    public string IdleAnimationOffhandOneHanded { get; set; } = "meleeweaponsframework-empty";
    public string IdleAnimationOffhandTwoHanded { get; set; } = "meleeweaponsframework-empty";
    public string ReadyAnimationOffhandOneHanded { get; set; } = "meleeweaponsframework-empty";
    public string ReadyAnimationOffhandTwoHanded { get; set; } = "meleeweaponsframework-empty";
    public string DirectionsConfigurationOneHanded { get; set; } = "None";
    public string DirectionsConfigurationTwoHanded { get; set; } = "None";

    public float MaxReach { get; set; } = 6.0f;
    public float[] DebugCollider { get; set; } = new float[6];
    public float[][] DebugParryColliders { get; set; } = Array.Empty<float[]>();
    public Dictionary<string, DirectionalWeaponAttackStats> AttacksOneHanded { get; set; } = new();
    public Dictionary<string, DirectionalWeaponAttackStats> AttacksTwoHanded { get; set; } = new();

    public bool StopOnCollisionWithTerrain { get; set; } = false;
    public bool StopOnCollisionWithEntity { get; set; } = false;
    public bool CanChangeGrip { get; set; } = false;
    public string DefaultGrip { get; set; } = "OneHanded";
    public float GripChangeCooldownMs { get; set; } = 200;

    public int FeintCooldownMs { get; set; } = 600;
    public int BlockCooldownMs { get; set; } = 300;
    public int StaggerDurationMs { get; set; } = 600;
    public string? BlockSound { get; set; }
    public string? ParrySound { get; set; }
    public string? CancelBlockSound { get; set; }
    public float[][] OneHandedParriesColliders { get; set; } = Array.Empty<float[]>();
    public float[][] TwoHandedParriesColliders { get; set; } = Array.Empty<float[]>();
    public Dictionary<string, DirectionalWeaponBlockStats> ParriesOneHanded { get; set; } = new();
    public Dictionary<string, DirectionalWeaponBlockStats> ParriesTwoHanded { get; set; } = new();
}

public class DirectionalWeaponAttackStats
{
    public string Animation { get; set; } = "";
    public float AnimationWindupFrame { get; set; } = 0f;
    public float AnimationStrikeFrame { get; set; } = 0f;
    public float[] Collider { get; set; } = new float[6];
    public int WindUpMs { get; set; } = 100;
    public int DurationMs { get; set; } = 300;
    public int EaseOutMs { get; set; } = 500;
    public float Damage { get; set; } = 1.0f;
    public int Tier { get; set; } = 0;
    public string DamageType { get; set; } = "PiercingAttack";
    public WeaponAnimationParameters[] AnimationParameters { get; set; } = Array.Empty<WeaponAnimationParameters>();
}

public class DirectionalWeaponBlockStats
{
    public string Animation { get; set; } = "";
    public int ParryDurationMs { get; set; } = 100;
    public float CoverageDegrees { get; set; } = 120;
    public float IncomingDamageMultiplier { get; set; } = 0.5f;
    public WeaponAnimationParameters[] AnimationParameters { get; set; } = Array.Empty<WeaponAnimationParameters>();
}

public class WeaponAnimationParameters
{
    public string Action { get; set; } = "EaseOut";
    public float DurationMs { get; set; } = 1.0f;
    public float Frame { get; set; } = 0.0f;
    public float StartFrame { get; set; } = 0.0f;
    public float TargetFrame { get; set; } = 0.0f;
    public string EasingFunction { get; set; } = "Linear";

    public RunParameters ToRunParameters()
    {
        AnimationPlayerAction action = Enum.Parse<AnimationPlayerAction>(Action);
        ProgressModifierType easingFunction = Enum.Parse<ProgressModifierType>(EasingFunction);
        TimeSpan duration = TimeSpan.FromMilliseconds(DurationMs);

        return action switch
        {
            AnimationPlayerAction.Set => RunParameters.Set(Frame),
            AnimationPlayerAction.EaseIn => RunParameters.EaseIn(duration, Frame, easingFunction),
            AnimationPlayerAction.EaseOut => RunParameters.EaseOut(duration, easingFunction),
            AnimationPlayerAction.Play => RunParameters.Play(duration, StartFrame, TargetFrame, easingFunction),
            AnimationPlayerAction.Stop => RunParameters.Stop(),
            AnimationPlayerAction.Rewind => RunParameters.Rewind(duration, StartFrame, TargetFrame, easingFunction),
            AnimationPlayerAction.Clear => RunParameters.Clear(),
            _ => throw new NotImplementedException(),
        };
    }
}

public enum GripType
{
    OneHanded,
    TwoHanded
}

public enum WeaponActivity
{
    Idle,
    Attacking,
    Blocking
}

public class DirectionalWeapon : Item, IBehaviorManagedItem, IHasParryCollider
{
    public int WeaponItemId => ItemId;
    public virtual IPlayerAnimationData IdleAnimation => Grip == GripType.OneHanded ? IdleOneHandedAnimation : IdleTwoHandedAnimation;
    public virtual IPlayerAnimationData ReadyAnimation => Grip == GripType.OneHanded ? ReadyOneHandedAnimation : ReadyTwoHandedAnimation;
    public virtual IPlayerAnimationData IdleAnimationOffhand => IdleOffhandAnimation;
    public virtual IPlayerAnimationData ReadyAnimationOffhand => ReadyOffhandAnimation;
    public virtual DirectionsConfiguration DirectionsType => Grip == GripType.OneHanded ? DirectionsConfigurationOneHanded : DirectionsConfigurationTwoHanded;
    public virtual bool RenderDirectionCursor { get; protected set; } = true;

    public IEnumerable<IParryCollider> RelativeColliders => ParryCollidersByGrip[Grip];
    public IEnumerable<IParryCollider> InWorldColliders { get; set; } = Array.Empty<IParryCollider>();

    public WeaponActivity CurrentMainHandActivity { get; protected set; } = WeaponActivity.Idle;
    public WeaponActivity CurrentOffhandActivity { get; protected set; } = WeaponActivity.Idle;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        DirectionalWeaponParameters = Attributes[MeleeWeaponStatsAttribute].AsObject<DirectionalWeaponParameters>();

        DefaultGrip = Enum.Parse<GripType>(DirectionalWeaponParameters.DefaultGrip);

        if (api is ICoreServerAPI serverApi)
        {
            ServerMeleeSystem = serverApi.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemServer;
            ServerBlockSystem = serverApi.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().BlockSystemServer;
        }

        if (api is ICoreClientAPI clientAPI)
        {
            Api = clientAPI;
            MeleeSystem = Api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemClient;
            BlockSystem = Api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().BlockSystemClient;
        }

        ConstructAnimations();
        RegisterAttacks();
        RegisterParries();

        FeintCooldown = TimeSpan.FromMilliseconds(DirectionalWeaponParameters.FeintCooldownMs);
        BlockCooldown = TimeSpan.FromMilliseconds(DirectionalWeaponParameters.BlockCooldownMs);
        StaggerDuration = TimeSpan.FromMilliseconds(DirectionalWeaponParameters.StaggerDurationMs);

        DebugCollider = new(DirectionalWeaponParameters.DebugCollider);
        DebugParryColliders = DirectionalWeaponParameters.DebugParryColliders.Select(collider => new RectangularCollider(collider)).ToArray();

        Manager = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        StaggerAnimation = new("stagger", "stagger", EnumAnimationBlendMode.AddAverage, 0);
        StaggerAnimationParameters = new RunParameters[]
        {
            RunParameters.EaseIn(TimeSpan.FromMilliseconds(100), 0, ProgressModifierType.Sin),
            RunParameters.EaseOut(TimeSpan.FromMilliseconds(300), ProgressModifierType.SinQuadratic)
        };
        Manager.Register(StaggerAnimation, AnimationData.Player("stagger"));
    }
    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        List<WorldInteraction> result = new();
        if (CanParry)
        {
            result.Add(new()
            {
                ActionLangCode = "meleeweaponsframework:interaction-parry",
                MouseButton = EnumMouseButton.Right
            });
        }
        if (CanAttack)
        {
            result.Add(new()
            {
                ActionLangCode = "meleeweaponsframework:interaction-attack",
                MouseButton = EnumMouseButton.Left
            });
        }
        if (DirectionalWeaponParameters.CanChangeGrip)
        {
            result.Add(new()
            {
                ActionLangCode = "meleeweaponsframework:interaction-change-grip",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "ctrl"
            });
        }

        return result.ToArray();
    }

    public virtual void OnSelected(ItemSlot slot, EntityPlayer player)
    {
        if (!DirectionalWeaponParameters.CanChangeGrip) return;

        ItemSlot otherHandSlot;
        bool mainHand = false;
        if (player.RightHandItemSlot != slot)
        {
            otherHandSlot = player.RightHandItemSlot;
        }
        else
        {
            otherHandSlot = player.LeftHandItemSlot;
            mainHand = true;
        }

        bool canHoldTwoHanded = otherHandSlot.Empty;

        if (
            !canHoldTwoHanded && Grip == GripType.TwoHanded ||
            Grip == GripType.OneHanded && Grip != DefaultGrip && canHoldTwoHanded ||
            Grip == GripType.TwoHanded && Grip != DefaultGrip
            )
        {
            ChangeGrip(mainHand);
        }
    }
    public virtual void OnDeselected(EntityPlayer player)
    {
        MeleeSystem?.Stop(rightHand: true);
    }
    public virtual void OnRegistered(MeleeWeaponPlayerBehavior behavior, ICoreClientAPI api)
    {
        Behavior = behavior;
    }

    public override void OnHeldRenderOpaque(ItemSlot inSlot, IClientPlayer byPlayer)
    {
        base.OnHeldRenderOpaque(inSlot, byPlayer);

#if DEBUG
        if (Api != null)
        {
            foreach (IPlayer player in Api.World.AllOnlinePlayers)
            {
                DebugCollider.Transform(byPlayer.Entity.Pos, player.Entity, player.Entity.RightHandItemSlot, Api)?.Render(Api, player.Entity);
                DebugParryColliders.Foreach(collider => collider.Transform(byPlayer.Entity.Pos, player.Entity, player.Entity.RightHandItemSlot, Api)?.Render(Api, player.Entity, ColorUtil.ToRgba(255, 255, 0, 0)));
            }
        }
#endif
    }
    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
    }
    public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
    {
        return false;
    }

    protected const string MeleeWeaponStatsAttribute = "melee-weapon-stats";
    protected MeleeWeaponPlayerBehavior? Behavior { get; private set; }
    protected MeleeSystemClient? MeleeSystem { get; private set; }
    protected MeleeSystemServer? ServerMeleeSystem { get; private set; }
    protected MeleeBlockSystemClient? BlockSystem { get; private set; }
    protected MeleeBlockSystemServer? ServerBlockSystem { get; private set; }
    protected ICoreClientAPI? Api { get; private set; }

    protected PlayerAnimationData IdleOneHandedAnimation { get; set; }
    protected PlayerAnimationData ReadyOneHandedAnimation { get; set; }
    protected PlayerAnimationData IdleTwoHandedAnimation { get; set; }
    protected PlayerAnimationData ReadyTwoHandedAnimation { get; set; }
    protected PlayerAnimationData IdleOffhandAnimation { get; set; }
    protected PlayerAnimationData ReadyOffhandAnimation { get; set; }
    protected DirectionsConfiguration DirectionsConfigurationOneHanded { get; set; } = DirectionsConfiguration.None;
    protected DirectionsConfiguration DirectionsConfigurationTwoHanded { get; set; } = DirectionsConfiguration.None;

    protected LineSegmentCollider DebugCollider { get; set; }
    protected RectangularCollider[] DebugParryColliders { get; set; } = Array.Empty<RectangularCollider>();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> AttacksAnimationsOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> AttacksAnimationsTwoHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> ParriesAnimationsOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> ParriesAnimationsTwoHanded { get; set; } = new();

    protected Dictionary<AttackDirection, AttackId> AttacksOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, AttackId> AttacksTwoHanded { get; set; } = new();
    protected Dictionary<AttackDirection, MeleeBlockId> BlockIdsOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, MeleeBlockId> BlockIdsTwoHanded { get; set; } = new();
    protected TimeSpan StaggerDuration { get; set; } = TimeSpan.Zero;
    protected TimeSpan BlockCooldown { get; set; } = TimeSpan.Zero;
    protected TimeSpan FeintCooldown { get; set; } = TimeSpan.Zero;
    protected Dictionary<GripType, IEnumerable<IParryCollider>> ParryCollidersByGrip { get; set; } = new();
    protected DirectionalWeaponParameters DirectionalWeaponParameters { get; private set; } = new();
    protected long CooldownTimer { get; set; } = -1;
    protected long FeintCooldownTimer { get; set; } = -1;
    protected long StaggerCooldownTimer { get; set; } = -1;
    protected GripType DefaultGrip { get; set; } = GripType.OneHanded;
    protected GripType Grip { get; set; } = GripType.OneHanded;
    protected bool CanParry => Grip == GripType.OneHanded ? ParriesAnimationsOneHanded.Any() : ParriesAnimationsTwoHanded.Any();
    protected bool CanAttack => Grip == GripType.OneHanded ? AttacksAnimationsOneHanded.Any() : AttacksAnimationsTwoHanded.Any();

    protected AnimationId StaggerAnimation;
    protected RunParameters[] StaggerAnimationParameters;
    protected AnimationManagerLibSystem Manager;

    [ActionEventHandler(EnumEntityAction.LeftMouseDown, ActionState.Active)]
    protected virtual bool OnAttackOneHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.OneHanded) return false;
        if (AltPressed() || eventData.Modifiers.Contains(EnumEntityAction.CtrlKey)) return false;
        if (!CheckIfCanAttack(mainHand, state)) return false;
        if (!AttacksOneHanded.ContainsKey(direction)) return false;
        if (CurrentActivity(mainHand) == WeaponActivity.Blocking)
        {
            if (FeintCooldownTimer != -1) return false;
            StopParry(mainHand);
            FeintCooldownTimer = Api?.World.RegisterCallback((dt) => FeintCooldownTimer = -1, (int)FeintCooldown.TotalMilliseconds) ?? 0;
        }

        

        MeleeSystem?.Start(AttacksOneHanded[direction], result => OnAttackCallback(result, slot, direction, mainHand), direction);
        TimeSpan easeOutTime = TimeSpan.FromMilliseconds(500);


        if (ReadyAnimation is PlayerAnimationData readyAnimation)
        {
            Behavior?.PlayAnimation(AttacksAnimationsOneHanded[direction].animation, readyAnimation, easeOutTime, true, true, null, AttacksAnimationsOneHanded[direction].parameters);
        }
        if (Behavior != null) Behavior.SuppressLMB = true;

        state = MeleeWeaponState.Active;
        if (mainHand)
        {
            CurrentMainHandActivity = WeaponActivity.Attacking;
        }
        else
        {
            CurrentOffhandActivity = WeaponActivity.Attacking;
        }

        OnAttackStart(slot, player, ref state, eventData, mainHand, direction);

        return true;
    }

    [ActionEventHandler(EnumEntityAction.LeftMouseDown, ActionState.Active)]
    protected virtual bool OnAttackTwoHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.TwoHanded) return false;
        if (AltPressed() || eventData.Modifiers.Contains(EnumEntityAction.CtrlKey)) return false;
        if (!CheckIfCanAttack(mainHand, state)) return false;
        if (!AttacksTwoHanded.ContainsKey(direction)) return false;
        if (CurrentActivity(mainHand) == WeaponActivity.Blocking)
        {
            if (FeintCooldownTimer != -1) return false;
            StopParry(mainHand);
            FeintCooldownTimer = Api?.World.RegisterCallback((dt) => FeintCooldownTimer = -1, (int)FeintCooldown.TotalMilliseconds) ?? 0;
        }

        MeleeSystem?.Start(AttacksTwoHanded[direction], result => OnAttackCallback(result, slot, direction, mainHand), direction);
        TimeSpan easeOutTime = TimeSpan.FromMilliseconds(500);

        if (ReadyAnimation is PlayerAnimationData readyAnimation)
        {
            Behavior?.PlayAnimation(AttacksAnimationsTwoHanded[direction].animation, readyAnimation, easeOutTime, true, true, null, AttacksAnimationsTwoHanded[direction].parameters);
        }
        if (Behavior != null) Behavior.SuppressLMB = true;

        state = MeleeWeaponState.Active;
        if (mainHand)
        {
            CurrentMainHandActivity = WeaponActivity.Attacking;
        }
        else
        {
            CurrentOffhandActivity = WeaponActivity.Attacking;
        }

        OnAttackStart(slot, player, ref state, eventData, mainHand, direction);

        return true;
    }
    
    protected virtual void OnAttackStart(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {

    }
    protected virtual void OnAttackCallback(AttackResult result, ItemSlot slot, AttackDirection direction, bool mainHand)
    {
        if ((result.Result & AttackResultFlag.Parried) != 0)
        {
            StopAttack(mainHand);
            if (DirectionalWeaponParameters.ParrySound != null) Api?.World.PlaySoundAt(new(DirectionalWeaponParameters.ParrySound), Api?.World.Player.Entity);
            Behavior?.SetState(MeleeWeaponState.Cooldown);
            StaggerCooldownTimer = Api?.World.RegisterCallback(dt => StaggerCallback(mainHand), (int)StaggerDuration.TotalMilliseconds) ?? -1;
            Manager.Run(new(Behavior.entity.EntityId, AnimationTargetType.EntityFirstPerson), new(StaggerAnimation, StaggerAnimationParameters));
            Manager.Run(new(Behavior.entity.EntityId, AnimationTargetType.EntityThirdPerson), new(StaggerAnimation, StaggerAnimationParameters));
        }

        if ((result.Result & AttackResultFlag.Blocked) != 0)
        {
            StopAttack(mainHand);
            if (DirectionalWeaponParameters.BlockSound != null) Api?.World.PlaySoundAt(new(DirectionalWeaponParameters.BlockSound), Api?.World.Player.Entity);
            Behavior?.SetState(MeleeWeaponState.Cooldown);
            StaggerCooldownTimer = Api?.World.RegisterCallback(dt => StaggerCallback(mainHand), (int)BlockCooldown.TotalMilliseconds) ?? -1;
            Manager.Run(new(Behavior.entity.EntityId, AnimationTargetType.EntityFirstPerson), new(StaggerAnimation, StaggerAnimationParameters));
            Manager.Run(new(Behavior.entity.EntityId, AnimationTargetType.EntityThirdPerson), new(StaggerAnimation, StaggerAnimationParameters));
        }

        if (result.Result == AttackResultFlag.Finished)
        {
            StopAttack(mainHand);
        }
    }

    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Active)]
    protected virtual bool OnBlockOneHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.OneHanded) return false;
        if (AltPressed()) return false;
        if (!CheckIfCanParry(mainHand, state)) return false;
        if (!ParriesAnimationsOneHanded.ContainsKey(direction)) return false;
        if (CurrentActivity(mainHand) == WeaponActivity.Attacking)
        {
            if (FeintCooldownTimer != -1) return false;
            StopAttack(mainHand);
            FeintCooldownTimer = Api?.World.RegisterCallback((dt) => FeintCooldownTimer = -1, (int)FeintCooldown.TotalMilliseconds) ?? 0;
        }

        state = MeleeWeaponState.Active;
        if (mainHand)
        {
            CurrentMainHandActivity = WeaponActivity.Blocking;
        }
        else
        {
            CurrentOffhandActivity = WeaponActivity.Blocking;
        }

        Console.WriteLine($"PlayAnimation: {ParriesAnimationsOneHanded[direction].animation.FpHands}");
        Behavior?.PlayAnimation(ParriesAnimationsOneHanded[direction].animation, mainHand, false, null, ParriesAnimationsOneHanded[direction].parameters);
        BlockSystem?.Start(BlockIdsOneHanded[direction], (int)direction, mainHand);

        return true;
    }

    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Active)]
    protected virtual bool OnBlockTwoHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.TwoHanded) return false;
        if (AltPressed()) return false;
        if (!CheckIfCanParry(mainHand, state)) return false;
        if (!ParriesAnimationsTwoHanded.ContainsKey(direction)) return false;
        if (CurrentActivity(mainHand) == WeaponActivity.Attacking)
        {
            if (FeintCooldownTimer != -1) return false;
            StopAttack(mainHand);
            FeintCooldownTimer = Api?.World.RegisterCallback((dt) => FeintCooldownTimer = -1, (int)FeintCooldown.TotalMilliseconds) ?? 0;
        }

        state = MeleeWeaponState.Active;
        if (mainHand)
        {
            CurrentMainHandActivity = WeaponActivity.Blocking;
        }
        else
        {
            CurrentOffhandActivity = WeaponActivity.Blocking;
        }

        Behavior?.PlayAnimation(ParriesAnimationsTwoHanded[direction].animation, mainHand, false, null, ParriesAnimationsTwoHanded[direction].parameters);
        BlockSystem?.Start(BlockIdsTwoHanded[direction], (int)direction, mainHand);

        return true;
    }

    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Released)]
    protected virtual bool OnEase(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (state != MeleeWeaponState.Active) return false;
        if (CurrentActivity(mainHand) != WeaponActivity.Blocking) return false;
        
        state = MeleeWeaponState.Cooldown;
        StopParry(mainHand);

        return true;
    }

    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Pressed)]
    protected virtual bool OnChangeGrip(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (!DirectionalWeaponParameters.CanChangeGrip) return false;

        if (AltPressed() || !eventData.Modifiers.Contains(EnumEntityAction.CtrlKey)) return false;

        ChangeGrip(mainHand);

        return true;
    }

    protected virtual void ChangeGrip(bool mainHand)
    {
        if (Behavior == null) return;

        Grip = Grip == GripType.OneHanded ? GripType.TwoHanded : GripType.OneHanded;
        MeleeSystem?.Stop(rightHand: mainHand);
        BlockSystem?.Stop();
        Behavior.SetState(MeleeWeaponState.Cooldown, mainHand);
        CooldownTimer = Api?.World.RegisterCallback((dt) => Behavior?.SetState(MeleeWeaponState.Idle, mainHand), (int)DirectionalWeaponParameters.GripChangeCooldownMs) ?? 0;
        Behavior?.PlayAnimation(ReadyAnimation, mainHand);
    }
    protected bool AltPressed() => (Api?.Input.KeyboardKeyState[(int)GlKeys.AltLeft] ?? false) || (Api?.Input.KeyboardKeyState[(int)GlKeys.AltRight] ?? false);

    protected void ConstructAnimations()
    {
        IAnimationManagerSystem? animationSystem = Api?.ModLoader.GetModSystem<AnimationManagerLibSystem>();

        if (animationSystem == null) return;

        IdleOneHandedAnimation = new PlayerAnimationData(DirectionalWeaponParameters.IdleAnimationOneHanded, animationSystem);
        IdleTwoHandedAnimation = new PlayerAnimationData(DirectionalWeaponParameters.IdleAnimationTwoHanded, animationSystem);

        ReadyOneHandedAnimation = new PlayerAnimationData(DirectionalWeaponParameters.ReadyAnimationOneHanded, animationSystem);
        ReadyTwoHandedAnimation = new PlayerAnimationData(DirectionalWeaponParameters.ReadyAnimationTwoHanded, animationSystem);

        IdleOffhandAnimation = new PlayerAnimationData(DirectionalWeaponParameters.IdleAnimationOffhandOneHanded, animationSystem);
        ReadyOffhandAnimation = new PlayerAnimationData(DirectionalWeaponParameters.ReadyAnimationOffhandOneHanded, animationSystem);

        DirectionsConfigurationOneHanded = Enum.Parse<DirectionsConfiguration>(DirectionalWeaponParameters.DirectionsConfigurationOneHanded);
        DirectionsConfigurationTwoHanded = Enum.Parse<DirectionsConfiguration>(DirectionalWeaponParameters.DirectionsConfigurationTwoHanded);

        AttacksAnimationsOneHanded = ParseAttacksAnimationsFromStats(DirectionalWeaponParameters.AttacksOneHanded);
        AttacksAnimationsTwoHanded = ParseAttacksAnimationsFromStats(DirectionalWeaponParameters.AttacksTwoHanded);

        ParriesAnimationsOneHanded = ParseParriesAnimationsFromStats(DirectionalWeaponParameters.ParriesOneHanded);
        ParriesAnimationsTwoHanded = ParseParriesAnimationsFromStats(DirectionalWeaponParameters.ParriesTwoHanded);
    }
    protected void RegisterAttacks()
    {
        Dictionary<AttackDirection, MeleeAttackStats> attacksOneHanded = ParseAttacksFromStats(DirectionalWeaponParameters.AttacksOneHanded, DirectionalWeaponParameters.MaxReach);
        Dictionary<AttackDirection, MeleeAttackStats> attacksTwoHanded = ParseAttacksFromStats(DirectionalWeaponParameters.AttacksTwoHanded, DirectionalWeaponParameters.MaxReach);

        foreach ((AttackDirection direction, MeleeAttackStats stats) in attacksOneHanded)
        {
            AttackId attackId = new(itemId: WeaponItemId, id: (int)direction + 100);
            AttacksOneHanded.Add(direction, attackId);
            ServerMeleeSystem?.Register(id: (int)direction, itemId: WeaponItemId, stats);
            MeleeSystem?.Register(attackId, stats);
        }
        foreach ((AttackDirection direction, MeleeAttackStats stats) in attacksTwoHanded)
        {
            AttackId attackId = new(itemId: WeaponItemId, id: (int)direction + 200);
            AttacksTwoHanded.Add(direction, attackId);
            ServerMeleeSystem?.Register(id: (int)direction, itemId: WeaponItemId, stats);
            MeleeSystem?.Register(attackId, stats);
        }
    }
    protected void RegisterParries()
    {
        Dictionary<AttackDirection, MeleeBlock> blocksOneHanded = ParseBlocksFromStats(DirectionalWeaponParameters.ParriesOneHanded, DirectionalWeaponParameters);
        Dictionary<AttackDirection, MeleeBlock> blocksTwoHanded = ParseBlocksFromStats(DirectionalWeaponParameters.ParriesTwoHanded, DirectionalWeaponParameters);

        ParryCollidersByGrip[GripType.OneHanded] = DirectionalWeaponParameters.OneHandedParriesColliders.Select(collider => new RectangularCollider(collider)).OfType<IParryCollider>();
        ParryCollidersByGrip[GripType.TwoHanded] = DirectionalWeaponParameters.TwoHandedParriesColliders.Select(collider => new RectangularCollider(collider)).OfType<IParryCollider>();

        foreach ((AttackDirection direction, MeleeBlock block) in blocksOneHanded)
        {
            MeleeBlockId id = new(WeaponItemId, (int)direction + (int)GripType.OneHanded * Enum.GetValues<AttackDirection>().Length);
            BlockIdsOneHanded.Add(direction, id);
            ServerBlockSystem?.Register(id, block);
            BlockSystem?.Register(id, block);
        }

        foreach ((AttackDirection direction, MeleeBlock block) in blocksTwoHanded)
        {
            MeleeBlockId id = new(WeaponItemId, (int)direction + (int)GripType.TwoHanded * Enum.GetValues<AttackDirection>().Length);
            BlockIdsTwoHanded.Add(direction, id);
            ServerBlockSystem?.Register(id, block);
            BlockSystem?.Register(id, block);
        }
    }

    protected bool CheckIfCanParry(bool mainHand, MeleeWeaponState state)
    {
        if (FeintCooldownTimer != -1 || StaggerCooldownTimer != -1) return false;
        if (Behavior?.GetState(mainHand: !mainHand) != MeleeWeaponState.Idle) return false;
        if (state == MeleeWeaponState.Cooldown) return false;

        WeaponActivity activity = mainHand ? CurrentMainHandActivity : CurrentOffhandActivity;
        if (state == MeleeWeaponState.Idle || activity == WeaponActivity.Attacking) return true;

        return false;
    }
    protected bool CheckIfCanAttack(bool mainHand, MeleeWeaponState state)
    {
        if (Behavior?.GetState(mainHand: !mainHand) != MeleeWeaponState.Idle || StaggerCooldownTimer != -1) return false;
        if (state == MeleeWeaponState.Cooldown) return false;

        WeaponActivity activity = mainHand ? CurrentMainHandActivity : CurrentOffhandActivity;
        if (state == MeleeWeaponState.Idle || activity == WeaponActivity.Blocking) return true;

        return false;
    }

    protected void StopAttack(bool mainHand)
    {
        MeleeSystem?.Stop();
        Behavior?.SetState(MeleeWeaponState.Idle);
        if (Behavior != null) Behavior.SuppressLMB = false;
        if (mainHand)
        {
            CurrentMainHandActivity = WeaponActivity.Idle;
        }
        else
        {
            CurrentOffhandActivity = WeaponActivity.Idle;
        }
    }
    protected void StopParry(bool mainHand)
    {
        if (CooldownTimer != -1) Api?.World.UnregisterCallback(CooldownTimer);
        CooldownTimer = Api?.World.RegisterCallback((dt) => CooldownCallback(mainHand), (int)BlockCooldown.TotalMilliseconds) ?? 0;

        Behavior?.StopAnimation(mainHand, true, null, RunParameters.EaseOut(BlockCooldown, ProgressModifierType.Sin));
        if (Behavior != null) Behavior.SuppressRMB = false;
        BlockSystem?.Stop();
        
        if (mainHand)
        {
            CurrentMainHandActivity = WeaponActivity.Idle;
        }
        else
        {
            CurrentOffhandActivity = WeaponActivity.Idle;
        }
    }
    protected WeaponActivity CurrentActivity(bool mainHand) => mainHand ? CurrentMainHandActivity : CurrentOffhandActivity;
    protected void CooldownCallback(bool mainHand)
    {
        MeleeWeaponState currentState = Behavior?.GetState(mainHand) ?? MeleeWeaponState.Idle;
        if (currentState == MeleeWeaponState.Cooldown) Behavior?.SetState(MeleeWeaponState.Idle, mainHand);
        CooldownTimer = -1;
    }
    protected void StaggerCallback(bool mainHand)
    {
        StaggerCooldownTimer = -1;
        Behavior?.SetState(MeleeWeaponState.Idle);
    }

    protected static Dictionary<AttackDirection, MeleeAttackStats> ParseAttacksFromStats(Dictionary<string, DirectionalWeaponAttackStats> attacks, float maxReach)
    {
        Dictionary<AttackDirection, MeleeAttackStats> result = new();

        foreach ((string direction, DirectionalWeaponAttackStats stats) in attacks)
        {
            float duration = stats.DurationMs + stats.WindUpMs + stats.EaseOutMs;

            MeleeAttackStats attackStats = new()
            {
                Duration = duration,
                MaxReach = maxReach,
                DamageTypes = new MeleeAttackDamageTypeStats[]
                {
                    new()
                    {
                        Damage = stats.Damage,
                        Tier = stats.Tier,
                        DamageType = stats.DamageType,
                        Collider = stats.Collider,
                        HitWindow = new float[] { stats.WindUpMs / duration, (stats.DurationMs + stats.WindUpMs) / duration }
                    }
                }
            };

            result.Add(Enum.Parse<AttackDirection>(direction), attackStats);
        }

        return result;
    }
    protected static Dictionary<AttackDirection, MeleeBlock> ParseBlocksFromStats(Dictionary<string, DirectionalWeaponBlockStats> attacks, DirectionalWeaponParameters stats)
    {
        Dictionary<AttackDirection, MeleeBlock> result = new();

        foreach ((string directionName, DirectionalWeaponBlockStats? blockStats) in attacks)
        {
            AttackDirection direction = Enum.Parse<AttackDirection>(directionName);

            result.Add(direction, new()
            {
                ParryWindow = TimeSpan.FromMilliseconds(blockStats.ParryDurationMs),
                Coverage = DirectionConstrain.FromDegrees(blockStats.CoverageDegrees),
                DamageReduction = blockStats.IncomingDamageMultiplier,
                BlockSound = stats.BlockSound != null ? new AssetLocation(stats.BlockSound) : null,
                ParrySound = stats.ParrySound != null ? new AssetLocation(stats.ParrySound) : null,
                CancelSound = stats.CancelBlockSound != null ? new AssetLocation(stats.CancelBlockSound) : null,
            });
        }

        return result;
    }
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> ParseAttacksAnimationsFromStats(Dictionary<string, DirectionalWeaponAttackStats> attacks)
    {
        Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> result = new();
        AnimationManagerLibSystem? animationSystem = Api?.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        if (animationSystem == null) return result;

        foreach ((string direction, DirectionalWeaponAttackStats stats) in attacks)
        {
            RunParameters[] parameters;

            if (stats.AnimationParameters.Length > 0)
            {
                parameters = stats.AnimationParameters.Select(element => element.ToRunParameters()).ToArray();
            }
            else
            {
                parameters = new RunParameters[]
                {
                    RunParameters.EaseIn(stats.WindUpMs / 1000.0f, stats.AnimationWindupFrame, ProgressModifierType.SinQuadratic),
                    RunParameters.Play(stats.DurationMs / 1000.0f, stats.AnimationWindupFrame, stats.AnimationStrikeFrame, ProgressModifierType.Cubic),
                };
            }

            PlayerAnimationData animation = new(stats.Animation, animationSystem);

            result.Add(Enum.Parse<AttackDirection>(direction), (animation, parameters));
        }

        return result;
    }
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> ParseParriesAnimationsFromStats(Dictionary<string, DirectionalWeaponBlockStats> parries)
    {
        Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> result = new();
        AnimationManagerLibSystem? animationSystem = Api?.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        if (animationSystem == null) return result;

        foreach ((string direction, DirectionalWeaponBlockStats stats) in parries)
        {
            RunParameters[] parameters;

            parameters = stats.AnimationParameters.Select(element => element.ToRunParameters()).ToArray();

            PlayerAnimationData animation = new(stats.Animation, animationSystem);

            result.Add(Enum.Parse<AttackDirection>(direction), (animation, parameters));
        }

        return result;
    }
}
