using System;
using System.Collections.Generic;
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

    public static class IEnumerableExtensions
    {
        public static Vector2 Average(this IEnumerable<Vector2> vectors)
        {
            float x = 0f;
            float y = 0f;
            int count = 0;

            foreach (var pos in vectors)
            {
                x += pos.x;
                y += pos.y;
                count++;
            }

            return new Vector2(x / count, y / count);
        }

        public static Vector3 Average(this IEnumerable<Vector3> vectors)
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;
            int count = 0;

            foreach (var pos in vectors)
            {
                x += pos.x;
                y += pos.y;
                z += pos.z;
                count++;
            }

            return new Vector3(x / count, y / count, z / count);
        }

        public static float Average(this IEnumerable<float> values)
        {
            float x = 0f;
            int count = 0;

            foreach (var val in values)
            {
                x += val;
                count++;
            }

            return x / count;
        }
    }
}