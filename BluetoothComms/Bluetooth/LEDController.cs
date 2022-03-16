using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BluetoothComms.Audio;

namespace BluetoothComms.Bluetooth {
    public class LEDController {

        public LEDController() {

        }

        public bool Connected {
            get;
            private set;
        }

        public int TotalLeds {
            get;
            private set;
        }
        public int SpectrumLines {
            get;
            private set;
        }

        public LEDControllerMode ControllerMode {
            get;
            private set;
        }

        public delegate void ReadyHandler();
        public event ReadyHandler Ready;

        public delegate void DataReadyHandler();
        public event DataReadyHandler DataReady;

        public delegate void ModeChangedHandler(LEDControllerMode newMode);
        public event ModeChangedHandler ModeChanged;

        public bool Init() {
            BluetoothManager.Instance.InitializeClient();

            var devices = BluetoothManager.Instance.DiscoverDevices();
            var ledController = devices.FirstOrDefault(x => x.DeviceName == "LEDController");

            if (ledController is null) {
                return false;
            }

            if (!BluetoothManager.Instance.PairDevice(ledController, "1006")) {
                return false;
            }

            ControllerMode = LEDControllerMode.UNKNOWN;

            BluetoothManager.Instance.NewConnection += OnNewConnection;
            BluetoothManager.Instance.OpCodeReceived += OnOpCodeReceived;
            BluetoothManager.Instance.Connect(ledController);

            return true;
        }

        private readonly byte[] _header = {0xF0, 0xAA};
        public void SendHeader() {
            BluetoothManager.Instance.SendBytes(_header);
        }

        public void SetMode(LEDControllerMode mode) {
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.SET_MODE);
            BluetoothManager.Instance.SendByte((byte)mode);
        }


        #region Ping

