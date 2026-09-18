from pathlib import Path
import re


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[4]
SPEC_PATH = PROJECT_ROOT / "Assets" / "Doc" / "UnitPrefabAuthoring.md"
SKILL_PATH = SCRIPT_PATH.parents[1] / "SKILL.md"


def read_version(path: Path, label: str) -> str:
    content = path.read_text(encoding="utf-8")
    match = re.search(rf"^{re.escape(label)}：`([^`]+)`$", content, re.MULTILINE)
    if match is None:
        raise SystemExit(f"Missing version label in {path}: {label}")

    return match.group(1)


spec_version = read_version(SPEC_PATH, "规范版本")
skill_version = read_version(SKILL_PATH, "适配规范版本")

if spec_version != skill_version:
    raise SystemExit(
        f"Version mismatch: specification={spec_version}, skill={skill_version}"
    )

print(f"Unit prefab specification and Skill are synchronized: {spec_version}")
