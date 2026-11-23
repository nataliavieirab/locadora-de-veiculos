import { HttpErrorResponse } from '@angular/common/http';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PrecosComponent } from './precos.component';
import { PrecosService, SeguroVersionado, TipoSeguro } from './precos.service';

describe('PrecosComponent', () => {
  let fixture: ComponentFixture<PrecosComponent>;
  let tela: HTMLElement;
  const originais = ['showModal', 'close'].map((nome) => [nome, Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, nome)] as const);
  const taxa = { id: 'taxa-1', nome: 'Assento sintético', preco: '18.123456', modalidade: 'Diaria' };
  const api = {
    listarTaxas: vi.fn(), taxa: vi.fn(), salvarTaxa: vi.fn(), excluirTaxa: vi.fn(),
    seguro: vi.fn(), salvarSeguro: vi.fn(), combustivel: vi.fn(), registrarCombustivel: vi.fn(),
  };

  beforeEach(async () => {
    vi.resetAllMocks();
    api.listarTaxas.mockResolvedValue({ items: [taxa], page: 1, pageSize: 20, total: 1 });
    api.taxa.mockResolvedValue({ dados: taxa, etag: '"v1"' });
    api.seguro.mockImplementation((tipo: TipoSeguro) => Promise.resolve({
      dados: { id: null, tipo, configurado: false, precoDiario: null }, etag: null,
    }));
    api.salvarSeguro.mockImplementation((tipo: TipoSeguro, precoDiario: string) => Promise.resolve({
      dados: { id: `seguro-${tipo}`, tipo, configurado: true, precoDiario }, etag: '"v2"',
    }));
    api.combustivel.mockImplementation((tipo: string) => Promise.resolve({ tipo, configurado: false, atual: null, historico: [] }));
    api.salvarTaxa.mockResolvedValue(undefined);
    api.excluirTaxa.mockResolvedValue(undefined);
    api.registrarCombustivel.mockResolvedValue(undefined);
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = true; },
    });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = false; this.dispatchEvent(new Event('close')); },
    });
    await TestBed.configureTestingModule({
      imports: [PrecosComponent],
      providers: [provideZonelessChangeDetection(), provideRouter([]), { provide: PrecosService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(PrecosComponent);
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

  async function enviar(id: string): Promise<void> {
    tela.querySelector(`#${id}`)!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
  }

  it('lista taxa em BRL e mantém ausência de seguro e combustível distinta de zero', async () => {
    expect(tela.querySelector('#painel-taxas')?.textContent).toContain('Assento sintético');
    expect(tela.querySelector('#painel-taxas')?.textContent).toContain('18,12');
    await clicar('#aba-seguros');
    expect(tela.querySelector('#painel-seguros')?.textContent).toContain('Não configurado');
    expect(tela.querySelector<HTMLInputElement>('#seguro-Cliente')?.value).toBe('');
    await clicar('#aba-combustiveis');
    expect(tela.querySelectorAll('#painel-combustiveis article')).toHaveLength(4);
    expect(tela.querySelectorAll('#painel-combustiveis ol')).toHaveLength(0);
    expect(tela.querySelector('#painel-combustiveis')?.textContent).toContain('Não configurado');
    expect(tela.querySelector('[id*="empresa" i], [type="file"]')).toBeNull();
  });

  it('cria taxa fixa com zero explícito e restaura o foco', async () => {
    await clicar('#nova-taxa');
    expect(document.activeElement?.id).toBe('nome-taxa');
    await preencher('nome-taxa', 'Limpeza sintética');
    await preencher('preco-taxa', '0,00');
    await enviar('formulario-taxa');
    expect(api.salvarTaxa).toHaveBeenCalledWith({ nome: 'Limpeza sintética', preco: '0', modalidade: 'Fixa' }, null);
    expect(tela.querySelector('dialog')?.open).toBe(false);
    expect(document.activeElement?.id).toBe('nova-taxa');
  });

  it('edita taxa com versão e sem reduzir as casas decimais', async () => {
    await clicar('[aria-label="Editar Assento sintético"]');
    expect(tela.querySelector<HTMLInputElement>('#preco-taxa')?.value).toBe('18,123456');
    await preencher('preco-taxa', '19,123456');
    await enviar('formulario-taxa');
    expect(api.salvarTaxa).toHaveBeenCalledWith({ nome: taxa.nome, preco: '19.123456', modalidade: 'Diaria' }, { dados: taxa, etag: '"v1"' });
  });

  it('rejeita taxa negativa, associa erro e foca o resumo', async () => {
    await clicar('#nova-taxa');
    await preencher('nome-taxa', 'Taxa');
    await preencher('preco-taxa', '-1');
    await enviar('formulario-taxa');
    expect(api.salvarTaxa).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erro-editor-precos');
    expect(tela.querySelector('#preco-taxa')?.getAttribute('aria-describedby')).toContain('erro-preco-taxa');
    expect(tela.querySelector('#erro-preco-taxa')?.textContent).toContain('não negativo');
  });

  it('bloqueio de exclusão mantém taxa visível e explica o vínculo', async () => {
    api.excluirTaxa.mockRejectedValue(new HttpErrorResponse({ status: 409, error: { code: 'taxa_com_aluguel_aberto', title: 'Conflito' } }));
    await clicar('[aria-label="Excluir Assento sintético"]');
    expect(document.activeElement?.id).toBe('erro-acao-precos');
    expect(tela.querySelector('#erro-acao-precos')?.textContent).toContain('aluguel aberto');
    expect(tela.querySelector('#painel-taxas tbody')?.textContent).toContain(taxa.nome);
  });

  it('salva dois seguros com preços independentes e envia apenas o alterado depois', async () => {
    await clicar('#aba-seguros');
    await preencher('seguro-Cliente', '0');
    await preencher('seguro-Terceiros', '7,123456');
    await enviar('formulario-seguros');
    expect(api.salvarSeguro).toHaveBeenCalledWith('Cliente', '0', expect.objectContaining({ etag: null }));
    expect(api.salvarSeguro).toHaveBeenCalledWith('Terceiros', '7.123456', expect.objectContaining({ etag: null }));
    api.salvarSeguro.mockClear();
    await preencher('seguro-Cliente', '5');
    await enviar('formulario-seguros');
    expect(api.salvarSeguro).toHaveBeenCalledTimes(1);
    expect(api.salvarSeguro).toHaveBeenCalledWith('Cliente', '5', expect.objectContaining({ etag: '"v2"' }));
    expect(tela.querySelector('article[aria-label="Seguro de terceiros"]')?.textContent).toContain('7,12');
  });

  it('um seguro negativo impede envio de ambos e move foco', async () => {
    await clicar('#aba-seguros');
    await preencher('seguro-Cliente', '5');
    await preencher('seguro-Terceiros', '-1');
    await enviar('formulario-seguros');
    expect(api.salvarSeguro).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erro-seguros');
    expect(tela.querySelector('#seguro-Terceiros')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('informa falha parcial sem desfazer nem reenviar seguro já salvo', async () => {
    const salvar = api.salvarSeguro.getMockImplementation()!;
    api.salvarSeguro.mockImplementation((tipo: TipoSeguro, preco: string, atual: SeguroVersionado) => {
      if (tipo === 'Terceiros') return Promise.reject(new HttpErrorResponse({ status: 412, error: { title: 'Recarregue a configuração.' } }));
      return salvar(tipo, preco, atual);
    });
    await clicar('#aba-seguros');
    await preencher('seguro-Cliente', '5');
    await preencher('seguro-Terceiros', '7');
    await enviar('formulario-seguros');
    expect(tela.querySelector('#erro-seguros')?.textContent).toContain('outro preço já foi salvo');
    expect(tela.querySelector('article[aria-label="Seguro do cliente"]')?.textContent).toContain('5,00');
    expect(tela.querySelector('article[aria-label="Seguro de terceiros"]')?.textContent).toContain('Não configurado');
    expect(document.activeElement?.id).toBe('erro-seguros');
  });

  it('cria vigência sem enviar instante e apresenta histórico do servidor', async () => {
    await clicar('#aba-combustiveis');
    await clicar('[aria-label="Definir primeiro preço · Gasolina"]');
    expect(document.activeElement?.id).toBe('preco-combustivel');
    await preencher('preco-combustivel', '6,123456');
    api.combustivel.mockImplementation((tipo: string) => Promise.resolve({
      tipo, configurado: true, atual: { id: 'v2', precoUnitario: '6.123456', inicioVigenciaUtc: '2026-11-23T12:00:00Z' },
      historico: [
        { id: 'v2', precoUnitario: '6.123456', inicioVigenciaUtc: '2026-11-23T12:00:00Z' },
        { id: 'v1', precoUnitario: '5', inicioVigenciaUtc: '2026-11-22T12:00:00Z' },
      ],
    }));
    await enviar('formulario-combustivel');
    expect(api.registrarCombustivel).toHaveBeenCalledWith('Gasolina', '6.123456');
    expect(tela.querySelector('article[aria-label="Gasolina"] ol')?.textContent).toContain('23/11/2026, 12:00:00');
    expect(tela.querySelectorAll('article[aria-label="Gasolina"] li')).toHaveLength(2);
    expect(tela.querySelector('article[aria-label="Gasolina"] ol')?.textContent).toContain('5,00');
  });

  it('rejeita combustível negativo com foco no resumo', async () => {
    await clicar('#aba-combustiveis');
    await clicar('[aria-label="Definir primeiro preço · Diesel"]');
    await preencher('preco-combustivel', '-1');
    await enviar('formulario-combustivel');
    expect(api.registrarCombustivel).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erro-editor-precos');
  });

  it('navega abas por setas e mantém contenção de tabulação no editor', async () => {
    const aba = tela.querySelector<HTMLButtonElement>('#aba-taxas')!;
    aba.focus();
    aba.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    await estabilizar();
    expect(document.activeElement?.id).toBe('aba-seguros');
    expect(tela.querySelector('#aba-seguros')?.getAttribute('aria-selected')).toBe('true');
    await clicar('#aba-taxas');
    await clicar('#nova-taxa');
    // JSDOM não calcula layout; a ordem e a visibilidade reais são verificadas em Playwright.
    for (const controle of tela.querySelectorAll<HTMLElement>('dialog button, dialog input, dialog select')) {
      vi.spyOn(controle, 'getClientRects').mockReturnValue([new DOMRect(0, 0, 100, 30)] as unknown as DOMRectList);
    }
    const primeiro = tela.querySelector<HTMLButtonElement>('dialog [aria-label="Fechar formulário"]')!;
    primeiro.focus();
    primeiro.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true, cancelable: true }));
    expect(document.activeElement?.textContent).toContain('Salvar taxa ou serviço');
  });
});
