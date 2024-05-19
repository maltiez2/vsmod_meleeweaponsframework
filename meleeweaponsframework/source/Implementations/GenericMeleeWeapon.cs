using AnimationManagerLib;
using AnimationManagerLib.API;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

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
    public string StrikeSound { get; set; } = "sounds/player/strike1";
    public string TerrainHitSound { get; set; } = "sounds/thud";
    public string EntityHitParticles { get; set; } = "";
    public string TerrainHitParticles { get; set; } = "";
    public WeaponAnimationParameters[] AttackAnimationParameters { get; set; } = Array.Empty<WeaponAnimationParameters>();
}

public class GenericMeleeWeapon : MeleeWeaponItem
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        GenericParameters = Attributes[MeleeWeaponStatsAttribute].AsObject<GenericMeleeWeaponParameters>();
        AttackAnimation = new(GenericParameters.AttackAnimation, api.ModLoader.GetModSystem<AnimationManagerLibSystem>());
        MeleeWeaponsFrameworkModSystem system = api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>();

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
            },
        };

        Attack = new(Id, 0);

        ServerMeleeSystem?.Register(0, Id, attackStats);
        MeleeSystem?.Register(Attack, attackStats);

        if (GenericParameters.AttackAnimationParameters.Length > 0)
        {
            AnimationParameters = GenericParameters.AttackAnimationParameters.Select(element => element.ToRunParameters()).ToArray();
        }
        else
        {
            AnimationParameters = new RunParameters[]
            {
                RunParameters.EaseIn(GenericParameters.AttackWindUpMs / 1000.0f, GenericParameters.AttackAnimationWindupFrame, ProgressModifierType.SinQuadratic),
                RunParameters.Play(GenericParameters.AttackDurationMs / 1000.0f, GenericParameters.AttackAnimationWindupFrame, GenericParameters.AttackAnimationStrikeFrame, ProgressModifierType.Linear),
                RunParameters.EaseOut(GenericParameters.AttackEaseOutMs * EaseOutAnimationFactor / 1000.0f, ProgressModifierType.Bounce)
            };
        }


        StrikeSound = new(GenericParameters.StrikeSound);
        TerrainHitSound = new(GenericParameters.TerrainHitSound);
        DebugCollider = new(GenericParameters.Collider);
        EntityHitParticles = GenericParameters.EntityHitParticles == "" ? null : system.ParticleEffectsManager?.Get(GenericParameters.EntityHitParticles);
        TerrainHitParticles = GenericParameters.TerrainHitParticles == "" ? null : system.ParticleEffectsManager?.Get(GenericParameters.TerrainHitParticles);
    }

    public override void OnDeselected(EntityPlayer player)
    {
        MeleeSystem?.Stop(rightHand: true);
        Api?.World.UnregisterCallback(CooldownCallback);
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
    protected AssetLocation? StrikeSound { get; set; }
    protected AssetLocation? TerrainHitSound { get; set; }
    protected AdvancedParticleProperties? EntityHitParticles { get; set; }
    protected AdvancedParticleProperties? TerrainHitParticles { get; set; }

    protected long CooldownCallback = 0;
    protected const float EaseOutAnimationFactor = 2.0f;

    [ActionEventHandler(EnumEntityAction.LeftMouseDown, ActionState.Active)]
    protected virtual bool OnAttack(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (AltPressed() || !mainHand || state != MeleeWeaponState.Idle || Behavior?.GetState(mainHand: !mainHand) == MeleeWeaponState.Active) return false;

        MeleeSystem?.Start(Attack, result => OnAttackCallback(result, slot, direction, mainHand), direction);
        Behavior?.PlayAnimation(AttackAnimation, mainHand, true, null, AnimationParameters);
        state = MeleeWeaponState.Active;

        Api?.World.PlaySoundAt(StrikeSound, player);

        return true;
    }

    protected virtual void OnAttackCallback(AttackResult result, ItemSlot slot, AttackDirection direction, bool mainHand)
    {
        if (EntityHitParticles != null)
        {
            foreach ((_, Vector3 point, _) in result.Entities)
            {
                EntityHitParticles.basePos.X = point.X;
                EntityHitParticles.basePos.Y = point.Y;
                EntityHitParticles.basePos.Z = point.Z;
                
                Api?.World.SpawnParticles(EntityHitParticles);
            }
        }

        if (TerrainHitParticles != null)
        {
            foreach ((_, Vector3 point) in result.Terrain)
            {
                TerrainHitParticles.basePos.X = point.X;
                TerrainHitParticles.basePos.Y = point.Y;
                TerrainHitParticles.basePos.Z = point.Z;

                Api?.World.SpawnParticles(TerrainHitParticles);
            }
        }


        if ((result.Result & AttackResultFlag.HitTerrain) != 0)
        {
            MeleeSystem?.Stop();
            Behavior?.StopAnimation(mainHand);
            Behavior?.SetState(MeleeWeaponState.Cooldown);
            Api?.World.PlaySoundAt(TerrainHitSound, Api?.World.Player);
            CooldownCallback = Api?.World.RegisterCallback(dt => Behavior?.SetState(MeleeWeaponState.Idle), GenericParameters.AttackEaseOutMs + GenericParameters.AttackDurationMs) ?? 0;
        }

        if (result.Result == AttackResultFlag.Finished)
        {
            MeleeSystem?.Stop();
            Behavior?.SetState(MeleeWeaponState.Idle);
        }
    }
}
