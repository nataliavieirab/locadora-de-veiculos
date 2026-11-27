import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { URL_API } from '../../core/configuracao';
import { GruposPlanosService } from './grupos-planos.service';
import { DadosVeiculo, VeiculosService } from './veiculos.service';

describe('VeiculosService', () => {
  let api: VeiculosService;
  let http: HttpTestingController;
  const grupos = { listarGrupos: vi.fn() };
  const base = `${URL_API}/api/v1/veiculos`;
  const dados: DadosVeiculo = {
    placa: 'ABC-1D23', marca: 'Marca', cor: 'Azul', modelo: 'Modelo', combustivel: 'Gasolina',
    capacidadeLitros: '48.125', ano: 2026, grupoId: 'grupo-a',
  };

  beforeEach(() => {
    vi.resetAllMocks();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: GruposPlanosService, useValue: grupos }],
    });
    api = TestBed.inject(VeiculosService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('consulta página e grupo explicitamente sem parâmetro de empresa', async () => {
    const listar = api.listar(2, 'grupo-a');
    const pedido = http.expectOne((requisicao) => requisicao.url === base);
    expect(pedido.request.params.keys().sort()).toEqual(['grupoId', 'page', 'pageSize']);
    expect(pedido.request.params.get('page')).toBe('2');
    expect(pedido.request.params.get('grupoId')).toBe('grupo-a');
    pedido.flush({ items: [], page: 2, pageSize: 20, total: 0 });
    await listar;
  });

  it('carrega todas as páginas de grupos para não omitir opções de cadastro', async () => {
    grupos.listarGrupos.mockResolvedValueOnce({ items: [{ id: 'a' }], page: 1, pageSize: 1, total: 2 })
      .mockResolvedValueOnce({ items: [{ id: 'b' }], page: 2, pageSize: 1, total: 2 });
    expect(await api.listarGrupos()).toEqual([{ id: 'a' }, { id: 'b' }]);
    expect(grupos.listarGrupos.mock.calls).toEqual([[1], [2]]);
  });

  it('envia dados sem foto e mantém capacidade decimal em string', async () => {
    const salvar = api.salvar(dados, null);
    const pedido = http.expectOne(base);
    expect(pedido.request.method).toBe('POST');
    expect(pedido.request.body).toEqual(dados);
    pedido.flush({ id: 'veiculo-a', ...dados }, { status: 201, statusText: 'Created', headers: { ETag: '"v1"' } });
    expect((await salvar).etag).toBe('"v1"');
  });

  it('exige ETag na leitura e utiliza a versão recebida na edição', async () => {
    const consultar = api.consultar('veiculo-a');
    const veiculo = { id: 'veiculo-a', ...dados };
    http.expectOne(`${base}/veiculo-a`).flush(veiculo, { headers: { ETag: '"v1"' } });
    const atual = await consultar;
    const salvar = api.salvar({ ...dados, cor: 'Prata' }, atual);
    const pedido = http.expectOne(`${base}/veiculo-a`);
    expect(pedido.request.method).toBe('PUT');
    expect(pedido.request.headers.get('If-Match')).toBe('"v1"');
    pedido.flush(veiculo, { headers: { ETag: '"v2"' } });
    await salvar;
  });

  it('envia um único campo de arquivo sem definir Content-Type e conserva ETag de foto', async () => {
    const arquivo = new File(['imagem'], 'pessoal.png', { type: 'image/png' });
    const envio = api.enviarFoto('veiculo-a', '"v1"', arquivo);
    const pedido = http.expectOne(`${base}/veiculo-a/foto`);
    expect(pedido.request.method).toBe('PUT');
    expect(pedido.request.headers.get('If-Match')).toBe('"v1"');
    expect(pedido.request.headers.has('Content-Type')).toBe(false);
    expect([...pedido.request.body.keys()]).toEqual(['arquivo']);
    expect(pedido.request.body.get('arquivo').name).toBe('imagem');
    const foto = { id: 'foto-a', caminho: '/api/v1/veiculos/veiculo-a/foto', mime: 'image/png', tamanhoBytes: 6, largura: 1, altura: 1 };
    pedido.flush({ foto }, { headers: { ETag: '"v2"' } });
    expect(await envio).toEqual({ dados: foto, etag: '"v2"' });
    const leitura = api.lerFoto('veiculo-a');
    const obter = http.expectOne(`${base}/veiculo-a/foto`);
    expect(obter.request.responseType).toBe('blob');
    obter.flush(new Blob(['foto'], { type: 'image/png' }));
    expect((await leitura).type).toBe('image/png');
    const remover = api.removerFoto('veiculo-a', '"v2"');
    const exclusao = http.expectOne(`${base}/veiculo-a/foto`);
    expect(exclusao.request.method).toBe('DELETE');
    expect(exclusao.request.headers.get('If-Match')).toBe('"v2"');
    exclusao.flush(null, { status: 204, statusText: 'No Content', headers: { ETag: '"v3"' } });
    expect(await remover).toBe('"v3"');
  });

  it('obtém a versão antes de excluir e não chama rota de imagem', async () => {
    const excluir = api.excluir('veiculo-a');
    http.expectOne(`${base}/veiculo-a`).flush({ id: 'veiculo-a', ...dados }, { headers: { ETag: '"v2"' } });
    await new Promise((resolve) => setTimeout(resolve));
    const pedido = http.expectOne(`${base}/veiculo-a`);
    expect(pedido.request.method).toBe('DELETE');
    expect(pedido.request.headers.get('If-Match')).toBe('"v2"');
    pedido.flush(null, { status: 204, statusText: 'No Content' });
    await excluir;
  });
});
