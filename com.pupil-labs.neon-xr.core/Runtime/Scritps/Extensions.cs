using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            if (!task.IsCompleted || task.IsFaulted)
            {
                _ = ForgetAwaited(task);
            }

            async static Task ForgetAwaited(Task task)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        public static async Task NoThrow(this Task task, Action<Exception> onException = null, bool continueOnCapturedContext = false)
        {
            try
            {
                await task.ConfigureAwait(continueOnCapturedContext);
            }
            catch (Exception ex)
            {
                if (onException != null)
                {
                    onException(ex);
                }
            }
        }
    }
}