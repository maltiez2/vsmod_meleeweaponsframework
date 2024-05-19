using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace MeleeWeaponsFramework;

public class MeleeBlockBehavior : EntityBehavior
{
    public MeleeBlockBehavior(Entity entity) : base(entity)
    {
        CoreApi = entity.Api;
        Name = "FSMlibBlockAgainstDamage";
    }
    public override void AfterInitialized(bool onFirstSpawn)
    {
        EntityBehaviorHealth? behavior = entity.GetBehavior<EntityBehaviorHealth>();

        if (behavior == null)
        {
            return;
        }

        behavior.onDamaged += OnEntityReceiveDamage;
    }

    public override string PropertyName() => StatCategory;
    public float OnEntityReceiveDamage(float damage, DamageSource damageSource)
    {
        if (damageSource is ILocationalDamage) return damage;
        if (CurrentBlock == null) return damage;
        if (damageSource.SourceEntity == null) return damage;

        long currentTime = CoreApi.World.ElapsedMilliseconds;
        if (currentTime - BlockStartTime <= PerfectBlockTime)
        {
            OnParry(damageSource, ref damage, DirectionIndex);
        }
        else
        {
            OnBlock(damageSource, ref damage, DirectionIndex);
        }

        return damage;
    }

    public void Start(MeleeBlock parameters, int directionIndex, int itemId, bool rightHand)
    {
        Stop();
        DirectionIndex = directionIndex;
        BlockStartTime = CoreApi.World.ElapsedMilliseconds;
        PerfectBlockTime = (long)parameters.ParryWindow.TotalMilliseconds;
        CurrentBlock = parameters;
        ItemId = itemId;
        RightHand = rightHand;
    }
    public void Stop()
    {
        OnCancel();
        CurrentBlock = null;
    }

    public IEnumerable<IParryCollider> GetColliders()
    {
        IParryCollider[] empty = Array.Empty<IParryCollider>();

        if (Entity == null || CoreApi is not ICoreClientAPI clientApi) return empty;

        if (RightHand)
        {
            if (Entity.RightHandItemSlot.Itemstack?.Item.Id != ItemId) return empty;
            if (Entity.RightHandItemSlot.Itemstack?.Item is not IHasParryCollider rightHand) return empty;

            return rightHand.RelativeColliders.Select(collider => collider.Transform(Entity, Entity.RightHandItemSlot, clientApi)).OfType<IParryCollider>();
        }
        else
        {
            if (Entity.LeftHandItemSlot.Itemstack?.Item.Id != ItemId) return empty;
            if (Entity.LeftHandItemSlot.Itemstack?.Item is not IHasParryCollider leftHand) return empty;

            return leftHand.RelativeColliders.Select(collider => collider.Transform(Entity, Entity.LeftHandItemSlot, clientApi)).OfType<IParryCollider>();
        }
    }

    public bool IsBlocking() => CurrentBlock != null;
    public bool IsParrying() => CurrentBlock != null && CoreApi.World.ElapsedMilliseconds - BlockStartTime <= PerfectBlockTime;

    protected readonly string Name;
    protected MeleeBlock? CurrentBlock;
    protected ICoreAPI CoreApi;
    protected long BlockStartTime;
    protected long PerfectBlockTime;
    protected const string StatCategory = "parry-player-behavior";
    protected int DirectionIndex = 0;
    protected int ItemId = 0;
    protected bool RightHand = true;
    protected EntityAgent? Entity => entity as EntityAgent;

    protected virtual void OnParry(DamageSource damageSource, ref float damage, int directionIndex) => CurrentBlock?.OnParry(entity, damageSource, ref damage, directionIndex);
    protected virtual void OnBlock(DamageSource damageSource, ref float damage, int directionIndex) => CurrentBlock?.OnBlock(entity, damageSource, ref damage, directionIndex);
    protected virtual void OnCancel() => CurrentBlock?.OnCancel(entity);
}

public delegate void BlockDelegate(Entity entity, DamageSource damageSource, ref float damage);

public readonly struct BlockDirections
{
    public readonly bool Top = true;
    public readonly bool TopRight = true;
    public readonly bool Right = true;
    public readonly bool BottomRight = true;
    public readonly bool Bottom = true;
    public readonly bool BottomLeft = true;
    public readonly bool Left = true;
    public readonly bool TopLeft = true;

    public BlockDirections(params AttackDirection[] directions)
    {
        Top = directions.Contains(AttackDirection.Top);
        TopRight = directions.Contains(AttackDirection.TopRight);
        Right = directions.Contains(AttackDirection.Right);
        BottomRight = directions.Contains(AttackDirection.BottomRight);
        Bottom = directions.Contains(AttackDirection.Bottom);
        BottomLeft = directions.Contains(AttackDirection.BottomLeft);
        Left = directions.Contains(AttackDirection.Left);
        TopLeft = directions.Contains(AttackDirection.TopLeft);
    }

    public BlockDirections()
    {
    }

    public bool Test(AttackDirection direction)
    {
        return direction switch
        {
            AttackDirection.Top => Top,
            AttackDirection.TopRight => TopRight,
            AttackDirection.Right => Right,
            AttackDirection.BottomRight => BottomRight,
            AttackDirection.Bottom => Bottom,
            AttackDirection.BottomLeft => BottomLeft,
            AttackDirection.Left => Left,
            AttackDirection.TopLeft => TopLeft,
            _ => false
        };
    }
}

