# Qualificação local da F5

Ensaio técnico offline (DT-12/29), não prova do R2 nem configuração de produção.
Autorizado via `question` em 25/11/2026. Executar em Linux com cgroup v2
delegado ao usuário; não usar sudo nem mudar o grupo dos processos do host.

```sh
dotnet run --project ensaios/Fotos.Qualificacao
python3 ensaios/Fotos.Qualificacao/qualificar-cgroup.py
```

## Versões e licenças verificadas em 25/11/2026

- SkiaSharp **4.157.0**, MIT; assets Linux **4.157.0**,
  `SkiaSharp.NativeAssets.Linux.NoDependencies`, MIT. Ambos publicam grupo
  `net10.0`; os assets incluem `linux-x64`. Manter avisos/licenças de terceiros
  distribuídos pelos pacotes (Skia e codecs nativos não se tornam código próprio).
- AWSSDK.S3 **4.1.2.1**, Apache-2.0; exige AWSSDK.Core `[4.1.1.1, 5.0.0)`.
  As versões diretas ficam centralizadas em `Directory.Packages.props`.
- Fontes: índices e nuspec oficiais do NuGet; Context7 `/mono/skiasharp`,
  `/aws/aws-sdk-net`; [SkiaSharp](https://github.com/mono/SkiaSharp);
  [AWS SDK](https://github.com/aws/aws-sdk-net);
  [Cloudflare .NET](https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/).

## Resultado observado

.NET **10.0.112**, Linux x64 local: restore/compilação e encode/decode integral
JPEG/PNG aprovados; `ldd libSkiaSharp.so` sem bibliotecas ausentes (libstdc++,
libpthread, libdl, libm, libc, librt, libgcc_s e loader do sistema disponíveis).
“NoDependencies” dispensa fontconfig, não a libc nem todas as bibliotecas nativas.

O ensaio ingressou o filho em grupo próprio **antes do exec** com
`memory.max=268435456`, `memory.swap.max=0` e ambiente mínimo:

- runtime .NET/Skia: exit 0 dentro de 10 s;
- alocação nativa de 300 MiB: SIGKILL, `memory.events: oom_kill=1`;
- processo que espera 60 s: encerrado pelo supervisor após 1 s de ensaio,
  `cgroup.kill`, exit -9; grupos próprios removidos ao final.

Esses ensaios qualificam a capacidade local. O supervisor real ainda precisa
testar seu protocolo, prazo de 10 s, ocupação, temporários e falhas; não são
equivalentes à conclusão de 3.1/3.2, nem garantem App Service F1.

## Configuração R2 a preservar no adaptador futuro

Endpoint administrativo HTTPS fornecido pelo servidor, região de autenticação
`auto`, bucket privado e credenciais mínimas fora do repositório/navegador.
SDK v4: `ServiceURL`, `AuthenticationRegion`, `ForcePathStyle`,
`RequestChecksumCalculation.WHEN_REQUIRED`,
`ResponseChecksumValidation.WHEN_REQUIRED`; PUT com
`DisablePayloadSigning=true`, `DisableDefaultChecksumValidation=true`,
`UseChunkEncoding=false`. A superfície dessas opções compilou offline.
O exemplo Cloudflare ainda é v3: não representa homologação do SDK v4.
HTTPS mais confirmação de tamanho/MIME/hash por leitura limitada deverão
garantir integridade; ETag S3 não equivale a SHA-256.

Nenhum cliente autenticado ou requisição foi criado no ensaio. Não gerar URL
assinada, listar buckets nem configurar fallback público. Prova real só na
tarefa 8.3 com autorização específica e bucket isolado.

## Complemento JPEG estrito — DT-31

Skia retornou `Success` para um JPEG cujo scan havia sido removido mantendo
SOS/EOI. O Apply interrompeu a implementação e o solicitante autorizou via
`question` ensaio local da dependência adicional, mantendo requisitos/limites.

`preparar-libjpeg.py` baixa libjpeg-turbo oficial **3.2.1** e verifica SHA-256
antes de salvar/extrair em `.native-fotos/` (sem instalar globalmente).
Licenças IJG/BSD, hashes e preparo do executável documentados em
`src/Locadora.ProcessadorImagens/README.md`.

```sh
# Diretório: app/backend
python3 ensaios/Fotos.Qualificacao/preparar-libjpeg.py
dotnet run --project ensaios/Fotos.Qualificacao -- --corpus ensaios/Fotos.Qualificacao/obj/corpus-jpeg
python3 ensaios/Fotos.Qualificacao/qualificar-cgroup.py \
  "$PWD/.native-fotos/3.2.1/opt/libjpeg-turbo/lib64/libturbojpeg.so.0.5.0" \
  "$PWD/ensaios/Fotos.Qualificacao/obj/corpus-jpeg"
```

Ensaio **anterior à integração**: 6/6 expectativas aprovadas em subprocesso
com cgroup 256 MiB/swap zero/prazo 10 s/ambiente mínimo. JPEG válido aceito;
cinco negativos rejeitados (cabeçalho incompleto, sem EOI, scan parcial,
metade de scan e o caso original sem scan com EOI preservado).
Warnings fatais por `TJPARAM_STOPONWARNING=1`; decode completo e limite
`TJPARAM_MAXPIXELS`. Python/ctypes usado só no ensaio, sem novo wrapper instalado.
Após qualificação, a integração .NET usa diretamente a ABI pública no filho;
resultado da suíte real registrado na retomada, sem extrapolar para R2/F1.
