"""
ESP32 센서 원시값을 CSV로 저장한다.

ESP32 전송 형식: 측정시각_ms,센서원시값
가상 데이터 시험: python tools/sensor_logger.py --demo
실제 센서 수신: python tools/sensor_logger.py --port COM3
종료: Ctrl+C
"""

import argparse
import csv
import time
from datetime import datetime
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--demo", action="store_true")
parser.add_argument("--port", help="ESP32의 COM 포트 (예: COM3)")
args = parser.parse_args()

if not args.demo and not args.port:
    parser.error("--demo 또는 --port를 지정하세요.")

output_dir = Path("data")
output_dir.mkdir(exist_ok=True)
output_file = output_dir / f"sensor_{datetime.now():%Y%m%d_%H%M%S}.csv"

connection = None

try:
    if not args.demo:
        import serial
        connection = serial.Serial(args.port, 115200, timeout=1)
        print(f"{args.port}에서 수신 중... Ctrl+C로 종료")

    with output_file.open("w", newline="", encoding="utf-8") as file:
        writer = csv.writer(file)
        writer.writerow(["pc_time", "device_ms", "raw_value"])

        count = 0
        while True:
            if args.demo:
                if count >= 100:
                    break
                device_ms = count * 20
                raw_value = 400 if count < 25 or count >= 75 else 1800
                time.sleep(0.02)
                line = f"{device_ms},{raw_value}"
            else:
                line = connection.readline().decode(
                    "utf-8", errors="replace"
                ).strip()

            parts = line.split(",")
            if len(parts) != 2 or not all(part.isdigit() for part in parts):
                continue  # 부팅 메시지나 형식이 다른 줄은 건너뛴다.

            writer.writerow([
                datetime.now().isoformat(timespec="milliseconds"),
                parts[0],
                parts[1],
            ])
            file.flush()
            count += 1

            if count % 25 == 0:
                print(f"{count}개 기록")

except KeyboardInterrupt:
    print("수신 종료")
finally:
    if connection is not None:
        connection.close()

print(f"저장 위치: {output_file}")