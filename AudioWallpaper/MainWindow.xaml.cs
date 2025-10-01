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
using System.Security.Principal;

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
        int fs = 48000;
        int availableCalledCount = 0;
        
        private float[] currentSpectrum;
        private float[] targetSpectrum;
        private readonly object spectrumLock = new object();
        private const float smoothingFactor = 0.3f;
        private DateTime lastUpdateTime = DateTime.Now;
        
        private const float minFreq = 30f;
        private const float maxFreq = 10000f;
        private bool useLogScale = true;

        public MainWindow()
        {
            InitializeComponent();
            
            this.Loaded += MainWindow_Loaded;

            PerSec = new DispatcherTimer();
            PerSec.Interval = TimeSpan.FromMilliseconds(1000);
            PerSec.Tick += Debug;
            PerSec.Start();
            instance = this;
            visualizerRects = new VisualizerRect[detail];
            currentSpectrum = new float[detail];
            targetSpectrum = new float[detail];

            //FPSを決定します。ただし指定された値より実際のFPS値は低くなるようです。
            p = fpsMode == 1 ? 1.0 / 90 : 1.0 / 30.0;
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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GetWorkerW();
            //workerW = new IntPtr(0xD0434);
            
            if (workerW != IntPtr.Zero)
            {
                var handle = new WindowInteropHelper(this).Handle;
                var result = InteropModule.SetParent(handle, workerW);
                
                if (result != IntPtr.Zero)
                {
                    Console.WriteLine($"Successfully set parent to WorkerW. Previous parent: {result:X}");
                    InteropModule.SetWindowPos(handle, new IntPtr(InteropModule.HWND_BOTTOM), 0, 0, 0, 0,
                        InteropModule.SWP_NOMOVE | InteropModule.SWP_NOSIZE);
                }
                else
                {
                    Console.WriteLine($"Failed to set parent. Error: {Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                Console.WriteLine("WorkerW not found - running as normal window");
            }
        }

        List<double> recorded = new List<double>();
        float[] result;

        private int GetFrequencyBinForBar(int barIndex, int totalBins)
        {
            if (!useLogScale)
            {
                return barIndex * totalBins / detail / 4;
            }
            
            float logMin = (float)Math.Log10(minFreq);
            float logMax = (float)Math.Log10(maxFreq);
            
            float logFreq = logMin + (logMax - logMin) * barIndex / detail;
            float freq = (float)Math.Pow(10, logFreq);
            
            float nyquist = fs / 2f;
            int bin = (int)(freq * totalBins / nyquist);
            
            return Math.Min(Math.Max(bin, 0), totalBins - 1);
        }

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
                    c[i].X = (float)(recorded[i] * FastFourierTransform.BlackmannHarrisWindow(i, windowSize));
                    c[i].Y = 0.0f;
                }

                

                FastFourierTransform.FFT(true, (int)Math.Log(fftLength, 2), c);

                result = new float[windowSize / 2];
                for (int i = 0; i < windowSize / 2; i++)
                {
                    double diagonal = Math.Sqrt(c[i].X * c[i].X + c[i].Y * c[i].Y);
                    result[i] =(float) diagonal;
                }
                
                lock (spectrumLock)
                {
                    if (useLogScale)
                    {
                        for (int i = 0; i < detail; i++)
                        {
                            int startBin = GetFrequencyBinForBar(i, result.Length);
                            int endBin = GetFrequencyBinForBar(i + 1, result.Length);
                            
                            if (startBin >= endBin) endBin = startBin + 1;
                            
                            float sum = 0;
                            int count = 0;
                            for (int j = startBin; j < Math.Min(endBin, result.Length); j++)
                            {
                                sum += result[j];
                                count++;
                            }
                            if (count > 0)
                            {
                                float avgValue = sum / count;
                                float scaleFactor = 10000f * (1.0f + i * 0.005f);
                                float newValue = avgValue * scaleFactor;
                                targetSpectrum[i] = targetSpectrum[i] * 0.4f + newValue * 0.6f;
                            }
                        }
                    }
                    else
                    {
                        int binSize = result.Length / detail / 4;
                        for (int i = 0; i < detail; i++)
                        {
                            float sum = 0;
                            int count = 0;
                            for (int j = i * binSize; j < Math.Min((i + 1) * binSize, result.Length); j++)
                            {
                                sum += result[j];
                                count++;
                            }
                            if (count > 0)
                            {
                                float newValue = (sum / count) * 10000;
                                targetSpectrum[i] = targetSpectrum[i] * 0.4f + newValue * 0.6f;
                            }
                        }
                    }
                    lastUpdateTime = DateTime.Now;
                }
                recorded.Clear();
            }
        }
        IntPtr workerW = IntPtr.Zero;
        private void GetWorkerW()
        {
            IntPtr progman = InteropModule.FindWindow("Progman", null);
            IntPtr smtResult = IntPtr.Zero;

            InteropModule.SendMessageTimeout(progman,
                0x52C,
                new IntPtr(0),
                IntPtr.Zero,
                0x0,
                1000,
                out smtResult);

            InteropModule.EnumWindows(new InteropModule.EnumWindowDelegate((topHandle, topparams) =>
            {
                IntPtr shell = InteropModule.FindWindowEx(topHandle,
                    IntPtr.Zero,
                    "SHELLDLL_DefView",
                    null);
                if (shell != IntPtr.Zero)
                {
                    workerW = InteropModule.FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                }

                return true;
            }), IntPtr.Zero);
            
            Console.WriteLine($"Final WorkerW handle: {workerW:X}");
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
            //DebugText.Text = $"DataAvailableCount/s : {availableCalledCount}";
            availableCalledCount = 0;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) this.Close();
            if (e.Key == Key.F || e.Key == Key.F11) setFullScreen();
            if (e.Key == Key.L)
            {
                useLogScale = !useLogScale;
                Console.WriteLine($"Scale mode: {(useLogScale ? "Logarithmic" : "Linear")}");
            }
        }

        private void animTick(object sender, EventArgs e)
        {
            lock (spectrumLock)
            {
                float lerpFactor = 0.85f;
                
                for (int i = 0; i < detail; i++)
                {
                    float diff = targetSpectrum[i] - currentSpectrum[i];
                    if (diff > 0)
                    {
                        float attackSpeed = Math.Min(0.95f, 0.85f + Math.Abs(diff) * 0.0001f);
                        currentSpectrum[i] = currentSpectrum[i] + diff * attackSpeed;
                    }
                    else
                    {
                        float releaseSpeed = 0.45f;
                        if (currentSpectrum[i] < targetSpectrum[i] * 1.5f)
                        {
                            releaseSpeed = 0.35f;
                        }
                        currentSpectrum[i] = currentSpectrum[i] + diff * releaseSpeed;
                    }
                    
                    visualizerRects[i].setTargetValue(currentSpectrum[i]);
                    visualizerRects[i].animTick();
                }
            }
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (workerW != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                InteropModule.SetParent(helper.Handle, workerW);
            }
        }
    }
}
