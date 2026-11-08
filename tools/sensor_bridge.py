"""
ESP32의 FSR 4개 값을 Unity로 전송한다.

실행: python tools/sensor_bridge.py --port COM3
종료: Ctrl+C
아두이노 시리얼 모니터는 먼저 닫는다.

입력 및 출력: device_ms,raw1,raw2,raw3,raw4
Unity 수신 주소: 127.0.0.1:5005
"""

import argparse
import socket

import serial

parser = argparse.ArgumentParser()
parser.add_argument("--port", required=True, help="ESP32 포트, 예: COM3")
args = parser.parse_args()

udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

try:
    with serial.Serial(args.port, 115200, timeout=1) as esp32:
        print(f"{args.port} → Unity UDP 5005 전송 중 (Ctrl+C로 종료)")

        while True:
            line = esp32.readline().decode("utf-8", errors="replace").strip()
            parts = line.split(",")

            # 시간 1개 + 센서값 4개가 있는 줄만 사용한다.
            if len(parts) != 5 or not all(part.isdigit() for part in parts):
                continue

            raw_values = [int(value) for value in parts[1:]]
            if any(value < 0 or value > 4095 for value in raw_values):
                continue

            udp.sendto(line.encode("ascii"), ("127.0.0.1", 5005))
            print("센서값:", *raw_values)

except KeyboardInterrupt:
    print("\n센서 전송 종료")
finally:
    udp.close()