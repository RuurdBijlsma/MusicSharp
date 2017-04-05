using System;
using System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace Music
{
    internal class Clock
    {
        private readonly TextBlock clockText;
        private readonly CoreDispatcher dispatcher;
        private Timer timer;

        public Clock(TextBlock ct)
        {
            clockText = ct;
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            StartTimer();
        }

        public void StartTimer()
        {
            timer =
                new Timer(
                    async obj =>
                    {
                        await dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                            () =>
                            {
                                clockText.Text = DateTime.Now.Hour.ToString("D2") + ":" +
                                                 DateTime.Now.Minute.ToString("D2");
                            });
                    }, null, 0, 500);
        }
    }
}