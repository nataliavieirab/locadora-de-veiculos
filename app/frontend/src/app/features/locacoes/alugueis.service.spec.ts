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
});