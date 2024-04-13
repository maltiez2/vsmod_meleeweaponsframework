using ImGuiNET;
using ProtoBuf;
using System.Numerics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace MeleeWeaponsFramework;

internal readonly struct MeleeAttackDamageId
{
    public readonly int ItemId;
    public readonly int AttackId;
    public readonly int DamageId;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal struct MeleeAttackDamagePacket
{
    public MeleeAttackDamageId Id { get; set; }
    public Vector3 Position { get; set; }
    public long AttackerEntityId { get; set; }
    public long TargetEntityId { get; set; }
}

internal struct MeleeAttackDamageType : IHasLineCollider
{
    public MeleeAttackDamageId Id { get; }
    public LineSegmentCollider RelativeCollider { get; set; }
    public LineSegmentCollider InWorldCollider { get; set; }

    public readonly float Damage;
    public readonly float Knockback;
    public readonly float Stagger;
    public readonly int Tier;
    public readonly EnumDamageType DamageType;
    public readonly Vector2 HitWindow;
    public readonly int DurabilityDamage = 1;

    public MeleeAttackDamageType(
        MeleeAttackDamageId id,
        float damage,
        EnumDamageType damageType,
        LineSegmentCollider collider,
        Vector2 hitWindow,
        int durabilityDamage = 1,
        int tier = 0,
        float knockback = 0,
        float stagger = 1.0f
        )
    {
        Damage = damage;
        DamageType = damageType;
        Tier = tier;
        Knockback = knockback;
        Stagger = stagger;
        RelativeCollider = collider;
        InWorldCollider = collider;
        Id = id;
        HitWindow = hitWindow;
        DurabilityDamage = durabilityDamage;
    }

    public readonly Vector3? TryAttack(IPlayer attacker, Entity target)
    {
        Vector3? collisionPoint = Collide(target);

        if (collisionPoint == null) return null;

        bool received = Attack(attacker.Entity, target);

        return received ? collisionPoint : null;
    }
    public readonly bool Attack(Entity attacker, Entity target)
    {
        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker,
            Type = DamageType,
            DamageTier = Tier
        }, Damage);

        bool received = damageReceived || Damage <= 0;

        if (received)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Pos.XYZFloat).Normalize() * Knockback * _knockbackFactor * (1.0f - target.Properties.KnockbackResistance);
            target.SidedPos.Motion.X *= Stagger;
            target.SidedPos.Motion.Z *= Stagger;
            target.SidedPos.Motion.Add(knockback);
        }

        return received;
    }

#if DEBUG
    public void Editor(string title)
    {
        Vector3 head = RelativeCollider.Position;
        Vector3 tail = RelativeCollider.Position + RelativeCollider.Direction;

        bool headChanged = ImGui.DragFloat3($"Head##{title}", ref head);
        bool tailChanged = ImGui.DragFloat3($"Tail##{title}", ref tail);

        if (headChanged || tailChanged) RelativeCollider = new LineSegmentCollider(head, tail - head);
    }
#endif

    private const float _knockbackFactor = 0.1f;
    private readonly Vector3? Collide(Entity target)
    {
        Cuboidf collisionBox = GetCollisionBox(target);

        if (!InWorldCollider.RoughIntersect(collisionBox)) return null;

        return InWorldCollider.IntersectCuboid(collisionBox);
    }
    private static Cuboidf GetCollisionBox(Entity entity)
    {
        Cuboidf collisionBox = entity.CollisionBox.Clone(); // @TODO: Refactor to not clone
        EntityPos position = entity.Pos;
        collisionBox.X1 += (float)position.X;
        collisionBox.Y1 += (float)position.Y;
        collisionBox.Z1 += (float)position.Z;
        collisionBox.X2 += (float)position.X;
        collisionBox.Y2 += (float)position.Y;
        collisionBox.Z2 += (float)position.Z;
        return collisionBox;
    }
}