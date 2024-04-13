using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ProjectileWeaponsFramework;

public enum ActionState
{
    Inactive,
    Pressed,
    Active,
    Hold,
    Released
}

public readonly struct ActionEventId
{
    public readonly EnumEntityAction Action;
    public readonly ActionState State;

    public ActionEventId(EnumEntityAction action, ActionState state)
    {
        Action = action;
        State = state;
    }
}

public readonly struct ActionEventData
{
    public readonly ActionEventId Action;
    public readonly IEnumerable<EnumEntityAction> Modifiers;

    public ActionEventData(ActionEventId action, IEnumerable<EnumEntityAction> modifiers)
    {
        Action = action;
        Modifiers = modifiers;
    }
}

public sealed class ActionListener : IDisposable
{
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
    public bool IsActive(EnumEntityAction action)
    {
        return _actionStates[action] != ActionState.Inactive && _actionStates[action] != ActionState.Released;
    }
    public void Subscribe(ActionEventId action, Action<ActionEventData> callback)
    {
        if (!_subscriptions.ContainsKey(action))
        {
            _subscriptions[action] = new();
        }

        _subscriptions[action].Add(callback);
        _actionsToTrack.Add(action.Action);
    }
    public void Dispose()
    {
        _clientApi.Input.InWorldAction -= OnEntityAction;
    }


    private readonly Dictionary<ActionEventId, List<Action<ActionEventData>>> _subscriptions = new();
    private readonly HashSet<EnumEntityAction> _actionsToTrack = new();

    private readonly Dictionary<EnumEntityAction, long> _timers = new();
    private readonly Dictionary<EnumEntityAction, ActionState> _actionStates = new();

    private readonly ICoreClientAPI _clientApi;
    private readonly TimeSpan _holdDuration = TimeSpan.FromSeconds(0.5);

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

    private void OnEntityAction(EnumEntityAction action, bool on, ref EnumHandling handled)
    {
        if (!_actionsToTrack.Contains(action)) return;

        _actionStates[action] = SwitchState(action, on);

        switch (_actionStates[action])
        {
            case ActionState.Pressed:
                _clientApi.World.RegisterCallback(_ => OnHoldTimer(action), (int)_holdDuration.TotalMilliseconds);
                break;
            case ActionState.Released:
            case ActionState.Inactive:
                _clientApi.World.UnregisterCallback(_timers[action]);
                break;
        }

        CallSubscriptions(action);
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
    private void CallSubscriptions(EnumEntityAction action)
    {
        ActionEventId id = new(action, _actionStates[action]);
        ActionEventData eventData = new(id, _modifiers.Where(IsActive));
        foreach (Action<ActionEventData> callback in _subscriptions[id])
        {
            callback.Invoke(eventData);
        }
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

    
}