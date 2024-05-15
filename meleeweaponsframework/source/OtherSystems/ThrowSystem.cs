using ProtoBuf;
using System;
using System.Collections.ObjectModel;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

namespace MeleeWeaponsFramework;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct ThrowAttackPacket
{
    public bool RightHand { get; set; }
    public int ItemId { get; set; }
    public int ThrowAttackId { get; set; }
    public long AttackerEntityId { get; set; }

    public float Accuracy { get; set; }
    public float AimingRandPitch { get; set; }
    public float AimingRandYaw { get; set; }
}

public readonly struct ThrowAttackId
{
    public readonly int Id;
    public readonly int ItemId;

    public ThrowAttackId(int id, int itemId)
    {
        Id = id;
        ItemId = itemId;
    }
}

public interface IThrowAttack
{
    void SpawnProjectile(Entity attacker, ItemStack projectileStack, ICoreAPI api, ThrowAttackPacket packet);
}

public class ThrowAttack : IThrowAttack
{
    public ThrowAttack(float damage, string entity)
    {
        Damage = damage;
        ProjectileEntityCode = entity;
    }

    public float Damage { get; }
    public string ProjectileEntityCode { get; }

    public const float TyronMagicNumber_1 = 0.75f;
    public const float TyronMagicNumber_2 = 0.2f;
    public const float TyronMagicNumber_3 = 0.65f;
    public const float TyronMagicNumber_4 = 0.21f;
    public const float TyronMagicNumber_5 = 1.1f;
    public const float TyronMagicNumber_6 = 0.3f;

    public void SpawnProjectile(Entity attacker, ItemStack projectileStack, ICoreAPI api, ThrowAttackPacket packet)
    {
        try
        {
            EntityProperties entityType = api.World.GetEntityType(new AssetLocation(ProjectileEntityCode));
            EntityProjectile entityProjectile = api.World.ClassRegistry.CreateEntity(entityType) as EntityProjectile;
            entityProjectile.FiredBy = attacker;
            entityProjectile.Damage = Damage;
            entityProjectile.ProjectileStack = projectileStack;
            entityProjectile.DropOnImpactChance = TyronMagicNumber_5;
            entityProjectile.DamageStackOnImpact = true;
            entityProjectile.Weight = TyronMagicNumber_6;

            double pitch = packet.AimingRandPitch * (1 - packet.Accuracy) * TyronMagicNumber_1;
            double yaw = packet.AimingRandYaw * (1 - packet.Accuracy) * TyronMagicNumber_1;

            Vec3d vec3d = attacker.ServerPos.XYZ.Add(0.0, attacker.LocalEyePos.Y - TyronMagicNumber_2, 0.0);
            Vec3d pos = (vec3d.AheadCopy(1.0, (double)attacker.ServerPos.Pitch + pitch, (double)attacker.ServerPos.Yaw + yaw) - vec3d) * TyronMagicNumber_3;
            Vec3d pos2 = attacker.ServerPos.BehindCopy(TyronMagicNumber_4).XYZ.Add(attacker.LocalEyePos.X, attacker.LocalEyePos.Y - TyronMagicNumber_2, attacker.LocalEyePos.Z);
            entityProjectile.ServerPos.SetPos(pos2);
            entityProjectile.ServerPos.Motion.Set(pos);
            entityProjectile.Pos.SetFrom(entityProjectile.ServerPos);
            entityProjectile.World = api.World;
            entityProjectile.SetRotation();
            api.World.SpawnEntity(entityProjectile);
        }
        catch (Exception exception)
        {
            LoggerUtil.Error(api, this, $"Exception on spawning projectile.");
            LoggerUtil.Debug(api, this, $"Exception on spawning projectile:\n{exception}");
        }
    }
}

public abstract class ThrowSystem
{
    public const string NetworkChannelId = "melee-weapons-framework:throw-packets";
}

public class ThrowSystemClient : ThrowSystem
{
    public ThrowSystemClient(ICoreClientAPI api)
    {
        _api = api;
        _random = new(0, 1, EnumDistribution.GAUSSIAN);
        _clientChannel = api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<ThrowAttackPacket>();
    }

    public bool Register(ThrowAttackId id, IThrowAttack attack) => _attacks.TryAdd(id, attack);
    public void Aim()
    {
        _player = _api.World.Player.Entity;
        _player.Attributes.SetInt("aiming", 1);
        _player.Attributes.SetInt("aimingCancel", 0);
    }
    public void Stop()
    {
        _player = _api.World.Player.Entity;
        _player.Attributes.SetInt("aiming", 0);
        _player.Attributes.SetInt("aimingCancel", 1);
    }
    public void Throw(ThrowAttackId id, bool rightHand = true)
    {
        _player = _api.World.Player.Entity;
        

        float accuracy = _player.Attributes.GetFloat("aimingAccuracy");
        float aimingRandPitch = _random.nextFloat();
        float aimingRandYaw = _random.nextFloat();

        Console.WriteLine($"aimingAccuracy: {accuracy}, aimingRandPitch: {aimingRandPitch}, aimingRandYaw: {aimingRandYaw}");

        _player.Attributes.SetInt("aiming", 0);

        ThrowAttackPacket packet = new()
        {
            RightHand = rightHand,
            Accuracy = accuracy,
            AimingRandPitch = aimingRandPitch,
            AimingRandYaw = aimingRandYaw,
            ItemId = id.ItemId,
            ThrowAttackId = id.Id,
            AttackerEntityId = _player.EntityId
        };

        _clientChannel.SendPacket(packet);

        ItemSlot slot;
        if (rightHand)
        {
            slot = _player.RightHandItemSlot;
        }
        else
        {
            slot = _player.LeftHandItemSlot;
        }

        //_attacks[id].SpawnProjectile(_player, slot.TakeOut(1), _api, packet);
    }

    private readonly Dictionary<ThrowAttackId, IThrowAttack> _attacks = new();
    private readonly IClientNetworkChannel _clientChannel;
    private readonly ICoreClientAPI _api;
    private readonly NatFloat _random;
    private EntityPlayer _player;
}

public class ThrowSystemServer : ThrowSystem
{
    public ThrowSystemServer(ICoreServerAPI api)
    {
        _api = api;
        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<ThrowAttackPacket>()
            .SetMessageHandler<ThrowAttackPacket>(HandlePacket);
    }

    public bool Register(ThrowAttackId id, IThrowAttack attack) => _attacks.TryAdd(id, attack);

    private readonly Dictionary<ThrowAttackId, IThrowAttack> _attacks = new();
    private readonly ICoreServerAPI _api;

    private void HandlePacket(IServerPlayer player, ThrowAttackPacket packet)
    {
        ItemSlot slot;
        if (packet.RightHand)
        {
            slot = player.Entity.RightHandItemSlot;
        }
        else
        {
            slot = player.Entity.LeftHandItemSlot;
        }

        if (slot.Itemstack?.Item?.Id != packet.ItemId) return;
        if (player.Entity.EntityId != packet.AttackerEntityId) return;

        IThrowAttack attack = _attacks[new(packet.ThrowAttackId, packet.ItemId)];

        ItemStack stack = slot.TakeOut(1);
        slot.MarkDirty();

        attack.SpawnProjectile(player.Entity, stack, _api, packet);
    }
}