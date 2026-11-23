import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { URL_API } from '../../core/configuracao';
import { ClienteDetalhe, DadosCliente, DadosCondutor, PessoasService, ResumoCliente } from './pessoas.service';

describe('PessoasService', () => {
  let api: PessoasService;
  let http: HttpTestingController;
  const base = `${URL_API}/api/v1`;
  const clientePf: ResumoCliente = {
    id: 'pf-1', tipo: 'PF', nome: 'Lia Monteiro', telefone: '(11) 90000-1842', email: null,
    cpf: '52998224725', cnpj: null, rg: 'RG-1', cnh: 'CNH-418',
  };
  const clientePj: ResumoCliente = {
    id: 'pj-1', tipo: 'PJ', nome: 'Horizonte Azul Ltda.', telefone: '(21) 90000-2764', email: null,
    cpf: null, cnpj: '11222333000181', rg: null, cnh: null,
  };
  const detalhe: ClienteDetalhe = {
    id: 'pf-1', tipo: 'PF', nome: 'Lia Monteiro', telefone: '(11) 90000-1842', email: null,
    cpf: '52998224725', rg: 'RG-1', cnh: 'CNH-418',
    endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua A', numero: '10' },
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(PessoasService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('consulta páginas de clientes e condutores sem parâmetro de empresa', async () => {
    const clientes = api.listarClientes(2);
    const pedidoClientes = http.expectOne((requisicao) => requisicao.url === `${base}/clientes`);
    expect(pedidoClientes.request.params.get('page')).toBe('2');
    expect(pedidoClientes.request.params.get('pageSize')).toBe('20');
    expect(pedidoClientes.request.params.has('empresaId')).toBe(false);
    pedidoClientes.flush({ items: [], page: 2, pageSize: 20, total: 0 });
    await clientes;

    const condutores = api.listarCondutores(1);
    const pedidoCondutores = http.expectOne((requisicao) => requisicao.url === `${base}/condutores`);
    expect(pedidoCondutores.request.params.has('empresaId')).toBe(false);
    pedidoCondutores.flush({ items: [], page: 1, pageSize: 20, total: 0 });
    await condutores;
  });

  it('carrega todas as páginas de clientes para as seleções de vínculo e condutor', async () => {
    const todos = api.listarTodosClientes();
    http.expectOne((requisicao) => requisicao.url === `${base}/clientes` && requisicao.params.get('page') === '1')
      .flush({ items: [clientePf], page: 1, pageSize: 20, total: 21 });
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne((requisicao) => requisicao.url === `${base}/clientes` && requisicao.params.get('page') === '2')
      .flush({ items: [clientePj], page: 2, pageSize: 20, total: 21 });
    expect(await todos).toEqual([clientePf, clientePj]);
  });

  it('envia cliente PF discriminado sem CNPJ e cliente PJ sem documentos de PF', async () => {
    const pf: DadosCliente = {
      tipo: 'PF', nome: 'Lia Monteiro', cpf: '52998224725', rg: 'RG-1', cnh: 'CNH-418',
      telefone: '(11) 90000-1842', email: null, endereco: detalhe.endereco,
    };
    const criarPf = api.salvarCliente(pf, null);
    const pedidoPf = http.expectOne(`${base}/clientes`);
    expect(pedidoPf.request.method).toBe('POST');
    expect(pedidoPf.request.body).toEqual(pf);
    expect(pedidoPf.request.body).not.toHaveProperty('cnpj');
    pedidoPf.flush(detalhe, { status: 201, statusText: 'Created' });
    await criarPf;

    const pj: DadosCliente = {
      tipo: 'PJ', nome: 'Horizonte Azul Ltda.', cnpj: '11222333000181',
      telefone: '(21) 90000-2764', email: null, endereco: detalhe.endereco,
    };
    const criarPj = api.salvarCliente(pj, null);
    const pedidoPj = http.expectOne(`${base}/clientes`);
    expect(pedidoPj.request.body).toEqual(pj);
    expect(pedidoPj.request.body).not.toHaveProperty('cpf');
    expect(pedidoPj.request.body).not.toHaveProperty('rg');
    expect(pedidoPj.request.body).not.toHaveProperty('cnh');
    pedidoPj.flush(detalhe, { status: 201, statusText: 'Created' });
    await criarPj;
  });

  it('exige ETag na leitura e usa a versão recebida na edição e exclusão', async () => {
    const consultar = api.consultarCliente('pf-1');
    http.expectOne(`${base}/clientes/pf-1`).flush(detalhe, { headers: { ETag: '"v1"' } });
    const atual = await consultar;

    const salvar = api.salvarCliente({ ...detalhe, tipo: 'PF', cpf: detalhe.cpf!, rg: detalhe.rg!, cnh: detalhe.cnh!, email: null, endereco: detalhe.endereco }, atual);
    const pedidoEdicao = http.expectOne(`${base}/clientes/pf-1`);
    expect(pedidoEdicao.request.method).toBe('PUT');
    expect(pedidoEdicao.request.headers.get('If-Match')).toBe('"v1"');
    pedidoEdicao.flush(detalhe);
    await salvar;

    const excluir = api.excluirCliente('pf-1');
    http.expectOne(`${base}/clientes/pf-1`).flush(detalhe, { headers: { ETag: '"v2"' } });
    await new Promise((resolve) => setTimeout(resolve));
    const pedidoExclusao = http.expectOne(`${base}/clientes/pf-1`);
    expect(pedidoExclusao.request.method).toBe('DELETE');
    expect(pedidoExclusao.request.headers.get('If-Match')).toBe('"v2"');
    pedidoExclusao.flush(null, { status: 204, statusText: 'No Content' });
    await excluir;
  });

  it('rejeita leitura sem ETag para impedir edição sem versão', async () => {
    const consultar = api.consultarCliente('pf-1');
    http.expectOne(`${base}/clientes/pf-1`).flush(detalhe);
    await expect(consultar).rejects.toThrow('versão');
  });

  it('envia condutor com cliente e próprio condutor e usa ETag na alteração', async () => {
    const dados: DadosCondutor = {
      clienteId: 'pf-1', nome: 'Lia Monteiro', email: 'lia@example.invalid', cpf: '52998224725',
      cnh: 'CNH-418', validadeCnh: '2028-09-18', telefone: '(11) 90000-1842', proprioCliente: true,
    };
    const criar = api.salvarCondutor(dados, null);
    const pedidoCriacao = http.expectOne(`${base}/condutores`);
    expect(pedidoCriacao.request.body).toEqual(dados);
    pedidoCriacao.flush({ id: 'cond-1', clienteNome: 'Lia Monteiro', ...dados }, { status: 201, statusText: 'Created' });
    await criar;

    const consultar = api.consultarCondutor('cond-1');
    http.expectOne(`${base}/condutores/cond-1`).flush(
      { id: 'cond-1', clienteNome: 'Lia Monteiro', ...dados }, { headers: { ETag: '"v3"' } },
    );
    const atual = await consultar;
    const alterar = api.salvarCondutor({ ...dados, nome: 'Lia M.' }, atual);
    const pedidoAlteracao = http.expectOne(`${base}/condutores/cond-1`);
    expect(pedidoAlteracao.request.method).toBe('PUT');
    expect(pedidoAlteracao.request.headers.get('If-Match')).toBe('"v3"');
    pedidoAlteracao.flush({ id: 'cond-1', clienteNome: 'Lia Monteiro', ...dados });
    await alterar;
  });

  it('cria vínculo pela pessoa jurídica e obtém ETag antes de remover', async () => {
    const criar = api.vincular('pj-1', 'pf-1');
    const pedidoCriacao = http.expectOne(`${base}/clientes/pj-1/vinculos-pj`);
    expect(pedidoCriacao.request.method).toBe('POST');
    expect(pedidoCriacao.request.body).toEqual({ outroClienteId: 'pf-1' });
    const vinculo = { id: 'vinculo-1', clientePfId: 'pf-1', clientePfNome: 'Lia', clientePjId: 'pj-1', clientePjNome: 'Horizonte' };
    pedidoCriacao.flush(vinculo, { status: 201, statusText: 'Created', headers: { ETag: '"v4"' } });
    await criar;

    const listar = api.listarVinculos('pj-1');
    http.expectOne(`${base}/clientes/pj-1/vinculos-pj`).flush([vinculo]);
    expect(await listar).toEqual([vinculo]);

    const remover = api.desvincular('pj-1', 'vinculo-1');
    http.expectOne(`${base}/clientes/pj-1/vinculos-pj/vinculo-1`).flush(vinculo, { headers: { ETag: '"v5"' } });
    await new Promise((resolve) => setTimeout(resolve));
    const pedidoRemocao = http.expectOne(`${base}/clientes/pj-1/vinculos-pj/vinculo-1`);
    expect(pedidoRemocao.request.method).toBe('DELETE');
    expect(pedidoRemocao.request.headers.get('If-Match')).toBe('"v5"');
    pedidoRemocao.flush(null, { status: 204, statusText: 'No Content' });
    await remover;
  });
});
