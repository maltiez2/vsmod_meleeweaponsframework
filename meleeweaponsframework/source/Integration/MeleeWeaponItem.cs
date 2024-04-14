using Vintagestory.API.Common;

namespace MeleeWeaponsFramework;

public abstract class MeleeWeaponItem : Item
{
    public virtual PlayerAnimationData IdleAnimation { get; protected set; }
    public virtual PlayerAnimationData ReadyAnimation { get; protected set; }

    public virtual PlayerAnimationData IdleAnimationOffhand { get; protected set; }
    public virtual PlayerAnimationData ReadyAnimationOffhand { get; protected set; }
}
