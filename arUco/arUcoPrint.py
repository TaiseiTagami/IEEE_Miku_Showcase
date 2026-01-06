# generate_aruco.py
import argparse
import math
import numpy as np
import cv2
from reportlab.lib.units import mm
from reportlab.pdfgen import canvas
from reportlab.lib.colors import black
import os

DICT_MAP = {
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

def make_marker_image(dict_name, marker_id, size_px):
    dictionary = cv2.aruco.getPredefinedDictionary(DICT_MAP[dict_name])
    img = np.zeros((size_px, size_px), dtype=np.uint8)
    cv2.aruco.generateImageMarker(dictionary, marker_id, size_px, img, 1)  # borderBits=1 (default)
    return img

def save_png_marker(dict_name, marker_id, size_mm, margin_mm, dpi, out_path):
    # Compute pixel sizes
    inch_per_mm = 1.0 / 25.4
    marker_px = max(64, int(round(size_mm * inch_per_mm * dpi)))  # ensure at least 64 px
    margin_px = int(round(margin_mm * inch_per_mm * dpi))
    canvas_w = marker_px + 2 * margin_px
    canvas_h = marker_px + 2 * margin_px

    marker = make_marker_image(dict_name, marker_id, marker_px)

    # Put marker on white canvas with margin
    canvas_img = np.full((canvas_h, canvas_w), 255, dtype=np.uint8)
    canvas_img[margin_px:margin_px+marker_px, margin_px:margin_px+marker_px] = marker

    # Save as PNG
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    cv2.imwrite(out_path, canvas_img)
    print(f"Saved PNG: {out_path} (marker {size_mm} mm + {margin_mm} mm margin, {dpi} DPI)")

def draw_pdf_scale_bar(c, x_mm, y_mm, length_mm=100):
    # Draw a 100 mm scale bar
    c.setStrokeColor(black)
    c.setLineWidth(1)
    c.line(x_mm*mm, y_mm*mm, (x_mm+length_mm)*mm, y_mm*mm)
    c.drawString((x_mm+length_mm+2)*mm, (y_mm-2)*mm, f"{length_mm} mm")

def save_pdf_single_marker(dict_name, marker_id, size_mm, margin_mm, out_pdf, label=True, scale_bar=True):
    page_w_mm = size_mm + 2*margin_mm
    page_h_mm = size_mm + 2*margin_mm

    c = canvas.Canvas(out_pdf, pagesize=(page_w_mm*mm, page_h_mm*mm))

    # Render marker as raster at sufficient DPI, then place exactly sized
    dpi_render = 600
    marker_px = max(64, int(round(size_mm/25.4 * dpi_render)))
    marker_img = make_marker_image(dict_name, marker_id, marker_px)
    tmp_png = out_pdf + f".tmp_marker_{marker_id}.png"
    cv2.imwrite(tmp_png, marker_img)

    # Place marker centered within margins (top-left origin in PDF is bottom-left)
    c.drawImage(tmp_png,
                margin_mm*mm,
                margin_mm*mm,
                width=size_mm*mm,
                height=size_mm*mm,
                preserveAspectRatio=False,
                mask='auto')

    if label:
        c.setFont("Helvetica", 10)
        c.drawString(margin_mm*mm, (margin_mm*mm) + (size_mm*mm) + 2*mm, f"{dict_name} ID {marker_id} | {size_mm:.1f} mm")

    if scale_bar:
        draw_pdf_scale_bar(c, x_mm=margin_mm, y_mm=margin_mm/2.0, length_mm=100)

    c.showPage()
    c.save()
    try:
        os.remove(tmp_png)
    except Exception:
        pass

    print(f"Saved PDF: {out_pdf} (marker {size_mm} mm, margin {margin_mm} mm)")

def save_pdf_grid(dict_name, start_id, rows, cols, size_mm, spacing_mm, margin_mm, out_pdf, label=True, scale_bar=True):
    # Grid layout: each cell has marker size plus spacing (spacing between markers)
    grid_w_mm = cols*size_mm + (cols-1)*spacing_mm
    grid_h_mm = rows*size_mm + (rows-1)*spacing_mm
    page_w_mm = grid_w_mm + 2*margin_mm
    page_h_mm = grid_h_mm + 2*margin_mm

    c = canvas.Canvas(out_pdf, pagesize=(page_w_mm*mm, page_h_mm*mm))

    dpi_render = 600
    marker_px = max(64, int(round(size_mm/25.4 * dpi_render)))
    # Pre-generate temp PNGs for IDs in the sheet
    tmp_files = []
    for i in range(rows*cols):
        mid = start_id + i
        img = make_marker_image(dict_name, mid, marker_px)
        tmp_png = out_pdf + f".tmp_marker_{mid}.png"
        cv2.imwrite(tmp_png, img)
        tmp_files.append((mid, tmp_png))

    # Draw grid
    k = 0
    for r in range(rows):
        for ccol in range(cols):
            mid, tmp_png = tmp_files[k]
            x_mm = margin_mm + ccol*(size_mm + spacing_mm)
            y_mm = margin_mm + (rows-1-r)*(size_mm + spacing_mm)  # place from bottom
            c.drawImage(tmp_png, x_mm*mm, y_mm*mm, width=size_mm*mm, height=size_mm*mm, preserveAspectRatio=False, mask='auto')
            if label:
                c.setFont("Helvetica", 8)
                c.drawString(x_mm*mm, (y_mm*mm) - 3*mm, f"ID {mid}")
            k += 1

    if scale_bar:
        draw_pdf_scale_bar(c, x_mm=margin_mm, y_mm=margin_mm/2.0, length_mm=100)

    c.showPage()
    c.save()

    # Cleanup
    for _, f in tmp_files:
        try:
            os.remove(f)
        except Exception:
            pass

    print(f"Saved PDF grid: {out_pdf} ({rows}x{cols}, marker {size_mm} mm, spacing {spacing_mm} mm, margin {margin_mm} mm)")

def parse_args():
    ap = argparse.ArgumentParser(description="Generate printable ArUco markers (single or grid) at exact physical size.")
    ap.add_argument("--dict", type=str, default="DICT_4X4_50", choices=list(DICT_MAP.keys()), help="ArUco dictionary")
    # Single marker options
    ap.add_argument("--id", type=int, help="Marker ID for single marker")
    # Grid options
    ap.add_argument("--grid-rows", type=int, default=0, help="Rows for a grid (>=1 for grid mode)")
    ap.add_argument("--grid-cols", type=int, default=0, help="Cols for a grid (>=1 for grid mode)")
    ap.add_argument("--start-id", type=int, default=0, help="Start ID for grid markers")
    # Physical sizing
    ap.add_argument("--size-mm", type=float, required=True, help="Marker edge length (outer black border) in mm")
    ap.add_argument("--margin-mm", type=float, default=10.0, help="White margin around marker/page in mm")
    ap.add_argument("--spacing-mm", type=float, default=10.0, help="Spacing between markers in grid in mm")
    # Outputs
    ap.add_argument("--pdf", type=str, help="Output PDF path")
    ap.add_argument("--png", type=str, help="Output PNG path (single marker only)")
    ap.add_argument("--dpi", type=int, default=300, help="PNG DPI for raster output")
    ap.add_argument("--label", action="store_true", help="Add labels to PDF")
    ap.add_argument("--no-scale-bar", action="store_true", help="Disable 100 mm scale bar on PDF")
    return ap.parse_args()

def main():
    args = parse_args()

    # Decide mode
    grid_mode = (args.grid_rows and args.grid_rows > 0) and (args.grid_cols and args.grid_cols > 0)

    if grid_mode:
        if not args.pdf:
            raise SystemExit("Grid mode requires --pdf output.")
        save_pdf_grid(
            dict_name=args.dict,
            start_id=args.start_id,
            rows=args.grid_rows,
            cols=args.grid_cols,
            size_mm=args.size_mm,
            spacing_mm=args.spacing_mm,
            margin_mm=args.margin_mm,
            out_pdf=args.pdf,
            label=args.label,
            scale_bar=not args.no_scale_bar
        )
    else:
        if args.id is None:
            raise SystemExit("Single marker mode requires --id.")
        if args.pdf:
            save_pdf_single_marker(
                dict_name=args.dict,
                marker_id=args.id,
                size_mm=args.size_mm,
                margin_mm=args.margin_mm,
                out_pdf=args.pdf,
                label=args.label,
                scale_bar=not args.no_scale_bar
            )
        if args.png:
            save_png_marker(
                dict_name=args.dict,
                marker_id=args.id,
                size_mm=args.size_mm,
                margin_mm=args.margin_mm,
                dpi=args.dpi,
                out_path=args.png
            )
        if not args.pdf and not args.png:
            raise SystemExit("Specify at least one output: --pdf or --png")

if __name__ == "__main__":
    main()