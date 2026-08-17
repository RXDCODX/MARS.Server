import xml.etree.ElementTree as ET
import subprocess
import sys
import os
import re


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def short_class_name(full_name):
    """MARS.Tests.Foo.BarTests.Method → BarTests.Method"""
    parts = full_name.split(".")
    if len(parts) >= 2:
        return ".".join(parts[-2:])
    return full_name


def summarize_error(msg, max_len=120):
    """Extract the core assertion failure or exception, skip stack traces."""
    if not msg:
        return ""
    # Take first line only
    first = msg.split("\n")[0].strip()
    # Remove "Assert.Xxx() Failure:" prefix noise, keep the actual mismatch
    first = re.sub(r"^Assert\.\w+\(\)\s*Failure:\s*", "", first)
    # Remove Moq boilerplate
    first = re.sub(r"^Expected invocation.*but was \d+ times:\s*", "Moq: ", first)
    if len(first) > max_len:
        first = first[:max_len] + "…"
    return first


def parse_trx(trx_path):
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    try:
        tree = ET.parse(trx_path)
        failed = [
            r for r in tree.findall(".//t:UnitTestResult", ns) if r.get("outcome") == "Failed"
        ]
        if not failed:
            return ""
        lines = []
        for r in failed[:8]:
            raw_name = r.get("testName", "?")
            name = short_class_name(raw_name)
            msg_el = r.find("t:Output/t:ErrorInfo/t:Message", ns)
            msg_text = msg_el.text if msg_el is not None and msg_el.text else ""
            err_type = ""
            # Detect exception type
            if "NullReferenceException" in msg_text:
                err_type = "NRE"
            elif "Assert.Contains" in msg_text:
                err_type = "Contains"
            elif "Assert.False" in msg_text:
                err_type = "False"
            elif "Assert.Null" in msg_text:
                err_type = "NotNull"
            elif "Assert.True" in msg_text:
                err_type = "True"
            elif "Moq.MockException" in msg_text:
                err_type = "Moq"
            summary = summarize_error(msg_text)
            tag = f" <code>[{err_type}]</code>" if err_type else ""
            line = f"• <code>{esc(name)}</code>{tag}"
            if summary:
                line += f"\n  <i>{esc(summary)}</i>"
            lines.append(line)
        result = f"<b>{len(failed)} тестов упали:</b>\n\n" + "\n".join(lines)
        if len(failed) > 8:
            result += f"\n… и ещё {len(failed) - 8}"
        return result
    except Exception as e:
        return f"Ошибка парсинга TRX: {e}"


def parse_coverage(xml_path, warn_threshold, fail_threshold):
    """Parse Cobertura XML and return coverage report."""
    try:
        tree = ET.parse(xml_path)
        root = tree.getroot()
        line_pct = float(root.get("line-rate", 0)) * 100
        branch_pct = float(root.get("branch-rate", 0)) * 100

        if line_pct >= fail_threshold:
            emoji = "🟢"
        elif line_pct >= warn_threshold:
            emoji = "🟡"
        else:
            emoji = "🔴"

        result = f"{emoji} <b>Покрытие кода:</b>\n"
        result += f"  Строки: <code>{line_pct:.1f}%</code>\n"
        result += f"  Ветки: <code>{branch_pct:.1f}%</code>\n"

        if line_pct < warn_threshold:
            result += f"\n⚠️ Покрытие ниже порога {warn_threshold}%"

        return result
    except Exception as e:
        return f"Ошибка парсинга coverage XML: {e}"


def main():
    workflow_name = sys.argv[1] if len(sys.argv) > 1 else "CI"

    # Режим coverage: --coverage <xml_path> <warn> <fail>
    if len(sys.argv) > 2 and sys.argv[2] == "--coverage":
        xml_path = sys.argv[3] if len(sys.argv) > 3 else ""
        warn = float(sys.argv[4]) if len(sys.argv) > 4 else 50
        fail = float(sys.argv[5]) if len(sys.argv) > 5 else 75
        if xml_path and os.path.isfile(xml_path):
            trx_info = parse_coverage(xml_path, warn, fail)
        else:
            trx_info = f"Coverage XML не найден: {xml_path}"
    else:
        trx_path = sys.argv[2] if len(sys.argv) > 2 else ""

        trx_info = ""
        if trx_path and os.path.isfile(trx_path):
            trx_info = parse_trx(trx_path)
        elif trx_path:
            # TRX не найден — возможно, тесты не запускались (ошибка сборки/restore)
            results_dir = os.path.dirname(trx_path)
            if os.path.isdir(results_dir):
                files = os.listdir(results_dir)
                if files:
                    trx_info = f"TRX не найден ({trx_path}). Файлы в {results_dir}: {', '.join(files[:5])}"
                else:
                    trx_info = f"Тесты не запускались — {results_dir} пуст (вероятна ошибка сборки)"
            else:
                trx_info = f"Тесты не запускались — {results_dir} не создан (вероятна ошибка сборки)"
        else:
            trx_info = "Путь к TRX не указан"

    branch = os.environ.get("GITHUB_REF_NAME", "?")
    sha = os.environ.get("GITHUB_SHA", "?")[:8]
    actor = os.environ.get("GITHUB_ACTOR", "?")
    run_url = (
        os.environ.get("GITHUB_SERVER_URL", "")
        + "/"
        + os.environ.get("GITHUB_REPOSITORY", "")
        + "/actions/runs/"
        + os.environ.get("GITHUB_RUN_ID", "")
    )

    msg = f"🔴 <b>{esc(workflow_name)}</b>\n"
    msg += f"<code>{esc(branch)}</code> @ {sha} — {esc(actor)}\n"
    msg += f'<a href="{run_url}">Логи →</a>\n\n'
    msg += trx_info

    bot_token = os.environ.get("BOT_TOKEN", "")
    admin_id = os.environ.get("ADMIN_ID", "")
    subprocess.run(
        [
            "curl", "-s", "-X", "POST",
            f"https://api.telegram.org/bot{bot_token}/sendMessage",
            "-d", f"chat_id={admin_id}",
            "-d", "parse_mode=HTML",
            "--data-urlencode", f"text={msg}",
        ]
    )


if __name__ == "__main__":
    main()
