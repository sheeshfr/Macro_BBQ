using System;

namespace AutoClicker.Services;

public interface IInputSimulator : IDisposable
{
    void SendButtonDown(int vkCode);
    void SendButtonUp(int vkCode);
    void ReleaseAllButtons(int primaryVkCode, bool isModifierEnabled, int modifierVkCode);
}
