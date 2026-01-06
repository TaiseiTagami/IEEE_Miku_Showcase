import cv2
import mediapipe as mp
import math
import numpy as np
import socket
import serial
import serial.tools.list_ports
import time
import json
import os

# --- Combined Configuration ---

# Use Script 1's angle logic (DO NOT CHANGE)
FOV_X_DEG = 40.0  # approximate horizontal FOV of your camera (Script1)
FOV_Y_DEG = 20.0  # approximate vertical FOV of your camera (Script1)
WINDOW_W = 640
WINDOW_H = 480

ALPHA = 0.3  # Script1 smoothing for angles (keep Script1's logic)

# Script2 settings to keep
CALIB_JSON_PATH = "calib.json"
UDP_IP = "127.0.0.1"
UDP_PORT = 5005

Z_FACTOR = 1.0
X_FACTOR = 1.0
Y_FACTOR = 1.0
MIN_Z = 0.6

MAX_X = 2.0
MAX_Y = 2.0
MAX_Z = 2.0

# Extra smoothing from Script2 (for Arduino-angles & 3D pos smoothing)
ANGLE_SMOOTH_ALPHA = 0.1   # used to smooth incoming Arduino-reported angles
POS_SMOOTH_ALPHA = 0.3     # used to smooth 3D position

# Serial port settings
BAUD = 115200
SERIAL_PORT = "COM9"  # change as needed

# 3D face model points and indices (Script2)
model_points_3d = np.array([
    (0.0,  0.0,   0.0),   # Nose Tip (Landmark 1)
    (0.0, -0.110, 0.0),   # Chin (152)
    (-0.032, -0.015, 0.010),  # Left Eye Inner Corner (133)
    ( 0.032, -0.015, 0.010),  # Right Eye Inner Corner (362)
    (-0.050, -0.070, 0.000),  # Left Mouth Corner (61)
    ( 0.050, -0.070, 0.000)   # Right Mouth Corner (291)
], dtype=np.float64)

face_landmarks_indices = [1, 152, 133, 362, 61, 291]


# --- Helper functions ---

def focal_length_from_fov(fov_deg, dimension_pixels):
    fov_rad = math.radians(fov_deg)
    return dimension_pixels / (2.0 * math.tan(fov_rad / 2.0))


def load_calibration(json_path):
    # Placeholder loader: if user has a calib.json, try to parse it into K and dist.
    # If structure unknown, return None to fallback to default intrinsics.
    if not os.path.exists(json_path):
        # No calibration file present
        return None
    try:
        with open(json_path, "r") as f:
            data = json.load(f)
        # Expecting camera matrix K and distortion 'dist' maybe saved as lists
        K = None
        dist = None
        img_size = None
        extrinsics = None
        if "K" in data:
            K = np.array(data["K"], dtype=np.float64)
        if "dist" in data:
            dist = np.array(data["dist"], dtype=np.float64).reshape(-1, 1)
        if "img_size" in data:
            img_size = tuple(data["img_size"])
        if "extrinsics" in data:
            extrinsics = data["extrinsics"]
        return K, dist, img_size, extrinsics
    except Exception as e:
        print("Failed to load calibration:", e)
        # Let caller fallback
        raise


def undistort_points_to_pixels(points_xy, K, dist):
    # If a proper distortion model is present, undistort the 2D image points.
    # This function expects 'points_xy' shaped (N,2), K camera matrix and 'dist' as numpy array.
    # If dist is None or empty, return original.
    if dist is None:
        return points_xy
    pts = np.array(points_xy, dtype=np.float64).reshape(-1, 1, 2)
    undist = cv2.undistortPoints(pts, cameraMatrix=K, distCoeffs=dist, P=K)
    undist = undist.reshape(-1, 2)
    return undist


def setup_serial():
    ports = list(serial.tools.list_ports.comports())
    print("Detected serial ports:")
    for p in ports:
        print("  ", p.device, "-", p.description)
    try:
        ser = serial.Serial(SERIAL_PORT, BAUD, timeout=0.1)
        print(f"Opened serial {SERIAL_PORT} at {BAUD} baud")
        time.sleep(2.0)  # allow Arduino to reset
        ser.reset_input_buffer()
        return ser
    except Exception as e:
        print("Could not open serial port:", e)
        return None


# --- Main routine ---

