using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Malang.Core;
using UnityEngine;

namespace Malang.Input
{
    /// <summary>
    /// Python 브리지(tools/sensor_bridge.py)가 보내는 FSR 원시값을 UDP 로 받는다. (노트북 센서 시연용)
    /// 한 줄: t_ms,c0,c1,...,cN-1   (N 가변, 최대 MaxChannels)
    /// 백그라운드 스레드가 받아 파싱하고, 메인 스레드는 최신 값만 가져간다(GC 할당 없음).
    /// disconnectTimeoutSeconds 동안 새 샘플이 없으면 IsConnected = false.
    /// </summary>
    public class UdpGripInput : IGripDevice
    {
        public const int MaxChannels = 8;
        private const int BufferSize = 256;

        private readonly object gate = new object();
        private readonly int[] sharedChannels = new int[MaxChannels];
        private int sharedCount;
        private long sharedMs;
        private int sharedSequence;

        private readonly int[] channels = new int[MaxChannels];
        private int channelCount;
        private long deviceMs;
        private int lastSequence;
        private int sampleCount;
        private float sum;

        private Socket socket;
        private Thread thread;
        private volatile bool running;
        private int rawMax;
        private float timeoutSeconds;

        private double lastNow;
        private double lastSampleAt = -1d;
        private double rateWindowStart;
        private int rateWindowSamples;
        private float rate;

        public DeviceKind Kind => DeviceKind.Udp;
        public bool IsConnected => lastSampleAt >= 0d && lastNow - lastSampleAt < timeoutSeconds;
        public bool IsNormalized => false;
        public int ChannelCount => channelCount;
        public int[] Channels => channels;
        public float Sum => sum;
        public long DeviceMs => deviceMs;
        public int SampleCount => sampleCount;
        public float SampleRateHz => rate;

        public int Port { get; private set; }
        public string LastError { get; private set; }

        public void Open(GameConfig config)
        {
            Port = config.udpPort;
            rawMax = config.rawChannelMax;
            timeoutSeconds = config.disconnectTimeoutSeconds;
            LastError = null;

            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.ReceiveTimeout = config.udpReceiveTimeoutMs;
                socket.Bind(new IPEndPoint(IPAddress.Loopback, Port));

                running = true;
                thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "MalangUdpGripInput" };
                thread.Start();
                Debug.Log("UdpGripInput: UDP " + Port + " 수신 대기 (sensor_bridge.py 를 실행하세요)");
            }
            catch (SocketException e)
            {
                LastError = e.Message;
                Debug.LogError("UdpGripInput: UDP " + Port + " 열기 실패: " + e.Message);
                Close();
            }
        }

        public void Close()
        {
            running = false;

            if (socket != null)
            {
                try
                {
                    socket.Close();
                }
                catch (SocketException)
                {
                }

                socket = null;
            }

            if (thread != null)
            {
                if (thread.IsAlive)
                    thread.Join(1000);
                thread = null;
            }
        }

        public void Tick(float deltaTime, double now)
        {
            lastNow = now;
            int sequence;

            lock (gate)
            {
                sequence = sharedSequence;
                if (sequence != lastSequence)
                {
                    channelCount = sharedCount;
                    Array.Copy(sharedChannels, channels, channelCount);
                    deviceMs = sharedMs;
                }
            }

            if (sequence != lastSequence)
            {
                rateWindowSamples += sequence - lastSequence;
                lastSequence = sequence;
                lastSampleAt = now;
                sampleCount++;

                sum = 0f;
                for (int i = 0; i < channelCount; i++)
                    sum += channels[i];
            }

            if (now - rateWindowStart >= 1d)
            {
                rate = rateWindowStart > 0d ? (float)(rateWindowSamples / (now - rateWindowStart)) : 0f;
                rateWindowSamples = 0;
                rateWindowStart = now;
            }
        }

        // ---------- 백그라운드 스레드 ----------

        private void ReceiveLoop()
        {
            var buffer = new byte[BufferSize];
            var fields = new long[MaxChannels + 1];
            Socket s = socket;

            while (running)
            {
                int length;
                try
                {
                    length = s.Receive(buffer);
                }
                catch (SocketException e)
                {
                    if (!running)
                        break;
                    if (e.SocketErrorCode == SocketError.TimedOut)
                        continue;
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                int count = Parse(buffer, length, fields);
                if (count < 2)
                    continue;

                lock (gate)
                {
                    sharedMs = fields[0];
                    sharedCount = count - 1;
                    for (int i = 0; i < sharedCount; i++)
                        sharedChannels[i] = (int)fields[i + 1];
                    sharedSequence++;
                }
            }
        }

        /// <summary>"t,c0,c1,..." 를 문자열 할당 없이 파싱. 잘못된 줄이면 0.</summary>
        private int Parse(byte[] data, int length, long[] fields)
        {
            int count = 0;
            long current = 0;
            bool hasDigit = false;

            for (int i = 0; i < length; i++)
            {
                byte b = data[i];

                if (b >= (byte)'0' && b <= (byte)'9')
                {
                    current = current * 10 + (b - (byte)'0');
                    hasDigit = true;
                    if (current > int.MaxValue)
                        return 0;
                }
                else if (b == (byte)',')
                {
                    if (!hasDigit || count >= fields.Length)
                        return 0;
                    fields[count++] = current;
                    current = 0;
                    hasDigit = false;
                }
                else if (b == (byte)'\r' || b == (byte)'\n' || b == (byte)' ')
                {
                    // 줄 끝 공백 무시
                }
                else
                {
                    return 0;
                }
            }

            if (!hasDigit || count >= fields.Length)
                return 0;
            fields[count++] = current;

            for (int i = 1; i < count; i++)
            {
                if (fields[i] > rawMax)
                    return 0;
            }

            return count;
        }
    }
}
