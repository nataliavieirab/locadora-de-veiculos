import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { URL_API } from '../../core/configuracao';
import { Grupo, GruposPlanosService, Plano } from './grupos-planos.service';

describe('GruposPlanosService', () => {
  let api: GruposPlanosService;
  let http: HttpTestingController;
  const base = `${URL_API}/api/v1`;
  const grupo: Grupo = { id: 'grupo-a', nome: 'Compactos', planos: [] };
  const plano: Plano = {
    id: 'plano-a', grupoId: grupo.id, modalidade: 'Livre', precoDiario: '118.123456',
    precoKm: null, franquiaDiariaKm: null, precoKmExcedente: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(GruposPlanosService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('consulta a página solicitada sem escolher empresa no cliente', async () => {
    const resultado = api.listarGrupos(2);
    const pedido = http.expectOne((requisicao) => requisicao.url === `${base}/grupos`);
    expect(pedido.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    expect(pedido.request.params.get('page')).toBe('2');
    expect(pedido.request.params.get('pageSize')).toBe('20');
    pedido.flush({ items: [grupo], page: 2, pageSize: 20, total: 21 });
    expect((await resultado).items).toEqual([grupo]);
  });

  it('cria grupo e plano sem converter preço em number', async () => {
    const salvarGrupo = api.salvarGrupo(grupo.nome, null);
    const pedidoGrupo = http.expectOne(`${base}/grupos`);
    expect(pedidoGrupo.request.method).toBe('POST');
    expect(pedidoGrupo.request.body).toEqual({ nome: grupo.nome });
    pedidoGrupo.flush(grupo, { status: 201, statusText: 'Created' });
    await salvarGrupo;
    const { id: _id, grupoId, modalidade, ...precos } = plano;
    const salvarPlano = api.salvarPlano(grupoId, modalidade, precos, null);
    const pedidoPlano = http.expectOne(`${base}/planos`);
    expect(pedidoPlano.request.method).toBe('POST');
    expect(pedidoPlano.request.body).toEqual({ grupoId, modalidade, ...precos });
    pedidoPlano.flush(plano, { status: 201, statusText: 'Created' });
    await salvarPlano;
  });

  it('consulta e altera grupo usando exatamente o ETag recebido', async () => {
    const leitura = api.grupo(grupo.id);
    http.expectOne(`${base}/grupos/${grupo.id}`).flush(grupo, { headers: { ETag: '"versao-grupo"' } });
    const atual = await leitura;
    const salvar = api.salvarGrupo('Novo nome', atual);
    const pedido = http.expectOne(`${base}/grupos/${grupo.id}`);
    expect(pedido.request.method).toBe('PUT');
    expect(pedido.request.headers.get('If-Match')).toBe('"versao-grupo"');
    expect(pedido.request.body).toEqual({ nome: 'Novo nome' });
    pedido.flush(grupo);
    await salvar;
  });

  it('edita somente os parâmetros do plano com a versão lida', async () => {
    const leitura = api.plano(plano.id);
    http.expectOne(`${base}/planos/${plano.id}`).flush(plano, { headers: { ETag: '"versao-plano"' } });
    const atual = await leitura;
    const { id: _id, grupoId, modalidade, ...precos } = plano;
    const salvar = api.salvarPlano(grupoId, modalidade, precos, atual);
    const pedido = http.expectOne(`${base}/planos/${plano.id}`);
    expect(pedido.request.method).toBe('PUT');
    expect(pedido.request.headers.get('If-Match')).toBe('"versao-plano"');
    expect(pedido.request.body).toEqual(precos);
    pedido.flush(plano);
    await salvar;
  });

  it.each(['grupo', 'plano'] as const)('obtém a versão antes de excluir %s', async (tipo) => {
    const entidade = tipo === 'grupo' ? grupo : plano;
    const rota = `${base}/${tipo === 'grupo' ? 'grupos' : 'planos'}/${entidade.id}`;
    const excluir = tipo === 'grupo' ? api.excluirGrupo(entidade.id) : api.excluirPlano(entidade.id);
    http.expectOne(rota).flush(entidade, { headers: { ETag: '"versao-exclusao"' } });
    await new Promise((resolve) => setTimeout(resolve));
    const pedido = http.expectOne(rota);
    expect(pedido.request.method).toBe('DELETE');
    expect(pedido.request.headers.get('If-Match')).toBe('"versao-exclusao"');
    pedido.flush(null, { status: 204, statusText: 'No Content' });
    await excluir;
  });

  it('rejeita recurso sem ETag em vez de permitir sobrescrita sem versão', async () => {
    const leitura = api.grupo(grupo.id);
    const rejeicao = expect(leitura).rejects.toThrow('A API não retornou os dados e a versão esperados.');
    http.expectOne(`${base}/grupos/${grupo.id}`).flush(grupo);
    await rejeicao;
  });
});
