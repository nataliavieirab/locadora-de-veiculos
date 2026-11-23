import { HttpErrorResponse } from '@angular/common/http';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { VeiculosComponent } from './veiculos.component';
import { Veiculo, VeiculosService } from './veiculos.service';

describe('VeiculosComponent', () => {
  let fixture: ComponentFixture<VeiculosComponent>;
  let tela: HTMLElement;
  const originais = ['showModal', 'close'].map((nome) => [nome, Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, nome)] as const);
  const grupos = [{ id: 'grupo-a', nome: 'Compactos', planos: [] }, { id: 'grupo-b', nome: 'Executivos', planos: [] }];
  const veiculo: Veiculo = {
    id: 'veiculo-a', grupoId: 'grupo-a', placa: 'ABC-1D23', marca: 'Marca sintética', cor: 'Azul',
    modelo: 'Modelo sintético', ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '48.125',
  };
  const api = { listar: vi.fn(), listarGrupos: vi.fn(), consultar: vi.fn(), salvar: vi.fn(), excluir: vi.fn() };
  const pagina = (items: Veiculo[]) => ({ items, page: 1, pageSize: 20, total: items.length });

  beforeEach(async () => {
    vi.resetAllMocks();
    api.listar.mockResolvedValue(pagina([veiculo]));
    api.listarGrupos.mockResolvedValue(grupos);
    api.consultar.mockResolvedValue({ dados: veiculo, etag: '"v1"' });
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = true; },
    });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = false; this.dispatchEvent(new Event('close')); },
    });
    await TestBed.configureTestingModule({
      imports: [VeiculosComponent],
      providers: [provideZonelessChangeDetection(), provideRouter([]), { provide: VeiculosService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(VeiculosComponent);
    tela = fixture.nativeElement;
    document.body.appendChild(tela);
    await estabilizar();
  });

  afterEach(() => {
    fixture.destroy();
    document.body.replaceChildren();
    for (const [nome, original] of originais) {
      if (original) Object.defineProperty(HTMLDialogElement.prototype, nome, original);
      else Reflect.deleteProperty(HTMLDialogElement.prototype, nome);
    }
  });

  async function estabilizar(): Promise<void> {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    await fixture.whenStable();
  }

  async function clicar(seletor: string): Promise<void> {
    const elemento = tela.querySelector<HTMLButtonElement>(seletor)!;
    elemento.focus();
    elemento.click();
    await estabilizar();
  }

  async function preencher(id: string, valor: string): Promise<void> {
    const elemento = tela.querySelector<HTMLInputElement | HTMLSelectElement>(`#${id}`)!;
    elemento.value = valor;
    elemento.dispatchEvent(new Event('input', { bubbles: true }));
    if (elemento instanceof HTMLSelectElement) elemento.dispatchEvent(new Event('change', { bubbles: true }));
    await estabilizar();
  }

  async function enviar(): Promise<void> {
    tela.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
  }

  it('lista os campos da frota e não contém controle ou referência de imagem', async () => {
    expect(tela.querySelector('tbody')?.textContent).toContain('ABC-1D23');
    expect(tela.querySelector('tbody')?.textContent).toContain('Marca sintética');
    expect(tela.querySelector('tbody')?.textContent).toContain('Gasolina');
    expect(tela.querySelector('tbody')?.textContent).toContain('Compactos');
    await clicar('#novo-veiculo');
    expect(tela.querySelector('input[type="file"], img')).toBeNull();
    expect(tela.textContent?.toLowerCase()).not.toContain('foto');
    expect(document.activeElement?.id).toBe('placa-veiculo');
  });

  it('associa erros obrigatórios e foca o resumo sem salvar formulário inválido', async () => {
    await clicar('#novo-veiculo');
    await enviar();
    expect(api.salvar).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erro-editor-veiculos');
    for (const id of ['placa-veiculo', 'grupo-veiculo', 'marca-veiculo', 'modelo-veiculo', 'cor-veiculo', 'ano-veiculo', 'capacidade-veiculo']) {
      const elemento = tela.querySelector(`#${id}`)!;
      expect(elemento.getAttribute('aria-invalid')).toBe('true');
      expect(elemento.getAttribute('aria-describedby')).toContain(`erro-${id}`);
    }
  });

  it.each(['0', '-1', '0,0001', '1000000000', '1e3'])('rejeita capacidade inválida %s', async (valor) => {
    await clicar('[aria-label="Editar veículo ABC-1D23"]');
    await preencher('capacidade-veiculo', valor);
    await enviar();
    expect(api.salvar).not.toHaveBeenCalled();
    expect(tela.querySelector('#erro-capacidade-veiculo')?.textContent).toContain('positiva');
  });

  it('edita com ETag e transporta litros decimais sem converter para number', async () => {
    await clicar('[aria-label="Editar veículo ABC-1D23"]');
    expect(document.activeElement?.id).toBe('placa-veiculo');
    expect(tela.querySelector<HTMLInputElement>('#capacidade-veiculo')?.value).toBe('48,125');
    await preencher('capacidade-veiculo', '00048,126');
    await preencher('cor-veiculo', 'Prata');
    await enviar();
    const { id: _id, ...dados } = veiculo;
    expect(api.salvar).toHaveBeenCalledWith({ ...dados, cor: 'Prata', capacidadeLitros: '48.126' }, { dados: veiculo, etag: '"v1"' });
    expect(tela.querySelector('dialog')?.open).toBe(false);
  });

  it('cria cadastro completo sem imagem e mantém o grupo selecionado no filtro', async () => {
    await preencher('filtro-grupo', 'grupo-b');
    await clicar('#novo-veiculo');
    expect(tela.querySelector<HTMLSelectElement>('#grupo-veiculo')?.value).toBe('grupo-b');
    for (const [id, valor] of [
      ['placa-veiculo', 'XYZ-9A87'], ['marca-veiculo', 'Marca'], ['modelo-veiculo', 'Modelo'],
      ['cor-veiculo', 'Prata'], ['ano-veiculo', '2026'], ['capacidade-veiculo', '50,125'],
    ]) await preencher(id, valor);
    await preencher('combustivel-veiculo', 'Alcool');
    await enviar();
    expect(api.salvar).toHaveBeenCalledWith({
      placa: 'XYZ-9A87', marca: 'Marca', modelo: 'Modelo', cor: 'Prata', ano: 2026,
      capacidadeLitros: '50.125', combustivel: 'Alcool', grupoId: 'grupo-b',
    }, null);
  });

  it('filtra no servidor, distingue vazio contextual e limpa o filtro', async () => {
    api.listar.mockResolvedValueOnce(pagina([]));
    await preencher('filtro-grupo', 'grupo-b');
    expect(api.listar).toHaveBeenLastCalledWith(1, 'grupo-b');
    expect(tela.textContent).toContain('Não há veículos ativos no grupo selecionado.');
    const limpar = [...tela.querySelectorAll('button')].find((botao) => botao.textContent?.includes('Limpar filtro'))!;
    limpar.click();
    await estabilizar();
    expect(api.listar).toHaveBeenLastCalledWith(1, '');
    expect(tela.querySelector('tbody')?.textContent).toContain('ABC-1D23');
  });

  it('agrupa a página sem mutar nem requisitar novos registros', async () => {
    api.listar.mockResolvedValueOnce(pagina([veiculo, { ...veiculo, id: 'veiculo-b', grupoId: 'grupo-b', placa: 'XYZ-9A87' }]));
    await preencher('filtro-grupo', '');
    const antes = api.listar.mock.calls.length;
    const agrupar = tela.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    agrupar.checked = true;
    agrupar.dispatchEvent(new Event('change', { bubbles: true }));
    await estabilizar();
    expect(tela.querySelector('section[aria-label="Compactos"]')?.textContent).toContain('ABC-1D23');
    expect(tela.querySelector('section[aria-label="Executivos"]')?.textContent).toContain('XYZ-9A87');
    expect(api.listar).toHaveBeenCalledTimes(antes);
    expect(api.salvar).not.toHaveBeenCalled();
  });

  it('associa duplicidade à placa sem perder a intenção de edição', async () => {
    api.salvar.mockRejectedValue(new HttpErrorResponse({
      status: 409, error: { title: 'Conflito de placa', errors: { placa: ['Já existe um veículo com esta placa.'] } },
    }));
    await clicar('[aria-label="Editar veículo ABC-1D23"]');
    await preencher('placa-veiculo', 'XYZ-9A87');
    await enviar();
    expect(document.activeElement?.id).toBe('erro-editor-veiculos');
    expect(tela.querySelector('#erro-placa-veiculo')?.textContent).toContain('Já existe');
    expect(tela.querySelector<HTMLInputElement>('#placa-veiculo')?.value).toBe('XYZ-9A87');
    expect(tela.querySelector('dialog')?.open).toBe(true);
  });

  it('explica bloqueio por aluguel sem simular estado de locação', async () => {
    api.excluir.mockRejectedValue(new HttpErrorResponse({
      status: 409, error: { title: 'O aluguel aberto impede esta alteração' },
    }));
    await clicar('[aria-label="Excluir veículo ABC-1D23"]');
    expect(document.activeElement?.id).toBe('erro-lista-veiculos');
    expect(tela.textContent).toContain('O aluguel aberto impede esta alteração');
    expect(api.listar).toHaveBeenCalledTimes(1);
  });
});
