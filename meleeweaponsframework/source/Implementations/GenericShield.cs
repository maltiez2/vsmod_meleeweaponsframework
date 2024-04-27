using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Common;

namespace MeleeWeaponsFramework;

public class GenericShieldParameters : MeleeWeaponParameters
{
    public float BlockEaseInTimeMs { get; set; } = 100;
    public float BlockEaseOutTimeMs { get; set; } = 100;
    public string BlockAnimation { get; set; } = "";
    public float BlockAnimationFrame { get; set; } = 0f;

    public float PerfectBlockWindowMs { get; set; } = 300;
    public float CoverageDegrees { get; set; } = 120;
    public float DamageMultiplier { get; set; } = 0.5f;

    public string? BlockSound { get; set; }
    public string? PerfectBlockSound { get; set; }
    public string? CancelBlockSound { get; set; }
}

public enum GenericShieldState
{
    Idle = 0,
    Blocking
}

public class GenericShield : MeleeWeaponItem
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        GenericParameters = Attributes[MeleeWeaponStatsAttribute].AsObject<GenericShieldParameters>();
        BlockAnimation = new(GenericParameters.BlockAnimation, api.ModLoader.GetModSystem<AnimationManagerLibSystem>());

        EaseInAnimationParameters = new RunParameters[]
        {
            RunParameters.EaseIn(GenericParameters.BlockEaseInTimeMs / 1000.0f, GenericParameters.BlockAnimationFrame, ProgressModifierType.SinQuadratic)
        };
        EaseOutAnimationParameters = new RunParameters[]
        {
            RunParameters.EaseOut(GenericParameters.BlockEaseOutTimeMs / 1000.0f, ProgressModifierType.Sin)
        };

        MeleeBlock block = new()
        {
            PerfectBlockWindow = TimeSpan.FromMilliseconds(GenericParameters.PerfectBlockWindowMs),
            Coverage = DirectionConstrain.FromDegrees(GenericParameters.CoverageDegrees),
            Directions = new() { new BlockDirections() },
            DirectionlessPerfectBlock = true,
            DamageReduction = GenericParameters.DamageMultiplier,

            BlockSound = GenericParameters.BlockSound != null ? new AssetLocation(GenericParameters.BlockSound) : null,
            PerfectBlockSound = GenericParameters.PerfectBlockSound != null ? new AssetLocation(GenericParameters.PerfectBlockSound) : null,
            CancelSound = GenericParameters.CancelBlockSound != null ? new AssetLocation(GenericParameters.CancelBlockSound) : null
        };

        BlockId = new(WeaponItemId, 0);

        BlockSystem?.Register(BlockId, block);
        BlockSystemServer?.Register(BlockId, block);
    }

    public override void OnDeselected(EntityPlayer player)
    {
        MeleeSystem?.Stop(rightHand: true);
    }

    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
    {
        return false;
    }

    protected MeleeBlockId BlockId { get; set; } = new(0, 0);
    protected virtual PlayerSimpleAnimationData BlockAnimation { get; set; }
    protected GenericShieldParameters GenericParameters { get; private set; } = new();
    protected RunParameters[] EaseInAnimationParameters { get; private set; } = Array.Empty<RunParameters>();
    protected RunParameters[] EaseOutAnimationParameters { get; private set; } = Array.Empty<RunParameters>();

    [ActionEventHandler(EnumEntityAction.Right, ActionState.Pressed)]
    protected virtual bool OnBlock(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (!mainHand || state != (int)GenericShieldState.Idle) return true;
        state = (int)GenericShieldState.Blocking;

        Behavior?.PlayAnimation(BlockAnimation, mainHand, false, null, EaseInAnimationParameters);
        BlockSystem?.Start(BlockId, 0, mainHand);

        return true;
    }

    [ActionEventHandler(EnumEntityAction.Right, ActionState.Released)]
    protected virtual bool OnEase(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (!mainHand || state != (int)GenericShieldState.Blocking) return true;
        state = (int)GenericShieldState.Idle;

        Behavior?.StopAnimation(mainHand, true, null, EaseOutAnimationParameters);
        BlockSystem?.Stop();

        return true;
    }
}
