import cv2
import numpy as np
import socket
import math
import json
import os
from collections import defaultdict

# --------------- SETTINGS ---------------
CALIB_JSON_PATH = "calib.json"

# Physical marker side length in meters (e.g., 0.04 for 40 mm)
MARKER_SIZE_M = 0.04

# Choose dictionary (must match the printed markers)
ARUCO_DICT_NAME = "DICT_4X4_50"

# If you only want a specific marker, set TARGET_ID to an integer; otherwise None
TARGET_ID = None
# If TARGET_ID is None, choose the nearest marker to send
SEND_NEAREST = True

# Visualization
MIRROR_FRAME = True
DRAW_AXES = True

# UDP output (same as your previous setup)
UDP_IP = "127.0.0.1"
UDP_PORT = 5005

# Scale factors for output
X_FACTOR = 2.0
Y_FACTOR = 2.0
Z_FACTOR = 4.0

# Smoothing (exponential)
SMOOTH_ALPHA = 0.3

# --------------- CALIBRATION LOADING ---------------
def load_calibration(json_path):
    if not os.path.exists(json_path):
        raise FileNotFoundError(f"Calibration JSON not found: {json_path}")

    with open(json_path, "r") as f:
        calib = json.load(f)

    intr = calib["intrinsics"]
    fx, fy, cx, cy = intr["fx"], intr["fy"], intr["cx"], intr["cy"]
    img_w, img_h = intr["imageWidth"], intr["imageHeight"]
    dist_coeffs = np.array(intr["distCoeffs"], dtype=np.float64).reshape(-1, 1)

    K = np.array([[fx, 0.0, cx],
                  [0.0, fy, cy],
                  [0.0, 0.0, 1.0]], dtype=np.float64)

    extrinsics = calib.get("extrinsics", None)
    return K, dist_coeffs, (img_w, img_h), extrinsics

# --------------- EXTRINSICS HELPERS ---------------
def euler_xyz_to_R(rx_deg, ry_deg, rz_deg):
    rx = math.radians(rx_deg)
    ry = math.radians(ry_deg)
    rz = math.radians(rz_deg)

    Rx = np.array([[1, 0, 0],
                   [0, math.cos(rx), -math.sin(rx)],
                   [0, math.sin(rx),  math.cos(rx)]], dtype=np.float64)
    Ry = np.array([[ math.cos(ry), 0, math.sin(ry)],
                   [0,            1, 0           ],
                   [-math.sin(ry), 0, math.cos(ry)]], dtype=np.float64)
    Rz = np.array([[math.cos(rz), -math.sin(rz), 0],
                   [math.sin(rz),  math.cos(rz), 0],
                   [0,             0,            1]], dtype=np.float64)

    # XYZ -> R = Rz * Ry * Rx (apply X, then Y, then Z)
    return Rz @ Ry @ Rx

def apply_camera_to_screen_transform(p_cam, extrinsics):
    if extrinsics is None or "cameraToScreen" not in extrinsics:
        return None
    t = np.array(extrinsics["cameraToScreen"]["position_m"], dtype=np.float64).reshape(3)
    rx, ry, rz = extrinsics["cameraToScreen"]["orientationEulerXYZ_deg"]
    R_cs = euler_xyz_to_R(rx, ry, rz)
    return R_cs @ p_cam + t

# --------------- ARUCO DICTIONARY ---------------
def get_aruco_dictionary(name):
    name_map = {
        "DICT_4X4_50": cv2.aruco.DICT_4X4_50,
        "DICT_4X4_100": cv2.aruco.DICT_4X4_100,
        "DICT_4X4_250": cv2.aruco.DICT_4X4_250,
        "DICT_4X4_1000": cv2.aruco.DICT_4X4_1000,
        "DICT_5X5_50": cv2.aruco.DICT_5X5_50,
        "DICT_5X5_100": cv2.aruco.DICT_5X5_100,
        "DICT_5X5_250": cv2.aruco.DICT_5X5_250,
        "DICT_5X5_1000": cv2.aruco.DICT_5X5_1000,
        "DICT_6X6_50": cv2.aruco.DICT_6X6_50,
        "DICT_6X6_100": cv2.aruco.DICT_6X6_100,
        "DICT_6X6_250": cv2.aruco.DICT_6X6_250,
        "DICT_6X6_1000": cv2.aruco.DICT_6X6_1000,
        "DICT_7X7_50": cv2.aruco.DICT_7X7_50,
        "DICT_7X7_100": cv2.aruco.DICT_7X7_100,
        "DICT_7X7_250": cv2.aruco.DICT_7X7_250,
        "DICT_7X7_1000": cv2.aruco.DICT_7X7_1000,
        "DICT_ARUCO_ORIGINAL": cv2.aruco.DICT_ARUCO_ORIGINAL,
    }
    return cv2.aruco.getPredefinedDictionary(name_map[name])

