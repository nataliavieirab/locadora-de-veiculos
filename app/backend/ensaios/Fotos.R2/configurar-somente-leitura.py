"""Entrada exclusiva do solicitante em terminal local; não executar pelo agente."""

import getpass
import json
from pathlib import Path
import subprocess
import sys
import warnings


def main():
    if not sys.stdin.isatty():
        raise SystemExit("Bloqueado: use um terminal local interativo, sem gravação.")
    projeto = Path(__file__).resolve().parents[2] / "src/Locadora.Api/Locadora.Api.csproj"
    # Se o terminal não suportar entrada sem eco, interromper em vez de degradar.
    with warnings.catch_warnings():
        warnings.simplefilter("error", getpass.GetPassWarning)
        access = getpass.getpass("Access Key ID adicional Object Read only (sem eco): ")
        secret = getpass.getpass("Secret Access Key adicional (sem eco): ")
    if not access.strip() or not secret.strip():
        raise SystemExit("Bloqueado: valores vazios; nada configurado.")
    dados = {
        "Ensaios:FotosR2:SomenteLeitura:AccessKey": access,
        "Ensaios:FotosR2:SomenteLeitura:SecretKey": secret,
    }
    # Somente duas chaves adicionais via stdin; não listar/ler/imprimir as atuais.
    resultado = subprocess.run(
        ["dotnet", "user-secrets", "set", "--project", str(projeto)],
        input=json.dumps(dados), text=True, stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL, check=False,
    )
    print("Configuração adicional salva; permissões remotas NÃO verificadas."
          if resultado.returncode == 0 else "Falha local; detalhes sensíveis suprimidos.")
    return resultado.returncode


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EOFError, KeyboardInterrupt, getpass.GetPassWarning):
        raise SystemExit("Entrada interrompida; nenhuma evidência remota produzida.")
