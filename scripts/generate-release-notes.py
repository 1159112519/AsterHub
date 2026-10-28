#!/usr/bin/env python3
"""Render user-facing GitHub notes from version/platform CHANGELOG sections."""
import argparse
from pathlib import Path
import re
import sys


def render(changelog: str, version: str, platform: str) -> str:
    sections = []
    header = re.compile(r"^## (\d+\.\d+\.\d+) [^\n]*· (Windows|Ubuntu)\s*$", re.MULTILINE)
    headings = list(header.finditer(changelog))
    wanted = {"windows": {"Windows"}, "ubuntu": {"Ubuntu"}, "all": {"Windows", "Ubuntu"}}[platform]
    seen = set()
    for index, match in enumerate(headings):
        if match.group(1) != version or match.group(2) not in wanted:
            continue
        name = match.group(2)
        if name in seen:
            raise ValueError("Duplicate release section: " + version + " / " + name)
        seen.add(name)
        end = headings[index + 1].start() if index + 1 < len(headings) else len(changelog)
        sections.append(changelog[match.start():end].strip())
    if not sections or (platform != "all" and seen != wanted):
        raise ValueError("No changelog section for " + version + " / " + platform)
    body = "\n\n".join(sections) + "\n"
    # Public notes contain user-facing behavior, not internal operators or evidence.
    if re.search(r"hermes|测试员|/root/|C:[\\/]|签名私钥|access[_ -]?key", body, re.IGNORECASE):
        raise ValueError("Release notes contain internal workflow or private-path content")
    return body


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version")
    parser.add_argument("--platform", choices=("windows", "ubuntu", "all"), required=True)
    parser.add_argument("--changelog", type=Path, default=Path(__file__).resolve().parents[1] / "CHANGELOG.md")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if not re.fullmatch(r"\d+\.\d+\.\d+", args.version):
        parser.error("version must be X.Y.Z")
    try:
        body = render(args.changelog.read_text(encoding="utf-8-sig"), args.version, args.platform)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(body, encoding="utf-8")
        print("RELEASE_NOTES=" + str(args.output))
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(body, end="")


if __name__ == "__main__":
    main()
