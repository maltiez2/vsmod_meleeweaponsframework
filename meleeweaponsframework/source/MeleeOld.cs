﻿using ProtoBuf;
using System.Collections.Immutable;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace MeleeWeaponsFramework.Old;

public sealed class MeleeSystem
{
    public MeleeSystem(ICoreAPI api, string debugName = "")
    {
        _clientApi = api as ICoreClientAPI;
        _serverApi = api as ICoreServerAPI;
    }

    public void StartClientSide(long id, IPlayer player, MeleeAttack attack, ItemSlot slot, System.Func<MeleeAttack.AttackResult, bool> callback, bool rightHand = true)
    {
        if (_clientApi == null) throw new InvalidOperationException();

        Stop(id, player);
        attack.Start(player);
        long timer = _clientApi.World.RegisterGameTickListener(dt => Step(dt, attack, player, slot, rightHand, callback, id), 0);
        _timers[id] = timer;
    }
    public void StartServerSide(long id, IPlayer player, MeleeAttack attack, System.Func<MeleeCollisionPacket, bool> callback)
    {
        if (_serverApi == null) throw new InvalidOperationException();

        MeleeSynchronizer._attacks[(player.Entity.EntityId, id)] = (packet =>
        {
            if (callback.Invoke(packet) || packet.Finished)
            {
                Stop(id, player);
            }
        }, attack.MaxReach);
    }
    public void Stop(long id, IPlayer player)
    {
        if (_clientApi != null && _timers.ContainsKey(id))
        {
            _clientApi.World.UnregisterGameTickListener(_timers[id]);
            _timers.Remove(id);
        }

        if (_serverApi != null)
        {
            (long EntityId, long id) fullId = (player.Entity.EntityId, id);
            MeleeSynchronizer._attacks.Remove(fullId);
        }
    }


    private readonly ICoreClientAPI? _clientApi;
    private readonly ICoreServerAPI? _serverApi;
    private readonly Dictionary<long, long> _timers = new();

    private void Step(float dt, MeleeAttack attack, IPlayer player, ItemSlot slot, bool rightHand, System.Func<MeleeAttack.AttackResult, bool> callback, long id)
    {
        MeleeAttack.AttackResult result = attack.Step(player, dt, slot, packet => SynchronizeDamage(packet, player, id), rightHand);

        if (result.Result == MeleeAttack.Result.None) return;

        SynchronizeCollisions(result, id);

        if (callback.Invoke(result) || result.Result == MeleeAttack.Result.Finished)
        {
            Stop(id, player);
        }
    }
    private static void SynchronizeDamage(List<MeleeAttackDamagePacket> packets, IPlayer player, long id)
    {
        MeleeAttackPacket packet = new()
        {
            AttackId = id,
            PlayerId = player.Entity.EntityId,
            Damages = packets.ToArray()
        };

        MeleeSynchronizer.Send(packet);
    }
    private static void SynchronizeCollisions(MeleeAttack.AttackResult result, long id)
    {
        MeleeCollisionPacket packet = new()
        {
            Id = id,
            Finished = result.Result == MeleeAttack.Result.Finished,
            Blocks = result.Terrain?.Select(entry => entry.block.Code.ToString()).ToArray() ?? Array.Empty<string>(),
            Entities = result.Entities?.Select(entry => entry.entity.EntityId).ToArray() ?? Array.Empty<long>()
        };
        MeleeSynchronizer.Send(packet);
    }
}

public sealed class MeleeAttack
{
    public enum Result
    {
        None,
        HitEntity,
        HitTerrain,
        Finished
    }

    public readonly struct AttackResult
    {
        public readonly Result Result;
        public readonly IEnumerable<(Block block, Vector3 point)>? Terrain;
        public readonly IEnumerable<(Entity entity, Vector3 point)>? Entities;

        public AttackResult(Result result = Result.None, IEnumerable<(Block block, Vector3 point)>? terrain = null, IEnumerable<(Entity entity, Vector3 point)>? entities = null)
        {
            Result = result;
            Terrain = terrain;
            Entities = entities;
        }
    }

    public TimeSpan Duration { get; }
    public IEnumerable<MeleeAttackDamageType> DamageTypes { get; }
    public StatsModifier? DurationModifier { get; }
    public float MaxReach { get; }

