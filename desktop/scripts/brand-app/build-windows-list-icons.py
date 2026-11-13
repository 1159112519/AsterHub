"""Center the native small icons shown in Windows process and window lists.

The original app, shortcut, tray and web artwork is left intact. Regenerate
this asset whenever the approved Windows icon master changes.
"""
from pathlib import Path
import io
import struct

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2] / "host"
SHELL_SIZES = {16, 20, 24, 32}


def offsets(image):
    """Alpha center and optical center on dark and light Windows backgrounds."""
    rgba = np.asarray(image, dtype=float)
    alpha = rgba[:, :, 3] / 255
    y, x = np.indices(alpha.shape)
    def center(weights):
        return [(weights * (x + .5)).sum() / weights.sum() - image.width / 2,
                (weights * (y + .5)).sum() / weights.sum() - image.height / 2]
    return np.array([center(alpha)] + [
        center(alpha * np.linalg.norm(rgba[:, :, :3] - background, axis=2))
        for background in (30, 246)
    ])


def fit(master, original):
    size = original.width
    scale = master.width / size
    origin = offsets(master)[0]
    def sample(adjustment):
        x, y = origin + scale * adjustment
        return master.transform((4 * size, 4 * size), Image.Transform.AFFINE,
            (scale / 4, 0, x, 0, scale / 4, y), resample=Image.Resampling.BICUBIC
        ).resize((size, size), Image.Resampling.LANCZOS)
    current = offsets(sample(np.zeros(2)))
    # Minimize the worst offset across both system themes and the alpha outline.
    aligned = sample((current.min(axis=0) + current.max(axis=0)) / 2)
    return aligned if np.abs(offsets(aligned)).max() < np.abs(offsets(original)).max() else original


def save_ico(path, frames):
    entries, chunks = [], []
    offset = 6 + 16 * len(frames)
    for size, frame in frames:
        buffer = io.BytesIO()
        frame.save(buffer, format="PNG")
        data = buffer.getvalue()
        pixel_size = size if size < 256 else 0
        entries.append(struct.pack("<BBBBHHII", pixel_size, pixel_size, 0, 0, 1, 32, len(data), offset))
        chunks.append(data)
        offset += len(data)
    path.write_bytes(struct.pack("<HHH", 0, 1, len(frames)) + b"".join(entries + chunks))


def frames_for(path):
    with Image.open(path) as source:
        master = source.ico.getimage((256, 256)).convert("RGBA")
        result = []
        for size, _ in sorted(source.ico.sizes()):
            frame = source.ico.getimage((size, size)).convert("RGBA")
            result.append((size, fit(master, frame) if size in SHELL_SIZES else frame))
    return result


if __name__ == "__main__":
    for value in range(1, 10):
        folder = ROOT / "brand-icons" / f"{value:02}"
        frames = frames_for(folder / "app.ico")
        save_ico(folder / "window.ico", frames)
        if value == 1:
            save_ico(ROOT / "AsterHub.Process.ico", frames)
    print("CENTERED_WINDOWS_LIST_ICON_SETS=9; embedded fallback=01")
