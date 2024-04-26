﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MeleeWeaponsFramework;

/// <summary>
/// Represents <see cref="EnumEntityAction"/> status for a on client side for the player running this client.
/// </summary>
public enum ActionState
{
    /// <summary>
    /// Action is 'off', called every tick
    /// </summary>
    Inactive,
    /// <summary>
    /// Action was started this tick
    /// </summary>
    Pressed,
    /// <summary>
    /// Action is 'on', called every tick
    /// </summary>
    Active,
    /// <summary>
    /// Action was active for more than <see cref="ActionListener.HoldDuration"/> this tick once
    /// </summary>
    Hold,
    /// <summary>
    /// Action was stopped this tick
    /// </summary>
    Released
}

/// <summary>
/// Represents event that can be triggered by <see cref="ActionListener"/> when action is in specified state
/// </summary>
public readonly struct ActionEventId
{
    /// <summary>
    /// Action this event is related to
    /// </summary>
    public readonly EnumEntityAction Action;
    /// <summary>
    /// Action status
    /// </summary>
    public readonly ActionState State;

    public ActionEventId(EnumEntityAction action, ActionState state)
    {
        Action = action;
        State = state;
    }
}

/// <summary>
/// Represents data that is passed to <see cref="ActionListener"/> subscribers on action
/// </summary>
public readonly struct ActionEventData
{
    /// <summary>
    /// Action and its status
    /// </summary>
    public readonly ActionEventId Action;
    /// <summary>
    /// Current active actions
    /// </summary>
    public readonly IEnumerable<EnumEntityAction> Modifiers;

    public ActionEventData(ActionEventId action, IEnumerable<EnumEntityAction> modifiers)
    {
        Action = action;
        Modifiers = modifiers;
    }
}

/// <summary>
/// Listen for action change status events, tracks action states and calls subscriptions on specified action in specified state
/// </summary>
public sealed class ActionListener : IDisposable
{
    /// <summary>
    /// Time before action is considered to be in <see cref="ActionState.Hold"/> state
    /// </summary>
    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromSeconds(0.5);

    public ActionListener(ICoreClientAPI api)
    {
        _clientApi = api;
        api.Input.InWorldAction += OnEntityAction;

        foreach (EnumEntityAction action in Enum.GetValues<EnumEntityAction>())
        {
            _actionStates[action] = ActionState.Inactive;
            _timers[action] = new();
        }
    }

    /// <summary>
    /// Determines whether the specified action is currently 'on', i.e. in <see cref="ActionState.Pressed"/>, <see cref="ActionState.Active"/> or <see cref="ActionState.Hold"/> state.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <param name="asModifier">Specifies if the action is being used as a modifier, i.e. should account for "separateCtrlKeyForMouse" setting</param>
    /// <returns>True if the action is active, false otherwise.</returns>
    public bool IsActive(EnumEntityAction action, bool asModifier)
    {
        if (asModifier && _modifiers.Contains(action) && !_clientApi.Settings.Bool.Get("separateCtrlKeyForMouse"))
        {
            return IsActive(action) || IsActive(_modifiersRemapping[action]);
        }
        else
        {
            return IsActive(action);
        }
    }
    /// <summary>
    /// Determines whether the specified action is currently 'on', i.e. in <see cref="ActionState.Pressed"/>, <see cref="ActionState.Active"/> or <see cref="ActionState.Hold"/> state. Not affected by "separateCtrlKeyForMouse" setting.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns>True if the action is active, false otherwise.</returns>
    public bool IsActive(EnumEntityAction action)
    {
        return _actionStates[action] != ActionState.Inactive && _actionStates[action] != ActionState.Released;
    }
    /// <summary>
    /// Subscribes to specified action being in specified state.
    /// </summary>
    /// <param name="action">Action and its state that will trigger the event.</param>
    /// <param name="callback">The callback method to be invoked when the event occurs.</param>
    public void Subscribe(ActionEventId action, System.Func<ActionEventData, bool> callback)
    {
        if (!_subscriptions.ContainsKey(action))
        {
            _subscriptions[action] = new();
        }

        _subscriptions[action].Add(callback);
    }
    /// <summary>
    /// Unsubscribes from InputAPI events
    /// </summary>
    public void Dispose()
    {
        _clientApi.Input.InWorldAction -= OnEntityAction;
    }

