// Réseau local sans serveur : l'hôte écoute en TCP, les messages sont des lignes JSON.
// La découverte des parties se fait par broadcast UDP. Aucune dépendance Unity.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Menteur.Core;

namespace Menteur.Net
{
    public static class Protocol
    {
        public const int GamePort = 4050;
        public const int DiscoveryPort = 4051;
        public const string Magic = "MENTEUR-UNITY/1";

        public static string Encode(Dictionary<string, object> m) => MiniJson.Serialize(m);

        public static Dictionary<string, object> Decode(string line)
        {
            try { return MiniJson.Deserialize(line).Obj(); }
            catch { return null; }
        }

        /// <summary>Adresses IPv4 locales (Wi-Fi / point d'accès), privées d'abord.</summary>
        public static List<IPAddress> LocalAddresses()
        {
            var result = new List<IPAddress>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        var a = ua.Address;
                        if (a.AddressFamily != AddressFamily.InterNetwork) continue;
                        var b = a.GetAddressBytes();
                        if (b[0] == 169 && b[1] == 254) continue;
                        result.Add(a);
                    }
                }
            }
            catch { }
            if (result.Count == 0)
            {
                // Secours (certains Android n'énumèrent pas les interfaces) : l'adresse
                // locale utilisée pour sortir, sans rien envoyer sur le réseau.
                try
                {
                    using (var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        probe.Connect("192.168.255.255", 65530);
                        if (probe.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address) &&
                            !ep.Address.Equals(IPAddress.Any))
                            result.Add(ep.Address);
                    }
                }
                catch { }
            }
            return result.OrderBy(a => IsPrivate(a) ? 0 : 1).ToList();
        }

        static bool IsPrivate(IPAddress a)
        {
            var b = a.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }
    }

    /// <summary>Connexion texte ligne par ligne au-dessus d'un TcpClient.</summary>
    public sealed class LineConnection
    {
        public readonly int Id;
        readonly TcpClient tcp;
        readonly StreamWriter writer;
        readonly object writeLock = new object();
        volatile bool closed;
        public DateTime LastSeen = DateTime.UtcNow;

        readonly Action<LineConnection, string> onLine;
        readonly Action<LineConnection> onClosed;
        readonly StreamReader reader;

        public LineConnection(int id, TcpClient tcp, Action<LineConnection, string> onLine, Action<LineConnection> onClosed)
        {
            Id = id;
            this.tcp = tcp;
            this.onLine = onLine;
            this.onClosed = onClosed;
            tcp.NoDelay = true;
            var stream = tcp.GetStream();
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            reader = new StreamReader(stream, Encoding.UTF8);
        }

        /// <summary>
        /// Démarre la lecture. À appeler une fois la connexion enregistrée par son propriétaire,
        /// sinon les premiers messages pourraient arriver avant l'événement de connexion.
        /// </summary>
        public LineConnection Start()
        {
            var t = new Thread(() =>
            {
                try
                {
                    string line;
                    while (!closed && (line = reader.ReadLine()) != null)
                    {
                        LastSeen = DateTime.UtcNow;
                        onLine(this, line);
                    }
                }
                catch { }
                Close();
                onClosed(this);
            }) { IsBackground = true, Name = "menteur-conn" };
            t.Start();
            return this;
        }

        public bool Send(string line)
        {
            if (closed) return false;
            try
            {
                lock (writeLock) writer.WriteLine(line);
                return true;
            }
            catch
            {
                Close();
                return false;
            }
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            try { tcp.Close(); } catch { }
        }
    }

    /// <summary>Événement réseau remonté au thread principal.</summary>
    public struct NetEvent
    {
        public enum Kind { Connected, Message, Closed }
        public Kind Type;
        public LineConnection Conn;
        public Dictionary<string, object> Msg;
    }

    /// <summary>Serveur de l'hôte.</summary>
    public sealed class LanServer
    {
        TcpListener listener;
        int nextId;
        public readonly ConcurrentQueue<NetEvent> Events = new ConcurrentQueue<NetEvent>();
        public int Port { get; private set; }

        public void Start(int port = Protocol.GamePort)
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
            }
            catch (SocketException)
            {
                listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
            }
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var t = new Thread(AcceptLoop) { IsBackground = true, Name = "menteur-accept" };
            t.Start();
        }

        void AcceptLoop()
        {
            while (true)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); }
                catch { return; }
                var conn = new LineConnection(Interlocked.Increment(ref nextId), c,
                    (cn, line) =>
                    {
                        var m = Protocol.Decode(line);
                        if (m != null) Events.Enqueue(new NetEvent { Type = NetEvent.Kind.Message, Conn = cn, Msg = m });
                    },
                    cn => Events.Enqueue(new NetEvent { Type = NetEvent.Kind.Closed, Conn = cn }));
                Events.Enqueue(new NetEvent { Type = NetEvent.Kind.Connected, Conn = conn });
                conn.Start();
            }
        }

        public void Stop()
        {
            try { listener?.Stop(); } catch { }
        }
    }

    public static class LanClient
    {
        /// <summary>Se connecte à l'hôte (lève une exception si injoignable).</summary>
        public static async Task<LineConnection> ConnectAsync(string host, int port, ConcurrentQueue<NetEvent> events,
            int timeoutMs = 5000)
        {
            var tcp = new TcpClient();
            var connect = tcp.ConnectAsync(host, port);
            if (await Task.WhenAny(connect, Task.Delay(timeoutMs)) != connect)
            {
                tcp.Close();
                throw new TimeoutException("Hôte injoignable");
            }
            await connect; // propage l'erreur éventuelle
            return new LineConnection(0, tcp,
                (cn, line) =>
                {
                    var m = Protocol.Decode(line);
                    if (m != null) events.Enqueue(new NetEvent { Type = NetEvent.Kind.Message, Conn = cn, Msg = m });
                },
                cn => events.Enqueue(new NetEvent { Type = NetEvent.Kind.Closed, Conn = cn }));
        }
    }

    public sealed class HostAnnouncement
    {
        public string Name, Address;
        public int Port, Players, MaxPlayers;
        public bool Open;
        public DateTime LastSeen;
        public string Key => Address + ":" + Port;
    }

    /// <summary>Côté hôte : annonce la partie chaque seconde.</summary>
    public sealed class DiscoveryBroadcaster
    {
        UdpClient udp;
        Timer timer;
        public volatile Dictionary<string, object> Info = new Dictionary<string, object>();

        public void Start()
        {
            try
            {
                udp = new UdpClient();
                udp.EnableBroadcast = true;
            }
            catch { return; }
            timer = new Timer(_ => Send(), null, 0, 1000);
        }

        void Send()
        {
            var u = udp;
            if (u == null) return;
            var payload = new Dictionary<string, object>(Info) { ["m"] = Protocol.Magic };
            var data = Encoding.UTF8.GetBytes(MiniJson.Serialize(payload));
            var targets = new HashSet<string> { "255.255.255.255" };
            foreach (var a in Protocol.LocalAddresses())
            {
                var b = a.GetAddressBytes();
                targets.Add(b[0] + "." + b[1] + "." + b[2] + ".255");
            }
            foreach (var t in targets)
            {
                try { u.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(t), Protocol.DiscoveryPort)); }
                catch { }
            }
        }

        public void Stop()
        {
            timer?.Dispose();
            try { udp?.Close(); } catch { }
            udp = null;
        }
    }

    /// <summary>Côté invité : écoute les annonces des parties.</summary>
    public sealed class DiscoveryListener
    {
        UdpClient udp;
        readonly ConcurrentDictionary<string, HostAnnouncement> hosts = new ConcurrentDictionary<string, HostAnnouncement>();

        public bool Start()
        {
            try
            {
                udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, Protocol.DiscoveryPort));
            }
            catch
            {
                return false;
            }
            var t = new Thread(Loop) { IsBackground = true, Name = "menteur-discovery" };
            t.Start();
            return true;
        }

        void Loop()
        {
            while (true)
            {
                try
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = udp.Receive(ref from);
                    var m = Protocol.Decode(Encoding.UTF8.GetString(data));
                    if (m == null || m.Str("m") != Protocol.Magic) continue;
                    var a = new HostAnnouncement
                    {
                        Name = m.Str("name") ?? "?", Address = from.Address.ToString(),
                        Port = m.Int("port", Protocol.GamePort), Players = m.Int("players"),
                        MaxPlayers = m.Int("max", Rules.MaxPlayers), Open = m.Bool("open"), LastSeen = DateTime.UtcNow,
                    };
                    hosts[a.Key] = a;
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { return; }
                catch { }
            }
        }

        /// <summary>Parties vues ces 4 dernières secondes.</summary>
        public List<HostAnnouncement> Current()
        {
            var now = DateTime.UtcNow;
            return hosts.Values.Where(h => (now - h.LastSeen).TotalSeconds < 4).OrderBy(h => h.Name).ToList();
        }

        public void Stop()
        {
            try { udp?.Close(); } catch { }
        }
    }
}
