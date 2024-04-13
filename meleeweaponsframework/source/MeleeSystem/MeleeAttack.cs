using System.Collections.Immutable;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace MeleeWeaponsFramework;

[Flags]
public enum AttackResultFlag
{
    None = 0,
    HitEntity = 1,
    HitTerrain = 2,
    Finished = 4
}

public readonly struct AttackResult
{
    public readonly AttackResultFlag Result;
    public readonly IEnumerable<(Block block, Vector3 point)> Terrain = Array.Empty<(Block block, Vector3 point)>();
    public readonly IEnumerable<(Entity entity, Vector3 point)> Entities = Array.Empty<(Entity entity, Vector3 point)>();

    public AttackResult(AttackResultFlag result = AttackResultFlag.None, IEnumerable<(Block block, Vector3 point)>? terrain = null, IEnumerable<(Entity entity, Vector3 point)>? entities = null)
    {
        Result = result;
        if (terrain != null) Terrain = terrain;
        if (entities != null) Entities = entities;
    }
}

public sealed class MeleeAttack
{
    public int Id { get; }
    public int ItemId { get; }
    public TimeSpan Duration { get; }
    public IEnumerable<MeleeAttackDamageType> DamageTypes { get; }
    public float MaxReach { get; }

    public bool StopOnTerrainHit { get; set; } = true;
    public bool StopOnEntityHit { get; set; } = false;
    public bool CollideWithTerrain { get; set; } = true;

    public MeleeAttack(int id, int itemId, ICoreClientAPI api, TimeSpan duration, IEnumerable<MeleeAttackDamageType> damageTypes, float maxReach, Dictionary<MeleeAttackDamageId, CollisionEffects> effects)
    {
        Id = id;
        ItemId = itemId;
        _api = api;
        Duration = duration;
        DamageTypes = damageTypes;
        MaxReach = maxReach;
        foreach (MeleeAttackDamageType damageType in damageTypes)
        {
            if (effects.TryGetValue(damageType.Id, out CollisionEffects? value))
            {
                _damageTypesEffects[damageType.Id] = value;
            }
            else
            {
                _damageTypesEffects[damageType.Id] = new();
            }
        }
    }

    public void Start(IPlayer player)
    {
        long entityId = player.Entity.EntityId;

        _currentTime[entityId] = 0;
        _totalTime[entityId] = (float)Duration.TotalMilliseconds; // @TODO: Make some stats to affect this
        if (_totalTime[entityId] <= 0) _totalTime[entityId] = 1;

        if (_attackedEntities.TryGetValue(entityId, out HashSet<long>? value))
        {
            value.Clear();
        }
        else
        {
            _attackedEntities[entityId] = new();
        }

    }
    public AttackResult Step(IPlayer player, float dt, ItemSlot slot, out IEnumerable<MeleeAttackDamagePacket> damagePackets, bool rightHand = true)
    {
        AttackResultFlag result = AttackResultFlag.None;

        damagePackets = Array.Empty<MeleeAttackDamagePacket>();

        _currentTime[player.Entity.EntityId] += dt * 1000;
        float progress = GameMath.Clamp(_currentTime[player.Entity.EntityId] / _totalTime[player.Entity.EntityId], 0, 1);
        if (progress >= 1)
        {
            return new(AttackResultFlag.Finished);
        }

        bool success = LineSegmentCollider.Transform(DamageTypes.Select(element => element as IHasLineCollider), player.Entity, slot, _api, rightHand);
        if (!success) return new(AttackResultFlag.None);

        if (CollideWithTerrain)
        {
            IEnumerable<(Block block, Vector3 point)> terrainCollisions = CheckTerrainCollision(progress);
            if (terrainCollisions.Any()) result |= AttackResultFlag.HitTerrain;

            if (StopOnTerrainHit && terrainCollisions.Any()) return new(result, terrain: terrainCollisions);
        }

        _damagePackets.Clear();

        IEnumerable<(Entity entity, Vector3 point)> entitiesCollisions = CollideWithEntities(progress, player, _damagePackets, slot);

        if (entitiesCollisions.Any())
        {
            result |= AttackResultFlag.HitEntity;
            damagePackets = _damagePackets;
            return new(result, entities: entitiesCollisions);
        }

        return new(result);
    }
    public void RenderDebugColliders(IPlayer player, ItemSlot slot, bool rightHand = true)
    {
        LineSegmentCollider.Transform(DamageTypes.Select(element => element as IHasLineCollider), player.Entity, slot, _api, rightHand);
        foreach (LineSegmentCollider collider in DamageTypes.Select(item => item.InWorldCollider))
        {
            collider.Render(_api, player.Entity);
        }
    }

    private readonly ICoreClientAPI _api;
    private readonly Dictionary<long, float> _currentTime = new();
    private readonly Dictionary<long, float> _totalTime = new();
    private readonly Dictionary<long, HashSet<long>> _attackedEntities = new();
    private readonly List<MeleeAttackDamagePacket> _damagePackets = new();
    private readonly HashSet<(Block block, Vector3 point)> _terrainCollisionsBuffer = new();
    private readonly HashSet<(Entity entity, Vector3 point)> _entitiesCollisionsBuffer = new();
    private readonly Dictionary<MeleeAttackDamageId, CollisionEffects> _damageTypesEffects = new();

