#!/usr/bin/env python3
"""
CodeMaster Release & Quality Verification Engine
Validates version consistency across .NET, Add-on, and Frontend targets,
markdown relative links and anchors, and optional test suite execution.
Supports --ci and --skip-tests flags.
"""

import os
import re
import sys
import argparse
import subprocess
import urllib.parse
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


def check_version_synchronization(root_dir: Path) -> Tuple[bool, Optional[str]]:
    print("🏷️  Checking SemVer synchronization...")
    canonical_version: Optional[str] = None
    has_errors = False

    # 1. Directory.Build.props
    props_path = root_dir / "Directory.Build.props"
    if props_path.exists():
        content = props_path.read_text(encoding="utf-8")
        match = re.search(r"<Version>(.*?)</Version>", content)
        if match:
            canonical_version = match.group(1).strip()
            print(f"  ✅ Directory.Build.props version: {canonical_version}")
        else:
            print("  ❌ Directory.Build.props missing <Version> tag")
            has_errors = True
    else:
        print("  ❌ Directory.Build.props not found")
        has_errors = True

    if not canonical_version:
        return False, None

    # 2. addon/config.yaml
    addon_config = root_dir / "addon" / "config.yaml"
    if addon_config.exists():
        content = addon_config.read_text(encoding="utf-8")
        match = re.search(r"^version:\s*['\"]?([^'\"\n]+)['\"]?", content, re.MULTILINE)
        if match:
            addon_ver = match.group(1).strip()
            if addon_ver == canonical_version:
                print(f"  ✅ addon/config.yaml version aligned: {addon_ver}")
            else:
                print(f"  ❌ addon/config.yaml version mismatch: expected {canonical_version}, found {addon_ver}")
                has_errors = True
        else:
            print("  ❌ addon/config.yaml missing version attribute")
            has_errors = True

    # 3. addon/CHANGELOG.md
    addon_changelog = root_dir / "addon" / "CHANGELOG.md"
    if addon_changelog.exists():
        content = addon_changelog.read_text(encoding="utf-8")
        match = re.search(r"##\s*\[([0-9.]+)\]", content)
        if match:
            cl_ver = match.group(1).strip()
            if cl_ver == canonical_version:
                print(f"  ✅ addon/CHANGELOG.md top release aligned: {cl_ver}")
            else:
                print(f"  ❌ addon/CHANGELOG.md version mismatch: expected {canonical_version}, found {cl_ver}")
                has_errors = True
        else:
            print("  ⚠️  addon/CHANGELOG.md has no ## [X.Y.Z] entry")

    # 4. src/CodeMaster.UI/package.json (if present)
    pkg_path = root_dir / "src" / "CodeMaster.UI" / "package.json"
    if pkg_path.exists():
        content = pkg_path.read_text(encoding="utf-8")
        match = re.search(r'"version":\s*"([^"]+)"', content)
        if match:
            pkg_ver = match.group(1).strip()
            if pkg_ver == canonical_version:
                print(f"  ✅ package.json version aligned: {pkg_ver}")
            else:
                print(f"  ❌ package.json version mismatch: expected {canonical_version}, found {pkg_ver}")
                has_errors = True

    return (not has_errors), canonical_version


def _gfm_anchor(heading: str) -> str:
    """Generates GitHub Flavored Markdown (GFM) anchor slug."""
    h = heading.strip().lower()
    h = re.sub(r"\[([^\]]+)\]\([^\)]+\)", r"\1", h)
    h = h.replace("`", "")
    h = re.sub(r"[^\w\s-]", "", h).strip()
    h = h.replace(" ", "-")
    return h


def _extract_anchors(filepath: Path) -> Set[str]:
    anchors: Set[str] = set()
    if not filepath.exists() or not filepath.is_file():
        return anchors
    try:
        content = filepath.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        return anchors

    heading_counts: Dict[str, int] = {}
    for line in content.splitlines():
        h_match = re.match(r"^#{1,6}\s+(.+)$", line)
        if h_match:
            slug = _gfm_anchor(h_match.group(1).strip())
            if slug:
                count = heading_counts.get(slug, 0)
                heading_counts[slug] = count + 1
                if count == 0:
                    anchors.add(slug)
                    anchors.add(re.sub(r"-+", "-", slug))
                else:
                    suffixed = f"{slug}-{count}"
                    anchors.add(suffixed)
                    anchors.add(re.sub(r"-+", "-", suffixed))
        for a_match in re.finditer(r"<a\s+(?:id|name)=[\'\"]([^\'\"]+)[\'\"]", line, re.IGNORECASE):
            anchor_name = a_match.group(1).lower()
            anchors.add(anchor_name)
            anchors.add(re.sub(r"-+", "-", anchor_name))
    return anchors


