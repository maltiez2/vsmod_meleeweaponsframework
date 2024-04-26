using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

namespace MeleeWeaponsFramework;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct HackingAttackPacket
{
    public bool RightHand { get; set; }
    public long AttackerEntityId { get; set; }
    public long HackedEntity { get; set; }
}

public abstract class HackingSystem
{
    public const string NetworkChannelId = "melee-weapons-framework:throw-packets";

    protected static bool CanHack(Entity entity, EntityPlayer hacker, ItemSlot slot, ICoreAPI api)
    {
        JsonObject attributes = entity.Properties.Attributes;
        return attributes != null && attributes["hackedEntity"].Exists && slot.Itemstack.ItemAttributes.IsTrue("hacking") && api.ModLoader.GetModSystem<CharacterSystem>().HasTrait(hacker.Player, "technical");
    }

    protected void SpawnEntityInPlaceOf(Entity hackedEntity, string code, EntityPlayer hacker, ICoreAPI api)
    {
        AssetLocation assetLocation = AssetLocation.Create(code, hackedEntity.Code.Domain);
        EntityProperties entityType = api.World.GetEntityType(assetLocation);
        if (entityType == null)
        {
            return;
        }

        Entity entity = api.World.ClassRegistry.CreateEntity(entityType);
        if (entity != null)
        {
            entity.ServerPos.X = hackedEntity.ServerPos.X;
            entity.ServerPos.Y = hackedEntity.ServerPos.Y;
            entity.ServerPos.Z = hackedEntity.ServerPos.Z;
            entity.ServerPos.Motion.X = hackedEntity.ServerPos.Motion.X;
            entity.ServerPos.Motion.Y = hackedEntity.ServerPos.Motion.Y;
            entity.ServerPos.Motion.Z = hackedEntity.ServerPos.Motion.Z;
            entity.ServerPos.Yaw = hackedEntity.ServerPos.Yaw;
            entity.Pos.SetFrom(entity.ServerPos);
            entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            entity.Attributes.SetString("origin", "playerplaced");
            entity.WatchedAttributes.SetLong("guardedEntityId", hackedEntity.EntityId);
            entity.WatchedAttributes.SetString("guardedPlayerUid", hacker.PlayerUID);

            api.World.SpawnEntity(entity);
        }

        (api as ICoreServerAPI)?.World.DespawnEntity(hackedEntity, new EntityDespawnData
        {
            Reason = EnumDespawnReason.Removed
        });
    }
}

public class HackingSystemClient : HackingSystem
{
    public HackingSystemClient(ICoreClientAPI api)
    {
        _api = api;
        _clientChannel = api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<HackingAttackPacket>();
    }

    public bool Hack(Entity entity, bool rightHand = true)
    {
        _player = _api.World.Player.Entity;
        ItemSlot slot;
        if (rightHand)
        {
            slot = _player.RightHandItemSlot;
        }
        else
        {
            slot = _player.LeftHandItemSlot;
        }

        if (!CanHack(entity, _player, slot, _api)) return false;

        HackingAttackPacket packet = new()
        {
            RightHand = rightHand,
            AttackerEntityId = _player.EntityId,
            HackedEntity = entity.EntityId
        };

        _clientChannel.SendPacket(packet);

        SpawnEntityInPlaceOf(entity, entity.Properties.Attributes["hackedEntity"].AsString(), _player, _api);

        return true;
    }

    private readonly IClientNetworkChannel _clientChannel;
    private readonly ICoreClientAPI _api;
    private EntityPlayer _player;
}

public class HackingSystemServer : HackingSystem
{
    public HackingSystemServer(ICoreServerAPI api)
    {
        _api = api;
        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<HackingAttackPacket>()
            .SetMessageHandler<HackingAttackPacket>(HandlePacket);
    }

    private readonly ICoreServerAPI _api;

    private void HandlePacket(IServerPlayer player, HackingAttackPacket packet)
    {
        Console.WriteLine("Hack packet");
        
        ItemSlot slot;
        if (packet.RightHand)
        {
            slot = player.Entity.RightHandItemSlot;
        }
        else
        {
            slot = player.Entity.LeftHandItemSlot;
        }

        if (player.Entity.EntityId != packet.AttackerEntityId) return;

        Entity entity = player.Entity.World.GetEntityById(packet.HackedEntity);

        if (!CanHack(entity, player.Entity, slot, _api)) return;

        Console.WriteLine("Can hack");

        SpawnEntityInPlaceOf(entity, entity.Properties.Attributes["hackedEntity"].AsString(), player.Entity, _api);
    }
}