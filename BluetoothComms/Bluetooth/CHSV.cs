using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BluetoothComms.Bluetooth {
    public class CHSV {

        public byte Hue { get; set; }
        public byte Saturation { get; set; }
        public byte Value { get; set; }

        public CHSV(byte h, byte s, byte v) {
            Hue = h;
            Saturation = s;
            Value = v;
        }

        public static CHSV FromHue(short hue) {
            return new CHSV((byte)(hue * 255 / 360), 255, 255);
        }
    }
}
