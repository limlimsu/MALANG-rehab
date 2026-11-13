/*
 * FSR 402 센서 4개 원시값 전송
 *
 * 연결:
 * 센서1의 분압 접점 → GPIO0
 * 센서2의 분압 접점 → GPIO1
 * 센서3의 분압 접점 → GPIO3
 * 센서4의 분압 접점 → GPIO4
 *
 * 실행: Arduino IDE에서 ESP32C3 Dev Module로 업로드
 * 시리얼 속도: 115200 baud
 * 측정 주기: 20 ms (50 Hz)
 * 출력: 경과시간_ms,센서1,센서2,센서3,센서4
 * 예: 1200,1210,980,1105,875
 */

const int FSR_PINS[4] = {0, 1, 3, 4};
const unsigned long SAMPLE_INTERVAL_MS = 20; // 50 Hz
unsigned long lastSampleTime = 0;

void setup() {
  Serial.begin(115200);
  analogReadResolution(12);
}

void loop() {
  unsigned long now = millis();

  if (now - lastSampleTime < SAMPLE_INTERVAL_MS) {
    return;
  }

  // 주기가 밀리지 않도록 이전 기준 시각에 간격을 더한다.
  lastSampleTime += SAMPLE_INTERVAL_MS;
  if (now - lastSampleTime >= SAMPLE_INTERVAL_MS) {
    lastSampleTime = now; // 크게 밀렸으면 현재 시각으로 다시 맞춘다.
  }

  Serial.print(now);

  for (int i = 0; i < 4; i++) {
    Serial.print(',');
    Serial.print(analogRead(FSR_PINS[i]));
  }

  Serial.println();
}