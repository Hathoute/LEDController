using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BluetoothComms.Bluetooth {
    public class BluetoothManager {

        public static BluetoothManager Instance {
            get;
        } = new BluetoothManager();


        public bool Initialized {
            get;
            private set;
        }

        public BluetoothClient Client {
            get;
            private set;
        }

        public delegate void NewConnectionHandler();
        public NewConnectionHandler NewConnection;

        public delegate void OpCodeReceivedHandler(byte opCode);
        public OpCodeReceivedHandler OpCodeReceived;

        public void InitializeClient() {
            if (Initialized) {
                return;
            }

            var addr = GetBluetoothMacAddress();
            if (addr is null) {
                throw new Exception("Cannot find a bluetooth device.");
            }

            Client = CreateClient(addr);
            Initialized = true;
        }

        public IEnumerable<BluetoothDeviceInfo> DiscoverDevices() {
            EnsureInitialized();

            return Client.DiscoverDevices(255, true, true, true, true);
        }

        public bool PairDevice(BluetoothDeviceInfo device, string devicePin = "") {
            EnsureInitialized();

            Client.SetPin(devicePin);
            if (device.Authenticated) {
                // Device already paired...
                return true;
            }

            return BluetoothSecurity.PairRequest(device.DeviceAddress, devicePin);
        }

        private Thread receiveThread;
        public void Connect(BluetoothDeviceInfo device) {
            EnsureInitialized();

            Client.BeginConnect(device.DeviceAddress, BluetoothService.SerialPort, ar => OnNewConnection(), device);
        }

        private void OnNewConnection() {
            receiveThread = new Thread(() => {
                while (true) {
                    if (!Client.Connected) {
                        receiveThread.Abort();
                    }

                    var opCode = (byte)Client.GetStream().ReadByte();
                    OpCodeReceived?.Invoke(opCode);
                }
            });

            receiveThread.Start();

            NewConnection?.Invoke();
        }

        public void SendString(string str) {
            EnsureInitialized(true);

            var stream = Client.GetStream();
            var data = Encoding.ASCII.GetBytes(str).ToArray();
            stream.Write(data, 0, data.Length);
        }

        public void SendByte(byte b) {
            EnsureInitialized(true);

            Client.GetStream().WriteByte(b);
        }

        public void SendBytes(byte[] b) {
            EnsureInitialized(true);

            Client.GetStream().Write(b, 0, b.Length);
        }

        public byte ReadByte() {
            EnsureInitialized(true);

            return (byte)Client.GetStream().ReadByte();
        }

        private void EnsureInitialized(bool connected = false) {
            if (!Initialized) {
                throw new Exception("BluetoothManager must be initialized before calling methods.");
            }

            if (connected && !Client.Connected) {
                throw new Exception("Client is not connected to a remote host.");
            }
        }

        private BluetoothAddress GetBluetoothMacAddress() {
            return (from nic in NetworkInterface.GetAllNetworkInterfaces()
                where nic.Name.Contains("Bluetooth") || nic.Description.Contains("Bluetooth")
                select nic.GetPhysicalAddress() into physical
                select BluetoothAddress.Parse(physical.ToString())).FirstOrDefault();
        }

        private BluetoothClient CreateClient(BluetoothAddress addr) {
            var localEndpoint = new BluetoothEndPoint(addr, BluetoothService.SerialPort);

            return new BluetoothClient(localEndpoint);
        }

    }
}