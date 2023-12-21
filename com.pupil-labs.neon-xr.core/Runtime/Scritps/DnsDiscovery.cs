using Kaitai;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class DnsDiscovery : Disposable
    {
        private Socket socket;
        private byte[] buffer = new byte[1024];

        public DnsDiscovery(IPAddress ip, int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.ReceiveTimeout = 1000;
            socket.Bind(new IPEndPoint(ip, port));
        }

        public void Abort()
        {
            //cancellation with sockets is bit problematic
            //some methods are not cancellable some return exceptions based on .NET version...
            //curently it is easiest to just close the socket and catch ObjectDisposedException
            socket.Close();
        }

        public async Task<IPAddress> DiscoverOneDevice(string name = "", int tryCount = 3)
        {
            for (int i = 0; i < tryCount; i++)
            {
                await SendDiscoveryQuery();
                Debug.Log($"[DnsDiscovery] waiting for responses {i + 1}/{tryCount}");
                while (true) //handle responses from multiple devices
                {
                    try
                    {
                        await Task.Run(() => socket.Receive(buffer)); //timeout ignored with async call and token based timeout corrupts socket
                    }
                    catch (SocketException e)
                    {
                        Debug.Log("[DnsDiscovery] no device responded in time");
                        Debug.Log(e.Message);
                        break;
                    }
                    DnsPacket packet = new DnsPacket(new KaitaiStream(buffer));
                    IPAddress ip = null;
                    string deviceName = null;
                    foreach (var a in packet.Answers)
                    {
                        if (a.Type == DnsPacket.TypeType.A)
                        {
                            ip = new IPAddress(a.M_RawPayload);
                            Debug.Log($"[DnsDiscovery] received response from: {ip}");
                        }
                        else if (a.Type == DnsPacket.TypeType.Txt)
                        {
                            var txt = String.Join("", a.Name.Name.Select(x => x.Name));
                            Debug.Log($"[DnsDiscovery] received response from: {txt}");
                            deviceName = txt.StartsWith("PI monitor") ? txt : null;
                        }
                        else
                        {
                            var txt = String.Join("", a.Name.Name.Select(x => x.Name));
                            Debug.Log($"[DnsDiscovery] received response of type {a.Type} from: {txt}");
                        }
                    }
                    if (ip != null)
                    {
                        if (deviceName != null)
                        {
                            deviceName = deviceName.Split(':')[1]; //PI monitor:Neon Companion:a95136f3304b9204
                            if (name == String.Empty || deviceName == name)
                            {
                                return ip;
                            }
                        }
                        else
                        {
                            try
                            {
                                using (var client = new HttpClient())
                                {
                                    client.Timeout = TimeSpan.FromMilliseconds(1000);
                                    var result = await client.GetStringAsync($"http://{ip}:8080/api/status");
                                    if (result.StartsWith("{\"message\":\"Success\""))
                                    {
                                        return ip;
                                    }
                                }
                            }
                            catch (TaskCanceledException e)
                            {
                                Debug.Log("[DnsDiscovery] REST probe timeout");
                                Debug.Log(e.Message);
                            }
                        }
                    }
                }
            }
            return null;
        }

        protected virtual async Task SendDiscoveryQuery()
        {
            Debug.Log("[DnsDiscovery] sending broadcast message");
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
            List<byte> bytes = new List<byte>();

            //header
            bytes.AddRange(new byte[] { 0x0, 0x0 });  // transaction id (ignored)
            bytes.AddRange(new byte[] { 0x1, 0x0 });  // standard query
            bytes.AddRange(new byte[] { 0x0, 0x1 });  // questions
            bytes.AddRange(new byte[] { 0x0, 0x0 });  // answer RRs
            bytes.AddRange(new byte[] { 0x0, 0x0 });  // authority RRs
            bytes.AddRange(new byte[] { 0x0, 0x0 });  // additional RRs

            //question section
            bytes.AddRange(new byte[] { 0x05, 0x5f, 0x68, 0x74, 0x74, 0x70, 0x04, 0x5f, 0x74, 0x63, 0x70, 0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00, 0x00, 0x0c, 0x00, 0x01 });  // _http._tcp.local: type PTR, class IN, "QM" question

            await socket.SendToAsync(bytes.ToArray(), SocketFlags.None, endpoint);
            Debug.Log("[DnsDiscovery] broadcast message sent");
        }

        protected override void DisposeManagedResources()
        {
            socket.Close();
        }
    }
}