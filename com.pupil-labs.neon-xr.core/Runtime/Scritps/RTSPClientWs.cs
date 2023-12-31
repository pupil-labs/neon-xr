using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using PupilLabs.Serializable;
using System.Net;

namespace PupilLabs
{
    public class RTSPClientWs : RTSPClient
    {
        private readonly RTSPSettings settings;
        private readonly bool autoReconnect;
        private readonly object gazePointLock = new object();
        private int gazePointBufferIndex;
        private Vector2[] gazePointBuffer;
        public override Vector2 GazePoint
        {
            get
            {
                lock (gazePointLock)
                {
                    return gazePointBuffer[gazePointBufferIndex];
                }
            }
        }

        public override Vector2 SmoothGazePoint
        {
            get
            {
                lock (gazePointLock)
                {
                    return VectorUtils.Average(gazePointBuffer);
                }
            }
        }

        private readonly byte[] receiveBuffer = new byte[4096];
        private readonly byte[] messageBuffer = new byte[8192];
        private readonly MemoryStream messageStream = null;
        private DnsDiscovery dnsDiscovery = null;

        CancellationTokenSource stopCts;
        CancellationTokenSource timeoutCts;
        CancellationToken timeoutToken;
        CancellationToken stopToken;
        CancellationToken stopOrTimeoutToken;

        public RTSPClientWs(RTSPSettings settings, bool autoReconnect = true, int gazePointBufferSize = 10)
        {
            this.settings = settings;
            this.autoReconnect = autoReconnect;
            gazePointBuffer = new Vector2[gazePointBufferSize];
            gazePointBufferIndex = gazePointBufferSize - 1;
            messageStream = new MemoryStream(messageBuffer, true);
        }

        public override async Task RunAsync()
        {
            string ip = settings.ip;
            int port = settings.port;
            int dnsPort = settings.dnsPort;

            using (stopCts = new CancellationTokenSource())
            {
                stopToken = stopCts.Token;
                while (stopToken.IsCancellationRequested == false)
                {
                    using (timeoutCts = new CancellationTokenSource())
                    using (CancellationTokenSource stopOrTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, stopToken))
                    {
                        stopOrTimeoutToken = stopOrTimeoutCts.Token;
                        timeoutToken = timeoutCts.Token;

                        if (settings.autoIp)
                        {
                            try
                            {
                                IPAddress[] localIps = NetworkUtils.GetLocalIPAddresses();
                                foreach (var localIp in localIps)
                                {
                                    Debug.Log($"[RTSPClientWs] trying local ip: {localIp}");
                                    using (dnsDiscovery = new DnsDiscovery(localIp, dnsPort))
                                    {
                                        IPAddress deviceIp = await dnsDiscovery.DiscoverOneDevice(settings.deviceName);
                                        if (deviceIp != null)
                                        {
                                            Debug.Log("[RTSPClientWs] device found");
                                            ip = deviceIp.ToString();
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (ObjectDisposedException e)
                            {
                                Debug.Log("[RTSPClientWs] device discovery aborted, using fallback ip from config.json");
                                Debug.Log(e.Message);
                            }
                        }

                        Debug.Log($"[RTSPClientWs] using ip: {ip} port: {settings.port}");
                        try
                        {
                            string url = $"rtsp://{ip}:{port}";
                            using (ClientWebSocket ws = new ClientWebSocket())
                            {
                                await InitiateConnection(ws, ip, port);
                                string session = await InitiateStreaming(ws, url);
                                await Listen(ws);
                                await StopStreaming(ws, url, session);
                            }
                        }
                        catch (Exception e)
                        {
                            if (e is TaskCanceledException || e is WebSocketException || e is OperationCanceledException)
                            {
                                Debug.Log("[RTSPClientWs] listening aborted");
                                Debug.Log(e.Message);
                            }
                            else
                            {
                                throw;
                            }
                        }

                        if (autoReconnect)
                        {
                            Debug.Log("[RTSPClientWs] sleeping for 5 seconds before attempting to reconnect");
                            await Task.Delay(5000, stopToken).NoThrow();
                            Debug.Log("[RTSPClientWs] awake");
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        public override void Stop()
        {
            try
            {
                stopCts.Cancel();
                dnsDiscovery.Abort();
            }
            catch (ObjectDisposedException e)
            {
                Debug.Log(e.Message);
            }
        }

        private async Task InitiateConnection(ClientWebSocket ws, string ip, int port, int timeout = 1000)
        {
            timeoutCts.CancelAfter(timeout);
            await ws.ConnectAsync(new Uri($"ws://{ip}:{port}"), stopOrTimeoutToken);
            timeoutCts.CancelAfter(Timeout.Infinite);
        }

        private async Task<string> InitiateStreaming(ClientWebSocket ws, string url, int timeout = 5000)
        {
            timeoutCts.CancelAfter(timeout);

            //send describe and read response
            string rmsg = String.Format(RTSPTemplates.describe, url);
            Debug.Log($"[RTSPClientWs] sending: {rmsg}");
            await SendMessageAsync(ws, rmsg, stopOrTimeoutToken);
            string response = await ReceiveStringMessageAsync(ws, stopOrTimeoutToken); //no need to send teardown before setup so we can just cancel
            Debug.Log($"[RTSPClientWs] received: {response}");

            //send setup and read response
            rmsg = String.Format(RTSPTemplates.setup, url);
            Debug.Log($"[RTSPClientWs] sending: {rmsg}");
            await SendMessageAsync(ws, rmsg, timeoutToken); //from this point we want to stop task peacefully if possible and end session using teardown
            response = await ReceiveStringMessageAsync(ws, timeoutToken);
            Debug.Log($"[RTSPClientWs] received: {response}");

            //get session
            Regex sessionRegex = new Regex(@"^Session: ([a-zA-Z0-9]+)", RegexOptions.Multiline);
            string session = sessionRegex.Match(response).Groups[1].Value;
            Debug.Log($"[RTSPClientWs] session: {session}");

            //send play
            rmsg = String.Format(RTSPTemplates.play, url, session);
            Debug.Log($"[RTSPClientWs] sending: {rmsg}");
            await SendMessageAsync(ws, rmsg, timeoutToken);

            timeoutCts.CancelAfter(Timeout.Infinite);
            return session;
        }

        private async Task Listen(ClientWebSocket ws, int readTimeout = 2500)
        {
            int msgCounter = 0;
            int msgsPerLog = 2000;
            //read data (mixed)
            await Task.Run(async () =>
            {
                while (stopToken.IsCancellationRequested == false)
                {
                    timeoutCts.CancelAfter(readTimeout);
                    bool binaryMessageReceived = await ReceiveMessageAsync(ws, timeoutToken);
                    timeoutCts.CancelAfter(Timeout.Infinite);
                    if (binaryMessageReceived && messageStream.Length == 21 && GetRTPType(messageBuffer) == 99)
                    {
                        gazePointBufferIndex = ++gazePointBufferIndex % gazePointBuffer.Length;
                        lock (gazePointLock)
                        {
                            DecodeGazePoint(messageBuffer, ref gazePointBuffer[gazePointBufferIndex]);
                        }
                        OnGazeDataReceived();

                        if (++msgCounter == msgsPerLog)
                        {
                            Debug.Log($"[RTSPClientWs] {msgsPerLog} messages processed");
                            msgCounter = 0;
                        }
                    }
                }
            });
        }

        private async Task StopStreaming(ClientWebSocket ws, string url, string session, int timeout = 2500)
        {
            timeoutCts.CancelAfter(timeout);

            //send teardown and wait for proper response
            string rmsg = String.Format(RTSPTemplates.tear, url, session);
            Debug.Log($"[RTSPClientWs] sending: {rmsg}");
            await SendMessageAsync(ws, rmsg, timeoutToken);
            await Task.Run(async () =>
            {
                while (true)
                {
                    string msg = await ReceiveStringMessageAsync(ws, timeoutToken);
                    if (msg != null && msg.StartsWith("RTSP/1.0 200 OK"))
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeoutToken);
                        break;
                    }
                }
            });
            Debug.Log("[RTSPClientWs] closed");

            timeoutCts.CancelAfter(Timeout.Infinite);
        }

        private int GetRTPType(byte[] bytes)
        {
            return bytes[1] & 127;
        }

        private void DecodeGazePoint(byte[] bytes, ref Vector2 gp)
        {
            gp.x = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 12, sizeof(float)), 0);
            gp.y = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 16, sizeof(float)), 0);
        }

        private async Task SendMessageAsync(ClientWebSocket ws, string message, CancellationToken cancellationToken)
        {
            await ws.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Binary, true, cancellationToken);
        }

        private async Task<string> ReceiveStringMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            string ret = null;
            bool binaryMessageReceived = await ReceiveMessageAsync(ws, cancellationToken);
            if (binaryMessageReceived)
            {
                ret = Encoding.UTF8.GetString(messageBuffer, 0, (int)messageStream.Length);
            }
            return ret;
        }