public class MeleeBlock
{
    public TimeSpan ParryWindow { get; set; }
    public DirectionConstrain Coverage { get; set; }
    public List<BlockDirections> Directions { get; set; } = new();
    public bool DirectionlessParry { get; set; }
    public float DamageReduction { get; set; }

    public AssetLocation? BlockSound { get; set; }
    public AssetLocation? ParrySound { get; set; }
    public AssetLocation? CancelSound { get; set; }

    public void OnBlock(Entity entity, DamageSource damageSource, ref float damage, int directionIndex)
    {
        Entity? source = damageSource.SourceEntity ?? damageSource.CauseEntity;

        if (source != null && !CheckCoverage(entity, source)) return;

        string attackerId = source?.Code.ToString() ?? "None";
        if (source is EntityPlayer player)
        {
            attackerId = player.GetName();
        }
        string targetId = entity.Code.ToString();
        if (entity is EntityPlayer playerTarget)
        {
            targetId = playerTarget.GetName();
        }

        if (damageSource is IDirectionalDamage directionDamage && !Directions[directionIndex].Test(directionDamage.Direction))
        {
            if (entity.Api.Side == EnumAppSide.Server) LoggerUtil.Verbose(entity.Api, this, $"Entity '{targetId}', attacked by '{attackerId}', failed direction check on blocking. Block direction: {Directions[directionIndex]}, attack direction: {directionDamage.Direction}.");
            return;
        }

        damage *= (1 - DamageReduction);

        if (entity.Api.Side == EnumAppSide.Server) LoggerUtil.Verbose(entity.Api, this, $"Entity '{targetId}', attacked by '{attackerId}', successfully blocked {DamageReduction * 100}% of incoming damage reducing it to: {damage}.");

        if (BlockSound != null) entity.Api.World.PlaySoundAt(BlockSound, entity);
    }

    public void OnParry(Entity entity, DamageSource damageSource, ref float damage, int directionIndex)
    {
        Entity? source = damageSource.SourceEntity ?? damageSource.CauseEntity;

        if (source != null && !CheckCoverage(entity, source)) return;

        string attackerId = source?.Code.ToString() ?? "None";
        if (source is EntityPlayer player)
        {
            attackerId = player.GetName();
        }
        string targetId = entity.Code.ToString();
        if (entity is EntityPlayer playerTarget)
        {
            targetId = playerTarget.GetName();
        }

        if (damageSource is IDirectionalDamage directionDamage && !DirectionlessParry && !Directions[directionIndex].Test(directionDamage.Direction))
        {
            if (entity.Api.Side == EnumAppSide.Server) LoggerUtil.Verbose(entity.Api, this, $"Entity '{targetId}', attacked by '{attackerId}', failed direction check on perfect blocking. Block direction: {Directions[directionIndex]}, attack direction: {directionDamage.Direction}.");
            return;
        }

        damage = 0;

        if (entity.Api.Side == EnumAppSide.Server) LoggerUtil.Verbose(entity.Api, this, $"Entity '{targetId}', attacked by '{attackerId}', successfully performed perfect block.");

        if (ParrySound != null) entity.Api.World.PlaySoundAt(ParrySound, entity);
    }

    public void OnCancel(Entity entity)
    {
        if (CancelSound != null && entity.Api.Side == EnumAppSide.Server) entity.Api.World.PlaySoundAt(CancelSound, entity);
    }

    protected virtual bool CheckCoverage(Entity receiver, Entity source)
    {
        Vec3f sourceEyesPosition = source.ServerPos.XYZFloat.Add(0, (float)source.LocalEyePos.Y, 0);
        Vec3f attackDirection = sourceEyesPosition - receiver.LocalEyePos.ToVec3f();
        Vec3f playerViewDirection = EntityPos.GetViewVector(receiver.SidedPos.Pitch, receiver.SidedPos.Yaw);
        Vec3f direction = DirectionOffset.ToReferenceFrame(playerViewDirection, attackDirection);
        DirectionOffset offset = new(direction, new Vec3f(0, 0, 1));

        string attackerId = source.Code.ToString();
        if (source is EntityPlayer player)
        {
            attackerId = player.GetName();
        }
        string targetId = receiver.Code.ToString();
        if (receiver is EntityPlayer playerTarget)
        {
            targetId = playerTarget.GetName();
        }

        bool result = Coverage.Check(offset);

        if (!result && receiver.Api.Side == EnumAppSide.Server)
        {
            LoggerUtil.Verbose(receiver.Api, this, $"Entity '{targetId}', attacked by '{attackerId}', failed coverage check. Pitch: {offset.Pitch}, yaw: {offset.Yaw}.");
        }

        return result;
    }
}