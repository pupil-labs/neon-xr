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

        private readonly byte[] receiveBuffer = new byte[4096];
        private readonly byte[] messageBuffer = new byte[8192];
        private readonly MemoryStream messageStream = null;

        CancellationToken stopToken;

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

        private async Task InitiateConnection(ClientWebSocket ws, string ip, int port, int timeout = 1000)
        {
            await ws.ConnectAsync(new Uri($"ws://{ip}:{port}"), stopToken);
        }

        private async Task<string> InitiateStreaming(ClientWebSocket ws, string url, int timeout = 5000)
        {
            //send describe and read response
            string rmsg = String.Format(RTSPTemplates.describe, url);
            Debug.Log($"[RTSPClientWs] sending: {rmsg}");
            await SendMessageAsync(ws, rmsg, stopToken);
            string response = await ReceiveStringMessageAsync(ws, stopToken); //no need to send teardown before setup so we can just cancel
            Debug.Log($"[RTSPClientWs] received: {response}");

            //send setup and read response
            rmsg = String.Format(RTSPTemplates.setup, url);
            Debug.Log($"[RTSPClientWs] sending: {rmsg}");
            await SendMessageAsync(ws, rmsg, stopToken); //from this point we want to stop task peacefully if possible and end session using teardown
            response = await ReceiveStringMessageAsync(ws, stopToken);
            Debug.Log($"[RTSPClientWs] received: {response}");

            //get session
            Regex sessionRegex = new Regex(@"^Session: ([a-zA-Z0-9]+)", RegexOptions.Multiline);
            string session = sessionRegex.Match(response).Groups[1].Value;
            Debug.Log($"[RTSPClientWs] session: {session}");

            //send play
            rmsg = String.Format(RTSPTemplates.play, url, session);
            Debug.Log($"[RTSPClientWs] sending: {rmsg}");
            await SendMessageAsync(ws, rmsg, stopToken);

            return session;
        }

        private async Task Listen(ClientWebSocket ws, int readTimeout = 7500)
        {
            int msgCounter = 0;
            int msgsPerTimer = 200;
            uint dataOffset = 12;
            //read data (mixed)
            await Task.Run(async () =>
            {
                while (stopToken.IsCancellationRequested == false)
                {
                    if (msgCounter++ == msgsPerTimer)
                    {
                        msgCounter = 0;
                    }
                    bool binaryMessageReceived = await ReceiveMessageAsync(ws, stopToken);
                    if (binaryMessageReceived && GetRTPType(messageBuffer) == 99)
                    {
                        OnDataReceived(0, false, 0, (byte)StreamId.Gaze, (uint)messageStream.Length, dataOffset, messageBuffer);
                    }
                }
            });
        }

        private async Task StopStreaming(ClientWebSocket ws, string url, string session, int timeout = 2500)
        {
            using (CancellationTokenSource teardownCts = new CancellationTokenSource(timeout))
            {
                CancellationToken teardownToken = teardownCts.Token;
                //send teardown and wait for proper response
                string rmsg = String.Format(RTSPTemplates.tear, url, session);
                Debug.Log($"[RTSPClientWs] sending: {rmsg}");
                await SendMessageAsync(ws, rmsg, teardownToken);
                await Task.Run(async () =>
                {
                    while (true)
                    {
                        string msg = await ReceiveStringMessageAsync(ws, teardownToken);
                        if (msg != null && msg.StartsWith("RTSP/1.0 200 OK"))
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, teardownToken);
                            break;
                        }
                    }
                });
                Debug.Log("[RTSPClientWs] closed");
            }
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
            base.DisposeManagedResources();
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
