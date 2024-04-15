using AnimationManagerLib;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MeleeWeaponsFramework;

internal class MeleeWeaponPlayerBehavior : EntityBehavior
{
    public TimeSpan AnimationsEaseOutTime = TimeSpan.FromMilliseconds(500);
    public TimeSpan DefaultIdleAnimationDelay = TimeSpan.FromSeconds(5);

    public delegate void ActionEventCallbackDelegate(ItemSlot slot, EntityPlayer player, ref int state, ActionEventData eventData, bool mainHand);

    public MeleeWeaponPlayerBehavior(Entity entity) : base(entity)
    {
        if (entity is not EntityPlayer player)
        {
            throw new ArgumentException($"This behavior should only be applied to player");
        }

        if (entity.Api is not ICoreClientAPI clientApi)
        {
            throw new ArgumentException($"This behavior is client side only");
        }

        _mainPlayer = clientApi.World.Player.Entity.EntityId == entity.EntityId;
        _player = player;
        _api = clientApi;

        MeleeWeaponsFrameworkModSystem frameworkSystem = _api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>();

        if (frameworkSystem.ActionListener is null)
        {
            throw new ArgumentException($"Action listener is null, it may be caused by instantiating this behavior to early");
        }

        _actionListener = frameworkSystem.ActionListener;
        _animationSystem = _api.ModLoader.GetModSystem<AnimationManagerLibSystem>();
    }
    public override string PropertyName() => _statCategory;
    public override void OnGameTick(float deltaTime)
    {
        if (_mainPlayer) _ = CheckIfItemsInHandsChanged();
    }

    public void RegisterWeapon(MeleeWeaponItem weapon, Dictionary<ActionEventId, ActionEventCallbackDelegate> callbacks)
    {
        int itemId = weapon.ItemId;

        foreach ((ActionEventId eventId, ActionEventCallbackDelegate callback) in callbacks)
        {
            _actionListener.Subscribe(eventId, (eventData) => HandleActionEvent(eventData, itemId, callback));
        }
    }

    /// <summary>
    /// Plays player model animations for main hand and off hand items.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="mainHand"></param>
    /// <param name="playIdleAnimation"></param>
    /// <param name="idleAnimationDelay">If <see cref="null"/> - default value is used, if <see cref="TimeSpan.Zero"/> - idle animation wont play, else - idle animation will be player after specified time if no animations are played before.</param>
    public void PlayAnimation(PlayerAnimationData animation, bool mainHand = true, bool playIdleAnimation = true, TimeSpan? idleAnimationDelay = null)
    {
        if (!_mainPlayer) return;
        ResetIdleTimer(idleAnimationDelay, playIdleAnimation);

        if (mainHand)
        {
            animation.Start(entity, _animationSystem, AnimationsEaseOutTime);
            _currentMainHandAnimation = animation;
        }
        else
        {
            animation.Start(entity, _animationSystem, AnimationsEaseOutTime);
            _currentOffHandAnimation = animation;
        }
    }
    public void SetStat(string stat, float value, bool mainHand = true)
    {
        if (!_mainPlayer) return;
        if (mainHand)
        {
            _currentMainHandPlayerStats.Add(stat);
        }
        else
        {
            _currentOffHandPlayerStats.Add(stat);
        }

        _player.Stats.Set(_statCategory, stat, value);
    }

    private readonly bool _mainPlayer = false;
    private readonly ICoreClientAPI _api;
    private readonly EntityPlayer _player;
    private PlayerAnimationData _currentMainHandAnimation = new();
    private PlayerAnimationData _currentOffHandAnimation = new();
    private readonly HashSet<string> _currentMainHandPlayerStats = new();
    private readonly HashSet<string> _currentOffHandPlayerStats = new();
    private readonly IAnimationManagerSystem _animationSystem;
    private const string _statCategory = "melee-weapon-player-behavior";
    private readonly ActionListener _actionListener;