def check_markdown_links(root_dir: Path) -> bool:
    print("🔍 Checking markdown relative links and anchors...")
    has_errors = False
    excluded_dirs = {".git", ".superpowers", "node_modules", "bin", "obj", ".venv", "dist", "TestResults"}
    anchor_cache: Dict[Path, Set[str]] = {}

    for md_file in root_dir.glob("**/*.md"):
        if any(part in excluded_dirs or part.startswith(".") for part in md_file.parts):
            continue
        try:
            content = md_file.read_text(encoding="utf-8", errors="ignore")
        except Exception as e:
            print(f"❌ Failed to read {md_file}: {e}")
            has_errors = True
            continue

        no_code = re.sub(r"```[\s\S]*?```", "", content)
        for line_idx, line in enumerate(no_code.splitlines(), start=1):
            clean_line = re.sub(r"`[^`]*`", "", line)
            links = re.findall(r'\[(?:[^\]]*)\]\(([^)]+)\)', clean_line)
            for raw_link in links:
                target = raw_link.split()[0].strip()
                if not target:
                    continue
                if re.match(r"^(https?|mailto|tel|ftp|javascript|file|conversation):", target, re.IGNORECASE):
                    continue

                parts = target.split("#", 1)
                path_str = parts[0].strip()
                anchor_str = parts[1].strip() if len(parts) > 1 else None

                if not path_str:
                    target_file = md_file
                else:
                    decoded = urllib.parse.unquote(path_str)
                    if decoded.startswith("/"):
                        target_file = (root_dir / decoded.lstrip("/")).resolve()
                    else:
                        target_file = (md_file.parent / decoded).resolve()

                if not target_file.exists():
                    print(f"❌ Broken link in {md_file.relative_to(root_dir)}:{line_idx}: target missing '{path_str}'")
                    has_errors = True
                    continue

                if anchor_str and target_file.suffix.lower() == ".md":
                    if target_file not in anchor_cache:
                        anchor_cache[target_file] = _extract_anchors(target_file)
                    clean_anchor = urllib.parse.unquote(anchor_str).lower()
                    norm_anchor = re.sub(r"-+", "-", clean_anchor)
                    if clean_anchor not in anchor_cache[target_file] and norm_anchor not in anchor_cache[target_file]:
                        print(f"❌ Broken anchor in {md_file.relative_to(root_dir)}:{line_idx}: #{anchor_str} in {target_file.name}")
                        has_errors = True

    if not has_errors:
        print("✅ All markdown links verified successfully.")
    return not has_errors


def run_tests(root_dir: Path) -> bool:
    print("🧪 Running test suite...")
    test_projects = list(root_dir.glob("tests/**/*.csproj"))
    if not test_projects:
        print("ℹ️ No test projects found, skipping test execution.")
        return True

    cmd = ["dotnet", "test", "--configuration", "Release", "--verbosity", "normal"]
    try:
        res = subprocess.run(cmd, cwd=root_dir, check=False)
        return res.returncode == 0
    except Exception as e:
        print(f"❌ Test execution failed: {e}")
        return False


def main():
    parser = argparse.ArgumentParser(description="CodeMaster Release & Quality Verification Engine")
    parser.add_argument("--skip-tests", action="store_true", help="Skip test suite execution")
    parser.add_argument("--ci", action="store_true", help="CI execution mode")
    args = parser.parse_args()

    root_dir = Path(__file__).resolve().parent
    print(f"🚀 Running release verification in {root_dir} (ci={args.ci}, skip_tests={args.skip_tests})...")

    version_ok, ver = check_version_synchronization(root_dir)
    links_ok = check_markdown_links(root_dir)

    success = version_ok and links_ok

    if not args.skip_tests:
        test_ok = run_tests(root_dir)
        success = success and test_ok

    if success:
        print("🎉 Verification passed cleanly.")
        sys.exit(0)
    else:
        print("❌ Verification failed.")
        sys.exit(1)


if __name__ == "__main__":
    main()
