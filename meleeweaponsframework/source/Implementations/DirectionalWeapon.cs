using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

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
    public Dictionary<string, DirectionalWeaponAttackStats> AttacksOneHanded { get; set; } = new();
    public Dictionary<string, DirectionalWeaponAttackStats> AttacksTwoHanded { get; set; } = new();

    public bool StopOnCollisionWithTerrain { get; set; } = false;
    public bool StopOnCollisionWithEntity { get; set; } = false;

    public bool CanChangeGrip { get; set; } = false;
    public string DefaultGrip { get; set; } = "OneHanded";
    public float GripChangeCooldownMs { get; set; } = 200;
    public bool DirectionlessPerfectBlockOneHanded { get; set; }
    public bool DirectionlessPerfectBlockTwoHanded { get; set; }
    public string? BlockSound { get; set; }
    public string? PerfectBlockSound { get; set; }
    public string? CancelBlockSound { get; set; }
    public float PerfectBlockWindowOneHandedMs { get; set; } = 300;
    public float PerfectBlockWindowTwoHandedMs { get; set; } = 300;
    public float CoverageDegreesOneHanded { get; set; } = 120;
    public float CoverageDegreesTwoHanded { get; set; } = 120;
    public float DamageMultiplierOneHanded { get; set; } = 0.5f;
    public float DamageMultiplierTwoHanded { get; set; } = 0.5f;
    public Dictionary<string, DirectionalWeaponParryStats> ParriesOnHanded { get; set; } = new();
    public Dictionary<string, DirectionalWeaponParryStats> ParriesTwoHanded { get; set; } = new();
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
}

public class DirectionalWeaponParryStats
{
    public string Animation { get; set; } = "";
    public float AnimationFrame { get; set; } = 0f;
    public float EaseInTimeMs { get; set; } = 100;
    public float EaseOutTimeMs { get; set; } = 100;
    public string[] Directions { get; set; } = Array.Empty<string>();
}

public enum GripType
{
    OneHanded,
    TwoHanded
}

public class DirectionalWeapon : Item, IMeleeWeaponItem
{
    public int WeaponItemId => ItemId;
    public virtual PlayerAnimationData IdleAnimation => Grip == GripType.OneHanded ? IdleOneHandedAnimation : IdleTwoHandedAnimation;
    public virtual PlayerAnimationData ReadyAnimation => Grip == GripType.OneHanded ? ReadyOneHandedAnimation : ReadyTwoHandedAnimation;
    public virtual PlayerAnimationData IdleAnimationOffhand => IdleOffhandAnimation;
    public virtual PlayerAnimationData ReadyAnimationOffhand => ReadyOffhandAnimation;
    public virtual DirectionsConfiguration DirectionsType => Grip == GripType.OneHanded ? DirectionsConfigurationOneHanded : DirectionsConfigurationTwoHanded;
    public virtual bool RenderDirectionCursor { get; protected set; } = true;

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

        if (api is not ICoreClientAPI clientAPI) return;

        Api = clientAPI;

        MeleeSystem = Api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemClient;
        BlockSystem = Api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().BlockSystemClient;

        ConstructAnimations();
        RegisterAttacks();
        RegisterParries();

