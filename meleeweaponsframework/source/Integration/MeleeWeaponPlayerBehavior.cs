using AnimationManagerLib;
using AnimationManagerLib.API;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;

namespace MeleeWeaponsFramework;

public enum MeleeWeaponState
{
    Idle = 0,
    Active = 1,
    Cooldown = 2
}

public class MeleeWeaponPlayerBehavior : EntityBehavior
{
    public TimeSpan AnimationsEaseOutTime { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan DefaultIdleAnimationDelay { get; set; } = TimeSpan.FromSeconds(5);

    public delegate bool ActionEventCallbackDelegate(ItemSlot slot, EntityPlayer player, ref MeleeWeaponState state, ActionEventData eventData, bool mainHand, AttackDirection direction);

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

        _mainPlayer = (entity as EntityPlayer)?.PlayerUID == clientApi.Settings.String["playeruid"];
        _player = player;
        _api = clientApi;

        MeleeWeaponsFrameworkModSystem frameworkSystem = _api.ModLoader.GetModSystem<MeleeWeaponsFrameworkModSystem>();

        if (frameworkSystem.ActionListener is null || frameworkSystem.DirectionController is null)
        {
            throw new ArgumentException($"Action listener or direction controller is null, it may be caused by instantiating this behavior to early");
        }

        _actionListener = frameworkSystem.ActionListener;
        _animationSystem = _api.ModLoader.GetModSystem<AnimationManagerLibSystem>();
        _directionController = frameworkSystem.DirectionController;
        _meleeBlockSystem = frameworkSystem.BlockSystemClient;

        if (_mainPlayer)
        {
            RegisterWeapons();
        }
    }
    public override string PropertyName() => _statCategory;
    public override void OnGameTick(float deltaTime)
    {
        if (!_mainPlayer) return;

        SetRenderDirectionCursorForMainHand();
        _directionController.OnGameTick();
        _ = CheckIfItemsInHandsChanged();
    }