    public MeleeAttack(ICoreClientAPI api, TimeSpan duration, IEnumerable<MeleeAttackDamageType> damageTypes, float maxReach, StatsModifier? durationModifier = null)
    {
        _api = api;
        Duration = duration;
        DamageTypes = damageTypes;
        DurationModifier = durationModifier;
        MaxReach = maxReach;
    }

    public void Start(IPlayer player)
    {
        long entityId = player.Entity.EntityId;

        _currentTime[entityId] = 0;
        _totalTime[entityId] = DurationModifier?.Calc(player, (float)Duration.TotalMilliseconds) ?? (float)Duration.TotalMilliseconds;
        if (_totalTime[entityId] <= 0) _totalTime[entityId] = 1;

        if (_attackedEntities.ContainsKey(entityId))
        {
            _attackedEntities[entityId].Clear();
        }
        else
        {
            _attackedEntities[entityId] = new();
        }

    }
    public AttackResult Step(IPlayer player, float dt, ItemSlot slot, Action<List<MeleeAttackDamagePacket>> networkCallback, bool rightHand = true)
    {
        _currentTime[player.Entity.EntityId] += dt * 1000;
        float progress = GameMath.Clamp(_currentTime[player.Entity.EntityId] / _totalTime[player.Entity.EntityId], 0, 1);
        if (progress >= 1)
        {
            return new(Result.Finished);
        }

        bool success = LineSegmentCollider.Transform(DamageTypes, player.Entity, slot, _api, rightHand);
        if (!success) return new(Result.None);

        IEnumerable<(Block block, Vector3 point)> terrainCollisions = CheckTerrainCollision(progress);

        if (terrainCollisions.Any()) return new(Result.HitTerrain, terrain: terrainCollisions);

        _damagePackets.Clear();

        IEnumerable<(Entity entity, Vector3 point)> entitiesCollisions = CollideWithEntities(progress, player, _damagePackets);

        if (entitiesCollisions.Any())
        {
            networkCallback.Invoke(_damagePackets);
            return new(Result.HitEntity, entities: entitiesCollisions);
        }

        return new(Result.None);
    }
    public void RenderDebugColliders(IPlayer player, ItemSlot slot, bool rightHand = true)
    {
        LineSegmentCollider.Transform(DamageTypes, player.Entity, slot, _api, rightHand);
        foreach (LineSegmentCollider collider in DamageTypes.Select(item => item.InWorldCollider))
        {
            collider.RenderAsLine(_api, player.Entity);
        }
    }

    private readonly ICoreClientAPI _api;
    private readonly Dictionary<long, float> _currentTime = new();
    private readonly Dictionary<long, float> _totalTime = new();
    private readonly Dictionary<long, HashSet<long>> _attackedEntities = new();
    private readonly List<MeleeAttackDamagePacket> _damagePackets = new();
    private readonly HashSet<(Block block, Vector3 point)> _terrainCollisionsBuffer = new();
    private readonly HashSet<(Entity entity, Vector3 point)> _entitiesCollisionsBuffer = new();

    private IEnumerable<(Block block, Vector3 point)> CheckTerrainCollision(float progress)
    {
        _terrainCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes.Where(item => item.Window.Check(progress)))
        {
            (Block block, Vector3 position)? result = damageType._inWorldCollider.IntersectTerrain(_api);

            if (result != null)
            {
                _terrainCollisionsBuffer.Add(result.Value);
                Vector3 direction = damageType._inWorldCollider.Direction / damageType._inWorldCollider.Direction.Length() * -1;
                damageType.Effects.OnTerrainCollision(result.Value.block, result.Value.position, direction, _api);
            }
        }

