using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class DeviceManager : MonoBehaviour
    {
        [SerializeField]
        private DataStorage storage;

        private DnsDiscovery dnsDiscovery = null;
        private Dictionary<string, IPAddress> discoveredDevices = null;
        private Task<bool> discoveryTask = null;
        private string selectedDeviceIp = null;

        public IReadOnlyDictionary<string, IPAddress> DiscoveredDevices { get { return discoveredDevices; } }
        public string SelectedDeviceIp { get { return selectedDeviceIp; } }

        public bool SelectDevice(string key)
        {
            if (discoveredDevices != null && discoveredDevices.TryGetValue(key, out IPAddress address))
            {
                selectedDeviceIp = address.ToString();
                return true;
            }
            return false;
        }

        public Task<bool> Discover()
        {
            if (discoveryTask != null && !discoveryTask.IsCompleted)
            {
                return discoveryTask;
            }

            discoveryTask = TryDiscoverDevices();
            return discoveryTask;
        }

        private async Task<bool> TryDiscoverDevices(string deviceName = "")
        {
            await storage.WhenReady();
            int dnsPort = storage.Config.rtspSettings.dnsPort;
            discoveredDevices = null;
            try
            {
                using (dnsDiscovery = new DnsDiscovery(IPAddress.Any, dnsPort))
                {
                    discoveredDevices = await dnsDiscovery.DiscoverDevices(deviceName);
                }
            }
            catch (ObjectDisposedException e)
            {
                Debug.Log("[DeviceManager] device discovery aborted");
                Debug.Log(e.Message);
            }
            finally
            {
                dnsDiscovery = null;
            }
            return discoveredDevices != null;
        }

        private void OnDestroy()
        {
            dnsDiscovery?.Abort();
        }
    }
}
