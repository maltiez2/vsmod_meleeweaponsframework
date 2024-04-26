using AnimationManagerLib;
using AnimationManagerLib.API;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

    protected VanillaSpearParameters SpearParameters { get; set; } = new();
    protected ThrowSystemClient? ThrowSystemClient { get; set; }
    protected ThrowSystemServer? ThrowSystemServer { get; set; }
    protected ThrowAttackId ThrowId { get; set; }
    protected long AimStartTime { get; set; } = 0;

    protected PlayerSimpleAnimationData AimAnimation { get; set; }
    protected RunParameters[] AimAnimationParameters { get; set; } = Array.Empty<RunParameters>();
    protected PlayerSimpleAnimationData ThrowAnimation { get; set; }
    protected RunParameters[] ThrowAnimationParameters { get; set; } = Array.Empty<RunParameters>();

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
            Behavior?.PlayAnimation(ReadyAnimation, mainHand);
        }
        else
        {
            Behavior?.PlayAnimation(ThrowAnimation, mainHand, true, null, ThrowAnimationParameters);
            Api?.World.RegisterCallback(dt => ThrowSystemClient?.Throw(ThrowId, mainHand), (int)SpearParameters.ThrowAnimationDurationMs);
        }

        return true;
    }

    protected override void OnAttackCallback(AttackResult result, ItemSlot slot, AttackDirection direction)
    {
        /*if ((result.Result & AttackResultFlag.HitEntity) != 0)
        {
            foreach ((Entity entity, _) in result.Entities)
            {
                if (HackEntity(entity, slot))
                {
                    MeleeSystem?.Stop();
                    Behavior?.SetState((int)GenericMeleeWeaponState.Idle);
                    return;
                }
            }
        }*/

        base.OnAttackCallback(result, slot, direction);
    }

    /*protected bool HackEntity(Entity entity, ItemSlot slot)
    {
        JsonObject attributes = entity.Properties.Attributes;
        bool canHack = attributes != null && attributes["hackedEntity"].Exists && slot.Itemstack.ItemAttributes.IsTrue("hacking") && api.ModLoader.GetModSystem<CharacterSystem>().HasTrait(Api?.World.Player, "technical");

        if (!canHack) return false;

        ICoreServerAPI coreServerAPI = api as ICoreServerAPI;
        api.World.PlaySoundAt(new AssetLocation("sounds/player/hackingspearhit.ogg"), entity);

        if (api.World.Rand.NextDouble() < 0.15)
        {
            SpawnEntityInPlaceOf(entity, entity.Properties.Attributes["hackedEntity"].AsString(), Api?.World.Player.Entity);
            coreServerAPI.World.DespawnEntity(entity, new EntityDespawnData
            {
                Reason = EnumDespawnReason.Removed
            });
        }

        return true;
    }

    protected void SpawnEntityInPlaceOf(Entity byEntity, string code, EntityAgent? causingEntity)
    {
        AssetLocation assetLocation = AssetLocation.Create(code, byEntity.Code.Domain);
        EntityProperties entityType = byEntity.World.GetEntityType(assetLocation);
        if (entityType == null)
        {
            byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", assetLocation);

            return;
        }

        Entity entity = byEntity.World.ClassRegistry.CreateEntity(entityType);
        if (entity != null)
        {
            entity.ServerPos.X = byEntity.ServerPos.X;
            entity.ServerPos.Y = byEntity.ServerPos.Y;
            entity.ServerPos.Z = byEntity.ServerPos.Z;
            entity.ServerPos.Motion.X = byEntity.ServerPos.Motion.X;
            entity.ServerPos.Motion.Y = byEntity.ServerPos.Motion.Y;
            entity.ServerPos.Motion.Z = byEntity.ServerPos.Motion.Z;
            entity.ServerPos.Yaw = byEntity.ServerPos.Yaw;
            entity.Pos.SetFrom(entity.ServerPos);
            entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            entity.Attributes.SetString("origin", "playerplaced");
            entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
            if (causingEntity is EntityPlayer entityPlayer)
            {
                entity.WatchedAttributes.SetString("guardedPlayerUid", entityPlayer.PlayerUID);
            }

            byEntity.World.SpawnEntity(entity);
        }
    }*/
}