        return _terrainCollisionsBuffer.ToImmutableHashSet();
    }
    private IEnumerable<(Entity entity, Vector3 point)> CollideWithEntities(float progress, IPlayer player, List<MeleeAttackDamagePacket> packets)
    {
        long entityId = player.Entity.EntityId;

        Entity[] entities = _api.World.GetEntitiesAround(player.Entity.Pos.XYZ, MaxReach, MaxReach);

        _entitiesCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes.Where(item => item.Window.Check(progress)))
        {
            foreach (
                (Entity entity, Vector3 point) entry in entities
                    .Where(entity => entity != player.Entity)
                    .Where(entity => !_attackedEntities[entityId].Contains(entity.EntityId))
                    .Where(entity => damageType.InWorldCollider.RoughIntersect(GetCollisionBox(entity)))
                    .Select(entity => (entity, CollideWithEntity(player, damageType, entity, packets)))
                    .Where(entry => entry.Item2 != null)
                    .Select(entry => (entry.entity, entry.Item2.Value))
                )
            {
                _entitiesCollisionsBuffer.Add(entry);
            }
        }

        return _entitiesCollisionsBuffer.ToImmutableHashSet();
    }
    private static Cuboidf GetCollisionBox(Entity entity)
    {
        Cuboidf collisionBox = entity.CollisionBox.Clone();
        EntityPos position = entity.Pos;
        collisionBox.X1 += (float)position.X;
        collisionBox.Y1 += (float)position.Y;
        collisionBox.Z1 += (float)position.Z;
        collisionBox.X2 += (float)position.X;
        collisionBox.Y2 += (float)position.Y;
        collisionBox.Z2 += (float)position.Z;
        return collisionBox;
    }
    private Vector3? CollideWithEntity(IPlayer player, MeleeAttackDamageType damageType, Entity entity, List<MeleeAttackDamagePacket> packets)
    {
        //System.Numerics.Vector3? result = damageType._inWorldCollider.IntersectCylinder(new(GetCollisionBox(entity)));
        System.Numerics.Vector3? result = damageType._inWorldCollider.IntersectCuboid(GetCollisionBox(entity));

        if (result == null) return null;

        bool attacked = damageType.Attack(player, entity, out MeleeAttackDamagePacket? packet);

        if (packet != null) packets.Add(packet);

        if (attacked)
        {
            _attackedEntities[player.Entity.EntityId].Add(entity.EntityId);
            Vector3 direction = damageType._inWorldCollider.Direction / damageType._inWorldCollider.Direction.Length() * -1;
            damageType.Effects.OnEntityCollision(entity, result.Value, direction, _api);
        }

        return attacked ? result : null;
    }
}

public readonly struct HitWindow
{
    public readonly float Start;
    public readonly float End;
    public HitWindow(float start, float end)
    {
        Start = start;
        End = end;
    }

    public readonly bool Check(float progress) => progress >= Start && progress <= End;
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

public sealed class MeleeAttackDamageType
{
    public LineSegmentCollider Collider { get; }
    public LineSegmentCollider InWorldCollider => _inWorldCollider;
    public HitWindow Window { get; }
    public CollisionEffects Effects { get; set; }

    public MeleeAttackDamageType(
        float damage,
        EnumDamageType damageType,
        LineSegmentCollider collider,
        HitWindow hitWindow,
        int tier = 0,
        float knockback = 0,
    float stagger = 1.0f,
        StatsModifier? damageModifier = null,
        StatsModifier? knockbackModifier = null,
        CollisionEffects? effects = null)
    {
        _damage = damage;
        _damageType = damageType;
        _tier = tier;
        _knockback = knockback;
        _stagger = stagger;
        _damageModifier = damageModifier;
        _knockbackModifier = knockbackModifier;
        Collider = collider;
        Window = hitWindow;
        _inWorldCollider = collider;
        Effects = effects ?? new();
    }

    public bool Attack(IPlayer attacker, Entity target, out MeleeAttackDamagePacket? packet)
    {
        float damage = GetDamage(attacker);
        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker.Entity,
            Type = _damageType,
            DamageTier = _tier
        }, damage);

        packet = null;

        bool received = damageReceived || damage <= 0;

        if (received)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * GetKnockback(attacker) * _knockbackFactor * (1.0f - target.Properties.KnockbackResistance);
            target.SidedPos.Motion.X *= _stagger;
            target.SidedPos.Motion.Z *= _stagger;
            target.SidedPos.Motion.Add(knockback);

            packet = new()
            {
                Target = target.EntityId,
                Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
                CauseEntity = attacker.Entity.EntityId,
                DamageType = _damageType,
                DamageTier = _tier,
                Damage = damage,
                Knockback = new float[] { knockback.X, knockback.Y, knockback.Z },
                Stagger = _stagger
            };
        }

        return received;
    }

    internal LineSegmentCollider _inWorldCollider;

    private readonly float _damage;
    private readonly float _knockback;
    private readonly float _stagger;
    private readonly int _tier;
    private readonly EnumDamageType _damageType;
    private readonly StatsModifier? _damageModifier;
    private readonly StatsModifier? _knockbackModifier;
    private const float _knockbackFactor = 0.1f;

    private float GetDamage(IPlayer attacker)
    {
        if (_damageModifier == null) return _damage;
        return _damageModifier.Calc(attacker, _damage);
    }
    private float GetKnockback(IPlayer attacker)
    {
        if (_knockbackModifier == null) return _knockback;
        return _knockbackModifier.Calc(attacker, _knockback);
    }
}