    private int _currentMainHandItemId = -1;
    private int _currentOffHandItemId = -1;
    private long _idleTimer = -1;
    private int _mainHandState = 0;
    private int _offHandState = 0;

    private void HandleActionEvent(ActionEventData eventData, int itemId, ActionEventCallbackDelegate callback)
    {
        int mainHandId = _player.ActiveHandItemSlot.Itemstack?.Id ?? -1;
        int offHandId = _player.LeftHandItemSlot.Itemstack?.Id ?? -1;

        if (mainHandId == itemId)
        {
            callback.Invoke(_player.ActiveHandItemSlot, _player, ref _mainHandState, eventData, true);
        }

        if (offHandId == itemId)
        {
            callback.Invoke(_player.LeftHandItemSlot, _player, ref _offHandState, eventData, false);
        }
    }
    private bool CheckIfItemsInHandsChanged()
    {
        int mainHandId = _player.ActiveHandItemSlot.Itemstack?.Id ?? -1;
        int offHandId = _player.LeftHandItemSlot.Itemstack?.Id ?? -1;
        bool anyChanged = mainHandId != _currentMainHandItemId || offHandId != _currentOffHandItemId;

        if (anyChanged && _currentMainHandItemId != mainHandId)
        {
            _mainHandState = 0;
            ProcessMainHandItemChanged();
            _currentMainHandItemId = mainHandId;
        }

        if (anyChanged && _currentOffHandItemId != offHandId)
        {
            _offHandState = 0;
            ProcessOffHandItemChanged();
            _currentOffHandItemId = offHandId;
        }

        return !anyChanged;
    }
    private void ProcessMainHandItemChanged()
    {
        _currentMainHandAnimation.Stop(entity, _animationSystem, AnimationsEaseOutTime);

        foreach (string stat in _currentMainHandPlayerStats)
        {
            _player.Stats.Remove(_statCategory, stat);
        }

        ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;

        if (stack == null || stack.Item is not MeleeWeaponItem weapon) return;

        weapon.ReadyAnimation.Start(entity, _animationSystem, AnimationsEaseOutTime);

        ResetIdleTimer();
    }
    private void ProcessOffHandItemChanged()
    {
        _currentOffHandAnimation.Stop(entity, _animationSystem, AnimationsEaseOutTime);

        foreach (string stat in _currentOffHandPlayerStats)
        {
            _player.Stats.Remove(_statCategory, stat);
        }

        ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;

        if (stack == null || stack.Item is not MeleeWeaponItem weapon) return;

        weapon.ReadyAnimationOffhand.Start(entity, _animationSystem, AnimationsEaseOutTime);

        ResetIdleTimer();
    }
    private void ResetIdleTimer(TimeSpan? idleAnimationDelay = null, bool playIdleAnimation = true)
    {
        if (_idleTimer != -1) _api.World.UnregisterCallback(_idleTimer);
        if (idleAnimationDelay == TimeSpan.Zero || !playIdleAnimation)
        {
            _idleTimer = -1;
        }
        else
        {
            _idleTimer = _api.World.RegisterCallback((dt) => PlayIdleAnimation(), (int)(idleAnimationDelay ?? DefaultIdleAnimationDelay).TotalMilliseconds);
        }
    }
    private void PlayIdleAnimation()
    {
        _idleTimer = -1;

        ItemStack? stackMainHand = _player.ActiveHandItemSlot.Itemstack;
        if (stackMainHand != null && stackMainHand.Item is MeleeWeaponItem weaponMainHand)
        {
            weaponMainHand.IdleAnimation.Start(entity, _animationSystem, AnimationsEaseOutTime);
        }

        ItemStack? stackOffHand = _player.LeftHandItemSlot.Itemstack;
        if (stackOffHand != null && stackOffHand.Item is MeleeWeaponItem weaponOffHand)
        {
            weaponOffHand.IdleAnimationOffhand.Start(entity, _animationSystem, AnimationsEaseOutTime);
        }
    }
}