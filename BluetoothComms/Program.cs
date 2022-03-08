using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BluetoothComms.Audio;
using BluetoothComms.Bluetooth;
using BluetoothComms.Games;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BluetoothComms {
    class Program {
        private static LEDController ledController;

        static void Main(string[] args) {
            var deviceId = AudioManager.ShowAndSelectDevice();

            AudioManager.Instance.Initialize(deviceId);
            AudioManager.Instance.DataAvailable += OnDataAvailable;

            ledController = new LEDController();
            ledController.Ready += () => {
                Console.WriteLine("LEDController ready.");
                dataReady = true;
                AudioManager.SpectrumLines = ledController.SpectrumLines;
            };
            ledController.DataReady += () => {
                dataReady = true;
            };
            ledController.ModeChanged += LedControllerOnModeChanged;

            ledController.Init();

            string cmd;
            Console.Write("Command: ");
            while ((cmd = Console.ReadLine()) != "quit") {
                if (!ledController.Connected) {
                    Console.WriteLine("Wait for connection.");
                    continue;
                }

                switch (cmd) {
                    case "ping":
                        ledController.Ping();
                        break;
                    case "music":
                        ledController.SetMode(LEDControllerMode.MUSIC_SYNC);
                        break;
                    case "freeform":
                        ledController.SetMode(LEDControllerMode.FREE_FORM);
                        break;
                    case "color":
                        var v = Console.ReadLine();
                        var s = v.Split(' ').Select(x => byte.Parse(x)).ToArray();
                        var h = new CHSV(s[0], s[1], s[2]);
                        ledController.SendFadeInOut(3000, 3000, h);
                        break;
                    default:
                        Console.WriteLine("Unknown command: " + cmd);
                        break;
                }

                Console.Write("Command: ");
            }
        }

        private static AbstractLEDProvider _provider;
        private static void LedControllerOnModeChanged(LEDControllerMode newmode) {
            if (AudioManager.Instance.Capturing) {
                AudioManager.Instance.StopCapture();
            }

            _provider?.Stop();
            _provider = null;

            switch (newmode) {
                case LEDControllerMode.FREE_FORM:
                    // Use csgo now to test
                    var csgoProvider = new CSGOProvider(ledController);
                    csgoProvider.PlayerName = "[The-Whitesmith]";
                    csgoProvider.Start();
                    _provider = csgoProvider;
                    break;
                case LEDControllerMode.MUSIC_SYNC:
                    AudioManager.Instance.StartCapture();
                    dataReady = true;
                    break;
                default:
                    throw new NotImplementedException($"Controller Mode {newmode} is not implemented.");
            }
        }

        private static byte[] oldLevels;
        private static bool dataReady;
        private static void OnDataAvailable(byte[] spectrumlevels) {
            if (oldLevels is null) {
                oldLevels = spectrumlevels;
            }

            if (!dataReady) {
                return;
            }

            oldLevels = SpectrumAnalyzer.ProcessSpectrum(spectrumlevels);
            ledController.SendMusicData(oldLevels);
            dataReady = false;
        }
    }
}
