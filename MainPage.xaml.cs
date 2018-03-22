using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using com.yoctopuce.YoctoAPI;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Raspberry_PI_Scale
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private int hardwaredetect;
        private NonReentrantDispatcherTimer timer;
        private YWeighScale sensor;
        private YDisplay display;
        private int w;
        private int h;
        private YDisplayLayer l0;
        private string unit;
        private YDisplayLayer l1;

        public MainPage()
        {
            this.InitializeComponent();
        }

        async Task sensorValueChangeCallBack(YSensor fct, string value)
        {
            await UpdateDisplay(value);
        }

        private async Task UpdateDisplay(string value)
        {
            double dval = Convert.ToDouble(value);
            string txt = String.Format("{0:F2} {1}", dval, unit);
            CurVal.Text = txt;
            if (display != null) {
                long start = DateTime.Now.Ticks;
                try {
                    await l0.clear();
                    await l0.drawText(w -1, h / 2, YDisplayLayer.ALIGN.CENTER_RIGHT, txt);
                    await display.swapLayerContent(0, 1);
                } catch (YAPI_Exception ex) {
                    await FatalError(ex.Message);
                }

                long stop = DateTime.Now.Ticks;
                long delta = (stop - start) / 1000;
                if (delta > 50) {
                    Debug.WriteLine("screen update took {0}ms", delta);
                }
            }
        }

        private async void onLoad(object sender, RoutedEventArgs e)
        {
            try {
                await YAPI.RegisterHub("usb");
                sensor = YWeighScale.FirstWeighScale();
                if (sensor == null) {
                    await FatalError("No WeighScale connected");
                }

                display = YDisplay.FirstDisplay();
                if (display != null) {
                    //clean up
                    await display.resetAll();

                    // retreive the display size
                    w = await display.get_displayWidth();
                    h = await display.get_displayHeight();

                    // reteive the first layer
                    l0 = await display.get_displayLayer(0);
                    l1 = await display.get_displayLayer(1);

                    // display a text in the middle of the screen
                    await l0.selectFont("Large.yfm");
                }

                unit = await sensor.get_unit();
                await sensor.registerValueCallback(sensorValueChangeCallBack);
            } catch (YAPI_Exception ex) {
                await FatalError(ex.Message);
            }

            timer = new NonReentrantDispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.TickTask = async () => { await dispatcherTimer_Tick(); };
            timer.Start();
        }

        private async Task FatalError(string error)
        {
            var dialog = new MessageDialog("Error:" + error);
            await dialog.ShowAsync();
            CoreApplication.Exit();
        }

        async Task dispatcherTimer_Tick()
        {
            long start = DateTime.Now.Ticks;
            try {
                await YAPI.HandleEvents();
            } catch (YAPI_Exception ex) {
                await FatalError(ex.Message);
            }

            long stop = DateTime.Now.Ticks;
            long delta = (stop - start) / 1000;
            if (delta > 50) {
                Debug.WriteLine("Long Timer handler :{0}ms", delta);
            }
        }
    }


    public class NonReentrantDispatcherTimer : DispatcherTimer
    {
        private bool IsRunning;

        public NonReentrantDispatcherTimer()
        {
            base.Tick += SmartDispatcherTimer_Tick;
        }

        async void SmartDispatcherTimer_Tick(object sender, object e)
        {
            if (TickTask == null || IsRunning) {
                return;
            }

            try {
                IsRunning = true;
                await TickTask.Invoke();
            } finally {
                IsRunning = false;
            }
        }

        public Func<Task> TickTask { get; set; }
    }
}