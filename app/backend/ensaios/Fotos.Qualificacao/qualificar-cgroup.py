#!/usr/bin/env python3
"""Ensaio Linux local, sem segredos, serviços, privilégios ou acesso ao R2."""
import os
from pathlib import Path
import signal
import subprocess
import sys
import uuid

raiz = Path("/sys/fs/cgroup")
atual = raiz / Path("/proc/self/cgroup").read_text().strip().split("::", 1)[1].lstrip("/")
pai = atual.parent
executavel = Path(__file__).parent / "bin/Debug/net10.0/Fotos.Qualificacao.dll"
if not executavel.is_file():
    sys.exit("Compile Fotos.Qualificacao antes do ensaio.")


def executar(nome, comando, prazo, esperar_oom=False):
    grupo = pai / ("locadora-f5-ensaio-" + uuid.uuid4().hex)
    processo = None
    try:
        grupo.mkdir()
        (grupo / "memory.max").write_text("268435456")
        (grupo / "memory.swap.max").write_text("0")

        def ingressar():
            os.setsid()
            (grupo / "cgroup.procs").write_text(str(os.getpid()))

        processo = subprocess.Popen(
            comando, env={"PATH": "/usr/bin:/bin", "HOME": "/nonexistent"},
            preexec_fn=ingressar, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        )
        expirou = False
        try:
            saida, erro = processo.communicate(timeout=prazo)
        except subprocess.TimeoutExpired:
            expirou = True
            (grupo / "cgroup.kill").write_text("1")
            saida, erro = processo.communicate(timeout=5)
        eventos = dict(linha.split() for linha in (grupo / "memory.events").read_text().splitlines())
        if esperar_oom:
            assert int(eventos["oom_kill"]) > 0 and processo.returncode == -signal.SIGKILL
        elif nome == "prazo":
            assert expirou and processo.returncode == -signal.SIGKILL
        else:
            assert processo.returncode == 0, erro.decode(errors="replace")[-1000:]
        print(f"PASS: {nome}; exit={processo.returncode}; oom_kill={eventos['oom_kill']}; timeout={expirou}")
        if saida:
            print(saida.decode().strip())
    finally:
        if processo and processo.poll() is None:
            (grupo / "cgroup.kill").write_text("1")
            processo.wait(timeout=5)
        if grupo.exists():
            grupo.rmdir()


if len(sys.argv) == 3:
    executar("jpeg-estrito", [sys.executable, str(Path(__file__).with_name("qualificar-jpeg-estrito.py")),
                            sys.argv[1], sys.argv[2]], 10)
else:
    executar("runtime", ["/usr/bin/dotnet", str(executavel)], 10)
    executar("memoria", [sys.executable, "-c", "a=bytearray(300*1024*1024)"], 10, True)
    executar("prazo", [sys.executable, "-c", "import time; time.sleep(60)"], 1)
