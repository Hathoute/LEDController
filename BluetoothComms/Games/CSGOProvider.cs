using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using BluetoothComms.Bluetooth;
using CSGSI;
using CSGSI.Events;
using CSGSI.Nodes;
using Timer = System.Timers.Timer;

namespace BluetoothComms.Games {
    public class CSGOProvider : AbstractLEDProvider {

        private GameStateListener listener;

        public static int ExecuteIntervalMs = 100;

        public string PlayerName {
            get;
            set;
        }

        private Queue<Tuple<byte,Action>> pendingActions;
        private Timer executeTimer;

        public CSGOProvider(LEDController c) : base(c) {
            listener = new GameStateListener(3000);
            pendingActions = new Queue<Tuple<byte, Action>>();

            executeTimer = new Timer();
            executeTimer.Enabled = false;
            executeTimer.Interval = ExecuteIntervalMs;
            executeTimer.Elapsed += ExecuteTimerOnElapsed;
        }

        public override void Start() {
            listener.RoundBegin += ListenerOnRoundBegin;
            listener.RoundEnd += ListenerOnRoundEnd;
            listener.RoundPhaseChanged += ListenerOnRoundPhaseChanged;
            listener.NewGameState += ListenerOnNewGameState;
            listener.EnableRaisingIntricateEvents = true;

            if (!listener.Start()) {
                throw new Exception("Could not start GameStateListener");
            }

            executeTimer.Start();

            Console.WriteLine("Started listening to CSGO events.");
        }

        private void ListenerOnNewGameState(GameState gs) {
            // Set up my own actions
            //Console.WriteLine(gs.JSON);


            var previous = listener.CurrentGameState.Previously;

            // Check for my stats (Player can be the spectated player...)
            if (previous.Player.Name != "") {
                return;
            }
            if (gs.Player.Name != PlayerName) {
                return;
            }

            if (previous.Player.MatchStats.Deaths != -1 && previous.Player.MatchStats.Deaths < gs.Player.MatchStats.Deaths) {
                // -1 is null -> User deaths changed.
                ListenerOnPlayerDied();
            }
            else if (previous.Player.MatchStats.Kills != -1 &&
                     previous.Player.MatchStats.Kills < gs.Player.MatchStats.Kills) {
                ListenerOnPlayerKilled();
            }
            else if (previous.Player.State.Flashed != -1 && previous.Player.State.Flashed < gs.Player.State.Flashed) {
                ListenerOnPlayerFlashed(new PlayerFlashedEventArgs(gs.Player));
            }
            else if (previous.Player.State.Health != -1 && previous.Player.State.Health > gs.Player.State.Health) {
                ListenerOnPlayerDamaged();
            }

            var previousWeaponBulletChange = previous.Player.Weapons.Weapons.FirstOrDefault(x => x.AmmoClip != -1 && x.State == WeaponState.Undefined);
            if (previousWeaponBulletChange != null && previousWeaponBulletChange.AmmoClip > gs.Player.Weapons.ActiveWeapon.AmmoClip) {
                ListenerOnPlayerShotBullet();
            }
        }

        private Request lastRequest = new Request() {LockUntil = 0, Priority = 0};

        private bool EnsurePriority(byte priority, ushort ms) {
            var curTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (priority > lastRequest.Priority || lastRequest.LockUntil < curTime) {
                lastRequest.Priority = priority;
                lastRequest.LockUntil = curTime + ms;
                return true;
            }

            return false;
        }

        private void ListenerOnPlayerShotBullet() {
            lock (Controller) {
                if (EnsurePriority(0, 100)) {
                    Execute(1, () => Controller.SendFadeOut(200, CHSV.FromHue(42)));
                }
            }
        }

        private void ListenerOnPlayerDamaged() {
            lock (Controller) {
                if (EnsurePriority(0, 200)) {
                    Execute(2, () => Controller.SendFadeOut(200, CHSV.FromHue(0)));
                }
            }
        }

        private void ListenerOnPlayerDied() {
            lock (Controller) {
                if (EnsurePriority(3, 3000)) {
                    Execute(3, () => Controller.SendFadeOut(3000, CHSV.FromHue(0)));
                }
            }
        }

        private void ListenerOnPlayerKilled() {
            Console.WriteLine("OnKill");
            lock (Controller) {
                if (EnsurePriority(1, 1000)) {
                    Execute(4, () => Controller.SendFadeOut(1000, CHSV.FromHue(115)));
                }
            }
        }

        private void ListenerOnPlayerFlashed(PlayerFlashedEventArgs e) {
            lock (Controller) {
                var time = (ushort)(e.Flashed * 12);
                if (EnsurePriority(2, time)) {
                    Execute(5, () => Controller.SendFadeOut(time, new CHSV(0, 0, 100)));
                }
            }
        }

        private void ListenerOnRoundEnd(RoundEndEventArgs e) {
            lock (Controller) {
                if (EnsurePriority(4, 5000)) {
                    switch (e.Winner) {
                        case RoundWinTeam.CT:
                            Execute(6, () => Controller.SendFadeOut(5000, CHSV.FromHue(229)));
                            break;
                        default:
                            Execute(7, () => Controller.SendFadeOut(5000, CHSV.FromHue(45)));
                            break;
                    }
                }
            }
        }

        private void ListenerOnRoundPhaseChanged(RoundPhaseChangedEventArgs e) {
        }

        private void ListenerOnRoundBegin(RoundBeginEventArgs e) {
            lock (Controller) {
                if (EnsurePriority(4, 5000)) {
                    Execute(8, () => Controller.SendFadeOut(5000, CHSV.FromHue(306)));
                }
            }
        }

        private void Execute(byte b, Action a) {
            lock (pendingActions) {
                pendingActions.Enqueue(new Tuple<byte, Action>(b,a));
            }
        }

        private void ExecuteTimerOnElapsed(object sender, ElapsedEventArgs e) {
            lock (pendingActions) {
                if (pendingActions.Count == 0) {
                    return;
                }

                var (id, action) = pendingActions.Dequeue();
                action.Invoke();

                while (pendingActions.Count > 0 && pendingActions.Peek().Item1 == id) {
                    // Same action, no need to send duplicate...
                    pendingActions.Dequeue();
                }
            }
        }

        public override void Stop() {
            listener.Stop();
            executeTimer.Stop();

            lock (pendingActions) {
                pendingActions.Clear();
            }
        }

        class Request {
            public byte Priority;
            public long LockUntil;
        }
    }
}
