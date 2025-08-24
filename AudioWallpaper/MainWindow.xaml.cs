using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NAudio.Wave;
using System.Threading;
using System.Diagnostics;
using NAudio.Dsp;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AudioWallpaper
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow instance { get; private set; }

        public int detail { get; private set; } = 128;
        VisualizerRect[] visualizerRects;
        bool isFullScreen = false;
        int a = 0;
        private int fpsMode = 1;
        private double p;
        DispatcherTimer animTimer;
        DispatcherTimer PerSec;
        WasapiLoopbackCapture capture;
        readonly int fftLength = 2048;
        int fs = 24000;
        int availableCalledCount = 0;

        public MainWindow()
        {
            InitializeComponent();

            PerSec = new DispatcherTimer();
            PerSec.Interval = TimeSpan.FromMilliseconds(1000);
            PerSec.Tick += Debug;
            PerSec.Start();
            instance = this;
            visualizerRects = new VisualizerRect[detail];

            p = fpsMode == 1 ? 1.0 / 60.0 : 1.0 / 30.0;
            for (int i = 0; i < visualizerRects.Length; i++)
            {
                visualizerRects[i] = new VisualizerRect(i);
            }

            WaveFormat waveFormat = new WaveFormat(fs, 1);
            capture = new WasapiLoopbackCapture();
            capture.WaveFormat = waveFormat;
            capture.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded == 0) return;
                float data;
                for (int i = 0; i < e.BytesRecorded; i += capture.WaveFormat.BlockAlign)
                {
                    //リトルエンディアンの並びで合成
                    short sample = ((short)(e.Buffer[i + 1] << 8 | e.Buffer[i + 0]));
                    //最大値が1.0fになるようにする
                    data = sample / 32768f;
                    processSample(data);
                }
                availableCalledCount++;
            };

            capture.RecordingStopped += (s, a) =>
            {
                capture.Dispose();
                Console.WriteLine("Disposed");
            };

            animTimer = new DispatcherTimer();
            animTimer.Interval = TimeSpan.FromSeconds(p);
            animTimer.Tick += animTick;
            animTimer.Start();
            capture.StartRecording();

            Visualizer.Loaded += (s, e) =>
            {
                int width =(int) D3DImgHost.ActualWidth;
                int height = (int)D3DImgHost.ActualHeight;
                var win = GetWindow(this);
                D3DImg.WindowOwner = new WindowInteropHelper(win).Handle;

            };
            Visualizer.SizeChanged += (s, e) =>
            {
                
            };
        }

        List<double> recorded = new List<double>();
        float[] result;

        private void processSample(float s)
        {
            var windowSize = fftLength;
            recorded.Add(s);
            if (recorded.Count >= windowSize)
            {
                Complex[] c = new Complex[windowSize];

                for (int i = 0; i < windowSize; i++)
                {
                    c[i] = new Complex();
                    c[i].X = (float)(recorded[i] * FastFourierTransform.HammingWindow(i, windowSize));
                    c[i].Y = 0.0f;
                }

                FastFourierTransform.FFT(true, (int)Math.Log(fftLength, 2), c);

                result = new float[windowSize / 2];
                for (int i = 0; i < windowSize / 2; i++)
                {
                    double diagonal = Math.Sqrt(c[i].X * c[i].X + c[i].Y * c[i].Y);
                    result[i] =(float) diagonal;
                }
                var tasks = new Task[visualizerRects.Length];
                int ir = 0;
                foreach(var rect in visualizerRects)
                {
                    rect.val = result[ir] * 10000;
                    tasks[ir++] = Dispatcher.InvokeAsync(() => rect.animTick()).Task;
                }
                recorded.Clear();
            }
        }

        private float[] fft(float[] sdata)
        {
            var fftsample = new Complex[sdata.Length];
            var res = new float[sdata.Length / 2];

            for (int i = 0; i < sdata.Length; i++)
            {
                fftsample[i].X = (float)(sdata[i] * FastFourierTransform.HammingWindow(i, sdata.Length));
                fftsample[i].Y = 0.0f;
            }

            int m = (int) Math.Log(sdata.Length, 2);
            FastFourierTransform.FFT(true, m, fftsample);

            for (int i = 0; i < sdata.Length / 2; i++)
            {
                res[i] = (float)Math.Sqrt(fftsample[i].X * fftsample[i].X + fftsample[i].Y * fftsample[i].Y);

                double intensityDB = 10.0 * Math.Log10(res[i]);

                const double minDB = -60.0;
                double ps = (intensityDB < minDB) ? 1.0 : intensityDB / minDB;
                Console.WriteLine(res[i]);
            }

            return res;
        }

        private void Debug(object sender, EventArgs e)
        {
            DebugText.Text = $"DataAvailableCount/s : {availableCalledCount}";
            availableCalledCount = 0;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) this.Close();
            if (e.Key == Key.F || e.Key == Key.F11) setFullScreen();
        }

        private void animTick(object sender, EventArgs e)
        {
            if (result == null) return;
            if (result.Length == 0 || result.Length < visualizerRects.Length) return;
            
        }

        private void setFullScreen()
        {
            isFullScreen = !isFullScreen;
            WindowState = isFullScreen ? WindowState.Maximized : WindowState.Normal;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (VisualizerRect rect in visualizerRects)
            {
                rect.respondToResize();
            }
            //double b = ActualWidth / 16;
            //Visualizer.Margin = new Thickness(b, b, b, b);
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0)
            {
                a++;
            } else
            {
                a--;
            }
            for (int i = 0; i < visualizerRects.Length; i++)
            {
                double b = a;
                visualizerRects[i].val = b / (i + 1);
                visualizerRects[i].animTick();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;

            this.DragMove();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            capture.StopRecording();
        }
    }
}
