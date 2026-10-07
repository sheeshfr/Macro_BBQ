using System;

namespace AutoClicker.Services;

public interface IGlobalInputHook : IDisposable
{
    int HotkeyVkCode { get; set; }
    RecordingSlot CurrentRecordingSlot { get; set; }
    bool IsRecording { get; set; }
    bool BlockHotkey { get; set; }
    bool StopOnEscape { get; set; }

    event Action<int>? HotkeyRecorded;
    event Action? RecordingCancelled;
    event Action<RecordingSlot, int>? SlotRecorded;
    event Action<RecordingSlot>? SlotCancelled;
    event Action? HotkeyDown;
    event Action? HotkeyUp;
    event Action? EmergencyStopTriggered;

    void ResetKeyState();
}
