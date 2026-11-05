/*
 * FSR 402 압력센서 1개 테스트
 *
 * 연결: 3V3 ─ FSR ─ GPIO0 ─ 10kΩ 저항 ─ GND
 *
 * 실행 방법:
 * 1. Arduino IDE에서 이 파일을 연다.
 * 2. 보드를 ESP32C3 Dev Module, 포트를 COM3으로 선택한다.
 * 3. 업로드한 뒤 시리얼 모니터를 115200 baud로 연다.
 *
 * 출력 형식: 경과 시간(ms),센서 원시값(0~4095)
 * 예: 1200,1834
 */

const int FSR_PIN = 0;
const unsigned long SAMPLE_INTERVAL_MS = 200; // 0.2초마다 한 번 측정
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

  lastSampleTime = now;

  int rawValue = analogRead(FSR_PIN);
  Serial.print(now);
  Serial.print(',');
  Serial.println(rawValue);
}