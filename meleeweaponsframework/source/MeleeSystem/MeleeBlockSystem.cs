using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace MeleeWeaponsFramework;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct MeleeBlockPacket
{
    public bool RightHand { get; set; }
    public int DirectionIndex { get; set; }
    public int ItemId { get; set; }
    public int Id { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct MeleeBlockStopPacket
{

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
        _clientChannel = api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeBlockPacket>()
            .RegisterMessageType<MeleeBlockStopPacket>();
    }

    public bool Register(MeleeBlockId id, MeleeBlock block) => _blocks.TryAdd(id, block);
    public void Start(MeleeBlockId id, int directionIndex, bool rightHand = true)
    {
        _behavior = _api.World.Player.Entity.GetBehavior<MeleeBlockPlayerBehavior>();

        ItemSlot slot = rightHand ? _api.World.Player.Entity.RightHandItemSlot : _api.World.Player.Entity.LeftHandItemSlot;

        if (id.ItemId != (slot.Itemstack?.Item?.Id ?? 0))
        {
            return;
        }

        Stop();

        _behavior?.Start(_blocks[id], directionIndex);

        _clientChannel.SendPacket(new MeleeBlockPacket
        {
            RightHand = rightHand,
            DirectionIndex = directionIndex,
            Id = id.Id,
            ItemId = id.ItemId
        });
    }
    public void Stop()
    {
        _behavior?.Stop();

        _clientChannel.SendPacket(new MeleeBlockStopPacket());
    }

    private readonly ICoreClientAPI _api;
    private readonly IClientNetworkChannel _clientChannel;
    private readonly Dictionary<MeleeBlockId, MeleeBlock> _blocks = new();
    private MeleeBlockPlayerBehavior? _behavior;
}

public sealed class MeleeBlockSystemServer : MeleeBlockSystem
{
    public MeleeBlockSystemServer(ICoreServerAPI api)
    {
        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeBlockPacket>()
            .RegisterMessageType<MeleeBlockStopPacket>()
            .SetMessageHandler<MeleeBlockPacket>(HandlePacket)
            .SetMessageHandler<MeleeBlockStopPacket>(HandlePacket);
    }

    public bool Register(MeleeBlockId id, MeleeBlock block) => _blocks.TryAdd(id, block);

    private readonly Dictionary<MeleeBlockId, MeleeBlock> _blocks = new();

    private void HandlePacket(IServerPlayer player, MeleeBlockPacket packet)
    {
        int itemId = packet.RightHand ? player.Entity.RightHandItemSlot.Itemstack?.Item?.Id ?? -1 : player.Entity.LeftHandItemSlot.Itemstack?.Item?.Id ?? -1;

        if (itemId != packet.ItemId)
        {
            return;
        }

        MeleeBlockPlayerBehavior? behavior = player.Entity.GetBehavior<MeleeBlockPlayerBehavior>();

        behavior?.Stop();
        behavior?.Start(_blocks[new(packet.ItemId, packet.Id)], packet.DirectionIndex);
    }

    private void HandlePacket(IServerPlayer player, MeleeBlockStopPacket packet)
    {
        player.Entity.GetBehavior<MeleeBlockPlayerBehavior>().Stop();
    }
}