internal static class MeleeSynchronizer
{
    public const string NetworkChannelId = "fsmlib:melee-damage-sync";

    public static void Init(ICoreClientAPI api)
    {
        _clientChannel = api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeAttackPacket>()
            .RegisterMessageType<MeleeCollisionPacket>();
    }
    public static void Init(ICoreServerAPI api)
    {
        _api = api;
        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeAttackPacket>()
            .RegisterMessageType<MeleeCollisionPacket>()
            .SetMessageHandler<MeleeAttackPacket>(HandlePacket)
            .SetMessageHandler<MeleeCollisionPacket>(HandlePacket);
    }
    public static void Send(MeleeAttackPacket packet)
    {
        _clientChannel?.SendPacket(packet);
    }
    public static void Send(MeleeCollisionPacket packet)
    {
        _clientChannel?.SendPacket(packet);
    }

    internal static IClientNetworkChannel? _clientChannel;
    internal static ICoreServerAPI? _api;
    internal static readonly Dictionary<(long playerId, long attackId), (Action<MeleeCollisionPacket> attack, float range)> _attacks = new();
    private const float _rangeFactor = 2.0f;

    private static void HandlePacket(IServerPlayer player, MeleeCollisionPacket packet)
    {
        (long, long) id = (player.Entity.EntityId, packet.Id);
        if (_attacks.ContainsKey(id))
        {
            _attacks[id].attack?.Invoke(packet);
        }
    }
    private static void HandlePacket(IServerPlayer player, MeleeAttackPacket packet)
    {
        (long PlayerId, long AttackId) attackId = (packet.PlayerId, packet.AttackId);

        if (!_attacks.ContainsKey(attackId)) return;

        int range = (int)Math.Ceiling(_attacks[attackId].range * _rangeFactor);

        foreach (MeleeAttackDamagePacket damagePacket in packet.Damages)
        {
            ApplyDamage(damagePacket, range);
        }
    }
    private static void ApplyDamage(MeleeAttackDamagePacket packet, int range)
    {
        if (_api == null) return;

        Entity target = _api.World.GetEntityById(packet.Target);
        Entity attacker = _api.World.GetEntityById(packet.CauseEntity);

        if (!target.ServerPos.InRangeOf(attacker.ServerPos, range * range))
        {
            return;
        }

        target.ReceiveDamage(new DamageSource()
        {
            Source = packet.Source,
            SourceEntity = null,
            CauseEntity = attacker,
            Type = packet.DamageType,
            DamageTier = packet.DamageTier
        }, packet.Damage);

        Vec3f knockback = new(packet.Knockback);
        target.SidedPos.Motion.X *= packet.Stagger;
        target.SidedPos.Motion.Y *= packet.Stagger;
        target.SidedPos.Motion.Add(knockback);
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeAttackPacket
{
    public long AttackId { get; set; }
    public long PlayerId { get; set; }
    public MeleeAttackDamagePacket[] Damages { get; set; } = Array.Empty<MeleeAttackDamagePacket>();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeAttackDamagePacket
{
    public long Target { get; set; }
    public EnumDamageSource Source { get; set; }
    public long CauseEntity { get; set; }
    public EnumDamageType DamageType { get; set; }
    public int DamageTier { get; set; }
    public float Damage { get; set; }
    public float[] Knockback { get; set; } = Array.Empty<float>();
    public float Stagger { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeCollisionPacket
{
    public long Id { get; set; }
    public bool Finished { get; set; }
    public string[] Blocks { get; set; } = Array.Empty<string>();
    public long[] Entities { get; set; } = Array.Empty<long>();
}

public readonly struct LineSegmentCollider
{
    public readonly Vector3 Position;
    public readonly Vector3 Direction;

    public LineSegmentCollider(Vector3 position, Vector3 direction)
    {
        Position = position;
        Direction = direction;
    }
    public LineSegmentCollider(JsonObject json)
    {
        Position = new(json["X1"].AsFloat(0), json["Y1"].AsFloat(0), json["Z1"].AsFloat(0));
        Direction = new(json["X2"].AsFloat(0), json["Y2"].AsFloat(0), json["Z2"].AsFloat(0));
        Direction -= Position;
    }
    public LineSegmentCollider(params float[] positions)
    {
        Position = new(positions[0], positions[1], positions[2]);
        Direction = new(positions[3], positions[4], positions[5]);
        Direction -= Position;
    }

    public void RenderAsLine(ICoreClientAPI api, EntityPlayer entityPlayer, int color = ColorUtil.WhiteArgb)
    {
        BlockPos playerPos = entityPlayer.Pos.AsBlockPos;
        Vector3 playerPosVector = new(playerPos.X, playerPos.Y, playerPos.Z);

        Vector3 tail = Position - playerPosVector;
        Vector3 head = Position + Direction - playerPosVector;

        api.Render.RenderLine(playerPos, tail.X, tail.Y, tail.Z, head.X, head.Y, head.Z, color);
    }
    public LineSegmentCollider? Transform(EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        EntityPos playerPos = entity.Pos;
        Matrixf? modelMatrix = GetHeldItemModelMatrix(entity, itemSlot, api, right);
        if (modelMatrix is null) return null;

        return TransformSegment(this, modelMatrix, playerPos);
    }

    public bool RoughIntersect(Cuboidf collisionBox)
    {
        if (collisionBox.MaxX < Position.X && collisionBox.MaxX < (Position.X + Direction.X)) return false;
        if (collisionBox.MinX > Position.X && collisionBox.MinX > (Position.X + Direction.X)) return false;

        if (collisionBox.MaxY < Position.Y && collisionBox.MaxY < (Position.Y + Direction.Y)) return false;
        if (collisionBox.MinY > Position.Y && collisionBox.MinY > (Position.Y + Direction.Y)) return false;

        if (collisionBox.MaxZ < Position.Z && collisionBox.MaxZ < (Position.Z + Direction.Z)) return false;
        if (collisionBox.MinZ > Position.Z && collisionBox.MinZ > (Position.Z + Direction.Z)) return false;

        return true;
    }
    public Vector3? IntersectCylinder(CylinderCollisionBox box)
    {
        Vector3 distance = new(Position.X - box.CenterX, Position.Z - box.CenterZ, 0);

        // Compute coefficients of the quadratic equation
        float a = (Direction.X * Direction.X) / (box.RadiusX * box.RadiusX) + (Direction.Z * Direction.Z) / (box.RadiusZ * box.RadiusZ);
        float b = 2 * ((distance.X * Direction.X) / (box.RadiusX * box.RadiusX) + (distance.Z * Direction.Z) / (box.RadiusZ * box.RadiusZ));
        float c = (distance.X * distance.X) / (box.RadiusX * box.RadiusX) + (distance.Z * distance.Z) / (box.RadiusZ * box.RadiusZ) - 1;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0 || a == 0) return null;

        float intersectionPointPositionInSegment1 = (-b + MathF.Sqrt(discriminant)) / (2 * a);
        float intersectionPointPositionInSegment2 = (-b - MathF.Sqrt(discriminant)) / (2 * a);

        float intersectionPointY1 = Position.Y + intersectionPointPositionInSegment1 * Direction.Y;
        float intersectionPointY2 = Position.Y + intersectionPointPositionInSegment2 * Direction.Y;

        float minY = Math.Min(intersectionPointY1, intersectionPointY2);
        float maxY = Math.Max(intersectionPointY1, intersectionPointY2);

        if (minY > box.TopY || maxY < box.BottomY) return null;

        float closestIntersectionPoint = MathF.Min(intersectionPointPositionInSegment1, intersectionPointPositionInSegment2);

        return Position + Direction * closestIntersectionPoint;
    }
    public Vector3? IntersectCuboid(Cuboidf collisionBox)
    {
        float tMin = 0.0f;
        float tMax = 1.0f;

        if (!CheckAxisIntersection(Direction.X, Position.X, collisionBox.MinX, collisionBox.MaxX, ref tMin, ref tMax)) return null;
        if (!CheckAxisIntersection(Direction.Y, Position.Y, collisionBox.MinY, collisionBox.MaxY, ref tMin, ref tMax)) return null;
        if (!CheckAxisIntersection(Direction.Z, Position.Z, collisionBox.MinZ, collisionBox.MaxZ, ref tMin, ref tMax)) return null;

        return Position + tMin * Direction;
    }
    public (Block, Vector3)? IntersectTerrain(ICoreClientAPI api)
    {
        int minX = (int)MathF.Min(Position.X, Position.X + Direction.X);
        int minY = (int)MathF.Min(Position.Y, Position.Y + Direction.Y);
        int minZ = (int)MathF.Min(Position.Z, Position.Z + Direction.Z);

        int maxX = (int)MathF.Max(Position.X, Position.X + Direction.X);
        int maxY = (int)MathF.Max(Position.Y, Position.Y + Direction.Y);
        int maxZ = (int)MathF.Max(Position.Z, Position.Z + Direction.Z);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    (Block, Vector3)? intersection = IntersectBlock(api.World.BlockAccessor, x, y, z);
                    if (intersection != null) return intersection;
                }
            }
        }

        return null;
    }

    public static IEnumerable<LineSegmentCollider> Transform(IEnumerable<LineSegmentCollider> segments, EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        EntityPos playerPos = entity.Pos;
        Matrixf? modelMatrix = GetHeldItemModelMatrix(entity, itemSlot, api, right);
        if (modelMatrix is null) return Array.Empty<LineSegmentCollider>();

        return segments.Select(segment => TransformSegment(segment, modelMatrix, playerPos));
    }
    public static bool Transform(IEnumerable<MeleeAttackDamageType> segments, EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        EntityPos playerPos = entity.Pos;
        Matrixf? modelMatrix = GetHeldItemModelMatrix(entity, itemSlot, api, right);
        if (modelMatrix is null) return false;

        foreach (MeleeAttackDamageType damageType in segments)
        {
            damageType._inWorldCollider = TransformSegment(damageType.Collider, modelMatrix, playerPos);
        }

        return true;
    }

    private static readonly Vec4f _inputBuffer = new(0, 0, 0, 1);
    private static readonly Vec4f _outputBuffer = new(0, 0, 0, 1);
    private static readonly Matrixf _matrixBuffer = new();
    private static readonly BlockPos _blockPosBuffer = new();
    private static readonly Vec3d _blockPosVecBuffer = new();
    private const float _epsilon = 1e-6f;

    private static LineSegmentCollider TransformSegment(LineSegmentCollider value, Matrixf modelMatrix, EntityPos playerPos)
    {
        Vector3 tail = TransformVector(value.Position, modelMatrix, playerPos);
        Vector3 head = TransformVector(value.Direction + value.Position, modelMatrix, playerPos);

        return new(tail, head - tail);
    }
    private static Vector3 TransformVector(Vector3 value, Matrixf modelMatrix, EntityPos playerPos)
    {
        _inputBuffer.X = value.X;
        _inputBuffer.Y = value.Y;
        _inputBuffer.Z = value.Z;

        Mat4f.MulWithVec4(modelMatrix.Values, _inputBuffer, _outputBuffer);

        _outputBuffer.X += (float)playerPos.X;
        _outputBuffer.Y += (float)playerPos.Y;
        _outputBuffer.Z += (float)playerPos.Z;

        return new(_outputBuffer.X, _outputBuffer.Y, _outputBuffer.Z);
    }
    private static Matrixf? GetHeldItemModelMatrix(EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        if (entity.Properties.Client.Renderer is not EntityShapeRenderer entityShapeRenderer) return null;

        ItemStack? itemStack = itemSlot?.Itemstack;
        if (itemStack == null) return null;

        AttachmentPointAndPose? attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
        if (attachmentPointAndPose == null) return null;

        AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;
        ItemRenderInfo itemStackRenderInfo = api.Render.GetItemStackRenderInfo(itemSlot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff, 0f);
        if (itemStackRenderInfo?.Transform == null) return null;

        return _matrixBuffer.Set(entityShapeRenderer.ModelMat).Mul(attachmentPointAndPose.AnimModelMatrix).Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
            .Scale(itemStackRenderInfo.Transform.ScaleXYZ.X, itemStackRenderInfo.Transform.ScaleXYZ.Y, itemStackRenderInfo.Transform.ScaleXYZ.Z)
            .Translate(attachPoint.PosX / 16.0 + itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + itemStackRenderInfo.Transform.Translation.Z)
            .RotateX((float)(attachPoint.RotationX + itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
            .RotateY((float)(attachPoint.RotationY + itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
            .RotateZ((float)(attachPoint.RotationZ + itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f))
            .Translate(0f - itemStackRenderInfo.Transform.Origin.X, 0f - itemStackRenderInfo.Transform.Origin.Y, 0f - itemStackRenderInfo.Transform.Origin.Z);
    }
    private static bool CheckAxisIntersection(float dirComponent, float startComponent, float minComponent, float maxComponent, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(dirComponent) < _epsilon)
        {
            // Ray is parallel to the slab, check if it's within the slab's extent
            if (startComponent < minComponent || startComponent > maxComponent) return false;
        }
        else
        {
            // Calculate intersection distances to the slab
            float t1 = (minComponent - startComponent) / dirComponent;
            float t2 = (maxComponent - startComponent) / dirComponent;

            // Swap t1 and t2 if needed so that t1 is the intersection with the near plane
            if (t1 > t2)
            {
                (t2, t1) = (t1, t2);
            }

            // Update the minimum intersection distance
            tMin = MathF.Max(tMin, t1);
            // Update the maximum intersection distance
            tMax = MathF.Min(tMax, t2);

            // Early exit if intersection is not possible
            if (tMin > tMax) return false;
        }

        return true;
    }
    private (Block, Vector3)? IntersectBlock(IBlockAccessor blockAccessor, int x, int y, int z)
    {
        Block block = blockAccessor.GetBlock(x, y, z, BlockLayersAccess.MostSolid);
        _blockPosBuffer.Set(x, y, z);

        Cuboidf[] collisionBoxes = block.GetCollisionBoxes(blockAccessor, _blockPosBuffer);
        if (collisionBoxes == null || collisionBoxes.Length == 0) return null;

        _blockPosVecBuffer.Set(x, y, z);
        for (int i = 0; i < collisionBoxes.Length; i++)
        {
            Cuboidf? collBox = collisionBoxes[i];
            if (collBox == null) continue;

            Cuboidf collBoxInWorld = collBox.Clone();
            collBoxInWorld.X1 += x;
            collBoxInWorld.Y1 += y;
            collBoxInWorld.Z1 += z;
            collBoxInWorld.X2 += x;
            collBoxInWorld.Y2 += y;
            collBoxInWorld.Z2 += z;

            Vector3? intersection = IntersectCuboid(collBoxInWorld);

            if (intersection == null) continue;

            return (block, intersection.Value);
        }

        return null;
    }
}

public readonly struct CylinderCollisionBox
{
    public readonly float RadiusX;
    public readonly float RadiusZ;
    public readonly float TopY;
    public readonly float BottomY;
    public readonly float CenterX;
    public readonly float CenterZ;

    public CylinderCollisionBox(Cuboidf collisionBox)
    {
        RadiusX = (collisionBox.MaxX - collisionBox.MinX) / 2;
        RadiusZ = (collisionBox.MaxZ - collisionBox.MinZ) / 2;
        TopY = collisionBox.MaxY;
        BottomY = collisionBox.MinY;
        CenterX = (collisionBox.MaxX + collisionBox.MinX) / 2;
        CenterZ = (collisionBox.MaxZ + collisionBox.MinZ) / 2;
    }
}