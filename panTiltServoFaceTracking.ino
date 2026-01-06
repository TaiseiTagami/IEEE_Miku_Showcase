#include <Servo.h>

// ---- Servo pins ----
const int YAW_PIN = 9;
const int PITCH_PIN = 10;

// ---- Servo objects ----
Servo yawServo;
Servo pitchServo;

// ---- Control parameters ----
int yawCenterDeg = 90;
int pitchCenterDeg = 20;        // was 10; 90 gives symmetric range
const bool INVERT_YAW = false;
const bool INVERT_PITCH = true; // often needed for camera tilt

// Gains: make small changes visible
float Kp_yaw   = 0.08f;          // try 0.3–1.0; tune to taste
float Kp_pitch = 0.07f;

// Range: widen if your mechanics allow
const int SERVO_MIN = 0;
const int SERVO_MAX = 180;

// Deadband
const float YAW_DEADBAND_DEG   = 5.0f;
const float PITCH_DEADBAND_DEG = 1.0f;

const unsigned long COMMAND_TIMEOUT_MS = 500;

// ---- State (use float accumulators) ----
float yawPosF   = yawCenterDeg;
float pitchPosF = pitchCenterDeg;
unsigned long lastCmdMs = 0;

void setup() {
  yawServo.attach(YAW_PIN);
  pitchServo.attach(PITCH_PIN);
  writeServos(); // writes from yawPosF/pitchPosF

  Serial.begin(115200);
  Serial.println("Setting up!");
  delay(1000);
}

void loop() {
  // Non-blocking read: process complete lines only
  static String line;
  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n') {
      line.trim();
      if (line.length() > 0) {
        processLine(line);
        lastCmdMs = millis();
      }
      line = "";
      break; // process one line per loop iteration
    } else {
      line += c;
    }
  }

  // Fail-safe: drift to center if no data
  if (millis() - lastCmdMs > COMMAND_TIMEOUT_MS) {
    yawPosF   = approachF(yawPosF,   yawCenterDeg, 5.0f);
    pitchPosF = approachF(pitchPosF, pitchCenterDeg, 3.0f);
    writeServos();
  }
}

void processLine(const String& line) {
  int commaIdx = line.indexOf(',');
  if (commaIdx < 0) return;

  float yawDeg   = line.substring(0, commaIdx).toFloat();
  float pitchDeg = line.substring(commaIdx + 1).toFloat();

  if (INVERT_YAW)   yawDeg   = -yawDeg;
  if (INVERT_PITCH) pitchDeg = -pitchDeg;

  if (fabsf(yawDeg)   < YAW_DEADBAND_DEG)   yawDeg   = 0.0f;
  if (fabsf(pitchDeg) < PITCH_DEADBAND_DEG) pitchDeg = 0.0f;

  // Use our tracked positions, not servo.read()
  float targetYawF   = yawPosF   + (Kp_yaw   * yawDeg);
  float targetPitchF = pitchPosF + (Kp_pitch * pitchDeg);

  // Clamp
  targetYawF   = constrainF(targetYawF,   SERVO_MIN, SERVO_MAX);
  targetPitchF = constrainF(targetPitchF, SERVO_MIN, SERVO_MAX);

  // Smooth approach (now effective)
  yawPosF   = approachF(yawPosF,   targetYawF,   5.0f);
  pitchPosF = approachF(pitchPosF, targetPitchF, 5.0f);

  writeServos();

  // Report error relative to center (if useful)
  Serial.print(yawCenterDeg - (int)round(yawPosF));
  Serial.print(",");
  Serial.println(pitchCenterDeg - (int)round(pitchPosF));
}

void writeServos() {
  int yawDeg   = (int)round(yawPosF);
  int pitchDeg = (int)round(pitchPosF);
  yawServo.write(yawDeg);
  pitchServo.write(pitchDeg);
}

float approachF(float current, float target, float step) {
  if (current < target) return min(current + step, target);
  if (current > target) return max(current - step, target);
  return current;
}

float constrainF(float x, float a, float b) { return max(a, min(b, x)); }
float fabsf(float x) { return x >= 0 ? x : -x; }