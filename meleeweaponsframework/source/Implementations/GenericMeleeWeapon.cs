using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace MeleeWeaponsFramework;

public class GenericMeleeWeaponParameters : MeleeWeaponParameters
{
    public string AttackAnimation { get; set; } = "melee-attack";
    public float AttackAnimationWindupFrame { get; set; } = 1f;
    public float AttackAnimationStrikeFrame { get; set; } = 2f;
    public float[] Collider { get; set; } = new float[6];
    public int AttackWindUpMs { get; set; } = 100;
    public int AttackDurationMs { get; set; } = 300;
    public int AttackEaseOutMs { get; set; } = 500;
    public float AttackDamage { get; set; } = 1.0f;
    public int AttackTier { get; set; } = 0;
    public string AttackDamageType { get; set; } = "BluntAttack";
}

public enum GenericMeleeWeaponState
{
    Idle = 0,
    Attacking
}

public class GenericMeleeWeapon : MeleeWeaponItem
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        GenericParameters = Attributes[MeleeWeaponStatsAttribute].AsObject<GenericMeleeWeaponParameters>();
        AttackAnimation = new(GenericParameters.AttackAnimation, api.ModLoader.GetModSystem<AnimationManagerLibSystem>());

        float duration = GenericParameters.AttackDurationMs + GenericParameters.AttackWindUpMs + GenericParameters.AttackEaseOutMs;

        MeleeAttackStats attackStats = new()
        {
            Duration = duration,
            MaxReach = 6,
            DamageTypes = new MeleeAttackDamageTypeStats[]
            {
                new()
                {
                    Damage = GenericParameters.AttackDamage,
                    Tier = GenericParameters.AttackTier,
                    DamageType = GenericParameters.AttackDamageType,
                    Collider = GenericParameters.Collider,
                    HitWindow = new float[] { GenericParameters.AttackWindUpMs / duration, (GenericParameters.AttackDurationMs + GenericParameters.AttackWindUpMs) / duration }
                }
            }
        };

        Attack = new(Id, 0);

        ServerMeleeSystem?.Register(0, Id, attackStats);
        MeleeSystem?.Register(Attack, attackStats);

        AnimationParameters = new RunParameters[]
        {
            RunParameters.EaseIn(GenericParameters.AttackWindUpMs / 1000.0f, GenericParameters.AttackAnimationWindupFrame, ProgressModifierType.SinQuadratic),
            RunParameters.Play(GenericParameters.AttackDurationMs / 1000.0f, GenericParameters.AttackAnimationWindupFrame, GenericParameters.AttackAnimationStrikeFrame, ProgressModifierType.Linear),
            RunParameters.EaseOut(GenericParameters.AttackEaseOutMs / 1000.0f, ProgressModifierType.Sin)
        };

        DebugCollider = new(GenericParameters.Collider);
    }

    public override void OnDeselected(EntityPlayer player)
    {
        MeleeSystem?.Stop(rightHand: true);
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

    protected LineSegmentCollider DebugCollider;
    protected virtual PlayerSimpleAnimationData AttackAnimation { get; set; }
    protected GenericMeleeWeaponParameters GenericParameters { get; private set; } = new();
    protected AttackId Attack { get; private set; } = new(0, 0);
    protected RunParameters[] AnimationParameters { get; private set; } = Array.Empty<RunParameters>();

    [ActionEventHandler(EnumEntityAction.InWorldLeftMouseDown, ActionState.Pressed)]
    protected virtual bool OnAttack(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (!mainHand || state != (int)GenericMeleeWeaponState.Idle) return true;

        MeleeSystem?.Start(Attack, result => OnAttackCallback(result, slot, direction, mainHand), direction);
        Behavior?.PlayAnimation(AttackAnimation, true, true, null, AnimationParameters);
        state = (int)GenericMeleeWeaponState.Attacking;

        return true;
    }

    protected virtual void OnAttackCallback(AttackResult result, ItemSlot slot, AttackDirection direction, bool mainHand)
    {
        if (result.Result == AttackResultFlag.Finished)
        {
            MeleeSystem?.Stop();
            Behavior?.SetState((int)GenericMeleeWeaponState.Idle);
        }
    }
}
