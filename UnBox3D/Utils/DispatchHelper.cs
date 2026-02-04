using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace UnBox3D.Utils
{
    public static class DispatcherHelper
    {
        /// <summary>
        /// Processes all UI messages currently in the message queue
        /// Some UI messages dissapear too quickly since its task was too short
        /// </summary>
        public static async Task DoEvents()
        {
            var tcs = new TaskCompletionSource<bool>();

            var dispatcher = System.Windows.Application.Current.Dispatcher;

            // Schedule a low priority action that will complete the task
            await dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => tcs.TrySetResult(true)));

            await tcs.Task;
        }
    }
}