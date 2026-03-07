using System;

namespace UnBox3D.Utils
{
    public static class ToastService
    {
        public static event Action<string, bool>? ToastRequested;

        public static void Show(string message, bool isError = false)
            => ToastRequested?.Invoke(message, isError);
    }
}
