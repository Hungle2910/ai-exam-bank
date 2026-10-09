"""Dependency-free hygiene checks for tracked files and local Markdown links.

This is not a full secret scanner, Markdown parser or application test suite.
External URLs and heading anchors are intentionally not checked.
"""
from __future__ import annotations

import re
import subprocess
from pathlib import Path
from urllib.parse import unquote, urlsplit

PRIVATE_KEY = re.compile(rb"(?m)^-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----")
LINK = re.compile(r"\]\((<[^>]+>|[^\s)]+)(?:\s+\"[^\"]*\")?\)")
FENCE = re.compile(r"(?ms)^```[^\n]*\n.*?^```[^\n]*$")
SENSITIVE_SUFFIXES = {'.pem', '.ppk', '.key', '.pfx', '.p12', '.tfstate', '.bak', '.dump'}
MODULE_OWNER = re.compile(r'^/src/Modules/([^/]+)/\s+(@\S+)\s+(@\S+)\s*$')


def inspect_file(root: Path, relative: Path) -> list[str]:
    """Report only file names/reasons; never include sensitive file contents."""
    path = root / relative
    name = relative.name.lower()
    issues: list[str] = []
    if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
        return [f'{relative}: tracked symlinks/outside-root files need explicit review']
    if not path.is_file():
        return [f'{relative}: tracked file missing from checkout']
    is_environment = name == '.env' or name.startswith('.env.')
    is_runtime_config = (name == 'credentials'
                         or (name.startswith('appsettings.') and name.endswith('.local.json')))
    if (relative.suffix.lower() in SENSITIVE_SUFFIXES
            or '.tfstate.' in name
            or (is_environment and name != '.env.example')
            or is_runtime_config
            or name.endswith(('.tfvars', '.tfvars.json'))):
        issues.append(f'{relative}: runtime secret/state/backup file must not be tracked')
    content = path.read_bytes()
    if PRIVATE_KEY.search(content):
        issues.append(f'{relative}: private-key material detected')
    if relative.suffix.lower() != '.md':
        return issues
    try:
        text = content.decode('utf-8-sig')
    except UnicodeDecodeError:
        return issues + [f'{relative}: Markdown must be UTF-8']
    for target in LINK.findall(FENCE.sub('', text)):
        parsed = urlsplit(target.strip('<>'))
        if parsed.scheme in {'http', 'https', 'mailto'} or parsed.netloc:
            continue
        if parsed.scheme:
            issues.append(f'{relative}: non-portable local link')
            continue
        if not parsed.path:
            continue
        destination = (root / unquote(parsed.path).lstrip('/') if parsed.path.startswith('/')
                       else path.parent / unquote(parsed.path)).resolve()
        if not destination.is_relative_to(root.resolve()) or not destination.exists():
            issues.append(f'{relative}: broken/outside-repository link: {target}')
    return issues


def inspect_module_ownership(root: Path) -> list[str]:
    """Keep the module owner/reviewer table aligned with CODEOWNERS routing."""
    codeowners = (root / '.github' / 'CODEOWNERS').read_text(encoding='utf-8')
    module_readme = (root / 'src' / 'Modules' / 'README.md').read_text(encoding='utf-8')
    routed = {
        match.group(1): (match.group(2), match.group(3))
        for line in codeowners.splitlines()
        if (match := MODULE_OWNER.fullmatch(line.strip()))
    }
    documented: dict[str, tuple[str, str]] = {}
    for line in module_readme.splitlines():
        cells = [cell.strip() for cell in line.strip().strip('|').split('|')]
        if len(cells) < 3 or not cells[1].startswith('@') or not cells[2].startswith('@'):
            continue
        for module in cells[0].split(' and '):
            documented[module] = (cells[1], cells[2])
    return [
        f'src/Modules/{module}: owner/reviewer differ between CODEOWNERS and module README'
        for module in sorted(routed.keys() | documented.keys())
        if routed.get(module) != documented.get(module)
    ]


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    result = subprocess.run(['git', 'ls-files', '-z'], cwd=root, check=True, capture_output=True)
    files = [Path(name.decode('utf-8')) for name in result.stdout.split(b'\0') if name]
    issues = [issue for file in files for issue in inspect_file(root, file)]
    issues.extend(inspect_module_ownership(root))
    for issue in issues:
        print(issue)
    print(f'Checked {len(files)} tracked files; {len(issues)} issue(s).')
    return 1 if issues else 0


if __name__ == '__main__':
    raise SystemExit(main())
