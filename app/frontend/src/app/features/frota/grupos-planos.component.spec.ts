import { HttpErrorResponse } from '@angular/common/http';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { GruposPlanosComponent } from './grupos-planos.component';
import { Grupo, GruposPlanosService, ModalidadePlano, Plano } from './grupos-planos.service';

describe('GruposPlanosComponent', () => {
  let fixture: ComponentFixture<GruposPlanosComponent>;
  let tela: HTMLElement;
  const showModalOriginal = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'showModal');
  const closeOriginal = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'close');
  const grupo: Grupo = { id: 'grupo-a', nome: 'Compactos', planos: [] };
  const api = {
    listarGrupos: vi.fn(),
    grupo: vi.fn(),
    plano: vi.fn(),
    salvarGrupo: vi.fn(),
    salvarPlano: vi.fn(),
    excluirGrupo: vi.fn(),
    excluirPlano: vi.fn(),
  };

  beforeEach(async () => {
    vi.resetAllMocks();
    api.listarGrupos.mockResolvedValue({ items: [grupo], page: 1, pageSize: 20, total: 1 });
    api.grupo.mockResolvedValue({ dados: grupo, etag: '"grupo-v1"' });
    // Stub apenas de abertura/fechamento; modalidade e teclado exigem prova no navegador.
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = true; },
    });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', {
      configurable: true, value: function (this: HTMLDialogElement) {
        this.open = false;
        this.dispatchEvent(new Event('close'));
      },
    });
    await TestBed.configureTestingModule({
      imports: [GruposPlanosComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: GruposPlanosService, useValue: api },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    fixture?.destroy();
    document.body.replaceChildren();
    vi.restoreAllMocks();
    for (const [nome, original] of [['showModal', showModalOriginal], ['close', closeOriginal]] as const) {
      if (original) Object.defineProperty(HTMLDialogElement.prototype, nome, original);
      else Reflect.deleteProperty(HTMLDialogElement.prototype, nome);
    }
  });

  async function iniciar(): Promise<void> {
    fixture = TestBed.createComponent(GruposPlanosComponent);
    tela = fixture.nativeElement;
    document.body.appendChild(tela);
    await fixture.whenStable();
  }

  async function estabilizar(): Promise<void> {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    await fixture.whenStable();
  }

  async function clicar(seletor: string): Promise<void> {
    const botao = tela.querySelector<HTMLButtonElement>(seletor)!;
    botao.focus();
    botao.click();
    await estabilizar();
  }

  async function preencher(id: string, valor: string): Promise<void> {
    const campo = tela.querySelector<HTMLInputElement>(`#${id}`)!;
    campo.value = valor;
    campo.dispatchEvent(new Event('input', { bubbles: true }));
    await estabilizar();
  }

  async function enviar(id: string): Promise<void> {
    tela.querySelector(`#${id}`)!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
  }

  it('apresenta estado vazio real nas duas abas, sem controle de demonstração', async () => {
    api.listarGrupos.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 });
    await iniciar();
    expect(tela.textContent).toContain('Nenhum grupo cadastrado');
    expect(tela.textContent).not.toContain('Ver estado vazio');
    await clicar('#aba-planos');
    expect(tela.querySelector('#painel-planos')?.textContent).toContain('Cadastre um grupo');
  });

  it('marca campos inválidos, associa mensagem e foca o resumo sem salvar', async () => {
    await iniciar();
    await clicar('#novo-grupo');
    expect(document.activeElement?.id).toBe('nome-grupo');
    await preencher('nome-grupo', '   ');
    await enviar('formulario-grupo');
    expect(api.salvarGrupo).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erro-editor-grupos');
    const campo = tela.querySelector('#nome-grupo')!;
    expect(campo.getAttribute('aria-invalid')).toBe('true');
    expect(campo.getAttribute('aria-describedby')).toContain('erro-nome-grupo');
    expect(tela.querySelector('#erro-nome-grupo')?.textContent).toContain('Informe o nome');
  });

  it('salva grupo e devolve foco à origem, sem fabricar configuração de plano', async () => {
    await iniciar();
    await clicar('#novo-grupo');
    await preencher('nome-grupo', 'Executivos');
    await enviar('formulario-grupo');
    expect(api.salvarGrupo).toHaveBeenCalledWith('Executivos', null);
    expect(api.salvarPlano).not.toHaveBeenCalled();
    expect(tela.querySelector('dialog')?.open).toBe(false);
    expect(document.activeElement?.id).toBe('novo-grupo');
    expect(tela.textContent).toContain('Grupo salvo.');
  });

  it('edita a versão obtida do grupo e mantém os planos separados', async () => {
    await iniciar();
    const focoOriginal = HTMLElement.prototype.focus;
    const desabilitadoAoFocar: boolean[] = [];
    vi.spyOn(HTMLElement.prototype, 'focus').mockImplementation(function (this: HTMLElement, opcoes?: FocusOptions) {
      if (this.id === 'nome-grupo') desabilitadoAoFocar.push(this.matches(':disabled'));
      focoOriginal.call(this, opcoes);
    });
    await clicar('[aria-label="Editar grupo Compactos"]');
    expect(desabilitadoAoFocar.at(-1)).toBe(false);
    expect(document.activeElement?.id).toBe('nome-grupo');
    await preencher('nome-grupo', 'Compactos urbanos');
    await enviar('formulario-grupo');
    expect(api.salvarGrupo).toHaveBeenCalledWith('Compactos urbanos', { dados: grupo, etag: '"grupo-v1"' });
    expect(api.salvarPlano).not.toHaveBeenCalled();
  });

  it.each([
    ['Diario', ['preco-diario', 'preco-km']],
    ['Controlado', ['preco-diario', 'franquia-diaria', 'preco-excedente']],
    ['Livre', ['preco-diario']],
  ] as const)('mostra somente os campos de %s e rejeita parâmetros ausentes', async (modalidade, ids) => {
    await iniciar();
    const rotulos = { Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre' };
    await clicar(`[aria-label="Configurar plano ${rotulos[modalidade]} de Compactos"]`);
    expect([...tela.querySelectorAll('dialog input')].map((campo) => campo.id)).toEqual([...ids]);
    await enviar('formulario-plano');
    expect(api.salvarPlano).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erro-editor-grupos');
    for (const id of ids) expect(tela.querySelector(`#${id}`)?.getAttribute('aria-invalid')).toBe('true');
  });

  it.each(['Diario', 'Controlado', 'Livre'] as ModalidadePlano[])('envia decimais canônicos e parâmetros exclusivos de %s', async (modalidade) => {
    await iniciar();
    const rotulos = { Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre' };
    await clicar(`[aria-label="Configurar plano ${rotulos[modalidade]} de Compactos"]`);
    await preencher('preco-diario', '00118,123456');
    if (modalidade === 'Diario') await preencher('preco-km', '1,25');
    if (modalidade === 'Controlado') {
      await preencher('franquia-diaria', '100');
      await preencher('preco-excedente', '1,05');
    }
    await enviar('formulario-plano');
    expect(api.salvarPlano).toHaveBeenCalledWith(grupo.id, modalidade, {
      precoDiario: '118.123456',
      precoKm: modalidade === 'Diario' ? '1.25' : null,
      franquiaDiariaKm: modalidade === 'Controlado' ? 100 : null,
      precoKmExcedente: modalidade === 'Controlado' ? '1.05' : null,
    }, null);
  });

  it.each(['-1', '1,0000001', '10000000000000', '1e3'])('rejeita preço inválido %s sem arredondar a entrada', async (valor) => {
    await iniciar();
    await clicar('[aria-label="Configurar plano Livre de Compactos"]');
    await preencher('preco-diario', valor);
    await enviar('formulario-plano');
    expect(api.salvarPlano).not.toHaveBeenCalled();
    expect(tela.querySelector('#erro-preco-diario')?.textContent).toContain('não negativo');
  });

  it('mostra preços BRL e edita plano com a versão e a precisão originais', async () => {
    const plano: Plano = {
      id: 'plano-a', grupoId: grupo.id, modalidade: 'Diario', precoDiario: '118.123456',
      precoKm: '1.250000', franquiaDiariaKm: null, precoKmExcedente: null,
    };
    api.listarGrupos.mockResolvedValue({ items: [{ ...grupo, planos: [plano] }], page: 1, pageSize: 20, total: 1 });
    api.plano.mockResolvedValue({ dados: plano, etag: '"plano-v1"' });
    await iniciar();
    expect(tela.textContent?.replace(/\u00a0/g, ' ')).toContain('R$ 118,12');
    await clicar('[aria-label="Configurar plano Diário de Compactos"]');
    expect(tela.querySelector<HTMLInputElement>('#preco-diario')?.value).toBe('118,123456');
    await enviar('formulario-plano');
    expect(api.salvarPlano).toHaveBeenCalledWith(grupo.id, 'Diario', {
      precoDiario: '118.123456', precoKm: '1.250000', franquiaDiariaKm: null, precoKmExcedente: null,
    }, { dados: plano, etag: '"plano-v1"' });
  });

  it('preserva os dados no conflito de versão e foca o erro de servidor', async () => {
    api.salvarGrupo.mockRejectedValue(new HttpErrorResponse({
      status: 412, error: { title: 'Cadastro alterado. Recarregue antes de tentar novamente.' },
    }));
    await iniciar();
    await clicar('[aria-label="Editar grupo Compactos"]');
    await preencher('nome-grupo', 'Nome pretendido');
    await enviar('formulario-grupo');
    expect(tela.querySelector('dialog')?.open).toBe(true);
    expect(tela.querySelector<HTMLInputElement>('#nome-grupo')?.value).toBe('Nome pretendido');
    expect(document.activeElement?.id).toBe('erro-editor-grupos');
    expect(api.salvarGrupo).toHaveBeenCalledTimes(1);
  });

  it('explica exclusão bloqueada e mantém o grupo na lista', async () => {
    api.excluirGrupo.mockRejectedValue(new HttpErrorResponse({
      status: 409, error: { title: 'O grupo possui planos associados.' },
    }));
    await iniciar();
    await clicar('[aria-label="Excluir grupo Compactos"]');
    expect(tela.textContent).toContain('O grupo possui planos associados.');
    expect(tela.querySelector('article h2')?.textContent).toBe('Compactos');
    expect(document.activeElement?.id).toBe('erro-lista-grupos');
  });

  it('distingue falha de carregamento de lista vazia e permite nova tentativa', async () => {
    api.listarGrupos.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    await iniciar();
    await estabilizar();
    expect(tela.textContent).not.toContain('Nenhum grupo cadastrado');
    expect(document.activeElement?.id).toBe('erro-lista-grupos');
    const tentar = [...tela.querySelectorAll('button')].find((botao) => botao.textContent?.includes('Tentar novamente'))!;
    tentar.click();
    await estabilizar();
    expect(tela.querySelector('article h2')?.textContent).toBe('Compactos');
  });

  it('troca abas por setas e mantém apenas a aba ativa na ordem de tabulação', async () => {
    await iniciar();
    tela.querySelector('#aba-grupos')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    await estabilizar();
    expect(document.activeElement?.id).toBe('aba-planos');
    expect(tela.querySelector('#aba-planos')?.getAttribute('aria-selected')).toBe('true');
    expect(tela.querySelector('#aba-grupos')?.getAttribute('tabindex')).toBe('-1');
  });
});
