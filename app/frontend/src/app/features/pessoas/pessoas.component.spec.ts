import { HttpErrorResponse } from '@angular/common/http';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PessoasComponent } from './pessoas.component';
import { ClienteDetalhe, Condutor, PessoasService, ResumoCliente, Vinculo } from './pessoas.service';

describe('PessoasComponent', () => {
  let fixture: ComponentFixture<PessoasComponent>;
  let tela: HTMLElement;
  const originais = ['showModal', 'close'].map((nome) => [nome, Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, nome)] as const);

  const clientePf: ResumoCliente = {
    id: 'pf-1', tipo: 'PF', nome: 'Lia Monteiro', telefone: '(11) 90000-1842', email: 'lia@example.invalid',
    cpf: '52998224725', cnpj: null, rg: 'RG-1', cnh: 'CNH-418',
  };
  const clientePj: ResumoCliente = {
    id: 'pj-1', tipo: 'PJ', nome: 'Horizonte Azul Ltda.', telefone: '(21) 90000-2764', email: null,
    cpf: null, cnpj: '11222333000181', rg: null, cnh: null,
  };
  const detalhePf: ClienteDetalhe = {
    id: 'pf-1', tipo: 'PF', nome: 'Lia Monteiro', telefone: '(11) 90000-1842', email: 'lia@example.invalid',
    cpf: '52998224725', rg: 'RG-1', cnh: 'CNH-418',
    endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua A', numero: '10' },
  };
  const detalhePj: ClienteDetalhe = {
    id: 'pj-1', tipo: 'PJ', nome: 'Horizonte Azul Ltda.', telefone: '(21) 90000-2764', email: null,
    cnpj: '11222333000181',
    endereco: { estado: 'RJ', cidade: 'Rio de Janeiro', bairro: 'Centro', rua: 'Rua B', numero: '20' },
  };
  const condutor: Condutor = {
    id: 'cond-1', clienteId: 'pf-1', clienteNome: 'Lia Monteiro', nome: 'Lia Monteiro', email: 'lia@example.invalid',
    cpf: '52998224725', cnh: 'CNH-418', validadeCnh: '2028-09-18', telefone: '(11) 90000-1842',
  };
  const vinculo: Vinculo = {
    id: 'vinculo-1', clientePfId: 'pf-1', clientePfNome: 'Lia Monteiro', clientePjId: 'pj-1', clientePjNome: 'Horizonte Azul Ltda.',
  };

  const api = {
    listarClientes: vi.fn(), listarCondutores: vi.fn(), listarTodosClientes: vi.fn(),
    consultarCliente: vi.fn(), salvarCliente: vi.fn(), excluirCliente: vi.fn(),
    consultarCondutor: vi.fn(), salvarCondutor: vi.fn(), excluirCondutor: vi.fn(),
    listarVinculos: vi.fn(), consultarVinculo: vi.fn(), vincular: vi.fn(), desvincular: vi.fn(),
  };
  const pagina = <T>(items: T[]) => ({ items, page: 1, pageSize: 20, total: items.length });

  beforeEach(async () => {
    vi.resetAllMocks();
    api.listarClientes.mockResolvedValue(pagina([clientePf, clientePj]));
    api.listarCondutores.mockResolvedValue(pagina([condutor]));
    api.listarTodosClientes.mockResolvedValue([clientePf, clientePj]);
    api.consultarCliente.mockImplementation((id: string) => Promise.resolve({ dados: id === 'pj-1' ? detalhePj : detalhePf, etag: '"v1"' }));
    api.consultarCondutor.mockResolvedValue({ dados: condutor, etag: '"v1"' });
    api.listarVinculos.mockResolvedValue([]);
    api.consultarVinculo.mockResolvedValue({ dados: vinculo, etag: '"v1"' });
    api.salvarCliente.mockResolvedValue(undefined);
    api.salvarCondutor.mockResolvedValue(undefined);
    api.vincular.mockResolvedValue(undefined);
    api.desvincular.mockResolvedValue(undefined);
    api.excluirCliente.mockResolvedValue(undefined);
    api.excluirCondutor.mockResolvedValue(undefined);
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = true; },
    });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = false; this.dispatchEvent(new Event('close')); },
    });
    await TestBed.configureTestingModule({
      imports: [PessoasComponent],
      providers: [provideZonelessChangeDetection(), provideRouter([]), { provide: PessoasService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(PessoasComponent);
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

  async function marcar(id: string, marcado: boolean): Promise<void> {
    const elemento = tela.querySelector<HTMLInputElement>(`#${id}`)!;
    elemento.checked = marcado;
    elemento.dispatchEvent(new Event('change', { bubbles: true }));
    await estabilizar();
  }

  async function enviar(): Promise<void> {
    tela.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await estabilizar();
  }

  async function preencherEndereco(): Promise<void> {
    await preencher('estado-cliente', 'SP');
    await preencher('cidade-cliente', 'São Paulo');
    await preencher('bairro-cliente', 'Centro');
    await preencher('rua-cliente', 'Rua A');
    await preencher('numero-cliente', '10');
  }

  it('lista clientes e condutores com documentos mascarados e sem seletor de empresa', () => {
    const clientes = tela.querySelector('#painel-clientes tbody')?.textContent ?? '';
    expect(clientes).toContain('Lia Monteiro');
    expect(clientes).toContain('Horizonte Azul Ltda.');
    expect(clientes).toContain('***.982.***-**');
    expect(clientes).toContain('**.***.333/****-**');
    expect(clientes).not.toContain('52998224725');
    expect(clientes).not.toContain('11222333000181');
    expect(clientes).toContain('Sem e-mail');
    const condutores = tela.querySelector('#painel-condutores tbody')?.textContent ?? '';
    expect(condutores).toContain('*******418');
    expect(condutores).toContain('18/09/2028');
    expect(tela.querySelector('[id*="empresa" i], [name*="empresa" i]')).toBeNull();
  });

  it('alterna PF e PJ ocultando os documentos exclusivos de cada tipo', async () => {
    await clicar('#novo-registro');
    expect(tela.querySelector('label[for="documento-cliente"]')?.textContent).toContain('CPF');
    expect(tela.querySelector('#rg-cliente')).not.toBeNull();
    expect(tela.querySelector('#cnh-cliente')).not.toBeNull();
    await clicar('button[aria-pressed="false"]');
    expect(tela.querySelector('label[for="documento-cliente"]')?.textContent).toContain('CNPJ');
    expect(tela.querySelector('#rg-cliente')).toBeNull();
    expect(tela.querySelector('#cnh-cliente')).toBeNull();
  });

  it('cria cliente PF sem e-mail e envia o payload discriminado', async () => {
    await clicar('#novo-registro');
    await preencher('nome-cliente', 'Ravi Campos');
    await preencher('documento-cliente', '111.444.777-35');
    await preencher('rg-cliente', 'RG-2');
    await preencher('cnh-cliente', 'CNH-731');
    await preencher('telefone-cliente', '(31) 90000-3191');
    await preencherEndereco();
    await enviar();
    expect(api.salvarCliente).toHaveBeenCalledWith({
      tipo: 'PF', nome: 'Ravi Campos', cpf: '111.444.777-35', rg: 'RG-2', cnh: 'CNH-731',
      telefone: '(31) 90000-3191', email: null,
      endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua A', numero: '10' },
    }, null);
    expect(tela.querySelector('dialog')?.open).toBe(false);
  });

  it('cria cliente PJ válido e não envia campos exclusivos de PF', async () => {
    await clicar('#novo-registro');
    await clicar('button[aria-pressed="false"]');
    await preencher('nome-cliente', 'Horizonte Azul Ltda.');
    await preencher('documento-cliente', '11.222.333/0001-81');
    await preencher('telefone-cliente', '(21) 90000-2764');
    await preencher('email-cliente', 'contato@horizonte-azul.example.invalid');
    await preencherEndereco();
    await enviar();
    expect(api.salvarCliente).toHaveBeenCalledWith({
      tipo: 'PJ', nome: 'Horizonte Azul Ltda.', cnpj: '11.222.333/0001-81',
      telefone: '(21) 90000-2764', email: 'contato@horizonte-azul.example.invalid',
      endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua A', numero: '10' },
    }, null);
  });

  it('associa erros obrigatórios e move o foco para o resumo sem salvar', async () => {
    await clicar('#novo-registro');
    await enviar();
    expect(api.salvarCliente).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erro-editor-pessoas');
    expect(tela.querySelector('#nome-cliente')?.getAttribute('aria-invalid')).toBe('true');
    expect(tela.querySelector('#documento-cliente')?.getAttribute('aria-describedby')).toContain('erro-documento-cliente');
    expect(tela.querySelector('#rg-cliente')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('associa a duplicidade de CPF ao campo de documento sem fechar o formulário', async () => {
    api.salvarCliente.mockRejectedValue(new HttpErrorResponse({
      status: 409,
      error: {
        title: 'A operação possui um conflito', code: 'cadastro_duplicado',
        errors: { cpf: ['Já existe um cadastro incompatível com este valor.'] },
      },
    }));
    await clicar('#novo-registro');
    await preencher('nome-cliente', 'Ravi Campos');
    await preencher('documento-cliente', '11144477735');
    await preencher('rg-cliente', 'RG-2');
    await preencher('cnh-cliente', 'CNH-731');
    await preencher('telefone-cliente', '(31) 90000-3191');
    await preencherEndereco();
    await enviar();
    expect(tela.querySelector('dialog')?.open).toBe(true);
    expect(document.activeElement?.id).toBe('erro-editor-pessoas');
    expect(tela.querySelector('#erro-documento-cliente')?.textContent).toContain('Já existe um cadastro');
  });

  it('edita cliente com ETag e mostra o documento completo apenas no formulário', async () => {
    await clicar('[aria-label="Editar Lia Monteiro"]');
    expect(tela.querySelector('dialog')?.open).toBe(true);
    expect(document.activeElement?.id).toBe('nome-cliente');
    expect(tela.querySelector<HTMLInputElement>('#documento-cliente')?.value).toBe('52998224725');
    await preencher('nome-cliente', 'Lia Monteiro Silva');
    await enviar();
    expect(api.salvarCliente).toHaveBeenCalledWith({
      tipo: 'PF', nome: 'Lia Monteiro Silva', cpf: '52998224725', rg: 'RG-1', cnh: 'CNH-418',
      telefone: '(11) 90000-1842', email: 'lia@example.invalid',
      endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua A', numero: '10' },
    }, { dados: detalhePf, etag: '"v1"' });
  });

  it('preenche o próprio condutor a partir do cliente PF e rejeita CPF incoerente', async () => {
    await clicar('#aba-condutores');
    await clicar('#novo-registro');
    await preencher('cliente-condutor', 'pf-1');
    await marcar('proprio-condutor', true);
    expect(tela.querySelector<HTMLInputElement>('#nome-condutor')?.value).toBe('Lia Monteiro');
    expect(tela.querySelector<HTMLInputElement>('#cpf-condutor')?.value).toBe('52998224725');
    expect(tela.querySelector<HTMLInputElement>('#cnh-condutor')?.value).toBe('CNH-418');
    await preencher('validade-condutor', '2028-09-18');
    await preencher('cpf-condutor', '111.444.777-35');
    await enviar();
    expect(api.salvarCondutor).not.toHaveBeenCalled();
    expect(tela.querySelector('#erro-cpf-condutor')?.textContent).toContain('mesmo do cliente PF');
  });

  it('cria condutor para cliente PJ com e-mail e validade informados', async () => {
    await clicar('#aba-condutores');
    await clicar('#novo-registro');
    await preencher('cliente-condutor', 'pj-1');
    await preencher('nome-condutor', 'Noah Duarte');
    await preencher('email-condutor', 'noah@example.invalid');
    await preencher('cpf-condutor', '11144477735');
    await preencher('telefone-condutor', '(21) 90000-5073');
    await preencher('cnh-condutor', 'CNH-073');
    await preencher('validade-condutor', '2029-05-07');
    await enviar();
    expect(api.salvarCondutor).toHaveBeenCalledWith({
      clienteId: 'pj-1', nome: 'Noah Duarte', email: 'noah@example.invalid', cpf: '11144477735',
      cnh: 'CNH-073', validadeCnh: '2029-05-07', telefone: '(21) 90000-5073', proprioCliente: false,
    }, null);
  });

  it('edita condutor com ETag preservando o próprio condutor', async () => {
    await clicar('#aba-condutores');
    await clicar('[aria-label="Editar condutor Lia Monteiro"]');
    expect(tela.querySelector<HTMLSelectElement>('#cliente-condutor')?.value).toBe('pf-1');
    expect(tela.querySelector<HTMLInputElement>('#proprio-condutor')?.checked).toBe(true);
    expect(tela.querySelector<HTMLInputElement>('#cpf-condutor')?.value).toBe('52998224725');
    await preencher('telefone-condutor', '(11) 90000-9999');
    await enviar();
    expect(api.salvarCondutor).toHaveBeenCalledWith({
      clienteId: 'pf-1', nome: 'Lia Monteiro', email: 'lia@example.invalid', cpf: '52998224725',
      cnh: 'CNH-418', validadeCnh: '2028-09-18', telefone: '(11) 90000-9999', proprioCliente: true,
    }, { dados: condutor, etag: '"v1"' });
  });

  it('associa CPF duplicado de condutor ao campo sem fechar o formulário', async () => {
    api.salvarCondutor.mockRejectedValue(new HttpErrorResponse({
      status: 409,
      error: {
        title: 'A operação possui um conflito', code: 'cadastro_duplicado',
        errors: { cpf: ['Já existe um condutor ativo com este CPF.'] },
      },
    }));
    await clicar('#aba-condutores');
    await clicar('#novo-registro');
    await preencher('cliente-condutor', 'pj-1');
    await preencher('nome-condutor', 'Noah Duarte');
    await preencher('email-condutor', 'noah@example.invalid');
    await preencher('cpf-condutor', '11144477735');
    await preencher('telefone-condutor', '(21) 90000-5073');
    await preencher('cnh-condutor', 'CNH-073');
    await preencher('validade-condutor', '2029-05-07');
    await enviar();
    expect(tela.querySelector('dialog')?.open).toBe(true);
    expect(document.activeElement?.id).toBe('erro-editor-pessoas');
    expect(tela.querySelector('#erro-cpf-condutor')?.textContent).toContain('Já existe um condutor ativo');
  });

  it('adiciona e remove vínculo PF–PJ', async () => {
    api.listarVinculos.mockResolvedValueOnce([]).mockResolvedValue([vinculo]);
    await clicar('#aba-vinculos');
    expect(tela.querySelector('#vinculo-pj')).not.toBeNull();
    expect(tela.querySelector<HTMLSelectElement>('#vinculo-pf')?.value).toBe('pf-1');
    await clicar('#adicionar-vinculo');
    expect(api.vincular).toHaveBeenCalledWith('pj-1', 'pf-1');
    expect(tela.querySelector('#painel-vinculos')?.textContent).toContain('Lia Monteiro');
    await clicar('[aria-label="Remover vínculo de Lia Monteiro"]');
    expect(api.desvincular).toHaveBeenCalledWith('pj-1', 'vinculo-1');
  });

  it('mantém o cliente e explica o bloqueio quando existe condutor ativo', async () => {
    api.excluirCliente.mockRejectedValue(new HttpErrorResponse({
      status: 409,
      error: { title: 'Exclua ou transfira os condutores ativos antes de excluir o cliente', code: 'cliente_com_condutores_ativos' },
    }));
    await clicar('[aria-label="Excluir Lia Monteiro"]');
    expect(api.excluirCliente).toHaveBeenCalledWith('pf-1');
    expect(document.activeElement?.id).toBe('erro-acao-pessoas');
    expect(tela.querySelector('#erro-acao-pessoas')?.textContent).toContain('possui condutor ativo');
    expect(tela.querySelector('#painel-clientes tbody')?.textContent).toContain('Lia Monteiro');
  });

  it('navega pelas abas com as setas do teclado', async () => {
    const aba = tela.querySelector<HTMLButtonElement>('#aba-clientes')!;
    aba.focus();
    aba.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    await estabilizar();
    expect(document.activeElement?.id).toBe('aba-condutores');
    expect(tela.querySelector('#aba-condutores')?.getAttribute('aria-selected')).toBe('true');
    expect(tela.querySelector('#aba-clientes')?.getAttribute('aria-selected')).toBe('false');
  });
});
