using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BluetoothComms.Bluetooth {
    public enum LEDControllerMode {
        UNKNOWN = 0xFF,
        FREE_FORM = 0x0,
        MUSIC_SYNC = 0x1
    }

    public enum FreeFormMode {
        STATIC = 0x0,
        FADE_IN = 0x1,
        FADE_OUT = 0x2,
        FADE_IN_OUT = 0x3
    }

    public enum DataMode {
        UNIFORM = 0x0,
        UNIQUE = 0X1
    }
}
