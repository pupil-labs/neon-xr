using PupilLabs.Serializable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class DeviceManager : MonoBehaviour
    {
        [SerializeField]
        private DataStorage storage;
        [SerializeField]
        private int repeatOffsetEstimationAfterMs = -1; // If < 0 no run, if == 0 run once, else repeat periodically
        [SerializeField]
        private bool tryAllLocalIps = false;

        public DStringEvent selectionChanged;
        public DeviceManagerEvent discoveryFinished;
        public StringLongEvent timeOffsetEstimated;

        private DnsDiscovery dnsDiscovery = null;
        private Dictionary<string, string> discoveredDevices = null;
        private Task<bool> discoveryTask = null;
        private string selectedDeviceIp = null;
        private CancellationTokenSource periodicOffsetCts = new CancellationTokenSource();

        public IReadOnlyDictionary<string, string> DiscoveredDevices { get { return discoveredDevices; } }
        public string SelectedDeviceIp { get { return selectedDeviceIp; } }

        private async void Awake()
        {
            await PeriodicTimeOffsetEstimationLoop(repeatOffsetEstimationAfterMs, periodicOffsetCts.Token);
        }

        public bool SelectAnyDevice()
        {
            if (discoveredDevices != null)
            {
                foreach (var key in discoveredDevices.Keys)
                {
                    return SelectDevice(key);
                }
            }
            return false;
        }

        public bool SelectDevice(string key)
        {
            bool result = discoveredDevices != null && discoveredDevices.TryGetValue(key, out selectedDeviceIp);
            if (result)
            {
                selectionChanged?.Invoke(key, selectedDeviceIp);
            }
            return result;
        }

        public void StartDiscovery()
        {
            Discover().Forget();
        }

        public Task<bool> Discover(string name = "")
        {
            if (discoveryTask != null && !discoveryTask.IsCompleted)
            {
                return discoveryTask;
            }

            discoveryTask = DiscoverDevices(name);
            return discoveryTask;
        }

        private async Task<bool> DiscoverDevices(string name)
        {
            await storage.WhenReady();
            int dnsPort = storage.Config.rtspSettings.dnsPort;
            try
            {
                IPAddress[] localIps = new IPAddress[] { IPAddress.Any };
                if (tryAllLocalIps)
                {
                    IPAddress[] allLocalIps = await Task.Run(() => NetworkUtils.GetLocalIpAddresses());
                    if (allLocalIps.Length > 0)
                    {
                        localIps = allLocalIps;
                    }
                }
                Dictionary<string, string> mergedDevices = new Dictionary<string, string>();
                foreach (IPAddress ip in localIps)
                {
                    Debug.Log($"[DeviceManager] Attempting discovery on network adapter: {ip}");
                    using (dnsDiscovery = new DnsDiscovery(ip, dnsPort))
                    {
                        foreach (var kvp in await dnsDiscovery.DiscoverDevices(name))
                        {
                            mergedDevices[kvp.Key] = kvp.Value;
                        }
                    }
                }
                discoveredDevices = mergedDevices.Count > 0 ? mergedDevices : null;
            }
            catch (ObjectDisposedException e)
            {
                Debug.Log("[DeviceManager] device discovery aborted");
                Debug.Log(e.Message);
                discoveredDevices = null;
            }
            finally
            {
                dnsDiscovery = null;
                discoveryFinished?.Invoke(this);
            }
            return discoveredDevices != null;
        }

        private void OnDestroy()
        {
            dnsDiscovery?.Abort();
            periodicOffsetCts.Cancel();
        }

        public void StartTimeOffsetEstimation()
        {
            EstimateTimeOffset().Forget();
        }

        public async Task<long> EstimateTimeOffset(CancellationToken cancellationToken = default)
        {
            string deviceIp = selectedDeviceIp;
            await storage.WhenReady();
            int timeEchoPort = storage.Config.rtspSettings.timeEchoPort;
            long offset = await EstimateTimeOffset(deviceIp, timeEchoPort, cancellationToken: cancellationToken);
            timeOffsetEstimated.Invoke(deviceIp, offset);
            return offset;
        }

        public static async Task<long> EstimateTimeOffset(string host, int port, int n = 100, int sleepMs = 0, CancellationToken cancellationToken = default)
        {
            long offsetSum = 0;

            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                using NetworkStream stream = client.GetStream();

                const int tsSize = sizeof(long);
                const int responseSize = tsSize * 2;
                byte[] response = new byte[responseSize];

                for (int i = 0; i < n; i++)
                {
                    long beforeMs = RTSPServiceWrapper.UnixTimeMs();
                    byte[] beforeBytes = NetworkUtils.NetworkBytesToLocal(BitConverter.GetBytes(beforeMs), 0, tsSize);
                    await stream.WriteAsync(beforeBytes, 0, tsSize, cancellationToken);

                    int read = 0;
                    while (read < responseSize)
                    {
                        int bytesRead = await stream.ReadAsync(response, read, responseSize - read, cancellationToken);
                        if (bytesRead == 0) throw new IOException("Connection closed unexpectedly");
                        read += bytesRead;
                    }
                    long afterMs = RTSPServiceWrapper.UnixTimeMs();

                    long validationMs = BitConverter.ToInt64(NetworkUtils.NetworkBytesToLocal(response, 0, tsSize), 0);
                    long serverMs = BitConverter.ToInt64(NetworkUtils.NetworkBytesToLocal(response, tsSize, tsSize), 0);

                    if (validationMs != beforeMs)
                    {
                        throw new InvalidDataException($"Validation failed. Expected {beforeMs}, got {validationMs}");
                    }

                    long clientMidpoint = (beforeMs + afterMs) / 2;
                    offsetSum += clientMidpoint - serverMs;

                    if (sleepMs > 0 && i < n - 1)
                    {
                        await Task.Delay(sleepMs, cancellationToken);
                    }
                }
            }
            return offsetSum / n;
        }

        private async Task PeriodicTimeOffsetEstimationLoop(int waitMs, CancellationToken token)
        {
            if (waitMs < 0)
            {
                return;
            }

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (selectedDeviceIp != null)
                    {
                        await EstimateTimeOffset(token);
                        if (waitMs == 0) break;
                    }
                    await Task.Delay(Mathf.Max(waitMs, 100), token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[DeviceManager] Periodic time offset loop cancelled");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeviceManager] Periodic time offset loop crashed: {e.Message}");
            }
        }
    }
}
