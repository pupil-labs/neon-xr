using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PupilLabs
{
    public class CameraIntrinsics
    {
        public float[] cameraMatrix = new float[9];
        public float[] distortionCoefficients = new float[8];
    }

    public static class DataUtils
    {
        public static void ParseCalibrationBytes(byte[] bytes, CameraIntrinsics ciToOverwrite)
        {
            for (int i = 0; i < 9; i++)
            {
                ciToOverwrite.cameraMatrix[i] = (float)BitConverter.ToDouble(bytes, 7 + i * sizeof(double));
            }
            for (int i = 0; i < 8; i++)
            {
                ciToOverwrite.distortionCoefficients[i] = (float)BitConverter.ToDouble(bytes, 79 + i * sizeof(double));
            }
        }

        public static string GetDataPath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        public static async Task<string> GetDataPath(string fileName, string defaultContentAddress)
        {
            string filePath = GetDataPath(fileName);
            if (File.Exists(filePath) == false)
            {
                //create default
                AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(defaultContentAddress);
                await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    TextAsset asset = handle.Result;
                    using (FileStream sourceStream = File.Open(filePath, FileMode.Create))
                    {
                        await sourceStream.WriteAsync(asset.bytes, 0, asset.bytes.Length);
                    }
                    Debug.Log($"[DataUtils] Default app config created at: {filePath}");
                }
                else
                {
                    Debug.LogError("[DataUtils] Failed to create default app config");
                    return null;
                }
            }
            return filePath;
        }
    }

    public static class CameraUtils
    {
        public static Vector3 ImgPointToDir(Vector2 point, float[] cameraMatrix, float[] distortionCoeffs)
        {
            return OpenCVUndistortPoint(point, cameraMatrix, distortionCoeffs);
        }

        private static Vector3 OpenCVUndistortPoint(Vector2 point, float[] cameraMatrix, float[] distortionCoeffs, int iters = 5)
        {
            float fx = cameraMatrix[0];
            float fy = cameraMatrix[4];
            float ifx = 1.0f / fx;
            float ify = 1.0f / fy;
            float cx = cameraMatrix[2];
            float cy = cameraMatrix[5];

            float x, x0, y, y0;
            x0 = x = (point.x - cx) * ifx;
            y0 = y = (point.y - cy) * ify;

            float[] dcs = distortionCoeffs;
            for (int i = 0; i < iters; i++)
            {
                float r2 = x * x + y * y;
                float icdist = (1 + ((dcs[7] * r2 + dcs[6]) * r2 + dcs[5]) * r2) / (1 + ((dcs[4] * r2 + dcs[1]) * r2 + dcs[0]) * r2);
                float deltaX = 2 * dcs[2] * x * y + dcs[3] * (r2 + 2 * x * x);
                float deltaY = dcs[2] * (r2 + 2 * y * y) + 2 * dcs[3] * x * y;
                x = (x0 - deltaX) * icdist;
                y = (y0 - deltaY) * icdist;
            }

            return new Vector3(x, -y, 1);
        }
    }

    public static class NetworkUtils
    {
        public static IPAddress[] GetLocalIPAddresses()
        {
            List<IPAddress> addresses = new List<IPAddress>();
            Debug.Log("[NetworkUtils] getting local IP address");
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                Debug.Log($"[NetworkUtils] iterating over network interface of type: {ni.NetworkInterfaceType} and status {ni.OperationalStatus}");
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        Debug.Log($"[NetworkUtils] iterating over unicast ip of address family: {ip.Address.AddressFamily}");
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && IPAddress.IsLoopback(ip.Address) == false && ip.IsDnsEligible)
                        {
                            Debug.Log($"[NetworkUtils] adding local IP address: {ip.Address}");
                            addresses.Add(ip.Address);
                        }
                    }
                }
            }
            return addresses.ToArray();
        }

        public static byte[] NetworkBytesToLocal(byte[] networkBytes, int startIndex, int count)
        {
            byte[] localBytes = networkBytes.Skip(startIndex).Take(count).ToArray();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(localBytes);
            }
            return localBytes;
        }
    }
}
