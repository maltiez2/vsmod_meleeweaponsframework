using AnimationManagerLib;
using AnimationManagerLib.API;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace MeleeWeaponsFramework;

public class VanillaSpearParameters : GenericMeleeWeaponParameters
{
    public float ThrowDamage { get; set; } = 1f;
    public string ProjectileEntity { get; set; } = "";
    public long AimMinimumDurationMs { get; set; } = 350;
    public string AimAnimation { get; set; } = "";
    public string ThrowAnimation { get; set; } = "";
    public float AimAnimationFrame { get; set; } = 0;
    public float ThrowAnimationFrame { get; set; } = 0;
    public float ThrowAnimationDurationMs { get; set; } = 200;
}

public class VanillaSpear : GenericMeleeWeapon
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        SpearParameters = Attributes[MeleeWeaponStatsAttribute].AsObject<VanillaSpearParameters>();

        ThrowSystemClient = api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().ThrowSystemClient;
        ThrowSystemServer = api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().ThrowSystemServer;
        HackingSystemClient = api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().HackingSystemClient;

        ThrowId = new(0, Id);
        ThrowAttack throwAttack = new(SpearParameters.ThrowDamage, SpearParameters.ProjectileEntity);

        ThrowSystemClient?.Register(ThrowId, throwAttack);
        ThrowSystemServer?.Register(ThrowId, throwAttack);

        AimAnimation = new(SpearParameters.AimAnimation, api.ModLoader.GetModSystem<AnimationManagerLibSystem>());
        ThrowAnimation = new(SpearParameters.ThrowAnimation, api.ModLoader.GetModSystem<AnimationManagerLibSystem>());

        AimAnimationParameters = new RunParameters[]
        {
            RunParameters.EaseIn(SpearParameters.AimMinimumDurationMs / 1000.0f, SpearParameters.AimAnimationFrame, ProgressModifierType.Sqrt),
        };
        ThrowAnimationParameters = new RunParameters[]
        {
            RunParameters.EaseIn(SpearParameters.ThrowAnimationDurationMs / 1000.0f, SpearParameters.ThrowAnimationFrame, ProgressModifierType.Cubic),
            RunParameters.EaseOut(SpearParameters.ThrowAnimationDurationMs / 1000.0f, ProgressModifierType.CosShifted)
        };
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        return false;
    }

    public override void OnDeselected(EntityPlayer player)
    {
        base.OnDeselected(player);
        ThrowSystemClient?.Stop();
    }

    protected VanillaSpearParameters SpearParameters { get; set; } = new();
    protected ThrowSystemClient? ThrowSystemClient { get; set; }
    protected ThrowSystemServer? ThrowSystemServer { get; set; }
    protected HackingSystemClient? HackingSystemClient { get; set; }
    protected ThrowAttackId ThrowId { get; set; }
    protected long AimStartTime { get; set; } = 0;

    protected PlayerSimpleAnimationData AimAnimation { get; set; }
    protected RunParameters[] AimAnimationParameters { get; set; } = Array.Empty<RunParameters>();
    protected PlayerSimpleAnimationData ThrowAnimation { get; set; }
    protected RunParameters[] ThrowAnimationParameters { get; set; } = Array.Empty<RunParameters>();

    protected const float TyronMagicNumber_1 = 0.15f;

    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Pressed)]
    protected virtual bool OnAim(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (!mainHand || state != (int)GenericMeleeWeaponState.Idle) return true;

        ThrowSystemClient?.Aim();
        AimStartTime = Api?.World.ElapsedMilliseconds ?? 0;
        Behavior?.PlayAnimation(AimAnimation, mainHand, true, null, AimAnimationParameters);

        return true;
    }

    [ActionEventHandler(EnumEntityAction.RightMouseDown, ActionState.Released)]
    protected virtual bool OnThrow(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        if (!mainHand || state != (int)GenericMeleeWeaponState.Idle) return true;

        long currentTime = Api?.World.ElapsedMilliseconds ?? 0;
        if (currentTime - AimStartTime < SpearParameters.AimMinimumDurationMs)
        {
            ThrowSystemClient?.Stop();
            Behavior?.StopAnimation(mainHand);
        }
        else
        {
            Console.WriteLine("Throwing");
            Behavior?.PlayAnimation(ThrowAnimation, mainHand, true, null, ThrowAnimationParameters);
            Api?.World.RegisterCallback(dt => ThrowSystemClient?.Throw(ThrowId, mainHand), (int)SpearParameters.ThrowAnimationDurationMs);
        }

        return true;
    }

    protected override void OnAttackCallback(AttackResult result, ItemSlot slot, AttackDirection direction, bool mainHand)
    {
        if ((result.Result & AttackResultFlag.HitEntity) != 0) // Api?.World.Rand.NextDouble() < TyronMagicNumber_1 && 
        {
            foreach ((Entity entity, _) in result.Entities)
            {
                if (HackingSystemClient?.Hack(entity, mainHand) == true)
                {
                    MeleeSystem?.Stop();
                    Behavior?.SetState((int)GenericMeleeWeaponState.Idle);
                    return;
                }
            }
        }

        base.OnAttackCallback(result, slot, direction, mainHand);
    }
}
