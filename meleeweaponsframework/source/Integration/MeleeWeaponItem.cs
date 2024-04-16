using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MeleeWeaponsFramework;


[AttributeUsage(AttributeTargets.Method)]
public class ActionEventHandlerAttribute : Attribute
{
    public ActionEventId Event { get; }

    public ActionEventHandlerAttribute(EnumEntityAction action, ActionState state)
    {
        Event = new(action, state);
    }
}

public abstract class MeleeWeaponItem : Item
{
    public virtual PlayerAnimationData IdleAnimation { get; protected set; }
    public virtual PlayerAnimationData ReadyAnimation { get; protected set; }
    public virtual PlayerAnimationData IdleAnimationOffhand { get; protected set; }
    public virtual PlayerAnimationData ReadyAnimationOffhand { get; protected set; }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);


    }

    public virtual void OnSelected(ItemSlot slot, EntityPlayer player)
    {

    }

    public virtual void OnRegistered(MeleeWeaponPlayerBehavior behavior, ICoreClientAPI api)
    {

    }
}
