import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { URL_API } from '../../core/configuracao';
import { AlugueisService, EntradaContratacao } from './alugueis.service';

describe('AlugueisService', () => {
  let api: AlugueisService;
  let http: HttpTestingController;
  const base = `${URL_API}/api/v1/alugueis`;
  const comando: EntradaContratacao = {
    clienteId: 'c', condutorId: 'd', veiculoId: 'v', planoId: 'p',
    retornoPrevisto: '2026-11-25T10:00:00-03:00', hodometroInicial: 100,
    taxasServicoIds: [], seguros: [],
  };
  const corpo = { ...comando, id: 'a', estado: 'EmAberto', revisao: 1, estimativa: { totalConhecido: '200.00', componentes: [], diarias: 2, caucao: '1000.00' } };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AlugueisService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('obtém todas as referências históricas em páginas com inclusão explícita de ocultos', async () => {
    const carregar = api.todasReferenciasVeiculos(true);
    const primeira = http.expectOne(r => r.url === `${base}/referencias-veiculos` && r.params.get('page') === '1');
    expect(primeira.request.params.get('incluirOcultos')).toBe('true');
    primeira.flush({ items: Array.from({ length: 20 }, (_, i) => ({ id: `v${i}`, placa: `FIC${i}`, modelo: 'Histórico' })), page: 1, pageSize: 20, total: 21 });
    await new Promise(resolve => setTimeout(resolve));
    const segunda = http.expectOne(r => r.url === `${base}/referencias-veiculos` && r.params.get('page') === '2');
    segunda.flush({ items: [{ id: 'v20', placa: 'FIC20', modelo: 'Histórico' }], page: 2, pageSize: 20, total: 21 });
    expect(await carregar).toHaveLength(21);
  });

  it('consulta oculto explicitamente e oculta somente com a versão informada, sem body', async () => {
    const consulta = api.consultar('a', true);
    const ler = http.expectOne(r => r.url === `${base}/a`);
    expect(ler.request.params.get('incluirOcultos')).toBe('true');
    ler.flush(corpo, { headers: { ETag: '"v4"' } });
    await consulta;
    const excluir = api.ocultar('a', '"v4"');
    const remover = http.expectOne(`${base}/a`);
    expect(remover.request.method).toBe('DELETE');
    expect(remover.request.headers.get('If-Match')).toBe('"v4"');
    expect(remover.request.body).toBeNull();
    remover.flush(null); await excluir;
  });

  it('estima sem ocupar e sem If-Match na criação', async () => {
    const chamar = api.estimar(comando);
    const pedido = http.expectOne(`${base}/estimativas`);
    expect(pedido.request.method).toBe('POST');
    expect(pedido.request.headers.has('If-Match')).toBe(false);
    pedido.flush(corpo);
    await chamar;
  });

  it('estima edição com If-Match contextual', async () => {
    const chamar = api.estimar({ ...comando, aluguelId: 'a' }, '"v1"');
    const pedido = http.expectOne(`${base}/estimativas`);
    expect(pedido.request.headers.get('If-Match')).toBe('"v1"');
    expect(pedido.request.body).toMatchObject({ aluguelId: 'a' });
    pedido.flush(corpo);
    await chamar;
  });

  it('cria com Idempotency-Key obrigatória', async () => {
    const chamar = api.criar(comando, '00000000-0000-4000-8000-000000000001');
    const pedido = http.expectOne(base);
    expect(pedido.request.headers.get('Idempotency-Key')).toBe('00000000-0000-4000-8000-000000000001');
    pedido.flush(corpo, { headers: { ETag: '"v1"' } });
    const resultado = await chamar;
    expect(resultado.etag).toBe('"v1"');
  });

  it('altera com If-Match', async () => {
    const chamar = api.alterar('a', comando, '"v2"');
    const pedido = http.expectOne(`${base}/a`);
    expect(pedido.request.method).toBe('PUT');
    expect(pedido.request.headers.get('If-Match')).toBe('"v2"');
    pedido.flush(corpo, { headers: { ETag: '"v3"' } });
    const resultado = await chamar;
    expect(resultado.etag).toBe('"v3"');
  });

  it('transporta prévia e conclusão de devolução com strings, versão e chave originais', async () => {
    const entrada = { retornoReal: '2026-11-25T10:00:00-03:00', hodometroFinal: 150, litrosRestantes: '40.125', taxasAdicionaisIds: [] };
    const previa = api.previaDevolucao('a', entrada, '"v2"');
    const pedido = http.expectOne(`${base}/a/devolucao/previa`);
    expect(pedido.request.headers.get('If-Match')).toBe('"v2"');
    expect(pedido.request.headers.has('Idempotency-Key')).toBe(false);
    expect(pedido.request.body).toEqual(entrada);
    pedido.flush({ versoesAceitas: { resumoCalculo: 'hash' } });
    await previa;
    const comandoFinal = { ...entrada, versoesAceitas: { revisaoId: 'r', versaoRegra: 'devolucao-v1', combustivelId: 'f',
      taxasNovas: [], resumoCalculo: 'hash' } };
    const final = api.devolver('a', comandoFinal, '"v2"', 'chave-original');
    const confirmar = http.expectOne(`${base}/a/devolucao`);
    expect(confirmar.request.headers.get('If-Match')).toBe('"v2"');
    expect(confirmar.request.headers.get('Idempotency-Key')).toBe('chave-original');
    expect(confirmar.request.body).toEqual(comandoFinal);
    confirmar.flush({ aluguelId: 'a', estado: 'Concluido', devolucao: {} }, { headers: { ETag: '"v3"' } });
    expect((await final).etag).toBe('"v3"');
  });
});
