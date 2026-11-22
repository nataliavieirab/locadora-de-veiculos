import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MENSAGEM_CREDENCIAL, MENSAGEM_REVOGACAO, URL_API } from './configuracao';
import { apiInterceptor } from './api.interceptor';
import { MemoriaSessao } from './memoria-sessao';
import { SessaoService } from './sessao.service';

describe('SessaoService', () => {
  let servico: SessaoService;
  let http: HttpTestingController;
  let memoria: MemoriaSessao;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    servico = TestBed.inject(SessaoService);
    http = TestBed.inject(HttpTestingController);
    memoria = TestBed.inject(MemoriaSessao);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
    document.cookie.split(';').forEach((item) => {
      const nome = item.split('=')[0]?.trim();
      if (nome) {
        document.cookie = `${nome}=; Max-Age=0; path=/`;
      }
    });
  });

  it('mostra o mesmo erro genérico sem revelar se o usuário existe', async () => {
    const primeira = servico.entrar('ana', 'errada12');
    http.expectOne(`${URL_API}/api/v1/session/login`).flush(
      { title: 'usuario nao existe' },
      { status: 401, statusText: 'Unauthorized' },
    );
    expect(await primeira).toBe(MENSAGEM_CREDENCIAL);

    const segunda = servico.entrar('ana', 'outra-senha');
    http.expectOne(`${URL_API}/api/v1/session/login`).flush(
      { title: 'senha incorreta' },
      { status: 401, statusText: 'Unauthorized' },
    );
    expect(await segunda).toBe(MENSAGEM_CREDENCIAL);
    expect(memoria.erro()).toBe(MENSAGEM_CREDENCIAL);
    expect(memoria.erro()?.toLowerCase()).not.toContain('não existe');
    expect(memoria.erro()?.toLowerCase()).not.toContain('incorreta');
    expect(memoria.accessToken()).toBeNull();
  });

  it('guarda o token só em memória e anexa o bearer só na API', async () => {
    const indexed = instalarIndexedDb();
    const entrada = servico.entrar('ana', 'abcdefgh');
    http.expectOne(`${URL_API}/api/v1/session/login`).flush({
      accessToken: 'token-acesso',
      refreshToken: 'token-refresh',
      contaId: 'conta',
      empresaId: 'empresa',
    });
    expect(await entrada).toBeNull();

    servico.consultar().catch(() => undefined);
    const api = http.expectOne(`${URL_API}/api/v1/session`);
    expect(api.request.headers.get('Authorization')).toBe('Bearer token-acesso');
    expect(api.request.withCredentials).toBe(false);
    api.flush({ contaId: 'conta', empresaId: 'empresa' });

    TestBed.inject(HttpClient).get('/assets/publico').subscribe();
    const local = http.expectOne('/assets/publico');
    expect(local.request.headers.has('Authorization')).toBe(false);
    local.flush({});

    expect(await armazenamentoContem('token-acesso')).toBe(false);
    expect(await armazenamentoContem('token-refresh')).toBe(false);
    expect(indexed.operacoes.join('\n')).not.toContain('token-acesso');
    expect(indexed.operacoes.join('\n')).not.toContain('token-refresh');
  });

  it('informa revogação remota não confirmada e só então limpa a aba', async () => {
    memoria.accessToken.set('token-acesso');
    memoria.refreshToken.set('token-refresh');
    const saida = servico.sair();
    const pedido = http.expectOne(`${URL_API}/api/v1/session/logout`);
    expect(pedido.request.headers.get('Authorization')).toBe('Bearer token-acesso');
    expect(memoria.accessToken()).toBe('token-acesso');
    pedido.error(new ProgressEvent('error'));
    await saida;

    expect(memoria.accessToken()).toBeNull();
    expect(memoria.refreshToken()).toBeNull();
    expect(memoria.revogacaoNaoConfirmada()).toBe(true);
    expect(memoria.erro()).toBe(MENSAGEM_REVOGACAO);
  });
});

async function armazenamentoContem(segredo: string): Promise<boolean> {
  if (document.cookie.includes(segredo)) {
    return true;
  }

  for (const store of [localStorage, sessionStorage]) {
    for (let indice = 0; indice < store.length; indice += 1) {
      const chave = store.key(indice) ?? '';
      if (chave.includes(segredo) || (store.getItem(chave) ?? '').includes(segredo)) {
        return true;
      }
    }
  }

  const banco = indexedDbAtual();
  if (!banco?.databases) {
    return false;
  }

  const bancos = await banco.databases();
  for (const item of bancos) {
    if (!item.name) {
      continue;
    }
    if (item.name.includes(segredo)) {
      return true;
    }
    const texto = await lerIndexedDb(item.name);
    if (texto.includes(segredo)) {
      return true;
    }
  }

  return false;
}

function indexedDbAtual(): IDBFactory | undefined {
  return (globalThis as { indexedDB?: IDBFactory }).indexedDB;
}

function instalarIndexedDb(): { operacoes: string[] } {
  const operacoes: string[] = [];
  const fake = {
    databases: async () => [],
    open: (nome: string) => {
      operacoes.push(`open:${nome}`);
      const pedido = {
        result: { close() {}, objectStoreNames: [] as unknown as DOMStringList },
        onerror: null as ((evento: Event) => void) | null,
        onsuccess: null as ((evento: Event) => void) | null,
      };
      queueMicrotask(() => pedido.onsuccess?.(new Event('success')));
      return pedido;
    },
  };
  Object.defineProperty(globalThis, 'indexedDB', { configurable: true, value: fake });
  return { operacoes };
}

function lerIndexedDb(nome: string): Promise<string> {
  const bancoGlobal = indexedDbAtual();
  if (!bancoGlobal) {
    return Promise.resolve('');
  }

  return new Promise((resolve) => {
    const pedido = bancoGlobal.open(nome);
    pedido.onerror = () => resolve('');
    pedido.onsuccess = () => {
      const banco = pedido.result;
      const nomes = Array.from(banco.objectStoreNames);
      if (nomes.length === 0) {
        banco.close();
        resolve('');
        return;
      }

      const transacao = banco.transaction(nomes, 'readonly');
      const partes: string[] = [];
      for (const nomeStore of nomes) {
        const leitura = transacao.objectStore(nomeStore).getAll();
        leitura.onsuccess = () => partes.push(JSON.stringify(leitura.result));
      }
      transacao.oncomplete = () => {
        banco.close();
        resolve(partes.join('\n'));
      };
      transacao.onerror = () => {
        banco.close();
        resolve(partes.join('\n'));
      };
    };
  });
}
