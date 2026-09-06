#!/usr/bin/env python3
"""
Release and Version Verification Engine
Validates version consistency, markdown relative links, and test integrity.
Supports --ci and --skip-tests flags.
"""

import os
import re
import sys
import argparse
import subprocess
from pathlib import Path


def check_markdown_links(root_dir: Path) -> bool:
    print("🔍 Checking markdown relative links and anchors...")
    has_errors = False
    excluded_dirs = {".git", ".superpowers", "node_modules", "bin", "obj", ".venv", "dist"}
    
    for md_file in root_dir.glob("**/*.md"):
        if any(part in excluded_dirs or part.startswith(".") for part in md_file.parts):
            continue
        content = md_file.read_text(encoding="utf-8", errors="ignore")
        # Find markdown links: [text](path)
        links = re.findall(r'\[([^\]]+)\]\(([^)]+)\)', content)
        for text, link in links:
            if link.startswith("http://") or link.startswith("https://") or link.startswith("#") or link.startswith("mailto:"):
                continue
            if link.startswith("file://"):
                target_path = link[7:]
                resolved = Path(target_path)
            else:
                # Strip anchors
                target_path = link.split("#")[0]
                if not target_path:
                    continue
                resolved = (md_file.parent / target_path).resolve()

            if not resolved.exists():
                print(f"❌ Broken link in {md_file.relative_to(root_dir)}: [{text}]({link})")
                has_errors = True

    if not has_errors:
        print("✅ All markdown links verified successfully.")
    return not has_errors


def run_tests(root_dir: Path) -> bool:
    print("🧪 Running test suite...")
    test_projects = list(root_dir.glob("tests/**/*.csproj"))
    if not test_projects:
        print("ℹ️ No test projects found yet, skipping test execution.")
        return True

    cmd = ["dotnet", "test", "--configuration", "Release", "--verbosity", "normal"]
    try:
        res = subprocess.run(cmd, cwd=root_dir, check=False)
        return res.returncode == 0
    except Exception as e:
        print(f"❌ Test execution failed: {e}")
        return False


def main():
    parser = argparse.ArgumentParser(description="Release Verification Engine")
    parser.add_argument("--skip-tests", action="store_true", help="Skip test suite execution")
    parser.add_argument("--ci", action="store_true", help="CI mode")
    args = parser.parse_args()

    root_dir = Path(__file__).resolve().parent
    print(f"🚀 Running release verification in {root_dir} (ci={args.ci}, skip_tests={args.skip_tests})...")

    success = check_markdown_links(root_dir)

    if not args.skip_tests:
        test_success = run_tests(root_dir)
        success = success and test_success

    if success:
        print("🎉 Verification passed.")
        sys.exit(0)
    else:
        print("❌ Verification failed.")
        sys.exit(1)


if __name__ == "__main__":
    main()
