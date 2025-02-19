using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace GameTranslationOverlay.Core.OCR.Utils
{
    public static class WindowsRuntimeExtensions
    {
        public static Task<T> AsTask<T>(this IAsyncOperation<T> operation)
        {
            return Task.Run(async () => await operation);
        }

        public static Task AsTask(this IAsyncAction action)
        {
            return Task.Run(async () => await action);
        }

        public static Stream AsStream(this IRandomAccessStream windowsStream)
        {
            return windowsStream.AsStream();
        }

        public static Task<T> AsTask<T, P>(this IAsyncOperationWithProgress<T, P> operation)
        {
            return Task.Run(async () => await operation);
        }
    }
}