    private readonly Dictionary<ActionEventId, List<System.Func<ActionEventData, bool>>> _subscriptions = new();
    private readonly HashSet<EnumEntityAction> _tyronDecidedToMakeThisActionsInconsistent_ThanksTyron = new()
    {
        EnumEntityAction.InWorldLeftMouseDown,
        EnumEntityAction.InWorldRightMouseDown
    };
    private readonly Dictionary<EnumEntityAction, long> _timers = new();
    private readonly Dictionary<EnumEntityAction, ActionState> _actionStates = new();
    private readonly Dictionary<EnumEntityAction, EnumEntityAction> _modifiersRemapping = new()
    {
        { EnumEntityAction.ShiftKey, EnumEntityAction.Sneak },
        { EnumEntityAction.CtrlKey, EnumEntityAction.Sprint }
    };
    private readonly HashSet<EnumEntityAction> _modifiers = new()
    {
        EnumEntityAction.ShiftKey,
        EnumEntityAction.CtrlKey,
    };
    private readonly ICoreClientAPI _clientApi;

    private void OnEntityAction(EnumEntityAction action, bool on, ref EnumHandling handled)
    {
        if (_tyronDecidedToMakeThisActionsInconsistent_ThanksTyron.Contains(action))
        {
            OnEntityActionInconsistent(action, ref handled);
            return;
        }
        
        _actionStates[action] = SwitchState(action, on);

        switch (_actionStates[action])
        {
            case ActionState.Pressed:
                _clientApi.World.RegisterCallback(_ => OnHoldTimer(action), (int)HoldDuration.TotalMilliseconds);
                break;
            case ActionState.Released:
            case ActionState.Inactive:
                _clientApi.World.UnregisterCallback(_timers[action]);
                break;
        }

        if (CallSubscriptions(action))
        {
            handled = EnumHandling.Handled;
        }
    }
    private void OnEntityActionInconsistent(EnumEntityAction action, ref EnumHandling handled)
    {
        _actionStates[action] = SwitchStateInconsistent(action);

        if (CallSubscriptions(action))
        {
            handled = EnumHandling.Handled;
        }

        _actionStates[action] = SwitchStateInconsistent(action);
    }
    private void OnHoldTimer(EnumEntityAction action)
    {
        _actionStates[action] = _actionStates[action] switch
        {
            ActionState.Pressed => ActionState.Hold,
            _ => _actionStates[action]
        };

        CallSubscriptions(action);
    }
    private bool CallSubscriptions(EnumEntityAction action)
    {
        ActionState state = _actionStates[action];

        bool handled = CallSubscriptionsForState(action, state);

        if (_actionStates[action] == ActionState.Hold || _actionStates[action] == ActionState.Pressed)
        {
            if (CallSubscriptionsForState(action, ActionState.Active))
            {
                handled = true;
            }
        }

        if (_actionStates[action] == ActionState.Released)
        {
            if (CallSubscriptionsForState(action, ActionState.Inactive))
            {
                handled = true;
            }
        }

        return handled;
    }
    private bool CallSubscriptionsForState(EnumEntityAction action, ActionState state)
    {
        ActionEventId id = new(action, state);

        if (!_subscriptions.TryGetValue(id, out List<System.Func<ActionEventData, bool>>? value)) return false;

        bool handled = false;
        
        ActionEventData eventData = new(id, _modifiers.Where(IsActive));
        foreach (System.Func<ActionEventData, bool> callback in value)
        {
            if (callback.Invoke(eventData))
            {
                handled = true;
            }
        }

        return handled;
    }
    private ActionState SwitchState(EnumEntityAction action, bool on)
    {
        return (on, _actionStates[action]) switch
        {
            (true, ActionState.Inactive) => ActionState.Pressed,
            (true, ActionState.Pressed) => ActionState.Active,
            (true, ActionState.Active) => ActionState.Active,
            (true, ActionState.Hold) => ActionState.Active,
            (true, ActionState.Released) => ActionState.Pressed,

            (false, ActionState.Inactive) => ActionState.Inactive,
            (false, ActionState.Pressed) => ActionState.Released,
            (false, ActionState.Active) => ActionState.Released,
            (false, ActionState.Hold) => ActionState.Released,
            (false, ActionState.Released) => ActionState.Inactive,
            _ => ActionState.Inactive
        };
    }
    private ActionState SwitchStateInconsistent(EnumEntityAction action)
    {
        return (true, _actionStates[action]) switch
        {
            (true, ActionState.Inactive) => ActionState.Pressed,
            (true, ActionState.Pressed) => ActionState.Inactive,
            (true, ActionState.Active) => ActionState.Inactive,
            (true, ActionState.Hold) => ActionState.Inactive,
            (true, ActionState.Released) => ActionState.Inactive,

            (false, ActionState.Inactive) => ActionState.Inactive,
            (false, ActionState.Pressed) => ActionState.Inactive,
            (false, ActionState.Active) => ActionState.Inactive,
            (false, ActionState.Hold) => ActionState.Inactive,
            (false, ActionState.Released) => ActionState.Inactive,
            _ => ActionState.Inactive
        };
    }
}