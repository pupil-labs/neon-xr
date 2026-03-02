using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class RTSPClientWs : RTSPClient
    {
        private readonly string ip;
        private readonly int port;
        private readonly object dataLock = new object();
        private GazeData gazeData = new GazeData();

        private readonly byte[] receiveBuffer = new byte[4096];
        private readonly byte[] messageBuffer = new byte[8192];
        private readonly MemoryStream messageStream = null;

        private float[] gazePoint = new float[2];
        private bool worn;
        private float[] gazePointDualLeft = new float[2];
        private float[] gazePointDualRight = new float[2];
        private float[] eyeStateLeft = new float[7];
        private float[] eyeStateRight = new float[7];
        private float[] eyelidLeft = new float[3];
        private float[] eyelidRight = new float[3];
        private EtDataType etDataType = EtDataType.Unknown;

        CancellationTokenSource timeoutCts;
        CancellationToken timeoutToken;
        CancellationToken stopToken;
        CancellationToken stopOrTimeoutToken;

        public override GazeData GazeData
        {
            get
            {
                lock (dataLock)
                {
                    gazeData.SetData(etDataType, gazePoint, worn, gazePointDualLeft, gazePointDualRight, eyeStateLeft, eyeStateRight, eyelidLeft, eyelidRight);
                    return gazeData;
                }
            }
        }

        public RTSPClientWs(string ip, int port)
        {
            this.ip = ip;
            this.port = port;

            messageStream = new MemoryStream(messageBuffer, true);
        }

        public override async Task RunAsync()
        {
            using (stopCts = new CancellationTokenSource())
            {
                stopToken = stopCts.Token;
                using (timeoutCts = new CancellationTokenSource())
                {
                    timeoutToken = timeoutCts.Token;
                    using (CancellationTokenSource stopOrTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, stopToken))
                    {
                        stopOrTimeoutToken = stopOrTimeoutCts.Token;

                        try
                        {
                            string url = $"rtsp://{ip}:{port}";
                            Debug.Log($"[RTSPClientWs] using url: {url}");
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
                    }
                }
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

        private async Task Listen(ClientWebSocket ws, int readTimeout = 7500)
        {
            int msgCounter = 0;
            int msgsPerTimer = 200;
            int msgsPerLog = msgsPerTimer * 10;
            uint dataOffset = 12;
            //read data (mixed)
            await Task.Run(async () =>
            {
                while (stopToken.IsCancellationRequested == false)
                {
                    if (msgCounter % msgsPerTimer == 0)
                    {
                        timeoutCts.CancelAfter(readTimeout);
                    }
                    bool binaryMessageReceived = await ReceiveMessageAsync(ws, timeoutToken);
                    if (binaryMessageReceived && GetRTPType(messageBuffer) == 99)
                    {
                        lock (dataLock)
                        {
                            etDataType = RTSPServiceWrapper.BytesToGazeData(
                                messageBuffer, (uint)messageStream.Length, dataOffset,
                                gazePoint, out worn, gazePointDualLeft, gazePointDualRight,
                                eyeStateLeft, eyeStateRight,
                                eyelidLeft, eyelidRight
                            );
                        }

                        OnGazeDataReceived();

                        if (++msgCounter == msgsPerLog)
                        {
                            Debug.Log($"[RTSPClientWs] {msgsPerLog} messages processed");
                            msgCounter = 0;
                        }
                    }
                }
                timeoutCts.CancelAfter(Timeout.Infinite);
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
