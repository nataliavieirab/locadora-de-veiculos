import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { URL_API } from '../../core/configuracao';
import { PrecosService } from './precos.service';

describe('PrecosService', () => {
  let api: PrecosService;
  let http: HttpTestingController;
  const base = `${URL_API}/api/v1`;
  const taxa = { id: 'taxa-1', nome: 'Taxa', preco: '5', modalidade: 'Fixa' as const };
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(PrecosService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('pagina taxas sem parâmetro de empresa e envia decimais em string', async () => {
    const listar = api.listarTaxas(2);
    const pedido = http.expectOne((r) => r.url === `${base}/taxas-servicos`);
    expect(pedido.request.params.get('page')).toBe('2');
    expect(pedido.request.params.has('empresaId')).toBe(false);
    pedido.flush({ items: [], page: 2, pageSize: 20, total: 0 });
    await listar;
    const criar = api.salvarTaxa({ nome: 'Taxa', preco: '5.123456', modalidade: 'Fixa' }, null);
    const criacao = http.expectOne(`${base}/taxas-servicos`);
    expect(criacao.request.method).toBe('POST');
    expect(criacao.request.body.preco).toBe('5.123456');
    criacao.flush(taxa);
    await criar;
  });

  it('usa ETag para editar e consulta versão antes de excluir taxa', async () => {
    const editar = api.salvarTaxa(taxa, { dados: taxa, etag: '"v1"' });
    const pedido = http.expectOne(`${base}/taxas-servicos/taxa-1`);
    expect(pedido.request.headers.get('If-Match')).toBe('"v1"');
    pedido.flush(taxa);
    await editar;
    const excluir = api.excluirTaxa('taxa-1');
    http.expectOne(`${base}/taxas-servicos/taxa-1`).flush(taxa, { headers: { ETag: '"v2"' } });
    await new Promise((resolve) => setTimeout(resolve));
    const exclusao = http.expectOne(`${base}/taxas-servicos/taxa-1`);
    expect(exclusao.request.method).toBe('DELETE');
    expect(exclusao.request.headers.get('If-Match')).toBe('"v2"');
    exclusao.flush(null);
    await excluir;
  });

  it('aceita seguro não configurado sem ETag e usa ETag ao salvar configurado', async () => {
    const ler = api.seguro('Cliente');
    http.expectOne(`${base}/configuracoes/seguros/Cliente`).flush({ tipo: 'Cliente', id: null, configurado: false, precoDiario: null });
    const vazio = await ler;
    expect(vazio.etag).toBeNull();
    const criar = api.salvarSeguro('Cliente', '0', vazio);
    const criacao = http.expectOne(`${base}/configuracoes/seguros/Cliente`);
    expect(criacao.request.headers.has('If-Match')).toBe(false);
    expect(criacao.request.body).toEqual({ precoDiario: '0' });
    const dados = { id: 'seg-1', tipo: 'Cliente', configurado: true, precoDiario: '0' };
    criacao.flush(dados, { headers: { ETag: '"v1"' } });
    const atual = await criar;
    const editar = api.salvarSeguro('Cliente', '7', atual);
    const edicao = http.expectOne(`${base}/configuracoes/seguros/Cliente`);
    expect(edicao.request.headers.get('If-Match')).toBe('"v1"');
    edicao.flush({ ...dados, precoDiario: '7' }, { headers: { ETag: '"v2"' } });
    expect((await editar).etag).toBe('"v2"');
  });

  it('recusa leitura configurada sem ETag', async () => {
    const ler = api.seguro('Terceiros');
    http.expectOne(`${base}/configuracoes/seguros/Terceiros`).flush({ tipo: 'Terceiros', id: 'seg-1', configurado: true, precoDiario: '1' });
    await expect(ler).rejects.toThrow('versão');
  });

  it('consulta histórico e acrescenta combustível sem enviar instante ou ETag', async () => {
    const ler = api.combustivel('Gasolina');
    http.expectOne(`${base}/configuracoes/combustiveis/Gasolina`).flush({ tipo: 'Gasolina', configurado: false, atual: null, historico: [] });
    expect((await ler).historico).toEqual([]);
    const salvar = api.registrarCombustivel('Gasolina', '6.123456');
    const pedido = http.expectOne(`${base}/configuracoes/combustiveis/Gasolina`);
    expect(pedido.request.method).toBe('PUT');
    expect(pedido.request.body).toEqual({ precoUnitario: '6.123456' });
    expect(pedido.request.headers.has('If-Match')).toBe(false);
    pedido.flush({ id: 'v1', precoUnitario: '6.123456', inicioVigenciaUtc: '2026-11-23T12:00:00Z' });
    await salvar;
  });
});
