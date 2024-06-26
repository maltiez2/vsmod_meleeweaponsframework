﻿using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

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

public interface IBehaviorManagedItem
{
    public int WeaponItemId { get; }
    IPlayerAnimationData IdleAnimation { get; }
    IPlayerAnimationData ReadyAnimation { get; }
    IPlayerAnimationData IdleAnimationOffhand { get; }
    IPlayerAnimationData ReadyAnimationOffhand { get; }
    DirectionsConfiguration DirectionsType { get; }
    bool RenderDirectionCursor { get; }

    void OnSelected(ItemSlot slot, EntityPlayer player);
    void OnDeselected(EntityPlayer player);
    void OnRegistered(MeleeWeaponPlayerBehavior behavior, ICoreClientAPI api);
}

public abstract class MeleeWeaponItem : Item, IBehaviorManagedItem
{
    public int WeaponItemId => ItemId;
    public virtual IPlayerAnimationData IdleAnimation { get; protected set; }
    public virtual IPlayerAnimationData ReadyAnimation { get; protected set; }
    public virtual IPlayerAnimationData IdleAnimationOffhand { get; protected set; }
    public virtual IPlayerAnimationData ReadyAnimationOffhand { get; protected set; }
    public virtual DirectionsConfiguration DirectionsType { get; protected set; } = DirectionsConfiguration.None;
    public virtual bool RenderDirectionCursor { get; protected set; } = false;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api is ICoreServerAPI serverApi)
        {
            ServerMeleeSystem = serverApi.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemServer;
            ServerBlockSystem = serverApi.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().BlockSystemServer;
        }

        if (api is not ICoreClientAPI clientAPI) return;

        IAnimationManagerSystem animationSystem = clientAPI.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        MeleeSystem = clientAPI.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemClient;
        BlockSystem = clientAPI.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().BlockSystemClient;

        Parameters = Attributes[MeleeWeaponStatsAttribute].AsObject<MeleeWeaponParameters>();

        IdleAnimation = new PlayerSimpleAnimationData(Parameters.IdleAnimation, animationSystem);
        ReadyAnimation = new PlayerSimpleAnimationData(Parameters.ReadyAnimation, animationSystem);
        IdleAnimationOffhand = new PlayerSimpleAnimationData(Parameters.IdleAnimationOffhand, animationSystem, mainHand: false);
        ReadyAnimationOffhand = new PlayerSimpleAnimationData(Parameters.ReadyAnimationOffhand, animationSystem, mainHand: false);
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
    protected MeleeBlockSystemServer? ServerBlockSystem { get; private set; }
    protected ICoreClientAPI? Api { get; private set; }
    protected MeleeWeaponParameters Parameters { get; private set; } = new();

    protected bool AltPressed() => (Api?.Input.KeyboardKeyState[(int)GlKeys.AltLeft] ?? false) || (Api?.Input.KeyboardKeyState[(int)GlKeys.AltRight] ?? false);
}

public abstract class MeleeShieldItem : ItemShield, IBehaviorManagedItem
{
    public int WeaponItemId => ItemId;
    public virtual IPlayerAnimationData IdleAnimation { get; protected set; }
    public virtual IPlayerAnimationData ReadyAnimation { get; protected set; }
    public virtual IPlayerAnimationData IdleAnimationOffhand { get; protected set; }
    public virtual IPlayerAnimationData ReadyAnimationOffhand { get; protected set; }
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

        IdleAnimation = new PlayerAnimationData(Parameters.IdleAnimation, animationSystem);
        ReadyAnimation = new PlayerAnimationData(Parameters.ReadyAnimation, animationSystem);
        IdleAnimationOffhand = new PlayerAnimationData(Parameters.IdleAnimationOffhand, animationSystem, mainHand: false);
        ReadyAnimationOffhand = new PlayerAnimationData(Parameters.ReadyAnimationOffhand, animationSystem, mainHand: false);
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

    protected bool AltPressed() => (Api?.Input.KeyboardKeyState[(int)GlKeys.AltLeft] ?? false) || (Api?.Input.KeyboardKeyState[(int)GlKeys.AltRight] ?? false);
}