using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace MeleeWeaponsFramework;

public class MeleeBlockPlayerBehavior : EntityBehavior
{
    public MeleeBlockPlayerBehavior(Entity entity) : base(entity)
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
        if (CurrentBlock == null)
        {
            return damage;
        }

        if (damageSource.SourceEntity == null)
        {
            return damage;
        }

        long currentTime = CoreApi.World.ElapsedMilliseconds;
        if (currentTime - BlockStartTime <= PerfectBlockTime)
        {
            OnPerfectBlock(damageSource, ref damage, DirectionIndex);
        }
        else
        {
            OnBlock(damageSource, ref damage, DirectionIndex);
        }

        return damage;
    }

    public void Start(MeleeBlock parameters, int directionIndex)
    {
        Stop();
        DirectionIndex = directionIndex;
        BlockStartTime = CoreApi.World.ElapsedMilliseconds;
        PerfectBlockTime = (long)parameters.PerfectBlockWindow.TotalMilliseconds;
        CurrentBlock = parameters;
    }
    public void Stop()
    {
        OnCancel();
        CurrentBlock = null;
    }

    protected readonly string Name;
    protected MeleeBlock? CurrentBlock;
    protected ICoreAPI CoreApi;
    protected long BlockStartTime;
    protected long PerfectBlockTime;
    protected const string StatCategory = "parry-player-behavior";
    protected int DirectionIndex = 0;

    protected virtual void OnPerfectBlock(DamageSource damageSource, ref float damage, int directionIndex) => CurrentBlock?.OnPerfectBlock(entity, damageSource, ref damage, directionIndex);
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
    public TimeSpan PerfectBlockWindow { get; set; }
    public DirectionConstrain Coverage { get; set; }
    public List<BlockDirections> Directions { get; set; } = new();
    public bool DirectionlessPerfectBlock { get; set; }
    public float DamageReduction { get; set; }

    public AssetLocation? BlockSound { get; set; }
    public AssetLocation? PerfectBlockSound { get; set; }
    public AssetLocation? CancelSound { get; set; }

    public void OnBlock(Entity entity, DamageSource damageSource, ref float damage, int directionIndex)
    {
        Entity? source = damageSource.SourceEntity ?? damageSource.CauseEntity;

        if (source != null && !CheckDirection(entity, source)) return;
        
        if (damageSource is IDirectionalDamage directionDamage && !Directions[directionIndex].Test(directionDamage.Direction))
        {
            return;
        }

        damage *= DamageReduction;

        if (BlockSound != null && entity.Api.Side == EnumAppSide.Server) entity.Api.World.PlaySoundAt(BlockSound, entity);
    }

    public void OnPerfectBlock(Entity entity, DamageSource damageSource, ref float damage, int directionIndex)
    {
        Entity? source = damageSource.SourceEntity ?? damageSource.CauseEntity;

        if (source != null && !CheckDirection(entity, source)) return;

        if (damageSource is IDirectionalDamage directionDamage && !DirectionlessPerfectBlock && !Directions[directionIndex].Test(directionDamage.Direction))
        {
            return;
        }

        damage = 0;

        if (PerfectBlockSound != null && entity.Api.Side == EnumAppSide.Server) entity.Api.World.PlaySoundAt(PerfectBlockSound, entity);
    }

    public void OnCancel(Entity entity)
    {
        if (CancelSound != null && entity.Api.Side == EnumAppSide.Server) entity.Api.World.PlaySoundAt(CancelSound, entity);
    }

    protected virtual bool CheckDirection(Entity receiver, Entity source)
    {
        Vec3f sourceEyesPosition = source.ServerPos.XYZFloat.Add(0, (float)source.LocalEyePos.Y, 0);
        Vec3f attackDirection = sourceEyesPosition - receiver.LocalEyePos.ToVec3f();
        Vec3f playerViewDirection = EntityPos.GetViewVector(receiver.SidedPos.Pitch, receiver.SidedPos.Yaw);
        Vec3f direction = DirectionOffset.ToReferenceFrame(playerViewDirection, attackDirection);
        DirectionOffset offset = new(direction, new Vec3f(0, 0, 1));

        return Coverage.Check(offset);
    }
}