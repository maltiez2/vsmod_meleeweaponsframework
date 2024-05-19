using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MeleeWeaponsFramework;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct MeleeBlockPacket
{
    public bool RightHand { get; set; }
    public int DirectionIndex { get; set; }
    public int ItemId { get; set; }
    public int Id { get; set; }
    public long EntityId { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct MeleeBlockStopPacket
{
    public long EntityId { get; set; }
}

public abstract class MeleeBlockSystem
{
    public const string NetworkChannelId = "melee-weapons-framework:block-packets";
}

public readonly struct MeleeBlockId
{
    public readonly int ItemId;
    public readonly int Id;

    public MeleeBlockId(int itemId, int id)
    {
        ItemId = itemId;
        Id = id;
    }
}

public sealed class MeleeBlockSystemClient : MeleeBlockSystem
{
    public MeleeBlockSystemClient(ICoreClientAPI api)
    {
        _api = api;
        _channel = api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeBlockPacket>()
            .RegisterMessageType<MeleeBlockStopPacket>()
            .SetMessageHandler<MeleeBlockPacket>(HandlePacket)
            .SetMessageHandler<MeleeBlockStopPacket>(HandlePacket);
    }

    public bool Register(MeleeBlockId id, MeleeBlock block) => _blocks.TryAdd(id, block);
    public void Start(MeleeBlockId id, int directionIndex, bool rightHand = true)
    {
        _behavior = _api.World.Player.Entity.GetBehavior<MeleeBlockBehavior>();

        ItemSlot slot = rightHand ? _api.World.Player.Entity.RightHandItemSlot : _api.World.Player.Entity.LeftHandItemSlot;

        if (id.ItemId != (slot.Itemstack?.Item?.Id ?? 0))
        {
            return;
        }

        Stop();

        _behavior?.Start(_blocks[id], directionIndex, id.ItemId, rightHand);

        _channel.SendPacket(new MeleeBlockPacket
        {
            RightHand = rightHand,
            DirectionIndex = directionIndex,
            Id = id.Id,
            ItemId = id.ItemId,
            EntityId = _api.World.Player.Entity.EntityId
        });
    }
    public void Stop()
    {
        _behavior?.Stop();

        _channel.SendPacket(new MeleeBlockStopPacket()
        {
            EntityId = _api.World.Player.Entity.EntityId
        });
    }

    private readonly ICoreClientAPI _api;
    private readonly IClientNetworkChannel _channel;
    private readonly Dictionary<MeleeBlockId, MeleeBlock> _blocks = new();
    private MeleeBlockBehavior? _behavior;

    private void HandlePacket(MeleeBlockPacket packet)
    {
        if (_api.World.GetEntityById(packet.EntityId) is not EntityAgent player)
        {
            return;
        }

        int itemId = packet.RightHand ? player.RightHandItemSlot.Itemstack?.Item?.Id ?? -1 : player.LeftHandItemSlot.Itemstack?.Item?.Id ?? -1;

        if (itemId != packet.ItemId)
        {
            return;
        }

        MeleeBlockBehavior? behavior = player.GetBehavior<MeleeBlockBehavior>();

        behavior?.Stop();
        behavior?.Start(_blocks[new(packet.ItemId, packet.Id)], packet.DirectionIndex, packet.ItemId, packet.RightHand);
    }
    private void HandlePacket(MeleeBlockStopPacket packet)
    {
        if (_api.World.GetEntityById(packet.EntityId) is not EntityAgent player)
        {
            return;
        }

        player.GetBehavior<MeleeBlockBehavior>()?.Stop();
    }
}

public sealed class MeleeBlockSystemServer : MeleeBlockSystem
{
    public MeleeBlockSystemServer(ICoreServerAPI api)
    {
        _channel = api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeBlockPacket>()
            .RegisterMessageType<MeleeBlockStopPacket>()
            .SetMessageHandler<MeleeBlockPacket>(HandlePacket)
            .SetMessageHandler<MeleeBlockStopPacket>(HandlePacket);
    }

    public bool Register(MeleeBlockId id, MeleeBlock block) => _blocks.TryAdd(id, block);

    private readonly Dictionary<MeleeBlockId, MeleeBlock> _blocks = new();
    private readonly IServerNetworkChannel _channel;

    private void HandlePacket(IServerPlayer player, MeleeBlockPacket packet)
    {
        if (player.Entity.EntityId != packet.EntityId) return;

        int itemId = packet.RightHand ? player.Entity.RightHandItemSlot.Itemstack?.Item?.Id ?? -1 : player.Entity.LeftHandItemSlot.Itemstack?.Item?.Id ?? -1;

        if (itemId != packet.ItemId)
        {
            return;
        }

        _channel.BroadcastPacket(packet, player);

        MeleeBlockBehavior? behavior = player.Entity.GetBehavior<MeleeBlockBehavior>();

        behavior?.Stop();
        behavior?.Start(_blocks[new(packet.ItemId, packet.Id)], packet.DirectionIndex, packet.ItemId, packet.RightHand);
    }

    private void HandlePacket(IServerPlayer player, MeleeBlockStopPacket packet)
    {
        player.Entity.GetBehavior<MeleeBlockBehavior>()?.Stop();
    }
}