using Silk.NET.SDL;

namespace TheAdventure;

public unsafe class Input
{
    private readonly Sdl _sdl;

    public EventHandler<(int x, int y)>? OnMouseClick;

    public Input(Sdl sdl)
    {
        _sdl = sdl;
    }

    private ReadOnlySpan<byte> GetKeyboardState()
    {
        return new ReadOnlySpan<byte>(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
    }

    public bool IsLeftPressed()
    {
        return GetKeyboardState()[(int)KeyCode.Left] == 1;
    }

    public bool IsRightPressed()
    {
        return GetKeyboardState()[(int)KeyCode.Right] == 1;
    }

    public bool IsUpPressed()
    {
        return GetKeyboardState()[(int)KeyCode.Up] == 1;
    }

    public bool IsDownPressed()
    {
        return GetKeyboardState()[(int)KeyCode.Down] == 1;
    }

    public bool IsSpacePressed()
    {
        return GetKeyboardState()[(int)KeyCode.Space] == 1;
    }

    public bool ProcessInput()
    {
        Event ev = new Event();
        while (_sdl.PollEvent(ref ev) != 0)
        {
            if (ev.Type == (uint)EventType.Quit)
            {
                return true;
            }

            switch (ev.Type)
            {
                case (uint)EventType.Windowevent:
                {
                    switch (ev.Window.Event)
                    {
                        case (byte)WindowEventID.TakeFocus:
                        {
                            _sdl.SetWindowInputFocus(_sdl.GetWindowFromID(ev.Window.WindowID));
                            break;
                        }
                    }
                    break;
                }

                case (uint)EventType.Mousebuttondown:
                {
                    if (ev.Button.Button == (byte)MouseButton.Primary)
                    {
                        OnMouseClick?.Invoke(this, (ev.Button.X, ev.Button.Y));
                    }
                    break;
                }
            }
        }

        return false;
    }
}