        private async Task<bool> ReceiveMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            //Exactly one send and one receive is supported in parallel, ClientWebSocket has the same restriction 
            messageStream.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(receiveBuffer, cancellationToken);
                messageStream.Write(receiveBuffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                throw new WebSocketException("Websocket connection closed by host during receive");
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                return true;
            }

            return false; //other message types are ignored
        }

        protected override void DisposeManagedResources()
        {
            messageStream.Dispose();
        }

        private static class RTSPTemplates
        {
            /*DESCRIBE rtsp://192.168.1.23:8686?camera=gaze RTSP/1.0
            CSeq: 1
            Accept: application/sdp*/
            public const string describe = "DESCRIBE {0}/?camera=gaze RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\n\r\n";

            /*SETUP rtsp://192.168.1.23:8686/ RTSP/1.0
            CSeq: 2
            Blocksize: 64000
            Transport: RTP/AVP/TCP;unicast;interleaved=0-1*/
            public const string setup = "SETUP {0} RTSP/1.0\r\nCSeq: 2\r\nBlocksize: 64000\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1\r\n\r\n";

            /*PLAY rtsp://192.168.1.23:8686/ RTSP/1.0
            CSeq: 3
            Range: npt=0-
            Session: 1185d20035702ca*/
            public const string play = "PLAY {0} RTSP/1.0\r\nCSeq: 3\r\nRange: npt=0-\r\nSession: {1}\r\n\r\n";

            /*TEARDOWN rtsp://192.168.1.23:8686 RTSP/1.0
            CSeq: 4
            Session: 1185d20035702ca*/
            public const string tear = "TEARDOWN {0} RTSP/1.0\r\nCSeq: 4\r\nSession: {1}\r\n\r\n";
        }
    }
}