# --------------- MAIN ---------------
def main():
    # Load calibration
    try:
        K, dist, calib_img_size, extrinsics = load_calibration(CALIB_JSON_PATH)
        print("Loaded calibration:")
        print(f"- Intrinsics: fx={K[0,0]:.2f}, fy={K[1,1]:.2f}, cx={K[0,2]:.2f}, cy={K[1,2]:.2f}")
        print(f"- Distortion: {dist.ravel().tolist()}")
        if extrinsics:
            print("- Extrinsics mode:", extrinsics.get("mode", "unknown"))
    except Exception as e:
        print("ERROR loading calibration:", e)
        print("Falling back to no distortion and approximate intrinsics.")
        dist = np.zeros((4, 1), dtype=np.float64)
        extrinsics = None
        K = None
        calib_img_size = None

    # UDP
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    # Video
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Error: Cannot open webcam.")
        return

    # ArUco detector
    dictionary = get_aruco_dictionary(ARUCO_DICT_NAME)
    detector_params = cv2.aruco.DetectorParameters()
    detector = cv2.aruco.ArucoDetector(dictionary, detector_params)

    # Smoothing storage per marker id
    smoothed_tvecs = defaultdict(lambda: None)
    smoothed_screen = defaultdict(lambda: None)

    print("Press 'q' to quit.")
    while True:
        ok, frame = cap.read()
        if not ok:
            print("Frame read failed.")
            break

        if MIRROR_FRAME:
            frame = cv2.flip(frame, 1)

        h, w = frame.shape[:2]

        # Fallback intrinsics if none
        if K is None:
            FOV_X_DEG = 90.0
            FOV_Y_DEG = 45.0
            FX = w / (2.0 * math.tan(math.radians(FOV_X_DEG) / 2.0))
            FY = h / (2.0 * math.tan(math.radians(FOV_Y_DEG) / 2.0))
            CX = w / 2.0
            CY = h / 2.0
            K = np.array([[FX, 0.0, CX],
                          [0.0, FY, CY],
                          [0.0, 0.0, 1.0]], dtype=np.float64)

        if calib_img_size and (w != calib_img_size[0] or h != calib_img_size[1]):
            cv2.putText(frame, f"Warning: capture {w}x{h} != calib {calib_img_size[0]}x{calib_img_size[1]}",
                        (20, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 2)

        # Detect markers
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        corners, ids, _ = detector.detectMarkers(gray)

        chosen_id = None
        chosen_tvec = None
        chosen_rvec = None

        if ids is not None and len(ids) > 0:
            # Estimate pose of each marker
            rvecs, tvecs, _ = cv2.aruco.estimatePoseSingleMarkers(corners, MARKER_SIZE_M, K, dist)

            # Draw markers
            cv2.aruco.drawDetectedMarkers(frame, corners, ids)

            # Optionally draw axes and choose marker to send
            candidates = []
            for i, marker_id in enumerate(ids.flatten()):
                rvec = rvecs[i].reshape(3, 1)
                tvec = tvecs[i].reshape(3)

                if DRAW_AXES:
                    # Axis length relative to marker size
                    axis_len = MARKER_SIZE_M * 0.5
                    cv2.drawFrameAxes(frame, K, dist, rvec, tvec.reshape(3,1), axis_len)

                # Collect candidates for nearest selection
                candidates.append((marker_id, tvec, rvec))

                # Text overlay
                x, y, z = tvec.tolist()
                cv2.putText(frame, f"ID {marker_id}: ({x:+.2f},{y:+.2f},{z:+.2f}) m",
                            (20, 60 + 24 * i), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 0), 2)

            # Choose marker to send
            if TARGET_ID is not None:
                for mid, tvec, rvec in candidates:
                    if mid == TARGET_ID:
                        chosen_id, chosen_tvec, chosen_rvec = mid, tvec, rvec
                        break
            elif SEND_NEAREST:
                # Nearest = smallest z (closest to camera)
                mid, tvec, rvec = min(candidates, key=lambda c: c[1][2])
                chosen_id, chosen_tvec, chosen_rvec = mid, tvec, rvec
            else:
                # Default: first detected
                chosen_id, chosen_tvec, chosen_rvec = candidates[0]

        # Send and display for the chosen marker
        if chosen_tvec is not None:
            mid = chosen_id
            p_cam = chosen_tvec.astype(np.float64)

            # Smoothing
            prev = smoothed_tvecs[mid]
            if prev is None:
                smoothed_tvecs[mid] = p_cam.copy()
            else:
                smoothed_tvecs[mid] = SMOOTH_ALPHA * p_cam + (1.0 - SMOOTH_ALPHA) * prev

            # Optional screen transform
            p_screen = apply_camera_to_screen_transform(smoothed_tvecs[mid], extrinsics) if extrinsics else None

            # Show chosen marker info
            xc, yc, zc = smoothed_tvecs[mid].tolist()
            cv2.putText(frame, f"Chosen ID {mid} Cam XYZ (m): {xc:+.2f}, {yc:+.2f}, {zc:+.2f}",
                        (20, h - 60), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)

            message = None
            use_screen_coords_for_udp = (extrinsics is not None and p_screen is not None)

            if use_screen_coords_for_udp:
                # Smooth screen coords too
                prev_s = smoothed_screen[mid]
                if prev_s is None:
                    smoothed_screen[mid] = p_screen.copy()
                else:
                    smoothed_screen[mid] = SMOOTH_ALPHA * p_screen + (1.0 - SMOOTH_ALPHA) * prev_s

                xs, ys, zs = smoothed_screen[mid].tolist()
                cv2.putText(frame, f"Screen XYZ (m): {xs:+.2f}, {ys:+.2f}, {zs:+.2f}",
                            (20, h - 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 200, 0), 2)
                # Send in screen frame (no sign flips)
                message = f"{xs*X_FACTOR:.3f},{ys*Y_FACTOR:.3f},{zs*Z_FACTOR:.3f}"
            else:
                # Keep the same mapping as your previous camera-coord UDP:
                # Unity mapping used: (-X_cam, -Y_cam, +Z_cam)
                message = f"{-xc*X_FACTOR:.3f},{-yc*Y_FACTOR:.3f},{zc*Z_FACTOR:.3f}"

            if message is not None:
                sock.sendto(message.encode("utf-8"), (UDP_IP, UDP_PORT))

        # Display
        cv2.imshow("ArUco 3D Tracking (meters)", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()
    sock.close()

if __name__ == "__main__":
    main()