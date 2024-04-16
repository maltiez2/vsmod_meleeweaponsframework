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
    public virtual DirectionsConfiguration DirectionsConfiguration { get; protected set; } = DirectionsConfiguration.None;
    public virtual bool RenderDirectionCursor { get; protected set; } = false;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);


    }

    public virtual void OnSelected(ItemSlot slot, EntityPlayer player)
    {

    }

    public virtual void OnRegistered(MeleeWeaponPlayerBehavior behavior, ICoreClientAPI api)
    {
        Behavior = behavior;
        Api = api;
    }

    protected MeleeWeaponPlayerBehavior? Behavior { get; private set; }
    protected ICoreClientAPI? Api { get; private set; }

    [ActionEventHandler(EnumEntityAction.LeftMouseDown, ActionState.Pressed)]
    private void EventHandler(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand, AttackDirection direction)
    {

    }
}