    /// <summary>
    /// Plays player model animations for main hand and off hand items.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="mainHand"></param>
    /// <param name="playIdleAnimation"></param>
    /// <param name="idleAnimationDelay">If <see cref="null"/> - default value is used, if <see cref="TimeSpan.Zero"/> - idle animation wont play, else - idle animation will be player after specified time if no animations are played before.</param>
    public void PlayAnimation(IPlayerAnimationData animation, bool mainHand = true, bool playIdleAnimation = true, TimeSpan? idleAnimationDelay = null)
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
    public void PlayAnimation(IPlayerAnimationData animation, bool mainHand = true, bool playIdleAnimation = true, TimeSpan? idleAnimationDelay = null, params RunParameters[] parameters)
    {
        if (!_mainPlayer) return;
        ResetIdleTimer(idleAnimationDelay, playIdleAnimation);

        if (mainHand)
        {
            animation.Start(entity, _animationSystem, parameters);
            _currentMainHandAnimation = animation;
        }
        else
        {
            animation.Start(entity, _animationSystem, parameters);
            _currentOffHandAnimation = animation;
        }
    }
    public void StopAnimation(bool mainHand = true, bool playIdleAnimation = true, TimeSpan? idleAnimationDelay = null, params RunParameters[] parameters)
    {
        if (!_mainPlayer) return;
        ResetIdleTimer(idleAnimationDelay, playIdleAnimation);

        if (mainHand)
        {
            _currentMainHandAnimation.Stop(entity, _animationSystem, AnimationsEaseOutTime);

            ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;
            if (stack == null || stack.Item is not IMeleeWeaponItem weapon) return;
            weapon.ReadyAnimation.Start(entity, _animationSystem, AnimationsEaseOutTime);
        }
        else
        {
            _currentOffHandAnimation.Stop(entity, _animationSystem, AnimationsEaseOutTime);

            ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;
            if (stack == null || stack.Item is not IMeleeWeaponItem weapon) return;
            weapon.ReadyAnimationOffhand.Start(entity, _animationSystem, AnimationsEaseOutTime);
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
    public void SetState(MeleeWeaponState state, bool mainHand = true)
    {
        if (!_mainPlayer) return;
        if (mainHand)
        {
            _mainHandState = state;
        }
        else
        {
            _offHandState = state;
        }
    }
    public MeleeWeaponState GetState(bool mainHand = true)
    {
        return mainHand ? _mainHandState : _offHandState;
    }

    private readonly bool _mainPlayer = false;
    private readonly ICoreClientAPI _api;
    private readonly EntityPlayer _player;
    private IPlayerAnimationData _currentMainHandAnimation = new PlayerAnimationData();
    private IPlayerAnimationData _currentOffHandAnimation = new PlayerAnimationData();
    private readonly HashSet<string> _currentMainHandPlayerStats = new();
    private readonly HashSet<string> _currentOffHandPlayerStats = new();
    private readonly IAnimationManagerSystem _animationSystem;
    private const string _statCategory = "melee-weapon-player-behavior";
    private readonly ActionListener _actionListener;
    private readonly AttackDirectionController _directionController;
    private readonly MeleeBlockSystemClient? _meleeBlockSystem;

    private IMeleeWeaponItem? _currentMainHandWeapon;
    private IMeleeWeaponItem? _currentOffHandWeapon;
    private int _currentMainHandItemId = -1;
    private int _currentOffHandItemId = -1;
    private long _idleTimer = -1;
    private MeleeWeaponState _mainHandState = 0;
    private MeleeWeaponState _offHandState = 0;

    private void RegisterWeapons()
    {
        _api.World.Items
            .OfType<IMeleeWeaponItem>()
            .Foreach(RegisterWeapon);
    }
    private void RegisterWeapon(IMeleeWeaponItem? weapon)
    {
        if (weapon is null) return;

        Dictionary<ActionEventId, List<ActionEventCallbackDelegate>> handlers = CollectHandlers(weapon);

        int itemId = weapon.WeaponItemId;

		foreach ((ActionEventId eventId, List<ActionEventCallbackDelegate> callbacks) in handlers)
        {
            callbacks.ForEach(callback =>
            {
				_actionListener.Subscribe(eventId, (eventData) => HandleActionEvent(eventData, itemId, callback));
			});
        }

        weapon.OnRegistered(this, _api);
    }
    private static Dictionary<ActionEventId, List<ActionEventCallbackDelegate>> CollectHandlers(object owner)
    {
        IEnumerable<MethodInfo> methods = owner.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(method => method.GetCustomAttributes(typeof(ActionEventHandlerAttribute), true).Any());

        Dictionary<ActionEventId, List<ActionEventCallbackDelegate>> handlers = new();
        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttributes(typeof(ActionEventHandlerAttribute), true)[0] is not ActionEventHandlerAttribute attribute) continue;

            if (Delegate.CreateDelegate(typeof(ActionEventCallbackDelegate), owner, method) is not ActionEventCallbackDelegate handler)
            {
                throw new InvalidOperationException($"Handler should have same signature as 'ActionEventCallbackDelegate' delegate.");
            }

			List<ActionEventCallbackDelegate>? callbackDelegates;
			if (handlers.TryGetValue(attribute.Event, out callbackDelegates))
            {
                callbackDelegates.Add(handler);
			}
            else
            {
				callbackDelegates = new()
				{
					handler
				};

				handlers.Add(attribute.Event, callbackDelegates);
			}
            
        }

        return handlers;
    }
    private bool HandleActionEvent(ActionEventData eventData, int itemId, ActionEventCallbackDelegate callback)
    {
        int mainHandId = _player.ActiveHandItemSlot.Itemstack?.Id ?? -1;
        int offHandId = _player.LeftHandItemSlot.Itemstack?.Id ?? -1;

        if (mainHandId == itemId)
        {
            return callback.Invoke(_player.ActiveHandItemSlot, _player, ref _mainHandState, eventData, true, _directionController.CurrentDirection);
        }

        if (offHandId == itemId)
        {
            return callback.Invoke(_player.LeftHandItemSlot, _player, ref _offHandState, eventData, false, _directionController.CurrentDirection);
        }

        return false;
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
        _meleeBlockSystem?.Stop();

        _currentMainHandWeapon?.OnDeselected(_player);
        _currentMainHandWeapon = null;

        foreach (string stat in _currentMainHandPlayerStats)
        {
            _player.Stats.Remove(_statCategory, stat);
        }

        ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;

        if (stack == null || stack.Item is not IMeleeWeaponItem weapon)
        {
            RenderingOffset.ResetOffset();
            return;
        }

        weapon.ReadyAnimation.Start(entity, _animationSystem, AnimationsEaseOutTime);
        weapon.OnSelected(_player.ActiveHandItemSlot, _player);
        _currentMainHandWeapon = weapon;

        if (stack.Item.Attributes?["fpHandsOffset"].Exists == true)
        {
            RenderingOffset.SetOffset(stack.Item.Attributes["fpHandsOffset"].AsFloat());
        }

        ResetIdleTimer();
    }
    private void ProcessOffHandItemChanged()
    {
        _currentOffHandAnimation.Stop(entity, _animationSystem, AnimationsEaseOutTime);
        _meleeBlockSystem?.Stop();

        _currentOffHandWeapon?.OnDeselected(_player);
        _currentOffHandWeapon = null;

        foreach (string stat in _currentOffHandPlayerStats)
        {
            _player.Stats.Remove(_statCategory, stat);
        }

        ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;

        if (stack == null || stack.Item is not IMeleeWeaponItem weapon) return;

        weapon.ReadyAnimationOffhand.Start(entity, _animationSystem, AnimationsEaseOutTime);
        weapon.OnSelected(_player.LeftHandItemSlot, _player);
        _currentOffHandWeapon = weapon;

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
    private void SetRenderDirectionCursorForMainHand()
    {
        ItemStack? stack = _player.ActiveHandItemSlot.Itemstack;

        if (stack == null || stack.Item is not IMeleeWeaponItem weapon)
        {
            _directionController.DirectionsConfiguration = DirectionsConfiguration.None;
            return;
        }

        _directionController.DirectionsConfiguration = weapon.DirectionsType;
    }
    private void PlayIdleAnimation()
    {
        _idleTimer = -1;

        ItemStack? stackMainHand = _player.ActiveHandItemSlot.Itemstack;
        if (stackMainHand != null && stackMainHand.Item is IMeleeWeaponItem weaponMainHand)
        {
            weaponMainHand.IdleAnimation.Start(entity, _animationSystem, AnimationsEaseOutTime);
        }

        ItemStack? stackOffHand = _player.LeftHandItemSlot.Itemstack;
        if (stackOffHand != null && stackOffHand.Item is IMeleeWeaponItem weaponOffHand)
        {
            weaponOffHand.IdleAnimationOffhand.Start(entity, _animationSystem, AnimationsEaseOutTime);
        }
    }
}