import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { transporteRetornoPrevisto } from '../../shared/horario-sao-paulo';
import { GruposPlanosService } from '../frota/grupos-planos.service';
import { VeiculosService } from '../frota/veiculos.service';
import { PessoasService } from '../pessoas/pessoas.service';
import { PrecosService } from '../precos/precos.service';
import { HttpErrorResponse } from '@angular/common/http';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ContratacaoLocacaoComponent } from './contratacao-locacao.component';
import { AlugueisService } from './alugueis.service';

describe('ContratacaoLocacaoComponent', () => {
  let fixture: ComponentFixture<ContratacaoLocacaoComponent>;
  let tela: HTMLElement;
  const clientePf = { id: 'c-pf', tipo: 'PF' as const, nome: 'Cliente PF', telefone: '11', email: null, cpf: '1', cnpj: null, rg: null, cnh: null };
  const clientePj = { id: 'c-pj', tipo: 'PJ' as const, nome: 'Cliente PJ', telefone: '11', email: null, cpf: null, cnpj: '1', rg: null, cnh: null };
  const condutorPf = {
    id: 'd-pf', clienteId: 'c-pf', clienteNome: 'Cliente PF', nome: 'Próprio condutor',
    email: 'a@b.invalid', cpf: '1', cnh: 'CNH1', validadeCnh: '2028-05-01', telefone: '11',
  };
  const condutorPj = {
    id: 'd-pj', clienteId: 'c-pj', clienteNome: 'Cliente PJ', nome: 'Condutor PJ',
    email: 'a@b.invalid', cpf: '2', cnh: 'CNH2', validadeCnh: '2028-05-01', telefone: '11',
  };
  const veiculo = {
    id: 'v1', placa: 'ABC-1D23', marca: 'Marca', cor: 'Azul', modelo: 'Modelo', combustivel: 'Gasolina' as const,
    capacidadeLitros: '50.000', ano: 2026, grupoId: 'g1',
  };
  const grupo = {
    id: 'g1', nome: 'Compactos', planos: [
      { id: 'p1', grupoId: 'g1', modalidade: 'Diario' as const, precoDiario: '100.00', precoKm: '2.00', franquiaDiariaKm: null, precoKmExcedente: null },
      { id: 'p2', grupoId: 'g1', modalidade: 'Controlado' as const, precoDiario: '80.00', precoKm: null, franquiaDiariaKm: 100, precoKmExcedente: '1.00' },
      { id: 'p3', grupoId: 'g1', modalidade: 'Livre' as const, precoDiario: '120.00', precoKm: null, franquiaDiariaKm: null, precoKmExcedente: null },
    ],
  };
  const pessoas = {
    listarTodosClientes: vi.fn(),
    listarCondutoresDoCliente: vi.fn(),
  };
  const veiculos = { listarDisponiveis: vi.fn() };
  const grupos = { listarGrupos: vi.fn() };
  const precos = {
    listarTodasTaxas: vi.fn(),
    seguro: vi.fn(),
  };
  const estimativa = {
    versaoRegra: 'v1', calculadoEmUtc: '2026-11-23T13:00:00Z', saidaUtc: '2026-11-23T13:00:00Z',
    retornoPrevistoUtc: '2026-11-25T13:00:00Z', diarias: 2, quilometros: null, franquiaTotalKm: null,
    quilometragemPendente: true, combustivelPendente: true, atrasoPendente: true,
    termoDiariasBruto: '200', termoKmBruto: '0', componentes: [
      { categoria: null, origemId: 'p1', tipoSeguro: null, nome: 'Plano Diário', precoUnitario: '100.00', multiplicador: 2, valorBruto: '200', valorArredondado: '200.00' },
    ], totalConhecido: '200.00', caucao: '1000.00',
  };
  const alugueis = {
    estimar: vi.fn(),
    criar: vi.fn(),
    alterar: vi.fn(),
    consultar: vi.fn(),
  };

  beforeEach(async () => {
    vi.resetAllMocks();
    pessoas.listarTodosClientes.mockResolvedValue([clientePf, clientePj]);
    pessoas.listarCondutoresDoCliente.mockImplementation((id: string) => Promise.resolve(id === 'c-pf' ? [condutorPf] : [condutorPj]));
    veiculos.listarDisponiveis.mockResolvedValue([veiculo]);
    grupos.listarGrupos.mockResolvedValue({ items: [grupo], page: 1, pageSize: 20, total: 1 });
    precos.listarTodasTaxas.mockResolvedValue([
      { id: 't1', nome: 'Taxa fixa', preco: '50.00', modalidade: 'Fixa' },
      { id: 't2', nome: 'Taxa diária', preco: '12.00', modalidade: 'Diaria' },
    ]);
    precos.seguro.mockImplementation((tipo: string) => Promise.resolve({
      dados: tipo === 'Cliente'
        ? { id: 's1', tipo: 'Cliente', configurado: true, precoDiario: '20.00' }
        : { id: null, tipo: 'Terceiros', configurado: false, precoDiario: null },
      etag: '"1"',
    }));
    alugueis.estimar.mockResolvedValue({ estimativa });
    alugueis.criar.mockResolvedValue({ dados: { id: 'loc-1', revisao: 1, estimativa }, etag: '"v1"' });
    await TestBed.configureTestingModule({
      imports: [ContratacaoLocacaoComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PessoasService, useValue: pessoas },
        { provide: VeiculosService, useValue: veiculos },
        { provide: GruposPlanosService, useValue: grupos },
        { provide: PrecosService, useValue: precos },
        { provide: AlugueisService, useValue: alugueis },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ContratacaoLocacaoComponent);
    tela = fixture.nativeElement;
    document.body.appendChild(tela);
    fixture.detectChanges();
    await estabilizar();
  });

  afterEach(() => {
    fixture.destroy();
    document.body.replaceChildren();
  });

  async function estabilizar(): Promise<void> {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    await fixture.whenStable();
  }

  async function selecionar(id: string, valor: string): Promise<void> {
    const campo = tela.querySelector<HTMLSelectElement>(`#${id}`)!;
    campo.value = valor;
    campo.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    await estabilizar();
  }

  it('F3-CT-01: apresenta contratação sem campos de F4/F5 e saída na confirmação', async () => {
    expect(tela.textContent).toContain('Nova locação');
    expect(tela.textContent).toContain('No momento da confirmação');
    expect(tela.textContent).toContain('Tanque cheio');
    expect(tela.textContent?.toLowerCase()).not.toMatch(/registrar devolução|funcionário|foto na saída/);
  });

  it('F3-CT-02: condutores dependentes do cliente PF ou PJ', async () => {
    await selecionar('cliente-contratacao', 'c-pf');
    expect(pessoas.listarCondutoresDoCliente).toHaveBeenCalledWith('c-pf');
    expect(tela.querySelector<HTMLSelectElement>('#condutor-contratacao')!.options.length).toBe(2);
    expect(tela.textContent).toContain('Próprio condutor');
    await selecionar('cliente-contratacao', 'c-pj');
    expect(tela.textContent).toContain('Condutor PJ');
  });

  it('F3-CT-02: exibe CNH e validade do condutor em São Paulo', async () => {
    await selecionar('cliente-contratacao', 'c-pf');
    await selecionar('condutor-contratacao', 'd-pf');
    expect(tela.textContent).toContain('CNH CNH1');
    expect(tela.textContent).toContain('01/05/2028');
    expect(tela.textContent).toContain('não há consulta a órgãos oficiais');
  });

  it('F3-CT-02: cliente sem condutor orienta cadastro', async () => {
    pessoas.listarCondutoresDoCliente.mockResolvedValueOnce([]);
    await selecionar('cliente-contratacao', 'c-pf');
    expect(tela.textContent).toContain('não possui condutor ativo');
    expect(tela.textContent).toContain('Clientes e condutores');
    expect(tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
  });

  it('F3-CT-03: sem frota disponível orienta consulta cadastral', async () => {
    veiculos.listarDisponiveis.mockResolvedValueOnce([]);
    fixture = TestBed.createComponent(ContratacaoLocacaoComponent);
    tela = fixture.nativeElement;
    document.body.appendChild(tela);
    fixture.detectChanges();
    await estabilizar();
    expect(tela.textContent).toContain('Não há veículo disponível');
    expect(tela.textContent).toContain('Veículos');
    expect(tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
  });

  it('F3-CT-03: oferece três planos do grupo do veículo', async () => {
    await selecionar('veiculo-contratacao', 'v1');
    expect(tela.querySelectorAll('input[name="plano"]').length).toBe(3);
    expect(tela.textContent).toContain('Diário');
    expect(tela.textContent).toContain('Controlado');
    expect(tela.textContent).toContain('Livre');
  });

  it('F3-CT-03: grupo sem planos orienta configuração', async () => {
    grupos.listarGrupos.mockResolvedValue({ items: [{ ...grupo, planos: [] }], page: 1, pageSize: 20, total: 1 });
    fixture = TestBed.createComponent(ContratacaoLocacaoComponent);
    tela = fixture.nativeElement;
    document.body.appendChild(tela);
    fixture.detectChanges();
    await estabilizar();
    await selecionar('veiculo-contratacao', 'v1');
    expect(tela.textContent).toContain('Não há plano configurado');
    expect(tela.textContent).toContain('Grupos e planos');
  });

  it('F3-CT-04: seguro ausente permanece indisponível sem bloquear demais adicionais', async () => {
    const terceiros = tela.querySelector<HTMLInputElement>('input[disabled]');
    expect(terceiros?.closest('label')?.textContent).toContain('não configurado');
    const taxa = tela.querySelector<HTMLInputElement>('input[type="checkbox"]:not([disabled])')!;
    taxa.click();
    expect(taxa.checked).toBe(true);
  });

  it('rejeita hodômetro inválido e foca resumo ao enviar', async () => {
    await selecionar('cliente-contratacao', 'c-pf');
    await selecionar('condutor-contratacao', 'd-pf');
    await selecionar('veiculo-contratacao', 'v1');
    tela.querySelector<HTMLInputElement>('input[name="plano"][value="p1"]')!.click();
    fixture.detectChanges();
    const hodometro = tela.querySelector<HTMLInputElement>('#hodometro-inicial')!;
    hodometro.value = '12.5';
    hodometro.dispatchEvent(new Event('input', { bubbles: true }));
    hodometro.dispatchEvent(new Event('blur', { bubbles: true }));
    tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await estabilizar();
    expect(tela.textContent).toContain('inteiro não negativo');
  });

  it('F3-CA-06: calcula estimativa remota e habilita confirmação', async () => {
    await selecionar('cliente-contratacao', 'c-pf');
    await selecionar('condutor-contratacao', 'd-pf');
    await selecionar('veiculo-contratacao', 'v1');
    tela.querySelector<HTMLInputElement>('input[name="plano"][value="p1"]')!.click();
    fixture.detectChanges();
    const hodometro = tela.querySelector<HTMLInputElement>('#hodometro-inicial')!;
    hodometro.value = '1000';
    hodometro.dispatchEvent(new Event('input', { bubbles: true }));
    hodometro.dispatchEvent(new Event('blur', { bubbles: true }));
    await estabilizar();
    tela.querySelector<HTMLButtonElement>('#calcular-estimativa')!.click();
    await estabilizar();
    expect(alugueis.estimar).toHaveBeenCalled();
    expect(tela.textContent).toContain('200,00');
    expect(tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
  });

  it('F3-CT-06: resultado incerto exibe verificação e reutiliza chave', async () => {
    await selecionar('cliente-contratacao', 'c-pf');
    await selecionar('condutor-contratacao', 'd-pf');
    await selecionar('veiculo-contratacao', 'v1');
    tela.querySelector<HTMLInputElement>('input[name="plano"][value="p1"]')!.click();
    const hodometro = tela.querySelector<HTMLInputElement>('#hodometro-inicial')!;
    hodometro.value = '1000';
    hodometro.dispatchEvent(new Event('input', { bubbles: true }));
    hodometro.dispatchEvent(new Event('blur', { bubbles: true }));
    await estabilizar();
    tela.querySelector<HTMLButtonElement>('#calcular-estimativa')!.click();
    await estabilizar();
    alugueis.criar.mockRejectedValueOnce(new HttpErrorResponse({ status: 0, statusText: 'Unknown' }));
    tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await estabilizar();
    expect(tela.textContent).toContain('pode ter sido registrada');
    const chave = alugueis.criar.mock.calls[0][1] as string;
    alugueis.criar.mockResolvedValueOnce({ dados: { id: 'loc-1', revisao: 1, estimativa }, etag: '"v1"' });
    tela.querySelector<HTMLButtonElement>('#aviso-incerto-contratacao button')!.click();
    await estabilizar();
    expect(alugueis.criar.mock.calls[1][1]).toBe(chave);
  });

  it('comando usa retorno em São Paulo com offset explícito', async () => {
    const instancia = fixture.componentInstance;
    instancia['modelo'].update((m) => ({
      ...m, retornoPrevistoLocal: '2026-11-25T10:00', hodometro: '1000',
      clienteId: 'c', condutorId: 'd', veiculoId: 'v', planoId: 'p',
    }));
    const comando = instancia['comandoTransporte']();
    expect(comando?.['retornoPrevisto']).toMatch(/^2026-11-25T10:00:00[+-]/);
    expect(transporteRetornoPrevisto('2026-11-25T10:00')).toBe(comando?.['retornoPrevisto']);
  });

  async function prepararConfirmacao(): Promise<void> {
    await selecionar('cliente-contratacao', 'c-pf');
    await selecionar('condutor-contratacao', 'd-pf');
    await selecionar('veiculo-contratacao', 'v1');
    tela.querySelector<HTMLInputElement>('input[name="plano"][value="p1"]')!.click();
    const campo = tela.querySelector<HTMLInputElement>('#hodometro-inicial')!;
    campo.value = '1000';
    campo.dispatchEvent(new Event('input', { bubbles: true }));
    await estabilizar();
    tela.querySelector<HTMLButtonElement>('#calcular-estimativa')!.click();
    await estabilizar();
  }

  it('F3-CA-06: resposta de cálculo obsoleta não habilita confirmação', async () => {
    await prepararConfirmacao();
    let resolver!: (valor: unknown) => void;
    alugueis.estimar.mockImplementationOnce(() => new Promise((resolve) => { resolver = resolve; }));
    tela.querySelector<HTMLButtonElement>('#calcular-estimativa')!.click();
    await Promise.resolve();
    fixture.componentInstance['invalidarEstimativa']();
    resolver({ estimativa });
    await estabilizar();
    expect(fixture.componentInstance['estimativa']()).toBeNull();
    expect(tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
  });

  it('F3-CT-06/11: replay 401/403/409 não descarta intenção nem libera formulário', async () => {
    await prepararConfirmacao();
    alugueis.criar.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await estabilizar();
    const comando = alugueis.criar.mock.calls[0][0];
    const chave = alugueis.criar.mock.calls[0][1];
    for (const status of [401, 403, 409]) {
      alugueis.criar.mockRejectedValueOnce(new HttpErrorResponse({ status }));
      await fixture.componentInstance['verificarContratacao']();
      await estabilizar();
      expect(fixture.componentInstance.navegacaoBloqueada()).toBe(true);
      expect(tela.querySelector<HTMLFieldSetElement>('form > fieldset')!.disabled).toBe(true);
      expect(alugueis.criar.mock.lastCall).toEqual([comando, chave]);
    }
    expect(tela.textContent).toContain('Reautenticar');
  });

  it('F3-CT-06: reautenticação de outro ator não reenvia comando original', async () => {
    await prepararConfirmacao();
    alugueis.criar.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await estabilizar();
    const { SessaoService } = await import('../../core/sessao.service');
    TestBed.inject(SessaoService).memoria.contaId.set('outro-ator');
    await fixture.componentInstance['verificarContratacao']();
    expect(alugueis.criar).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.navegacaoBloqueada()).toBe(true);
  });

  it('F3-CT-09: conflito bloqueia salvar e calcular até recarga explícita', async () => {
    await prepararConfirmacao();
    fixture.componentRef.setInput('contratoEdicao', { dados: { id: 'loc-1' }, etag: '"antiga"' });
    alugueis.alterar.mockRejectedValueOnce(new HttpErrorResponse({ status: 412 }));
    await fixture.componentInstance['confirmar']();
    await estabilizar();
    expect(tela.textContent).toContain('Recarregar contrato');
    expect(tela.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
    expect(tela.querySelector<HTMLButtonElement>('#calcular-estimativa')!.disabled).toBe(true);
    expect(alugueis.alterar).toHaveBeenCalledTimes(1);
  });
});
