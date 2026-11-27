import { randomBytes } from 'node:crypto';
import path from 'node:path';
import { mkdirSync, readFileSync, realpathSync } from 'node:fs';
import { defineConfig, devices } from '@playwright/test';

const raiz = path.resolve(__dirname, '../..');
const base = process.env.LOCADORA_SQL_CONNECTION;
if (!base) {
  throw new Error('Defina LOCADORA_SQL_CONNECTION para o Playwright.');
}

const chave = randomBytes(32).toString('base64');
const conexao = trocarCatalogo(base, 'locadora_e2e');
const ambienteApi: Record<string, string> = {};
for (const [nome, valor] of Object.entries(process.env)) {
  if (valor !== undefined) {
    ambienteApi[nome] = valor;
  }
}
ambienteApi.ASPNETCORE_ENVIRONMENT = 'Development';
ambienteApi.ASPNETCORE_URLS = 'http://127.0.0.1:5285';
ambienteApi.ConnectionStrings__Locadora = conexao;
ambienteApi.Autenticacao__Jwt__Chaves__v1 = chave;
// Os cenários compartilham o IP de loopback; rate limiting tem testes HTTP próprios.
// Estes limites valem somente no processo da API iniciado pela suíte E2E.
ambienteApi.Limites__Registro__Quantidade = '100';
ambienteApi.Limites__Login__Quantidade = '100';
// Volume sintético privado por execução, fora do webroot, sem R2/credenciais.
const fotos = path.join(raiz, 'app/backend/obj', `e2e-fotos-${randomBytes(8).toString('hex')}`);
mkdirSync(path.join(fotos, 'objetos'), { recursive: true, mode: 0o700 });
mkdirSync(path.join(fotos, 'temporarios'), { mode: 0o700 });
const cgroup = readFileSync('/proc/self/cgroup', 'utf8').trim().split('\n').find(l => l.startsWith('0::'))!.slice(3);
ambienteApi.Fotos__Provedor = 'Local';
ambienteApi.Fotos__RaizPrivada = path.join(fotos, 'objetos');
ambienteApi.Fotos__Processador__Executavel = realpathSync('/usr/bin/dotnet');
ambienteApi.Fotos__Processador__Assembly = path.join(raiz, 'app/backend/src/Locadora.ProcessadorImagens/bin/Debug/net10.0/Locadora.ProcessadorImagens.dll');
ambienteApi.Fotos__Processador__Cgroups = path.dirname(path.join('/sys/fs/cgroup', cgroup));
ambienteApi.Fotos__Processador__Temporarios = path.join(fotos, 'temporarios');

export default defineConfig({
  metadata: { fotosPrivadasRaiz: fotos },
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project app/backend/src/Locadora.Api/Locadora.Api.csproj --no-launch-profile',
      cwd: raiz,
      url: 'http://127.0.0.1:5285/health',
      timeout: 120_000,
      reuseExistingServer: false,
      env: ambienteApi,
    },
    {
      command: 'npm start -- --host localhost --port 4200',
      cwd: path.join(raiz, 'app/frontend'),
      url: 'http://localhost:4200',
      timeout: 120_000,
      reuseExistingServer: false,
    },
  ],
});

function trocarCatalogo(conexao: string, catalogo: string): string {
  const partes = conexao
    .split(';')
    .filter((parte) => {
      const nome = parte.split('=')[0]?.trim().toLowerCase();
      return nome !== 'database' && nome !== 'initial catalog' && nome !== '';
    });
  partes.push(`Database=${catalogo}`);
  return partes.join(';');
}
