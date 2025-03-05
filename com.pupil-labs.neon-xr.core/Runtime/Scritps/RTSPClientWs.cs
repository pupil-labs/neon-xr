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
        private readonly object eyeStateLock = new object();
        private readonly object eyeLidLock = new object();
        private int gazePointBufferIndex;
        private int eyeStateBufferIndex;
        private int eyeLidBufferIndex;
        private Vector2[] gazePointBuffer;

        private volatile bool eyeStateAvailable = false;
        private volatile bool eyeLidAvailable = false;

        private EyeState eyeState = new EyeState();
        private Vector3[] eyeballCenterLeftBuffer;
        private Vector3[] opticalAxisLeftBuffer;
        private float[] pupilDiameterLeftBuffer;
        private Vector3[] eyeballCenterRightBuffer;
        private Vector3[] opticalAxisRightBuffer;
        private float[] pupilDiameterRightBuffer;

        private EyeLid eyeLid = new EyeLid();
        private float[] eyelidAngleTopLeftBuffer;
        private float[] eyelidAngleBottomLeftBuffer;
        private float[] eyelidApertureLeftBuffer;
        private float[] eyelidAngleTopRightBuffer;
        private float[] eyelidAngleBottomRightBuffer;
        private float[] eyelidApertureRightBuffer;

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
                    return gazePointBuffer.Average();
                }
            }
        }

        public override bool EyeStateAvailable
        {
            get { return eyeStateAvailable; }
        }

        public override EyeState EyeState
        {
            get
            {
                lock (eyeStateLock)
                {
                    eyeState.pupilDiameterLeft = pupilDiameterLeftBuffer[eyeStateBufferIndex];
                    eyeState.eyeballCenterLeft = eyeballCenterLeftBuffer[eyeStateBufferIndex];
                    eyeState.opticalAxisLeft = opticalAxisLeftBuffer[eyeStateBufferIndex];

                    eyeState.pupilDiameterRight = pupilDiameterRightBuffer[eyeStateBufferIndex];
                    eyeState.eyeballCenterRight = eyeballCenterRightBuffer[eyeStateBufferIndex];
                    eyeState.opticalAxisRight = opticalAxisRightBuffer[eyeStateBufferIndex];
                    return eyeState;
                }
            }
        }

        public override EyeState SmoothEyeState
        {
            get
            {
                lock (eyeStateLock)
                {
                    eyeState.pupilDiameterLeft = pupilDiameterLeftBuffer.Average();
                    eyeState.eyeballCenterLeft = eyeballCenterLeftBuffer.Average();
                    eyeState.opticalAxisLeft = opticalAxisLeftBuffer.Average();

                    eyeState.pupilDiameterRight = pupilDiameterRightBuffer.Average();
                    eyeState.eyeballCenterRight = eyeballCenterRightBuffer.Average();
                    eyeState.opticalAxisRight = opticalAxisRightBuffer.Average();
                    return eyeState;
                }
            }
        }

        public override bool EyeLidAvailable
        {
            get { return eyeLidAvailable; }
        }

        public override EyeLid EyeLid
        {
            get
            {
                lock (eyeLidLock)
                {
                    eyeLid.eyelid_angle_top_left = eyelidAngleTopLeftBuffer[eyeLidBufferIndex];
                    eyeLid.eyelid_angle_bottom_left = eyelidAngleBottomLeftBuffer[eyeLidBufferIndex];
                    eyeLid.eyelid_aperture_left = eyelidApertureLeftBuffer[eyeLidBufferIndex];

                    eyeLid.eyelid_angle_top_right = eyelidAngleTopRightBuffer[eyeLidBufferIndex];
                    eyeLid.eyelid_angle_bottom_right = eyelidAngleBottomRightBuffer[eyeLidBufferIndex];
                    eyeLid.eyelid_aperture_right = eyelidApertureRightBuffer[eyeLidBufferIndex];
                    return eyeLid;
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

        public RTSPClientWs(RTSPSettings settings, bool autoReconnect = true, int gazePointBufferSize = 5, int eyeStateBufferSize = 5, , int eyeLidBufferSize = 5)
        {
            this.settings = settings;
            this.autoReconnect = autoReconnect;

            gazePointBuffer = new Vector2[gazePointBufferSize];
            gazePointBufferIndex = gazePointBufferSize - 1;

            pupilDiameterLeftBuffer = new float[eyeStateBufferSize];
            eyeballCenterLeftBuffer = new Vector3[eyeStateBufferSize];
            opticalAxisLeftBuffer = new Vector3[eyeStateBufferSize];
            pupilDiameterRightBuffer = new float[eyeStateBufferSize];
            eyeballCenterRightBuffer = new Vector3[eyeStateBufferSize];
            opticalAxisRightBuffer = new Vector3[eyeStateBufferSize];
            eyeStateBufferIndex = eyeStateBufferSize - 1;

            eyelidAngleTopLeftBuffer = new float[eyeLidBufferSize];
            eyelidAngleBottomLeftBuffer = new float[eyeLidBufferSize];
            eyelidApertureLeftBuffer = new float[eyeLidBufferSize];
            eyelidAngleTopRightBuffer = new float[eyeLidBufferSize];
            eyelidAngleBottomRightBuffer = new float[eyeLidBufferSize];
            eyelidApertureRightBuffer = new float[eyeLidBufferSize];
            eyeLidBufferIndex = eyeLidBufferSize - 1;

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
                    if (binaryMessageReceived && (messageStream.Length == 21 || messageStream.Length == 29 || messageStream.Length == 77 || messageStream.Length == 101) && GetRTPType(messageBuffer) == 99)
                    {
                        gazePointBufferIndex = ++gazePointBufferIndex % gazePointBuffer.Length;
                        lock (gazePointLock)
                        {
                            DecodeGazePoint(messageBuffer, ref gazePointBuffer[gazePointBufferIndex]);
                        }
                        if (messageStream.Length == 77 || messageStream.Length == 101)
                        {
                            eyeStateAvailable = true;
                            eyeStateBufferIndex = ++eyeStateBufferIndex % pupilDiameterLeftBuffer.Length;
                            lock (eyeStateLock)
                            {
                                DecodeEyeState(
                                    messageBuffer,
                                    ref pupilDiameterLeftBuffer[eyeStateBufferIndex],
                                    ref eyeballCenterLeftBuffer[eyeStateBufferIndex],
                                    ref opticalAxisLeftBuffer[eyeStateBufferIndex],
                                    ref pupilDiameterRightBuffer[eyeStateBufferIndex],
                                    ref eyeballCenterRightBuffer[eyeStateBufferIndex],
                                    ref opticalAxisRightBuffer[eyeStateBufferIndex]
                                );
                            }
                        }
                        if (messageStream.Length == 101)
                        {
                            eyeLidAvailable = true;
                            eyeLidBufferIndex = ++eyeLidBufferIndex % eyelidAngleTopLeftBuffer.Length;
                            lock (eyeLidLock)
                            {
                                DecodeEyeLid(
                                    messageBuffer,
                                    ref eyelidAngleTopLeftBuffer[eyeLidBufferIndex],
                                    ref eyelidAngleBottomLeftBuffer[eyeLidBufferIndex],
                                    ref eyelidApertureLeftBuffer[eyeLidBufferIndex],
                                    ref eyelidAngleTopRightBuffer[eyeLidBufferIndex],
                                    ref eyelidAngleBottomRightBuffer[eyeLidBufferIndex],
                                    ref eyelidApertureRightBuffer[eyeLidBufferIndex]
                                );
                            }
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

        private void DecodeEyeState(byte[] bytes, ref float pdl, ref Vector3 ecl, ref Vector3 oal, ref float pdr, ref Vector3 ecr, ref Vector3 oar, float scale = 0.001f)
        {
            pdl = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 21, sizeof(float)), 0) * scale;
            ecl.x = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 25, sizeof(float)), 0) * scale;
            ecl.y = -BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 29, sizeof(float)), 0) * scale;
            ecl.z = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 33, sizeof(float)), 0) * scale;
            oal.x = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 37, sizeof(float)), 0) * scale;
            oal.y = -BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 41, sizeof(float)), 0) * scale;
            oal.z = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 45, sizeof(float)), 0) * scale;

            pdr = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 49, sizeof(float)), 0) * scale;
            ecr.x = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 53, sizeof(float)), 0) * scale;
            ecr.y = -BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 57, sizeof(float)), 0) * scale;
            ecr.z = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 61, sizeof(float)), 0) * scale;
            oar.x = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 65, sizeof(float)), 0) * scale;
            oar.y = -BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 69, sizeof(float)), 0) * scale;
            oar.z = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 73, sizeof(float)), 0) * scale;
        }

        private void DecodeEyeLid(byte[] bytes, ref float tl_angle, ref float bl_angle, ref float apl, ref float tr_angle, ref float br_angle, ref float apr, float scale = 0.001f)
        {
            tl_angle = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 77, sizeof(float)), 0);
            bl_angle = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 81, sizeof(float)), 0);
            apl = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 85, sizeof(float)), 0) * scale;

            tr_angle = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 89, sizeof(float)), 0);
            br_angle = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 93, sizeof(float)), 0);
            apr = BitConverter.ToSingle(NetworkUtils.NetworkBytesToLocal(bytes, 97, sizeof(float)), 0) * scale;
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