    private IEnumerable<(Block block, Vector3 point)> CheckTerrainCollision(float progress)
    {
        _terrainCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes.Where(item => item.HitWindow.X >= progress && item.HitWindow.Y <= progress))
        {
            (Block block, Vector3 position)? result = damageType.InWorldCollider.IntersectTerrain(_api);

            if (result != null)
            {
                _terrainCollisionsBuffer.Add(result.Value);
                Vector3 direction = damageType.InWorldCollider.Direction / damageType.InWorldCollider.Direction.Length() * -1;
                _damageTypesEffects[damageType.Id].OnTerrainCollision(result.Value.block, result.Value.position, direction, _api);
            }
        }

        return _terrainCollisionsBuffer.ToImmutableHashSet();
    }
    private IEnumerable<(Entity entity, Vector3 point)> CollideWithEntities(float progress, IPlayer player, List<MeleeAttackDamagePacket> packets, ItemSlot slot)
    {
        long entityId = player.Entity.EntityId;

        Entity[] entities = _api.World.GetEntitiesAround(player.Entity.Pos.XYZ, MaxReach, MaxReach);

        _entitiesCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes.Where(item => item.HitWindow.X >= progress && item.HitWindow.Y <= progress))
        {
            foreach ((Entity entity, Vector3? point) in entities
                    .Where(entity => entity != player.Entity)
                    .Where(entity => !_attackedEntities[entityId].Contains(entity.EntityId))
                    .Select(entity => (entity, damageType.TryAttack(player, entity))))
            {
                if (point == null) continue;
                packets.Add(new MeleeAttackDamagePacket() { Id = damageType.Id, Position = point.Value, AttackerEntityId = player.Entity.EntityId, TargetEntityId = entity.EntityId });
                _damageTypesEffects[damageType.Id].OnEntityCollision(entity, damageType.InWorldCollider.Position, damageType.InWorldCollider.Direction, _api);
                _entitiesCollisionsBuffer.Add((entity, point.Value));
                if (damageType.DurabilityDamage > 0)
                {
                    slot.Itemstack.Collectible.DamageItem(_api.World, player.Entity, slot, damageType.DurabilityDamage);
                }
                if (StopOnEntityHit) break;
            }
        }

        IEnumerable<(Entity entity, Vector3 point)> result = _entitiesCollisionsBuffer.ToImmutableHashSet();
        _entitiesCollisionsBuffer.Clear();
        return result;
    }

}


public sealed class CollisionEffects
{
    public Dictionary<string, AssetLocation> EntityCollisionSounds { get; set; } = new();
    public Dictionary<string, AssetLocation> TerrainCollisionSounds { get; set; } = new();
    public Dictionary<string, (AdvancedParticleProperties effect, float directionFactor)> TerrainCollisionParticles { get; set; } = new();
    public Dictionary<string, (AdvancedParticleProperties effect, float directionFactor)> EntityCollisionParticles { get; set; } = new();

    public void OnTerrainCollision(Block block, Vector3 position, Vector3 direction, ICoreAPI api)
    {
        foreach (AssetLocation sound in TerrainCollisionSounds.Where(entry => WildcardUtil.Match(entry.Key, block.Code.ToString())).Select(entry => entry.Value))
        {
            api.World.PlaySoundAt(sound, position.X, position.Y, position.Z, randomizePitch: true);
            break;
        }

        foreach ((AdvancedParticleProperties effect, float directionFactor) in TerrainCollisionParticles.Where(entry => WildcardUtil.Match(entry.Key, block.Code.ToString())).Select(entry => entry.Value))
        {
            effect.basePos.X = position.X;
            effect.basePos.Y = position.Y;
            effect.basePos.Z = position.Z;
            effect.baseVelocity.X = direction.X * directionFactor;
            effect.baseVelocity.Y = direction.Y * directionFactor;
            effect.baseVelocity.Z = direction.Z * directionFactor;
            api.World.SpawnParticles(effect);
        }
    }
    public void OnEntityCollision(Entity entity, Vector3 position, Vector3 direction, ICoreAPI api)
    {
        foreach (AssetLocation sound in EntityCollisionSounds.Where(entry => WildcardUtil.Match(entry.Key, entity.Code.ToString())).Select(entry => entry.Value))
        {
            api.World.PlaySoundAt(sound, entity, randomizePitch: true);
            break;
        }

        foreach ((AdvancedParticleProperties effect, float directionFactor) in EntityCollisionParticles.Where(entry => WildcardUtil.Match(entry.Key, entity.Code.ToString())).Select(entry => entry.Value))
        {
            effect.basePos.X = position.X;
            effect.basePos.Y = position.Y;
            effect.basePos.Z = position.Z;
            effect.baseVelocity.X = direction.X * directionFactor;
            effect.baseVelocity.Y = direction.Y * directionFactor;
            effect.baseVelocity.Z = direction.Z * directionFactor;
            api.World.SpawnParticles(effect);
        }
    }
}