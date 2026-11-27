# Processador isolado de imagens — F5

Executável técnico .NET 10, sem referência à API, EF, SDK S3, configuração de
host ou regras de locação. Não executar como serviço nem decodificar no host.
O supervisor em Infrastructure/Fotos implementa a porta de Application/Fotos.
O runtime/API ainda não registra essa capacidade neste bloco.

## Preparação local reproduzível (Linux x64)

Após autorização de obtenção da dependência nativa:

```sh
# Diretório: app/backend
python3 ensaios/Fotos.Qualificacao/preparar-libjpeg.py
dotnet restore Locadora.slnx
```

O script verifica o SHA-256 do `.deb` oficial **antes** de extrair em
`.native-fotos/`, ignorada pelo Git. Não executa scripts do pacote, `apt`,
`dpkg -i`, `sudo`, serviços, nem altera o sistema.
O build copia somente a biblioteca versionada como `libturbojpeg.so.0` e seus
avisos/licenças para o artefato do filho. Falha se a dependência não foi preparada.
O loader usa somente o arquivo local com hash esperado, sem fallback para
biblioteca global. Publicação em outra arquitetura/F1 precisa nova qualificação,
não desativação desse gate.

- libjpeg-turbo **3.2.1**, artefato
  `libjpeg-turbo-official_3.2.1_amd64.deb`:
  SHA-256 `21c7af6e505386dda0fa05e9eab5a8d288947f2e09ade8d7a48ff393ed08182d`.
- Biblioteca `libturbojpeg.so.0.5.0`, SHA-256
  `dffb4dc98ac21a0bdb18ba5987b9ac97fb8ab6da92c9679add294c7cb50caa8f`.
- SkiaSharp/native assets Linux NoDependencies **4.157.0**, via NuGet.

## Supervisor e protocolo

Configuração interna `ConfiguracaoProcessadorImagem`: caminho absoluto/canônico
do executável dotnet (resolver symlink antes de configurar), DLL do filho,
raiz cgroup v2 delegada com controlador `memory` já habilitado nos descendentes,
e raiz de temporários privada existente. Não inferir privilégios nem iniciar
serviços para obter delegação. Configuração inválida falha fechada.

Um slot estático por processo da API, sem fila, prazo total de **10 s** inclusive
I/O. Pasta aleatória 0700 por execução. Ambiente do filho limpo, somente
HOME/TMPDIR próprios e opções não sensíveis do runtime. Sem herdar conexão SQL,
tokens ou configuração R2. O filho emite `F5RD`, espera o comando; pai ingressa
o PID no cgroup e confirma `memory.max=268435456`, `memory.swap.max=0` antes
de enviar bytes. O filho revalida o isolamento antes de usar os codecs.

Entrada binária: comando, MIME enumerado, tamanho int32 e bytes limitados.
Saída: código controlado, MIME, dimensões, tamanho e bytes; o pai valida tetos,
EOF e exit code. Nada de caminhos/nome do cliente, shell, JSON irrestrito,
exceção nativa ou conteúdo do arquivo em logs. Erro de protocolo/falha
técnica/ocupado/isolation/timeout vira indisponibilidade controlada pela porta.
O mapeamento HTTP pertence ao bloco de API.

Encerramento usa `cgroup.kill` para toda a árvore (inclusive órfãos), fallback
`Process.Kill(tree)` para falha antes do ingresso, espera saída e remove
somente grupos/temporários próprios; slot liberado em `finally`.
`RetomarTemporariosAsync` oferece uma passagem finita de até dez diretórios
próprios na inicialização e é chamada antes da próxima operação. Marcador
PID + instante de início protege outra instância viva/reuso de PID; depois de
host interrompido, mata o grupo correspondente e limpa o temporário.
Sem marcador só remove pasta vazia; marcador/link/nome inválido não é removido.
Não varre bucket/volume inteiro nem cria timer ou consulta keep-alive ao SQL.

**Limite da proteção:** cgroup impõe recursos; não é sandbox geral de syscall,
rede ou filesystem. Ausência de referências/configuração/variáveis SQL/R2 no
filho não comprova isolamento completo contra código nativo comprometido.
Hardening de publicação continua gate de segurança, sem afirmar capacidade F1.

## Política de saneamento

- JPEG/PNG estáticos; arquivo/saída **5.242.880 bytes**; dimensões positivas e
  produto em int64 de até **16.000.000**, verificados antes de alocar pixels.
- PNG: assinatura, chunks limitados/CRC, IHDR único, IDAT contíguo, IEND final,
  rejeição explícita de acTL/fcTL/fdAT e críticos desconhecidos.
- JPEG: estrutura delimitada até EOI final e dimensões antes de pixel buffer.
  **Validação adicional libjpeg-turbo**, com `TJPARAM_STOPONWARNING=1`,
  `TJPARAM_MAXPIXELS=16000000`, decode integral e descarte do buffer antes
  de Skia. Qualquer warning/erro rejeita a imagem; ausência/falha da biblioteca
  é falha técnica, não aceite nem fallback. Não expõe mensagens nativas.
- Skia exige `SKCodecResult.Success` (nunca aceita `IncompleteInput`), aplica
  oito orientações EXIF, converte a superfície para sRGB e reencoda pixels novos
  em JPEG/PNG, sem metadados originais, preservando alpha PNG.
- Não redimensiona, troca formato nem relaxa limite para caber; saída excessiva
  é rejeitada. Todos os handles/buffers são descartados; o SO limita o filho.

O teste descobriu que Skia podia retornar `Success` para JPEG sem scan com
EOI preservado; um parser que apenas exige “algum byte” no scan não resolveria
truncamento parcial. A validação TurboJPEG foi autorizada e qualificada antes
de integrar. Não é antivírus nem prova universal sobre qualquer entrada.

## Licenças e fontes oficiais

This software is based in part on the work of the Independent JPEG Group.

Distribuir `licencas/libjpeg-turbo/LICENSE.md` (BSD modificado de três cláusulas)
e `README.ijg` com o executável, além dos avisos dos pacotes SkiaSharp/Skia.
Não remover copyright nem apresentar terceiros como autores deste produto.

- [Release oficial 3.2.1](https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.2.1)
- [API e STOPONWARNING](https://github.com/libjpeg-turbo/libjpeg-turbo/blob/3.2.1/src/turbojpeg.h)
- [Licenças](https://github.com/libjpeg-turbo/libjpeg-turbo/blob/3.2.1/LICENSE.md)
- [Recomendação oficial de warnings fatais](https://github.com/libjpeg-turbo/libjpeg-turbo/issues/764)

Evidências/limitações de execução: `docs/retomada-fase-5-fotos-privadas.md` na
raiz do repositório. Sem R2, autenticação HTTP ou paridade UI ensaiados aqui.