def main():
    # Try to load calibration (Script2 feature). If fails, fallback to None.
    try:
        K, dist, calib_img_size, extrinsics = load_calibration(CALIB_JSON_PATH)
    except Exception:
        K, dist, calib_img_size, extrinsics = None, None, None, None

    # Default intrinsics (from Script1's FOV logic)
    default_FX = focal_length_from_fov(FOV_X_DEG, WINDOW_W)
    default_FY = focal_length_from_fov(FOV_Y_DEG, WINDOW_H)
    default_CX = WINDOW_W / 2.0
    default_CY = WINDOW_H / 2.0
    default_K = np.array([[default_FX, 0.0, default_CX],
                          [0.0, default_FY, default_CY],
                          [0.0, 0.0, 1.0]], dtype=np.float64)

    # Setup UDP (Script2)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    # Setup Serial (Script2)
    ser = setup_serial()

    # MediaPipe and drawing (common)
    mp_face_mesh = mp.solutions.face_mesh
    face_mesh = mp_face_mesh.FaceMesh(max_num_faces=1, min_detection_confidence=0.5)
    mp_drawing = mp.solutions.drawing_utils

    # Open webcam
    cap = cv2.VideoCapture(0)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, WINDOW_W)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, WINDOW_H)

    # Smoothing state (Script1 for angles, Script2 for Arduino smoothing & pos)
    smoothed_yaw_deg = None
    smoothed_pitch_deg = None
    smoothed_pos = None

    smoothed_arduino_yaw_deg = 0.0
    smoothed_arduino_pitch_deg = 0.0

    print("Press 'q' to quit")

    try:
        while True:
            # Read incoming Arduino angle reports (Script2 feature)
            if ser is not None and ser.is_open:
                try:
                    line = ser.readline().decode('utf-8', errors='ignore').strip()
                    if line:
                        # Expect "yaw,pitch" from Arduino
                        # Keep smoothing of Arduino-reported servo angles
                        parts = line.split(',')
                        if len(parts) >= 2:
                            try:
                                arduino_yaw_deg = float(parts[0])
                                arduino_pitch_deg = float(parts[1])
                                # Smooth servo angles
                                if smoothed_arduino_yaw_deg == 0.0 and smoothed_arduino_pitch_deg == 0.0:
                                    smoothed_arduino_yaw_deg = arduino_yaw_deg
                                    smoothed_arduino_pitch_deg = arduino_pitch_deg
                                else:
                                    smoothed_arduino_yaw_deg = ANGLE_SMOOTH_ALPHA * arduino_yaw_deg + (1.0 - ANGLE_SMOOTH_ALPHA) * smoothed_arduino_yaw_deg
                                    smoothed_arduino_pitch_deg = ANGLE_SMOOTH_ALPHA * arduino_pitch_deg + (1.0 - ANGLE_SMOOTH_ALPHA) * smoothed_arduino_pitch_deg
                                # Optional debug print from Arduino
                                print("Arduino:", line)
                            except ValueError:
                                # Not two floats, just print raw line
                                print("Arduino raw:", line)
                        else:
                            # Could be a status string; print it
                            print("Arduino:", line)
                except Exception as e:
                    print("Serial read error:", e)

            # Read camera
            ret, frame = cap.read()
            if not ret:
                break

            # Mirror frame (Script1)
            frame = cv2.flip(frame, 1)
            h, w = frame.shape[:2]

            # Use MediaPipe to detect face
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = face_mesh.process(rgb)

            if results.multi_face_landmarks:
                face = results.multi_face_landmarks[0]

                # --- Angle logic from Script 1 (DO NOT CHANGE) ---
                # Nose tip (468-pt mesh index 1)
                lm = face.landmark[1]
                px = lm.x * w
                py = lm.y * h

                # Determine intrinsics: if calibration present, use it; otherwise use Script1 defaults
                if K is not None:
                    FX = K[0, 0]
                    FY = K[1, 1]
                    CX = K[0, 2]
                    CY = K[1, 2]
                else:
                    FX = default_FX
                    FY = default_FY
                    CX = default_CX
                    CY = default_CY

                # Convert pixel offset to angles (pinhole model)
                yaw_rad = math.atan2((px - CX), FX)     # horizontal angle
                pitch_rad = math.atan2((py - CY), FY)   # vertical angle

                yaw_deg = math.degrees(yaw_rad)
                pitch_deg = -math.degrees(pitch_rad)

                # Exponential smoothing (Script1 uses ALPHA)
                if smoothed_yaw_deg is None:
                    smoothed_yaw_deg = yaw_deg
                    smoothed_pitch_deg = pitch_deg
                else:
                    smoothed_yaw_deg = ALPHA * yaw_deg + (1 - ALPHA) * smoothed_yaw_deg
                    smoothed_pitch_deg = ALPHA * pitch_deg + (1 - ALPHA) * smoothed_pitch_deg

                # Visualization (Script1)
                cv2.circle(frame, (int(px), int(py)), 6, (0, 0, 255), -1)
                text = f"Yaw: {smoothed_yaw_deg:+.1f} deg | Pitch: {smoothed_pitch_deg:+.1f} deg"
                cv2.putText(frame, text, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255,255,255), 2)
                mp_drawing.draw_landmarks(
                    frame, face, mp_face_mesh.FACEMESH_TESSELATION,
                    landmark_drawing_spec=None,
                    connection_drawing_spec=mp_drawing.DrawingSpec((0,255,0), 1, 1)
                )

                # --- Sending to Arduino: Use Script1's working format/logic ---
                # Always send the smoothed angles (Script1 sends angles when face detected)
                message = f"{smoothed_yaw_deg:.3f},{-smoothed_pitch_deg:.3f}\n"
                print("Sending:", message.strip())
                if ser is not None and ser.is_open:
                    try:
                        ser.write(message.encode('utf-8'))
                    except Exception as e:
                        print("Serial write error:", e)

                # --- 3D Position Tracking using PnP (Script2 feature) ---
                image_points_2d = []
                for idx in face_landmarks_indices:
                    lm2 = face.landmark[idx]
                    image_points_2d.append((lm2.x * w, lm2.y * h))
                image_points_2d = np.array(image_points_2d, dtype=np.float64)

                # Undistort if distortion available
                undist = image_points_2d
                try:
                    if dist is not None:
                        undist = undistort_points_to_pixels(image_points_2d, K if K is not None else default_K, dist)
                except Exception as e:
                    print("Undistort points failed:", e)
                    undist = image_points_2d

                # Solve PnP
                try:
                    success, rvec, tvec = cv2.solvePnP(
                        objectPoints=model_points_3d,
                        imagePoints=undist,
                        cameraMatrix=(K if K is not None else default_K),
                        distCoeffs=(dist if dist is not None else None),
                        flags=cv2.SOLVEPNP_ITERATIVE
                    )
                except Exception as e:
                    success = False
                    print("solvePnP error:", e)

                if success:
                    p_cam = tvec.reshape(3).astype(np.float64)

                    # Smooth 3D position (Script2)
                    if smoothed_pos is None:
                        smoothed_pos = p_cam
                    else:
                        smoothed_pos = POS_SMOOTH_ALPHA * p_cam + (1.0 - POS_SMOOTH_ALPHA) * smoothed_pos

                    # Clamp positions
                    x = max(-MAX_X, min(smoothed_pos[0], MAX_X))
                    y = max(-MAX_Y, min(smoothed_pos[1], MAX_Y))
                    z = min(max(smoothed_pos[2], MIN_Z), MAX_Z)
                    smoothed_pos = np.array([x, y, z], dtype=np.float64)

                    # Send position and Arduino-angle-over-UDP (Script2 behavior)
                    arduino_angle_message = f"{smoothed_arduino_yaw_deg:.3f},{smoothed_arduino_pitch_deg:.3f}"
                    pos_message = f"{-smoothed_pos[0]*X_FACTOR:.3f},{-smoothed_pos[1]*Y_FACTOR:.3f},{smoothed_pos[2]*Z_FACTOR:.3f}"

                    try:
                        sock.sendto(pos_message.encode('utf-8'), (UDP_IP, UDP_PORT))
                        print("Sending position:", pos_message.strip())
                        sock.sendto(arduino_angle_message.encode('utf-8'), (UDP_IP, UDP_PORT))
                        print("Sending angles:", arduino_angle_message.strip())
                    except Exception as e:
                        print("UDP send error:", e)

                    # Overlay extra visualization text
                    cv2.putText(frame, 
                        f"3D Pos: {-smoothed_pos[0]:+.2f}, {-smoothed_pos[1]:+.2f}, {smoothed_pos[2]:+.2f}", 
                        (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0,255,0), 2)
                    cv2.putText(frame, 
                        f"Arduino Pos: {smoothed_arduino_yaw_deg:+.2f}, {smoothed_arduino_pitch_deg:+.2f}", 
                        (10, 90), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0,255,255), 2)

            else:
                # No face detected: follow Script1's behavior and send zeros so Arduino centers
                message = "0.000,0.000\n"
                print("Sending:", message.strip())
                if ser is not None and ser.is_open:
                    try:
                        ser.write(message.encode('utf-8'))
                    except Exception as e:
                        print("Serial write error:", e)

            # Show frame
            cv2.imshow("Face Tracking", frame)
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    finally:
        cap.release()
        cv2.destroyAllWindows()
        face_mesh.close()
        if ser:
            try:
                ser.close()
            except Exception:
                pass
        sock.close()


if __name__ == "__main__":
    main()