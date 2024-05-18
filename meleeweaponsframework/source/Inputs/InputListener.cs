/*using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public sealed class KeyInputInvoker
{
    private enum InputType
    {
        Unknown,
        KeyDown,
        KeyUp,
        KeyHold,
        MouseDown,
        MouseUp,
        MouseMove,
        MouseHold,
        Count
    }

    private readonly ICoreClientAPI _clientApi;
    private readonly static Type? rHudMouseToolsType = typeof(Vintagestory.Client.NoObf.ClientMain).Assembly.GetType("Vintagestory.Client.NoObf.HudMouseTools");
    private readonly HashSet<string> rBlockingGuiDialogs = new();
    private bool mDisposed;

    public KeyInputInvoker(ICoreClientAPI api)
    {
        _clientApi = api;
        _clientApi.Event.KeyDown += HandleKeyDown;
        _clientApi.Event.KeyUp += HandleKeyUp;
        _clientApi.Event.MouseDown += HandleMouseDown;
        _clientApi.Event.MouseUp += HandleMouseUp;
        _clientApi.Event.MouseMove += HandleMouseMove;

        mHotkeyMapper = new(api);
        mHoldButtonInvoker = new(api);

        mHoldButtonInvoker.KeyHold += HandleKeyHold;
        mHoldButtonInvoker.MouseHold += HandleMouseHold;

        for (InputType input = InputType.Unknown + 1; input < InputType.Count; input++)
        {
            mInputs.Add(input, new());
        }
    }
    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        InputType inputType = InputType.Unknown;

        if (input is IKeyInput keyInput)
        {
            inputType = GetInputType(keyInput.EventType);
            mHotkeyMapper.RegisterKeyInput(keyInput);
        }
        else if (input is IMouseInput mouseInput)
        {
            inputType = GetInputType(mouseInput.EventType);
        }

        if (mInputs.ContainsKey(inputType))
        {
            mInputs[inputType].Add(input);
            mCallbacks.Add(input, callback);
            mCollectibles.Add(input, collectible);
        }
    }

    private static InputType GetInputType(KeyEventType eventType)
    {
        return eventType switch
        {
            KeyEventType.KeyDown => InputType.KeyDown,
            KeyEventType.KeyUp => InputType.KeyUp,
            KeyEventType.KeyHold => InputType.KeyHold,
            _ => InputType.Unknown,
        };
    }
    private static InputType GetInputType(MouseEventType eventType)
    {
        return eventType switch
        {
            MouseEventType.MouseDown => InputType.MouseDown,
            MouseEventType.MouseUp => InputType.MouseUp,
            MouseEventType.MouseMove => InputType.MouseMove,
            MouseEventType.MouseHold => InputType.MouseHold,
            _ => InputType.Unknown,
        };
    }

    private void HandleKeyDown(KeyEvent eventData)
    {
        mHoldButtonInvoker.HandleKeyDown(eventData);

        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyDown])
        {
            if (input is not IKeyInput keyInput) continue;

            if (!keyInput.CheckIfShouldBeHandled(eventData, KeyEventType.KeyDown)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleKeyUp(KeyEvent eventData)
    {
        mHoldButtonInvoker.HandleKeyUp(eventData);

        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyUp])
        {
            if (input is not IKeyInput keyInput) continue;

            if (!keyInput.CheckIfShouldBeHandled(eventData, KeyEventType.KeyUp)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseDown(MouseEvent eventData)
    {
        mHoldButtonInvoker.HandleMouseDown(eventData);

        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.MouseDown])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseDown)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseUp(MouseEvent eventData)
    {
        mHoldButtonInvoker.HandleMouseUp(eventData);

        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.MouseUp])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseUp)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseMove(MouseEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.MouseMove])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseMove)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleKeyHold(KeyEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyHold])
        {
            if (input is not IKeyInput keyInput) continue;

            if (!keyInput.CheckIfShouldBeHandled(eventData, KeyEventType.KeyHold)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseHold(MouseEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.MouseHold])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseHold)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }

    private bool HandleInput(IInput input)
    {
        if (_clientApi.World?.Player == null) return false;

        if ((input as IStandardInput)?.CheckModifiers(_clientApi.World.Player, _clientApi) == false) return false;

        SlotType slotType = input.Slot;

        IEnumerable<SlotData> slots = SlotData.GetForAllSlots(slotType, mCollectibles[input], _clientApi.World.Player);

        bool handled = false;
        foreach (SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, _clientApi.World.Player, input)))
        {
            handled = true;
        }

        return handled;
    }

    private bool EventShouldBeHandled()
    {
        foreach (GuiDialog item in _clientApi.Gui.OpenedGuis)
        {
            if (item is HudElement) continue;
            if (item.GetType().IsAssignableFrom(rHudMouseToolsType)) continue;
            if (item is Vintagestory.GameContent.GuiDialogWorldMap) continue;

            if (!rBlockingGuiDialogs.Contains(item.DebugName))
            {
                _clientApi.Logger.Debug("[FSMlib] [InputManager] [ClientIfEventShouldBeHandled()] Input was not handled due to opened: " + item.DebugName);
                rBlockingGuiDialogs.Add(item.DebugName);
            }

            return false;
        }

        if (_clientApi.IsGamePaused)
        {
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        if (!mDisposed)
        {
            _clientApi.Event.KeyDown -= HandleKeyDown;
            _clientApi.Event.KeyUp -= HandleKeyUp;
            _clientApi.Event.MouseDown -= HandleMouseDown;
            _clientApi.Event.MouseUp -= HandleMouseUp;
            _clientApi.Event.MouseMove -= HandleMouseMove;

            mHotkeyMapper.Dispose();

            mDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}*/