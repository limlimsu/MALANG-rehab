"""
ESP32의 FSR 원시값을 Unity로 전송한다.

실행: python tools/sensor_bridge.py --port COM3
종료: Ctrl+C
아두이노 시리얼 모니터는 먼저 닫는다.

입력 및 출력: device_ms,raw1,raw2,...,rawN  (N = 4 또는 5)
펌웨어 전송 주기: 20 ms (50 Hz). 받은 줄은 지연 없이 바로 전달한다.
Unity 수신 주소: 127.0.0.1:5005
"""

import argparse
import socket
import time

import serial

MIN_CHANNELS = 4
MAX_CHANNELS = 5
RAW_MAX = 4095
STATUS_INTERVAL_S = 1.0  # 콘솔 출력 주기. 50 Hz 전부 출력하면 전달이 늦어진다.

parser = argparse.ArgumentParser()
parser.add_argument("--port", required=True, help="ESP32 포트, 예: COM3")
parser.add_argument("--udp-port", type=int, default=5005, help="Unity 수신 포트")
args = parser.parse_args()

udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
target = ("127.0.0.1", args.udp_port)

try:
    # timeout을 짧게 두어 Ctrl+C에 빨리 반응하게 한다.
    with serial.Serial(args.port, 115200, timeout=0.1) as esp32:
        esp32.reset_input_buffer()  # 연결 전에 쌓인 오래된 줄은 버린다.
        print(f"{args.port} → Unity UDP {args.udp_port} 전송 중 (Ctrl+C로 종료)")

        sent = 0
        last_status = time.monotonic()
        last_values = []

        while True:
            line = esp32.readline().decode("utf-8", errors="replace").strip()
            parts = line.split(",")
            channels = len(parts) - 1

            # 시간 1개 + 센서값 4~5개가 있는 줄만 사용한다.
            if (channels < MIN_CHANNELS or channels > MAX_CHANNELS
                    or not all(part.isdigit() for part in parts)):
                continue

            raw_values = [int(value) for value in parts[1:]]
            if any(value > RAW_MAX for value in raw_values):
                continue

            udp.sendto(line.encode("ascii"), target)
            sent += 1
            last_values = raw_values

            now = time.monotonic()
            if now - last_status >= STATUS_INTERVAL_S:
                rate = sent / (now - last_status)
                print(f"{rate:5.1f} Hz | 센서값:", *last_values)
                sent = 0
                last_status = now

except KeyboardInterrupt:
    print("\n센서 전송 종료")
finally:
    udp.close()
