using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using System.Threading;
using Windows.UI.Core;

namespace Music
{
    class Clock
    {
        private Timer timer;
        private TextBlock clockText;
        private CoreDispatcher dispatcher;

        public Clock(TextBlock ct)
        {
            clockText = ct;
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            StartTimer();
        }

        public void StartTimer()
        {
            timer = new Timer(async (obj)  =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    clockText.Text = DateTime.Now.Hour.ToString("D2") + ":" + DateTime.Now.Minute.ToString("D2");
                });
            }, null, 0, 500);
        }
    }
}
