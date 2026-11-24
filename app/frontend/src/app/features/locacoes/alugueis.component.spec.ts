import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { AlugueisComponent } from './alugueis.component';
import { AlugueisService, ContratoAluguel, ItemListaAluguel } from './alugueis.service';
import { GruposPlanosService } from '../frota/grupos-planos.service';
import { VeiculosService } from '../frota/veiculos.service';
import { PessoasService } from '../pessoas/pessoas.service';
import { PrecosService } from '../precos/precos.service';

describe('AlugueisComponent', () => {
  let fixture: ComponentFixture<AlugueisComponent>;
  let tela: HTMLElement;
  const originais = ['showModal', 'close'].map((nome) => [nome, Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, nome)] as const);
  const item: ItemListaAluguel = {
    id: '00000000-0000-0000-0000-000000000101',
    estado: 'EmAberto',
    revisao: 1,
    condutorNome: 'Condutor Exemplo',
    condutorCpfResumo: '***.***.***-32',
    veiculoPlaca: 'ABC-1D23',
    planoModalidade: 'Diario',
    saidaUtc: '2026-11-23T13:00:00Z',
    retornoPrevistoUtc: '2026-11-25T13:00:00Z',
    totalConhecido: '254.00',
  };
  const contrato: ContratoAluguel = {
    id: item.id, estado: 'EmAberto', revisao: 1, clienteId: 'c', condutorId: 'd', veiculoId: 'v', planoId: 'p',
    saidaUtc: item.saidaUtc, retornoPrevistoUtc: item.retornoPrevistoUtc, hodometroInicial: 1000, caucao: '1000.00',
    cliente: {
      tipo: 'PF', nome: 'Cliente Original', cpf: '123', cnpj: null, rg: null, cnh: null, telefone: '11999999999',
      email: null, endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua A', numero: '1' },
    },
    condutor: { nome: 'Condutor Exemplo', cpf: '123', cnh: 'cnh', validadeCnh: '2028-01-01', email: 'a@b.invalid', telefone: '11' },
    veiculo: {
      placa: 'ABC-1D23', marca: 'Marca', cor: 'Azul', modelo: 'Modelo', combustivel: 'Gasolina',
      capacidadeLitros: '50.000', ano: 2026, grupoId: 'g', grupoNome: 'Compactos',
    },
    plano: { modalidade: 'Diario', precoDiario: '100.00', precoKm: '2.00', franquiaDiariaKm: null, precoKmExcedente: null },
    itens: [],
    estimativa: {
      versaoRegra: 'v1', calculadoEmUtc: item.saidaUtc, saidaUtc: item.saidaUtc, retornoPrevistoUtc: item.retornoPrevistoUtc,
      diarias: 2, quilometros: null, franquiaTotalKm: null, quilometragemPendente: true, combustivelPendente: true,
      atrasoPendente: true, termoDiariasBruto: '200', termoKmBruto: '0', componentes: [], totalConhecido: '254.00', caucao: '1000.00',
    },
  };
  const api = { listar: vi.fn(), consultar: vi.fn() };
  const frota = { listar: vi.fn(), listarDisponiveis: vi.fn().mockResolvedValue([]) };
  const pessoas = { listarTodosClientes: vi.fn().mockResolvedValue([]), listarCondutoresDoCliente: vi.fn().mockResolvedValue([]) };
  const grupos = {
    listarGrupos: vi.fn(async (page = 1) => ({ items: [], page, pageSize: 20, total: 0 })),
  };
  const precos = {
    listarTodasTaxas: vi.fn().mockResolvedValue([]),
    seguro: vi.fn().mockImplementation((tipo: string) => Promise.resolve({
      dados: { configurado: false, tipo, precoDiario: null, id: null },
      etag: null,
    })),
  };

  beforeEach(async () => {
    vi.resetAllMocks();
    api.listar.mockResolvedValue({ items: [item], page: 1, pageSize: 20, total: 1 });
    api.consultar.mockResolvedValue({ dados: contrato, etag: '"v1"' });
    frota.listar.mockResolvedValue({ items: [{ id: 'v', placa: 'ABC-1D23', modelo: 'Modelo' }], page: 1, pageSize: 20, total: 1 });
    frota.listarDisponiveis.mockResolvedValue([]);
    pessoas.listarTodosClientes.mockResolvedValue([]);
    pessoas.listarCondutoresDoCliente.mockResolvedValue([]);
    grupos.listarGrupos.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 });
    precos.listarTodasTaxas.mockResolvedValue([]);
    precos.seguro.mockImplementation((tipo: string) => Promise.resolve({
      dados: { configurado: false, tipo, precoDiario: null, id: null }, etag: null,
    }));
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = true; },
    });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = false; this.dispatchEvent(new Event('close')); },
    });
    await TestBed.configureTestingModule({
      imports: [AlugueisComponent],
      providers: [
        provideZonelessChangeDetection(), provideRouter([]),
        { provide: AlugueisService, useValue: api }, { provide: VeiculosService, useValue: frota },
        { provide: PessoasService, useValue: pessoas }, { provide: GruposPlanosService, useValue: grupos },
        { provide: PrecosService, useValue: precos },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AlugueisComponent);
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

  it('navega para a rota própria de contratação e carrega o formulário', async () => {
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    await estabilizar();
    tela.querySelector<HTMLButtonElement>('#nova-locacao')!.click();
    expect(navegar).toHaveBeenCalledWith('/alugueis/nova');
    TestBed.inject(ActivatedRoute).snapshot.data = { nova: true };
    await fixture.componentInstance.ngOnInit();
    await estabilizar();
    await estabilizar();
    expect(tela.textContent).not.toContain('Carregando dados para contratação');
    expect(tela.textContent).toContain('Cliente e condutor');
    expect(tela.textContent).toContain('No momento da confirmação');
    tela.querySelector<HTMLButtonElement>('app-contratacao-locacao > button')!.click();
    await estabilizar();
    expect(navegar).toHaveBeenCalledWith('/alugueis');
  });

  it('lista com ações sem estados concluídos ou registrar devolução', async () => {
    await estabilizar();
    expect(tela.textContent).toContain('Condutor Exemplo');
    expect(tela.textContent).toContain('Parcelas conhecidas');
    expect(tela.textContent?.toLowerCase()).not.toContain('conclu');
    expect(tela.querySelector('button')?.textContent).not.toMatch(/devolver|registrar devolução/i);
    expect(tela.querySelector('button[aria-label^="Consultar contrato"]')).toBeTruthy();
    expect(tela.querySelector('button[aria-label^="Editar"]')).toBeTruthy();
  });

  it('rejeita período inválido e aplica filtros', async () => {
    await estabilizar();
    const de = tela.querySelector<HTMLInputElement>('#filtro-saida-de')!;
    const ate = tela.querySelector<HTMLInputElement>('#filtro-saida-ate')!;
    de.value = '2026-11-25';
    de.dispatchEvent(new Event('change', { bubbles: true }));
    ate.value = '2026-11-23';
    ate.dispatchEvent(new Event('change', { bubbles: true }));
    tela.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
    expect(tela.textContent).toContain('O fim do período deve ser igual ou posterior ao início');
    expect(api.listar).toHaveBeenCalledTimes(1);
    de.value = '2026-11-23';
    de.dispatchEvent(new Event('change', { bubbles: true }));
    ate.value = '2026-11-25';
    ate.dispatchEvent(new Event('change', { bubbles: true }));
    tela.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
    expect(api.listar).toHaveBeenCalledTimes(2);
  });

  it('abre consulta lateral com demonstrativo e retorna foco ao fechar', async () => {
    await estabilizar();
    const botao = tela.querySelector<HTMLButtonElement>('button[aria-label^="Consultar contrato"]')!;
    botao.focus();
    botao.click();
    await estabilizar();
    expect(api.consultar).toHaveBeenCalledWith(item.id);
    expect(tela.textContent).toContain('Cliente Original');
    expect(tela.textContent).toContain('Não é o valor final da locação');
    expect(tela.textContent).toContain('caução fixa');
    expect(tela.textContent).toContain('01/01/2028');
    expect(tela.textContent).toContain('50,000 L');
    tela.querySelector<HTMLDialogElement>('dialog')!.dispatchEvent(new Event('close'));
    await estabilizar();
    expect(document.activeElement).toBe(botao);
  });

  it('recupera lista após erro com tentar novamente', async () => {
    await estabilizar();
    api.listar.mockRejectedValueOnce(new Error('falha'));
    tela.querySelector<HTMLButtonElement>('[type="submit"]')!.click();
    await estabilizar();
    expect(tela.textContent).toContain('Não foi possível concluir a operação');
    api.listar.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 });
    [...tela.querySelectorAll('button')].find((b) => b.textContent?.includes('Tentar novamente'))!.click();
    await estabilizar();
    expect(tela.textContent).toContain('Nenhum aluguel cadastrado');
  });
});
