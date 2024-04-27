using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MeleeWeaponsFramework;


[AttributeUsage(AttributeTargets.Method)]
public class ActionEventHandlerAttribute : Attribute
{
    public ActionEventId Event { get; }

    public ActionEventHandlerAttribute(EnumEntityAction action, ActionState state) => Event = new(action, state);
}

public class MeleeWeaponParameters
{
    public string IdleAnimation { get; set; } = "meleeweaponsframework-empty";
    public string ReadyAnimation { get; set; } = "meleeweaponsframework-empty";
    public string IdleAnimationOffhand { get; set; } = "meleeweaponsframework-empty";
    public string ReadyAnimationOffhand { get; set; } = "meleeweaponsframework-empty";
    public string DirectionsConfiguration { get; set; } = "None";
}

public interface IMeleeWeaponItem
{
    public int WeaponItemId { get; }
    PlayerAnimationData IdleAnimation { get; }
    PlayerAnimationData ReadyAnimation { get; }
    PlayerAnimationData IdleAnimationOffhand { get; }
    PlayerAnimationData ReadyAnimationOffhand { get; }
    DirectionsConfiguration DirectionsType { get; }
    bool RenderDirectionCursor { get; }

    void OnSelected(ItemSlot slot, EntityPlayer player);
    void OnDeselected(EntityPlayer player);
    void OnRegistered(MeleeWeaponPlayerBehavior behavior, ICoreClientAPI api);
}

public abstract class MeleeWeaponItem : Item, IMeleeWeaponItem
{
    public int WeaponItemId => ItemId;
    public virtual PlayerAnimationData IdleAnimation { get; protected set; }
    public virtual PlayerAnimationData ReadyAnimation { get; protected set; }
    public virtual PlayerAnimationData IdleAnimationOffhand { get; protected set; }
    public virtual PlayerAnimationData ReadyAnimationOffhand { get; protected set; }
    public virtual DirectionsConfiguration DirectionsType { get; protected set; } = DirectionsConfiguration.None;
    public virtual bool RenderDirectionCursor { get; protected set; } = false;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api is ICoreServerAPI serverApi)
        {
            ServerMeleeSystem = serverApi.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemServer;
            BlockSystemServer = serverApi.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().BlockSystemServer;
        }

        if (api is not ICoreClientAPI clientAPI) return;

        IAnimationManagerSystem animationSystem = clientAPI.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        MeleeSystem = clientAPI.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemClient;
        BlockSystem = clientAPI.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().BlockSystemClient;

        Parameters = Attributes[MeleeWeaponStatsAttribute].AsObject<MeleeWeaponParameters>();

        IdleAnimation = new(Parameters.IdleAnimation, animationSystem);
        ReadyAnimation = new(Parameters.ReadyAnimation, animationSystem);
        IdleAnimationOffhand = new(Parameters.IdleAnimationOffhand, animationSystem);
        ReadyAnimationOffhand = new(Parameters.ReadyAnimationOffhand, animationSystem);
        DirectionsType = Enum.Parse<DirectionsConfiguration>(Parameters.DirectionsConfiguration);
    }

    public virtual void OnSelected(ItemSlot slot, EntityPlayer player)
    {

    }

    public virtual void OnDeselected(EntityPlayer player)
    {

    }

    public virtual void OnRegistered(MeleeWeaponPlayerBehavior behavior, ICoreClientAPI api)
    {
        Behavior = behavior;
        Api = api;
    }

    protected const string MeleeWeaponStatsAttribute = "melee-weapon-stats";
    protected MeleeWeaponPlayerBehavior? Behavior { get; private set; }
    protected MeleeSystemClient? MeleeSystem { get; private set; }
    protected MeleeSystemServer? ServerMeleeSystem { get; private set; }
    protected MeleeBlockSystemClient? BlockSystem { get; private set; }
    protected MeleeBlockSystemServer? BlockSystemServer { get; private set; }
    protected ICoreClientAPI? Api { get; private set; }
    protected MeleeWeaponParameters Parameters { get; private set; } = new();
}