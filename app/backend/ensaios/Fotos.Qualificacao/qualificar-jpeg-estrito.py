#!/usr/bin/env python3
"""Ensaio negativo isolado da API/pipeline; usa ctypes, sem instalar wrappers."""
import ctypes
from pathlib import Path
import sys

lib = ctypes.CDLL(sys.argv[1])
lib.tj3Init.argtypes = [ctypes.c_int]
lib.tj3Init.restype = ctypes.c_void_p
lib.tj3Set.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int]
lib.tj3DecompressHeader.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t]
lib.tj3Get.argtypes = [ctypes.c_void_p, ctypes.c_int]
lib.tj3Decompress8.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_int, ctypes.c_int]
lib.tj3Destroy.argtypes = [ctypes.c_void_p]
for arquivo in sorted(Path(sys.argv[2]).glob("*.jpg")):
    handle = lib.tj3Init(1)  # TJINIT_DECOMPRESS
    assert handle
    try:
        assert lib.tj3Set(handle, 0, 1) == 0  # TJPARAM_STOPONWARNING
        assert lib.tj3Set(handle, 24, 16_000_000) == 0  # TJPARAM_MAXPIXELS
        bytes_imagem = arquivo.read_bytes()
        entrada = ctypes.create_string_buffer(bytes_imagem)
        resultado = lib.tj3DecompressHeader(handle, entrada, len(bytes_imagem))
        if resultado == 0:
            largura = lib.tj3Get(handle, 5)
            altura = lib.tj3Get(handle, 6)
            assert largura > 0 and altura > 0 and largura * altura <= 16_000_000
            saida = ctypes.create_string_buffer(largura * altura * 4)
            resultado = lib.tj3Decompress8(handle, entrada, len(bytes_imagem), saida, largura * 4, 7)  # TJPF_RGBA
        valido = arquivo.name == "valido.jpg"
        assert (resultado == 0) == valido, f"Falha de política: {arquivo.name}, resultado={resultado}"
        print(f"PASS: {arquivo.name}: {'aceito' if valido else 'rejeitado'}")
    finally:
        lib.tj3Destroy(handle)
