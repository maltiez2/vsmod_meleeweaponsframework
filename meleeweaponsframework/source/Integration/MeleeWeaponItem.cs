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
    public string IdleAnimation { get; set; } = "melee-idle";
    public string ReadyAnimation { get; set; } = "melee-ready";
    public string IdleAnimationOffhand { get; set; } = "melee-idle-offhand";
    public string ReadyAnimationOffhand { get; set; } = "melee-ready-offhand";
    public string DirectionsConfiguration { get; set; } = "None";
}

public abstract class MeleeWeaponItem : Item
{
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
        }

        if (api is not ICoreClientAPI clientAPI) return;

        IAnimationManagerSystem animationSystem = clientAPI.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        MeleeSystem = clientAPI.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>().MeleeSystemClient;

        Parameters = LoadParameters();

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
    protected ICoreClientAPI? Api { get; private set; }
    protected MeleeWeaponParameters? Parameters { get; private set; }

    protected virtual MeleeWeaponParameters LoadParameters()
    {
        return Attributes[MeleeWeaponStatsAttribute].AsObject<MeleeWeaponParameters>();
    }
}

public class GenericMeleeWeaponParameters : MeleeWeaponParameters
{
    public string AttackAnimation { get; set; } = "melee-attack";
    public float[] Collider { get; set; } = new float[6];
    public int AttackWindUpMs { get; set; } = 100;
    public int AttackDurationMs { get; set; } = 300;
    public int AttackEaseOutMs { get; set; } = 500;
    public float AttackDamage { get; set; } = 1.0f;
    public int AttackTier { get; set; } = 0;
    public string AttackDamageType { get; set; } = "BluntAttack";
}

public class GenericMeleeWeapon : MeleeWeaponItem
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
    }


    protected virtual PlayerAnimationData AttackAnimation { get; set; }

    [ActionEventHandler(EnumEntityAction.InWorldLeftMouseDown, ActionState.Pressed)]
    protected virtual void OnAttack(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {
        throw new NotImplementedException();
    }

    protected override MeleeWeaponParameters LoadParameters()
    {
        if (Api is null) return new();

        GenericMeleeWeaponParameters parameters = Attributes[MeleeWeaponStatsAttribute].AsObject<GenericMeleeWeaponParameters>();
        AttackAnimation = new(parameters.AttackAnimation, Api.ModLoader.GetModSystem<AnimationManagerLibSystem>());

        return parameters;
    }
}
