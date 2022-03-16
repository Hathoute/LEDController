using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace BluetoothComms.Audio {

    public class AudioManager {
        // Heavily relied on <url>https://www.codeproject.com/Articles/797537/Making-an-Audio-Spectrum-analyzer-with-Bass-dll-Cs</url>

        // Time between two output capture in milliseconds.
        public static double RefreshInterval = 50;

        public static int SpectrumLines = 16;

        public static AudioManager Instance {
            get;
        } = new AudioManager();

        public static int ShowAndSelectDevice() {
            var ids = new List<int>();
            for (var i = 0; i < BassWasapi.BASS_WASAPI_GetDeviceCount(); i++) {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device.IsEnabled && device.IsLoopback) {
                    Console.WriteLine((i, device.name));
                    ids.Add(i);
                }
            }

            Console.Write("\nChoose a device: ");
            int id;
            while (!int.TryParse(Console.ReadLine(), out id) || !ids.Contains(id)) {
                Console.WriteLine("Invalid choice.");
            }

            return id;
        }

        public AudioManager() {
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            if (!Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero)) {
                throw new Exception("AudioManager Initialization error...");
            }

            dataBuffer = new float[1024];

            process = new WASAPIPROC(Process);
        }

        public bool Initialized {
            get;
            private set;
        }

        public bool Capturing {
            get;
            private set;
        }

        public delegate void DataAvailableHandler(byte[] spectrumLevels);
        public DataAvailableHandler DataAvailable;

        private Thread captureThread;
        private WASAPIPROC process;
        private float[] dataBuffer;
        private int deviceId;

        public void Initialize(int deviceId) {
            if (Initialized) {
                throw new Exception("AudioManager already initialized.");
            }

            this.deviceId = deviceId;

            var result = BassWasapi.BASS_WASAPI_Init(deviceId, 0, 0,
                BASSWASAPIInit.BASS_WASAPI_BUFFER,
                1f, 0.05f,
                process, IntPtr.Zero);
            if (!result) {
                throw new Exception("Error while initializing BASS: " + Bass.BASS_ErrorGetCode());
            }

            Initialized = true;
        }

        public void StartCapture() {
            EnsureInitialized();

            BassWasapi.BASS_WASAPI_Start();
            captureThread = new Thread(() => {
                var timer = new System.Timers.Timer();
                timer.Elapsed += OnTick;
                timer.Interval = RefreshInterval;
                timer.Enabled = true;

                timer.Start();
            });

            captureThread.Start();
            Capturing = true;
        }

        public void StopCapture() {
            BassWasapi.BASS_WASAPI_Stop(true);
            Free();

            captureThread.Abort();
            Capturing = false;
        }

        private void OnTick(object sender, EventArgs e) {
            if (!Initialized) {
                return;
            }

            var ret = BassWasapi.BASS_WASAPI_GetData(dataBuffer, (int)BASSData.BASS_DATA_FFT2048);
            if (ret < 0) return;

            var spectrumData = SpectrumAnalyzer.GetSpectrum(dataBuffer, SpectrumLines);

            DataAvailable?.Invoke(spectrumData);

            CheckHang();
        }

        private int hangCount = 0;
        private int previousLevel;
        private object hangLock = new object();
        private void CheckHang() {
            lock (hangLock) {
                var level = BassWasapi.BASS_WASAPI_GetLevel();
                if (previousLevel == level && level != 0) {
                    hangCount++;
                }

                previousLevel = level;

                //Required, because some programs hang the output. If the output hangs for a 75ms
                //this piece of code re initializes the output
                //so it doesn't make a gliched sound for long.
                if (hangCount > 3) {
                    hangCount = 0;
                    Free();
                    Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                    Initialized = false;
                    Initialize(deviceId);
                }
            }
        }

        private void EnsureInitialized() {
            if (!Initialized) {
                throw new Exception("AudioManager must be initialized before calling a method.");
            }
        }

        // WASAPI callback, required for continuous recording
        private int Process(IntPtr buffer, int length, IntPtr user) {
            return length;
        } 
        
        //cleanup
        public void Free() {
            BassWasapi.BASS_WASAPI_Free();
            Bass.BASS_Free();
        }
    }
}
