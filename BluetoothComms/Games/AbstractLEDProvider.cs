using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BluetoothComms.Bluetooth;

namespace BluetoothComms.Games {
    public abstract class AbstractLEDProvider {

        public LEDController Controller {
            get;
        }

        public AbstractLEDProvider(LEDController c) {
            Controller = c;
        }

        public abstract void Start();

        public abstract void Stop();
    }
}
