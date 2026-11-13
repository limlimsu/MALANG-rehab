using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/*
 * Python 중계 프로그램에서 FSR 4개의 원시값을 받는다.
 *
 * 입력 한 줄: device_ms,raw1,raw2,raw3,raw4
 * 예: 1200,1210,980,1105,875
 *
 * 실행 방법:
 * 1. RehabGameUI 씬의 GameController에 추가한다.
 * 2. Play 중 Inspector의 Latest Raw Values 4개가 변하는지 확인한다.
 */
public class UdpSensorInput : MonoBehaviour
{
    public const int SensorCount = 4;

    [SerializeField] private int listenPort = 5005;
    [SerializeField] private int[] latestRawValues = new int[SensorCount];

    private UdpClient receiver;
    private bool hasReceived;
    private float lastReceivedTime;

    public bool HasFreshValue =>
        hasReceived && Time.realtimeSinceStartup - lastReceivedTime < 1f;

    public int GetRawValue(int sensorIndex)
    {
        if (sensorIndex < 0 || sensorIndex >= SensorCount)
            throw new ArgumentOutOfRangeException(nameof(sensorIndex));

        return latestRawValues[sensorIndex];
    }

    private void OnEnable()
    {
        if (latestRawValues == null || latestRawValues.Length != SensorCount)
            latestRawValues = new int[SensorCount];

        try
        {
            receiver = new UdpClient(
                new IPEndPoint(IPAddress.Loopback, listenPort)
            );
            Debug.Log($"FSR 4개 데이터 대기 중: UDP {listenPort}");
        }
        catch (SocketException exception)
        {
            Debug.LogError($"UDP 수신 시작 실패: {exception.Message}");
            enabled = false;
        }
    }

    private void Update()
    {
        if (receiver == null)
            return;

        try
        {
            while (receiver.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] packet = receiver.Receive(ref sender);
                string[] parts = Encoding.ASCII.GetString(packet).Trim().Split(',');

                if (parts.Length != SensorCount + 1 ||
                    !long.TryParse(parts[0], out _))
                    continue;

                int[] nextValues = new int[SensorCount];
                bool valid = true;

                for (int i = 0; i < SensorCount; i++)
                {
                    if (!int.TryParse(parts[i + 1], out nextValues[i]) ||
                        nextValues[i] < 0 || nextValues[i] > 4095)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                    continue;

                Array.Copy(nextValues, latestRawValues, SensorCount);
                hasReceived = true;
                lastReceivedTime = Time.realtimeSinceStartup;
            }
        }
        catch (SocketException exception)
        {
            Debug.LogError($"센서 데이터 수신 오류: {exception.Message}");
            enabled = false;
        }
    }

    private void OnDisable()
    {
        receiver?.Close();
        receiver = null;
        hasReceived = false;
    }
}