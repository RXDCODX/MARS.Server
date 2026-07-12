import xml.etree.ElementTree as ET
import subprocess
import sys
import os


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def parse_trx(trx_path):
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    try:
        tree = ET.parse(trx_path)
        failed = [
            r for r in tree.findall(".//t:UnitTestResult", ns) if r.get("outcome") == "Failed"
        ]
        if not failed:
            return "No failed tests found in TRX"
        lines = []
        for r in failed[:10]:
            name = esc(r.get("testName", "?"))
            msg_el = r.find("t:Output/t:ErrorInfo/t:Message", ns)
            msg = esc(msg_el.text[:200]) if msg_el is not None and msg_el.text else ""
            lines.append(f"  - {name}: {msg}")
        result = f"{len(failed)} test(s) failed:\n" + "\n".join(lines)
        if len(failed) > 10:
            result += f"\n  ... and {len(failed) - 10} more"
        return result
    except Exception as e:
        return f"Could not parse TRX: {e}"


def main():
    workflow_name = sys.argv[1] if len(sys.argv) > 1 else "CI"
    trx_path = sys.argv[2] if len(sys.argv) > 2 else ""

    trx_info = ""
    if trx_path and os.path.isfile(trx_path):
        trx_info = parse_trx(trx_path)
    else:
        trx_info = f"TRX file not found: {trx_path}"

    branch = os.environ.get("GITHUB_REF_NAME", "?")
    sha = os.environ.get("GITHUB_SHA", "?")
    actor = os.environ.get("GITHUB_ACTOR", "?")
    run_url = (
        os.environ.get("GITHUB_SERVER_URL", "")
        + "/"
        + os.environ.get("GITHUB_REPOSITORY", "")
        + "/actions/runs/"
        + os.environ.get("GITHUB_RUN_ID", "")
    )

    msg = f"{workflow_name} FAILED\n\n"
    msg += f"Branch: {branch}\n"
    msg += f"Commit: {sha}\n"
    msg += f"Actor: {actor}\n"
    msg += f"{run_url}\n\n"
    msg += trx_info

    bot_token = os.environ.get("BOT_TOKEN", "")
    admin_id = os.environ.get("ADMIN_ID", "")
    subprocess.run(
        [
            "curl", "-s", "-X", "POST",
            f"https://api.telegram.org/bot{bot_token}/sendMessage",
            "-d", f"chat_id={admin_id}",
            "--data-urlencode", f"text={msg}",
        ]
    )


if __name__ == "__main__":
    main()
