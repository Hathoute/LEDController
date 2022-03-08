using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BluetoothComms.Bluetooth {
    public class LEDControllerSendCodes {

        public const byte PING = 0x1;
        public const byte HELLO = 0x2;
        public const byte DATA_START = 0x3;
        public const byte DATA_RGB = 0x4;
        public const byte DATA_HSV = 0x5;
        public const byte SET_MODE = 0x6;
        public const byte CANCEL = 0x7;
    }

    public class LEDControllerReceiveCodes {

        public const byte PONG = 0x1;
        public const byte HELLO = 0x2;
        public const byte DATA_READY = 0x3;
        public const byte CUR_MODE = 0x4;
        public const byte IDLE = 0x5;
    }
}
