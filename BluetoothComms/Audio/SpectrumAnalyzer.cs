using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exception = System.Exception;

namespace BluetoothComms.Audio {
    public class SpectrumAnalyzer {

        public static byte[] GetSpectrum(float[] data, int spectrumLines) {
            int x, y;
            var b0 = 0;
            var spectrumData = new byte[spectrumLines];

            //computes the spectrum data, the code is taken from a bass_wasapi sample.
            for (x = 0; x < spectrumLines; x++) {
                float peak = 0;
                var b1 = (int)Math.Pow(2, x * 10.0 / (spectrumLines - 1));
                if (b1 > 1023) b1 = 1023;
                if (b1 <= b0) b1 = b0 + 1;
                for (; b0 < b1; b0++) {
                    if (peak < data[1 + b0]) peak = data[1 + b0];
                }
                y = (int)(Math.Sqrt(peak) * 3 * 255 - 4);
                if (y > 255) y = 255;
                if (y < 0) y = 0;
                spectrumData[x] = (byte)y;
            }

            return spectrumData;
        }

        public static byte[] ProcessSpectrum(byte[] spectrum) {
            var newSpectrum = new byte[spectrum.Length];
            newSpectrum[0] = spectrum[1];

            var i = 1;
            var j = 1;
            while (i < spectrum.Length) {
                newSpectrum[i] =
                    (byte)Math.Ceiling(spectrum[j] + (spectrum[j + 1] - spectrum[j]) * ((i - 1d) % 3) / 3);
                newSpectrum[i] = (byte) Math.Max(5, Math.Pow((double) newSpectrum[i] / 255, 1.3) * 255);
                i++;
                if (i % 3 == 1) {
                    j++;
                }
            }

            return newSpectrum;
        }
    }
}