        DebugCollider = new(DirectionalWeaponParameters.DebugCollider);
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
            DebugCollider.Transform(byPlayer.Entity, inSlot, Api)?.Render(Api, byPlayer.Entity);
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
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> AttacksAnimationsOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> AttacksAnimationsTwoHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> ParriesAnimationsOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> ParriesAnimationsTwoHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> ParriesEaseOutAnimationsOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] parameters)> ParriesEaseOutAnimationsTwoHanded { get; set; } = new();

    protected Dictionary<AttackDirection, int> ParriesCooldownsOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, int> ParriesCooldownsTwoHanded { get; set; } = new();
    protected Dictionary<AttackDirection, AttackId> AttacksOneHanded { get; set; } = new();
    protected Dictionary<AttackDirection, AttackId> AttacksTwoHanded { get; set; } = new();
    protected MeleeBlockId BlockIdOneHanded { get; set; } = new(0, 0);
    protected MeleeBlockId BlockIdTwoHanded { get; set; } = new(0, 1);
    protected DirectionalWeaponParameters DirectionalWeaponParameters { get; private set; } = new();
    protected long CooldownTimer { get; set; } = 0;
    protected GripType DefaultGrip { get; set; } = GripType.OneHanded;
    protected GripType Grip { get; set; } = GripType.OneHanded;
    protected bool CanParry => Grip == GripType.OneHanded ? ParriesAnimationsOneHanded.Any() : ParriesAnimationsTwoHanded.Any();
    protected bool CanAttack => Grip == GripType.OneHanded ? AttacksAnimationsOneHanded.Any() : AttacksAnimationsTwoHanded.Any();

    [ActionEventHandler(EnumEntityAction.InWorldLeftMouseDown, ActionState.Pressed)]
    protected virtual bool OnAttackOneHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.OneHanded) return false;

        if (AltPressed() || eventData.Modifiers.Contains(EnumEntityAction.CtrlKey)) return false;

        if (state != MeleeWeaponState.Idle || Behavior?.GetState(mainHand: !mainHand) != MeleeWeaponState.Idle) return false;

        if (!AttacksOneHanded.ContainsKey(direction)) return false;

        MeleeSystem?.Start(AttacksOneHanded[direction], result => OnAttackCallback(result, slot, direction, mainHand), direction);
        Behavior?.PlayAnimation(AttacksAnimationsOneHanded[direction].animation, true, true, null, AttacksAnimationsOneHanded[direction].parameters);
        state = MeleeWeaponState.Active;

        return true;
    }
    [ActionEventHandler(EnumEntityAction.InWorldLeftMouseDown, ActionState.Pressed)]
    protected virtual bool OnAttackTwoHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.TwoHanded) return false;

        if (AltPressed() || eventData.Modifiers.Contains(EnumEntityAction.CtrlKey)) return false;

        if (state != MeleeWeaponState.Idle || Behavior?.GetState(mainHand: !mainHand) != MeleeWeaponState.Idle) return false;

        if (!AttacksTwoHanded.ContainsKey(direction)) return false;

        MeleeSystem?.Start(AttacksTwoHanded[direction], result => OnAttackCallback(result, slot, direction, mainHand), direction);
        Behavior?.PlayAnimation(AttacksAnimationsTwoHanded[direction].animation, true, true, null, AttacksAnimationsTwoHanded[direction].parameters);
        state = MeleeWeaponState.Active;

        return true;
    }
    protected virtual void OnAttackCallback(AttackResult result, ItemSlot slot, AttackDirection direction, bool mainHand)
    {
        if (result.Result == AttackResultFlag.Finished)
        {
            MeleeSystem?.Stop();
            Behavior?.SetState(MeleeWeaponState.Idle);
        }
    }
    [ActionEventHandler(EnumEntityAction.InWorldRightMouseDown, ActionState.Pressed)]
    protected virtual bool OnBlockOneHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.OneHanded) return false;

        if (AltPressed()) return false;

        if (state != MeleeWeaponState.Idle || Behavior?.GetState(mainHand: !mainHand) != MeleeWeaponState.Idle) return false;

        if (!ParriesAnimationsOneHanded.ContainsKey(direction)) return false;

        state = MeleeWeaponState.Active;

        Behavior?.PlayAnimation(ParriesAnimationsOneHanded[direction].animation, mainHand, false, null, ParriesAnimationsOneHanded[direction].parameters);
        BlockSystem?.Start(BlockIdOneHanded, (int)direction, mainHand);

        return true;
    }
    [ActionEventHandler(EnumEntityAction.InWorldRightMouseDown, ActionState.Pressed)]
    protected virtual bool OnBlockTwoHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.TwoHanded) return false;

        if (AltPressed()) return false;

        if (state != MeleeWeaponState.Idle || Behavior?.GetState(mainHand: !mainHand) != MeleeWeaponState.Idle) return false;

        if (!ParriesAnimationsTwoHanded.ContainsKey(direction)) return false;

        state = MeleeWeaponState.Active;

        Behavior?.PlayAnimation(ParriesAnimationsTwoHanded[direction].animation, mainHand, false, null, ParriesAnimationsTwoHanded[direction].parameters);
        BlockSystem?.Start(BlockIdTwoHanded, (int)direction, mainHand);

        return true;
    }
    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Released)]
    protected virtual bool OnEaseOneHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.OneHanded) return false;

        if (mainHand) return false;

        if (state != MeleeWeaponState.Active) return true;
        state = MeleeWeaponState.Cooldown;

        CooldownTimer = Api?.World.RegisterCallback((dt) => Behavior?.SetState(MeleeWeaponState.Idle, mainHand), ParriesCooldownsOneHanded[direction]) ?? 0;
        Behavior?.StopAnimation(mainHand, true, null, ParriesEaseOutAnimationsOneHanded[direction].parameters);
        BlockSystem?.Stop();

        return true;
    }
    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Released)]
    protected virtual bool OnEaseTwoHanded(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (Grip != GripType.TwoHanded) return false;

        if (mainHand) return false;

        if (state != MeleeWeaponState.Active) return true;
        state = MeleeWeaponState.Cooldown;

        CooldownTimer = Api?.World.RegisterCallback((dt) => Behavior?.SetState(MeleeWeaponState.Idle, mainHand), ParriesCooldownsTwoHanded[direction]) ?? 0;
        Behavior?.StopAnimation(mainHand, true, null, ParriesEaseOutAnimationsTwoHanded[direction].parameters);
        BlockSystem?.Stop();

        return true;
    }
    [ActionEventHandler(EnumEntityAction.InWorldRightMouseDown, ActionState.Pressed)]
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

        IdleOneHandedAnimation = new(DirectionalWeaponParameters.IdleAnimationOneHanded, animationSystem);
        IdleTwoHandedAnimation = new(DirectionalWeaponParameters.IdleAnimationTwoHanded, animationSystem);

        ReadyOneHandedAnimation = new(DirectionalWeaponParameters.ReadyAnimationOneHanded, animationSystem);
        ReadyTwoHandedAnimation = new(DirectionalWeaponParameters.ReadyAnimationTwoHanded, animationSystem);

        IdleOffhandAnimation = new(DirectionalWeaponParameters.IdleAnimationOffhandOneHanded, animationSystem);
        ReadyOffhandAnimation = new(DirectionalWeaponParameters.ReadyAnimationOffhandOneHanded, animationSystem);

        DirectionsConfigurationOneHanded = Enum.Parse<DirectionsConfiguration>(DirectionalWeaponParameters.DirectionsConfigurationOneHanded);
        DirectionsConfigurationTwoHanded = Enum.Parse<DirectionsConfiguration>(DirectionalWeaponParameters.DirectionsConfigurationTwoHanded);

        AttacksAnimationsOneHanded = ParseAttacksAnimationsFromStats(DirectionalWeaponParameters.AttacksOneHanded);
        AttacksAnimationsTwoHanded = ParseAttacksAnimationsFromStats(DirectionalWeaponParameters.AttacksTwoHanded);

        ParriesAnimationsOneHanded = ParseParriesAnimationsFromStats(DirectionalWeaponParameters.ParriesOnHanded);
        ParriesAnimationsTwoHanded = ParseParriesAnimationsFromStats(DirectionalWeaponParameters.ParriesTwoHanded);

        ParriesEaseOutAnimationsOneHanded = ParseParriesEaseOutAnimationsFromStats(DirectionalWeaponParameters.ParriesOnHanded);
        ParriesEaseOutAnimationsTwoHanded = ParseParriesEaseOutAnimationsFromStats(DirectionalWeaponParameters.ParriesTwoHanded);

        ParriesCooldownsOneHanded = DirectionalWeaponParameters.ParriesOnHanded.ToDictionary(entry => Enum.Parse<AttackDirection>(entry.Key), entry => (int)entry.Value.EaseOutTimeMs);
        ParriesCooldownsTwoHanded = DirectionalWeaponParameters.ParriesTwoHanded.ToDictionary(entry => Enum.Parse<AttackDirection>(entry.Key), entry => (int)entry.Value.EaseOutTimeMs);
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
        MeleeBlock blockOneHanded = ParseBlocksFromStatsOneHanded(DirectionalWeaponParameters);
        MeleeBlock blockTwoHanded = ParseBlocksFromStatsTwoHanded(DirectionalWeaponParameters);

        BlockIdOneHanded = new(WeaponItemId, 0);
        BlockIdTwoHanded = new(WeaponItemId, 1);

        ServerBlockSystem?.Register(BlockIdOneHanded, blockOneHanded);
        ServerBlockSystem?.Register(BlockIdTwoHanded, blockTwoHanded);

        BlockSystem?.Register(BlockIdOneHanded, blockOneHanded);
        BlockSystem?.Register(BlockIdTwoHanded, blockTwoHanded);
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
    protected static MeleeBlock ParseBlocksFromStatsOneHanded(DirectionalWeaponParameters stats)
    {
        List<BlockDirections> directions = new();
        Dictionary<AttackDirection, AttackDirection[]> attacksDirections = stats.ParriesOnHanded
            .Select(entry => (Enum.Parse<AttackDirection>(entry.Key), entry.Value.Directions.Select(Enum.Parse<AttackDirection>).ToArray()))
            .ToDictionary(entry => entry.Item1, entry => entry.Item2);

        for (int index = 0; index < 8; index++)
        {
            AttackDirection direction = (AttackDirection)index;

            if (attacksDirections.TryGetValue(direction, out AttackDirection[] attackDirections))
            {
                directions.Add(new(attackDirections));
            }
            else
            {
                directions.Add(new());
            }
        }

        return new()
        {
            PerfectBlockWindow = TimeSpan.FromMilliseconds(stats.PerfectBlockWindowOneHandedMs),
            Coverage = DirectionConstrain.FromDegrees(stats.CoverageDegreesOneHanded),
            DirectionlessPerfectBlock = stats.DirectionlessPerfectBlockOneHanded,
            DamageReduction = stats.DamageMultiplierOneHanded,
            BlockSound = stats.BlockSound != null ? new AssetLocation(stats.BlockSound) : null,
            PerfectBlockSound = stats.PerfectBlockSound != null ? new AssetLocation(stats.PerfectBlockSound) : null,
            CancelSound = stats.CancelBlockSound != null ? new AssetLocation(stats.CancelBlockSound) : null,
            Directions = directions
        };
    }
    protected static MeleeBlock ParseBlocksFromStatsTwoHanded(DirectionalWeaponParameters stats)
    {
        List<BlockDirections> directions = new();
        Dictionary<AttackDirection, AttackDirection[]> attacksDirections = stats.ParriesTwoHanded
            .Select(entry => (Enum.Parse<AttackDirection>(entry.Key), entry.Value.Directions.Select(Enum.Parse<AttackDirection>).ToArray()))
            .ToDictionary(entry => entry.Item1, entry => entry.Item2);

        for (int index = 0; index < 8; index++)
        {
            AttackDirection direction = (AttackDirection)index;

            if (attacksDirections.TryGetValue(direction, out AttackDirection[] attackDirections))
            {
                directions.Add(new(attackDirections));
            }
            else
            {
                directions.Add(new());
            }
        }

        return new()
        {
            PerfectBlockWindow = TimeSpan.FromMilliseconds(stats.PerfectBlockWindowTwoHandedMs),
            Coverage = DirectionConstrain.FromDegrees(stats.CoverageDegreesTwoHanded),
            DirectionlessPerfectBlock = stats.DirectionlessPerfectBlockTwoHanded,
            DamageReduction = stats.DamageMultiplierTwoHanded,
            BlockSound = stats.BlockSound != null ? new AssetLocation(stats.BlockSound) : null,
            PerfectBlockSound = stats.PerfectBlockSound != null ? new AssetLocation(stats.PerfectBlockSound) : null,
            CancelSound = stats.CancelBlockSound != null ? new AssetLocation(stats.CancelBlockSound) : null,
            Directions = directions
        };
    }
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> ParseAttacksAnimationsFromStats(Dictionary<string, DirectionalWeaponAttackStats> attacks)
    {
        Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> result = new();
        AnimationManagerLibSystem? animationSystem = Api?.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        if (animationSystem == null) return result;

        foreach ((string direction, DirectionalWeaponAttackStats stats) in attacks)
        {
            RunParameters[] parameters = new RunParameters[]
            {
                RunParameters.EaseIn(stats.WindUpMs / 1000.0f, stats.AnimationWindupFrame, ProgressModifierType.SinQuadratic),
                RunParameters.Play(stats.DurationMs / 1000.0f, stats.AnimationWindupFrame, stats.AnimationStrikeFrame, ProgressModifierType.Linear),
                RunParameters.EaseOut(stats.EaseOutMs / 1000.0f, ProgressModifierType.Sin)
            };

            PlayerAnimationData animation = new(stats.Animation, animationSystem);

            result.Add(Enum.Parse<AttackDirection>(direction), (animation, parameters));
        }

        return result;
    }
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> ParseParriesAnimationsFromStats(Dictionary<string, DirectionalWeaponParryStats> parries)
    {
        Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> result = new();
        AnimationManagerLibSystem? animationSystem = Api?.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        if (animationSystem == null) return result;

        foreach ((string direction, DirectionalWeaponParryStats stats) in parries)
        {
            RunParameters[] parameters = new RunParameters[]
            {
                RunParameters.EaseIn(stats.EaseInTimeMs / 1000.0f, stats.AnimationFrame, ProgressModifierType.SinQuadratic)
            };

            PlayerAnimationData animation = new(stats.Animation, animationSystem);

            result.Add(Enum.Parse<AttackDirection>(direction), (animation, parameters));
        }

        return result;
    }
    protected Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> ParseParriesEaseOutAnimationsFromStats(Dictionary<string, DirectionalWeaponParryStats> parries)
    {
        Dictionary<AttackDirection, (PlayerAnimationData animation, RunParameters[] paremters)> result = new();
        AnimationManagerLibSystem? animationSystem = Api?.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        if (animationSystem == null) return result;

        foreach ((string direction, DirectionalWeaponParryStats stats) in parries)
        {
            RunParameters[] parameters = new RunParameters[]
            {
                RunParameters.EaseOut(stats.EaseOutTimeMs / 1000.0f, ProgressModifierType.Sin)
            };

            PlayerAnimationData animation = new(stats.Animation, animationSystem);

            result.Add(Enum.Parse<AttackDirection>(direction), (animation, parameters));
        }

        return result;
    }
}