#!/usr/bin/env python3
"""Obtém/extrai SOMENTE o artefato oficial aprovado; não instala no sistema."""
import hashlib
from pathlib import Path
import subprocess
import urllib.request

VERSAO = "3.2.1"
SHA256 = "21c7af6e505386dda0fa05e9eab5a8d288947f2e09ade8d7a48ff393ed08182d"
URL = ("https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/"
       f"{VERSAO}/libjpeg-turbo-official_{VERSAO}_amd64.deb")
raiz = Path(__file__).resolve().parents[2] / ".native-fotos"
raiz.mkdir(exist_ok=True)
arquivo = raiz / f"libjpeg-turbo-{VERSAO}-amd64.deb"
if not arquivo.exists():
    with urllib.request.urlopen(URL, timeout=60) as origem:
        conteudo = origem.read()
    if hashlib.sha256(conteudo).hexdigest() != SHA256:
        raise SystemExit("SHA-256 divergente; artefato não será salvo nem extraído.")
    arquivo.write_bytes(conteudo)
if hashlib.sha256(arquivo.read_bytes()).hexdigest() != SHA256:
    raise SystemExit("SHA-256 divergente; não extrair.")
destino = raiz / VERSAO
destino.mkdir(exist_ok=True)
subprocess.run(["dpkg-deb", "--extract", str(arquivo), str(destino)], check=True)
print(f"PASS: artefato oficial {VERSAO}, SHA-256 {SHA256}; extração local, sem instalação.")
for caminho in destino.rglob("*"):
    if caminho.is_file() and (caminho.name.startswith("libturbojpeg") or "copyright" in caminho.name.lower()
                              or "license" in caminho.name.lower() or caminho.name == "README.ijg"):
        print(caminho.relative_to(raiz.parent))