        private Stopwatch stopwatch;
        public void Ping() {
            stopwatch = Stopwatch.StartNew();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.PING);
        }

        private void Pong() {
            if (stopwatch is null) {
                Console.WriteLine("Received pong while not pinging...");
                return;
            }
            stopwatch.Stop();
            Console.WriteLine("Ping: " + stopwatch.ElapsedMilliseconds);
            stopwatch = null;
        }

        #endregion

        #region Music Sync

        public void SendMusicData(byte[] data) {
            if (ControllerMode != LEDControllerMode.MUSIC_SYNC) {
                throw new Exception("Must be in Music sync mode.");
            }

            if (data.Length != SpectrumLines) {
                throw new Exception("Data length must be equal to number of spectrum lines.");
            }

            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_START);
            BluetoothManager.Instance.SendBytes(data);
        }

        #endregion

        #region Free Form

        public void SendStatic(CHSV[] data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            if (data.Length != TotalLeds) {
                throw new Exception("Data length must be equal to number of leds");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.STATIC);
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIQUE);
            BluetoothManager.Instance.SendBytes(data.SelectMany(x => new byte[] { x.Hue, x.Saturation, x.Value }).ToArray());
        }
        public void SendStatic(CHSV data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.STATIC);
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIFORM);
            BluetoothManager.Instance.SendBytes(new byte[] { data.Hue, data.Saturation, data.Value });
        }

        public void SendFadeIn(ushort ms, CHSV[] data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            if (data.Length != TotalLeds) {
                throw new Exception("Data length must be equal to number of leds");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.FADE_IN);
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(ms));
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIQUE);
            BluetoothManager.Instance.SendBytes(data.SelectMany(x => new byte[] { x.Hue, x.Saturation, x.Value }).ToArray());
        }

        public void SendFadeIn(ushort ms, CHSV data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.FADE_IN);
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(ms));
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIFORM);
            BluetoothManager.Instance.SendBytes(new byte[] { data.Hue, data.Saturation, data.Value });
        }

        public void SendFadeOut(ushort ms, CHSV[] data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            if (data.Length != TotalLeds) {
                throw new Exception("Data length must be equal to number of leds");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.FADE_OUT);
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(ms));
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIQUE);
            BluetoothManager.Instance.SendBytes(data.SelectMany(x => new byte[] { x.Hue, x.Saturation, x.Value }).ToArray());
        }

        public void SendFadeOut(ushort ms, CHSV data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.FADE_OUT);
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(ms));
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIFORM);
            BluetoothManager.Instance.SendBytes(new byte[] { data.Hue, data.Saturation, data.Value });
        }

        public void SendFadeInOut(ushort msIn, ushort msOut, CHSV[] data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            if (data.Length != TotalLeds) {
                throw new Exception("Data length must be equal to number of leds");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.FADE_IN_OUT);
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(msIn));
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(msOut));
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIQUE);
            BluetoothManager.Instance.SendBytes(data.SelectMany(x => new byte[] { x.Hue, x.Saturation, x.Value }).ToArray());
        }

        public void SendFadeInOut(ushort msIn, ushort msOut, CHSV data) {
            if (ControllerMode != LEDControllerMode.FREE_FORM) {
                throw new Exception("Must be in free form mode.");
            }

            EnsureNoActivity();
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.DATA_HSV);
            BluetoothManager.Instance.SendByte((byte)FreeFormMode.FADE_IN_OUT);
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(msIn));
            BluetoothManager.Instance.SendBytes(BitConverter.GetBytes(msOut));
            BluetoothManager.Instance.SendByte((byte)DataMode.UNIFORM);
            BluetoothManager.Instance.SendBytes(new byte[] { data.Hue, data.Saturation, data.Value });
        }

        #endregion


        // Because every led needs 30us in WS2812B, FastLED disables interrupts to keep up with this low time value.
        // This causes packet loss (sending data when interrupts are off -> no data saved to buffers)
        // To send new data, we need to cancel current processing on arduino, this is done by sending 
        // cancel opcode for longer than the disabled interrupt time, this guarantees that a packet will
        // be there by the time the interrupt is enabled again, and then we can send data without worrying about
        // packet loss.
        private int arduinoDisableInterruptsUs;
        private bool isIdle;

        // Ensures that the arduino is idle, in the sense that there will be no packet loss caused by interrupts
        // when sending the next request.
        private void EnsureNoActivity() {
            if (isIdle) {
                // We call this method because we want to send a request, obviously the arduino wont be Idle after this request...
                isIdle = false;
                return;
            }

            var stopWatch = Stopwatch.StartNew();
            long elapsedUs;
            var runs = 0;
            stopWatch.Start();
            do {
                elapsedUs = stopWatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
                if (elapsedUs > runs * arduinoDisableInterruptsUs / 10) {
                    SendHeader();
                    BluetoothManager.Instance.SendByte(LEDControllerSendCodes.CANCEL);
                    ++runs;
                }
            } while(elapsedUs < arduinoDisableInterruptsUs);
        }

        private void OnHello() {
            var b = new byte[4];
            for (var i = 0; i < 4; i++) {
                b[i] = BluetoothManager.Instance.ReadByte();
            }

            TotalLeds = BitConverter.ToUInt16(b, 0);
            SpectrumLines = BitConverter.ToUInt16(b, 2);

            arduinoDisableInterruptsUs = (int)(TotalLeds * 30 * 1.5);
            isIdle = true;

            Ready?.Invoke();
        }

        private void OnCurrentMode() {
            var newMode = (LEDControllerMode) BluetoothManager.Instance.ReadByte();
            if (newMode == ControllerMode) {
                return;
            }

            ControllerMode = newMode;
            ModeChanged?.Invoke(ControllerMode);
        }

        private void OnDataReady() {
            if (ControllerMode != LEDControllerMode.MUSIC_SYNC) {
                Console.WriteLine("Received DATA_READY while not on music sync...");
                return;
            }

            DataReady?.Invoke();
        }

        private void OnOpCodeReceived(byte opcode) {
            switch (opcode) {
                case LEDControllerReceiveCodes.PONG:
                    Pong();
                    break;
                case LEDControllerReceiveCodes.HELLO:
                    OnHello();
                    break;
                case LEDControllerReceiveCodes.DATA_READY:
                    OnDataReady();
                    break;
                case LEDControllerReceiveCodes.CUR_MODE:
                    OnCurrentMode();
                    break;
                case LEDControllerReceiveCodes.IDLE:
                    isIdle = true;
                    break;
            }
        }

        private void OnNewConnection() {
            Connected = true;
            SendHeader();
            BluetoothManager.Instance.SendByte(LEDControllerSendCodes.HELLO);
        }
    }
}
