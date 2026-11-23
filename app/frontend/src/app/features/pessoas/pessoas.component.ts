import { Component, computed, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { form, FormField, FormRoot, maxLength, required, validate } from '@angular/forms/signals';
import { ShellCadastralComponent } from '../../shared/shell-cadastral.component';
import { CampoTextoComponent } from '../../shared/campo-texto.component';
import { problemaCadastro, ProblemaCadastro } from '../../shared/erro-cadastro';
import { conterTabulacao, focarResumo } from '../../shared/foco';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { Pagina, Recurso } from '../frota/grupos-planos.service';
import {
  ClienteDetalhe, Condutor, DadosCliente, DadosCondutor, PessoasService, ResumoCliente, TipoCliente, Vinculo,
} from './pessoas.service';

type Aba = 'clientes' | 'condutores' | 'vinculos';

interface ModeloCliente {
  tipo: TipoCliente;
  nome: string;
  documento: string;
  rg: string;
  cnh: string;
  telefone: string;
  email: string;
  estado: string;
  cidade: string;
  bairro: string;
  rua: string;
  numero: string;
}

interface ModeloCondutor {
  clienteId: string;
  proprioCliente: boolean;
  nome: string;
  email: string;
  cpf: string;
  cnh: string;
  validadeCnh: string;
  telefone: string;
}

const clienteVazio = (): ModeloCliente => ({
  tipo: 'PF', nome: '', documento: '', rg: '', cnh: '', telefone: '', email: '',
  estado: '', cidade: '', bairro: '', rua: '', numero: '',
});

const condutorVazio = (): ModeloCondutor => ({
  clienteId: '', proprioCliente: false, nome: '', email: '', cpf: '', cnh: '', validadeCnh: '', telefone: '',
});

@Component({
  selector: 'app-pessoas',
  imports: [FormRoot, FormField, ShellCadastralComponent, CampoTextoComponent, ResumoErroComponent],
  templateUrl: './pessoas.component.html',
})
export class PessoasComponent implements OnInit {
  private readonly api = inject(PessoasService);
  private readonly editor = viewChild<ElementRef<HTMLDialogElement>>('editor');
  private clienteAtual: Recurso<ClienteDetalhe> | null = null;
  private condutorAtual: Recurso<Condutor> | null = null;
  private origemFoco: HTMLElement | null = null;
  private abertura = 0;
  private consultaClientes = 0;
  private consultaCondutores = 0;

  protected readonly aba = signal<Aba>('clientes');
  protected readonly clientes = signal<Pagina<ResumoCliente>>({ items: [], page: 1, pageSize: 20, total: 0 });
  protected readonly condutores = signal<Pagina<Condutor>>({ items: [], page: 1, pageSize: 20, total: 0 });
  protected readonly todosClientes = signal<ResumoCliente[]>([]);
  protected readonly carregandoClientes = signal(true);
  protected readonly carregandoCondutores = signal(true);
  protected readonly erroLista = signal<string | null>(null);
  protected readonly erroAcao = signal<string | null>(null);
  protected readonly aviso = signal<string | null>(null);
  protected readonly editorTipo = signal<'cliente' | 'condutor'>('cliente');
  protected readonly tituloEditor = signal('Novo cliente');
  protected readonly erroEditor = signal<string | null>(null);
  protected readonly editorDisponivel = signal(false);
  protected readonly carregandoEditor = signal(false);
  protected readonly pjVinculo = signal('');
  protected readonly pfVinculo = signal('');
  protected readonly vinculos = signal<Vinculo[]>([]);
  protected readonly carregandoVinculos = signal(false);
  protected readonly erroVinculos = signal<string | null>(null);

  protected readonly modeloCliente = signal<ModeloCliente>(clienteVazio());
  protected readonly modeloCondutor = signal<ModeloCondutor>(condutorVazio());

  protected readonly clientesPf = computed(() => this.todosClientes().filter((cliente) => cliente.tipo === 'PF'));
  protected readonly clientesPj = computed(() => this.todosClientes().filter((cliente) => cliente.tipo === 'PJ'));
  protected readonly rotuloNovo = computed(() => this.aba() === 'condutores' ? '＋ Novo condutor' : '＋ Novo cliente');
  protected readonly clienteSelecionado = computed(() =>
    this.todosClientes().find((cliente) => cliente.id === this.modeloCondutor().clienteId));
  protected readonly pjSelecionada = computed(() => this.clientesPj().find((cliente) => cliente.id === this.pjVinculo()));

  protected readonly formularioCliente = form(this.modeloCliente, (caminho) => {
    required(caminho.nome, { message: 'Informe o nome.' });
    maxLength(caminho.nome, 200, { message: 'O nome pode ter até 200 caracteres.' });
    validate(caminho.nome, ({ value }) => value().trim() ? null : { kind: 'nome', message: 'Informe o nome.' });

    maxLength(caminho.documento, 64, { message: 'O documento pode ter até 64 caracteres.' });
    validate(caminho.documento, ({ value }) => {
      const tipo = this.modeloCliente().tipo;
      const texto = value().trim();
      if (!texto) return { kind: 'obrigatorio', message: tipo === 'PF' ? 'Informe o CPF.' : 'Informe o CNPJ.' };
      return tipo === 'PF'
        ? cpfValido(texto) ? null : { kind: 'cpf', message: 'Informe um CPF válido.' }
        : cnpjValido(texto) ? null : { kind: 'cnpj', message: 'Informe um CNPJ válido.' };
    });

    required(caminho.rg, { message: 'Informe o RG.', when: () => this.modeloCliente().tipo === 'PF' });
    maxLength(caminho.rg, 64, { message: 'O RG pode ter até 64 caracteres.' });
    required(caminho.cnh, { message: 'Informe a CNH.', when: () => this.modeloCliente().tipo === 'PF' });
    maxLength(caminho.cnh, 64, { message: 'A CNH pode ter até 64 caracteres.' });

    required(caminho.telefone, { message: 'Informe o telefone.' });
    maxLength(caminho.telefone, 32, { message: 'O telefone pode ter até 32 caracteres.' });

    maxLength(caminho.email, 254, { message: 'O e-mail pode ter até 254 caracteres.' });
    validate(caminho.email, ({ value }) => !value().trim() || emailValido(value())
      ? null : { kind: 'email', message: 'Informe um e-mail válido.' });

    for (const [campo, rotulo, maximo] of [
      [caminho.estado, 'o estado', 100],
      [caminho.cidade, 'a cidade', 100],
      [caminho.bairro, 'o bairro', 100],
      [caminho.rua, 'a rua', 200],
      [caminho.numero, 'o número', 200],
    ] as const) {
      required(campo, { message: `Informe ${rotulo}.` });
      maxLength(campo, maximo, { message: `Este campo pode ter até ${maximo} caracteres.` });
    }
  }, {
    submission: {
      onInvalid: () => this.mostrarErroEditor('Revise os dados informados antes de salvar.'),
      action: async (campos) => {
        if (!this.editorDisponivel()) return { kind: 'carregando', message: 'Aguarde os dados do cadastro.' };
        const modelo = this.modeloCliente();
        const endereco = {
          estado: modelo.estado, cidade: modelo.cidade, bairro: modelo.bairro, rua: modelo.rua, numero: modelo.numero,
        };
        const dados: DadosCliente = modelo.tipo === 'PF'
          ? {
            tipo: 'PF', nome: modelo.nome, cpf: modelo.documento, rg: modelo.rg, cnh: modelo.cnh,
            telefone: modelo.telefone, email: modelo.email.trim() || null, endereco,
          }
          : {
            tipo: 'PJ', nome: modelo.nome, cnpj: modelo.documento,
            telefone: modelo.telefone, email: modelo.email.trim() || null, endereco,
          };
        try {
          await this.api.salvarCliente(dados, this.clienteAtual);
          this.editor()?.nativeElement.close();
          this.aviso.set('Cliente salvo.');
          await this.recarregarClientes();
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          const mensagem = mensagemConflito(problema);
          await this.mostrarErroEditor(problema.errors?.['tipo']?.[0] ?? mensagem);
          const mapa: Record<string, keyof ModeloCliente> = {
            nome: 'nome', cpf: 'documento', cnpj: 'documento', documento: 'documento', rg: 'rg', cnh: 'cnh',
            telefone: 'telefone', email: 'email', estado: 'estado', cidade: 'cidade', bairro: 'bairro',
            rua: 'rua', numero: 'numero', endereco: 'estado',
          };
          const erros = Object.entries(problema.errors ?? {}).filter(([campo]) => campo !== 'tipo');
          return erros.length ? erros.map(([campo, mensagens]) => ({
            kind: 'servidor', message: mensagens[0], fieldTree: campos[mapa[campo] ?? 'nome'],
          })) : { kind: 'servidor', message: mensagem };
        }
      },
    },
  });

  protected readonly formularioCondutor = form(this.modeloCondutor, (caminho) => {
    required(caminho.clienteId, { message: 'Selecione o cliente vinculado.' });
    validate(caminho.clienteId, ({ value }) => {
      if (!value()) return { kind: 'obrigatorio', message: 'Selecione o cliente vinculado.' };
      const cliente = this.clienteSelecionado();
      if (!cliente) return { kind: 'cliente', message: 'Selecione um cliente ativo da empresa.' };
      if (this.modeloCondutor().proprioCliente && cliente.tipo !== 'PF') {
        return { kind: 'proprio', message: 'O próprio condutor exige um cliente pessoa física.' };
      }
      return null;
    });

    required(caminho.nome, { message: 'Informe o nome do condutor.' });
    maxLength(caminho.nome, 200, { message: 'O nome pode ter até 200 caracteres.' });
    validate(caminho.nome, ({ value }) => value().trim() ? null : { kind: 'nome', message: 'Informe o nome do condutor.' });

    required(caminho.email, { message: 'Informe o e-mail.' });
    maxLength(caminho.email, 254, { message: 'O e-mail pode ter até 254 caracteres.' });
    validate(caminho.email, ({ value }) => !value().trim() || emailValido(value())
      ? null : { kind: 'email', message: 'Informe um e-mail válido.' });

    required(caminho.cpf, { message: 'Informe o CPF.' });
    validate(caminho.cpf, ({ value }) => {
      const texto = value().trim();
      if (!texto) return { kind: 'obrigatorio', message: 'Informe o CPF.' };
      if (!cpfValido(texto)) return { kind: 'cpf', message: 'Informe um CPF válido.' };
      const cliente = this.clienteSelecionado();
      if (this.modeloCondutor().proprioCliente && cliente?.tipo === 'PF'
        && normalizarDocumento(texto) !== normalizarDocumento(cliente.cpf ?? '')) {
        return { kind: 'proprio', message: 'O CPF deve ser o mesmo do cliente PF selecionado.' };
      }
      return null;
    });

    required(caminho.cnh, { message: 'Informe a CNH.' });
    maxLength(caminho.cnh, 64, { message: 'A CNH pode ter até 64 caracteres.' });
    validate(caminho.cnh, ({ value }) => {
      const texto = value().trim();
      if (!texto) return { kind: 'obrigatorio', message: 'Informe a CNH.' };
      const cliente = this.clienteSelecionado();
      if (cliente?.tipo === 'PF'
        && normalizarDocumento(this.modeloCondutor().cpf) === normalizarDocumento(cliente.cpf ?? '')
        && normalizarCnh(texto) !== normalizarCnh(cliente.cnh ?? '')) {
        return { kind: 'coerencia', message: 'A CNH deve ser coerente com a do cliente PF.' };
      }
      return null;
    });

    required(caminho.validadeCnh, { message: 'Informe a validade da CNH.' });
    validate(caminho.validadeCnh, ({ value }) => !value() || dataValida(value())
      ? null : { kind: 'data', message: 'Informe uma data válida.' });

    required(caminho.telefone, { message: 'Informe o telefone.' });
    maxLength(caminho.telefone, 32, { message: 'O telefone pode ter até 32 caracteres.' });
  }, {
    submission: {
      onInvalid: () => this.mostrarErroEditor('Revise os dados informados antes de salvar.'),
      action: async (campos) => {
        if (!this.editorDisponivel()) return { kind: 'carregando', message: 'Aguarde os dados do cadastro.' };
        const modelo = this.modeloCondutor();
        const dados: DadosCondutor = {
          clienteId: modelo.clienteId, nome: modelo.nome, email: modelo.email, cpf: modelo.cpf, cnh: modelo.cnh,
          validadeCnh: modelo.validadeCnh, telefone: modelo.telefone, proprioCliente: modelo.proprioCliente,
        };
        try {
          await this.api.salvarCondutor(dados, this.condutorAtual);
          this.editor()?.nativeElement.close();
          this.aviso.set('Condutor salvo.');
          await this.carregarCondutores(this.condutores().page);
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          const mensagem = mensagemConflito(problema);
          await this.mostrarErroEditor(mensagem);
          const mapa: Record<string, keyof ModeloCondutor> = {
            clienteId: 'clienteId', nome: 'nome', email: 'email', cpf: 'cpf', cnh: 'cnh',
            validadeCnh: 'validadeCnh', telefone: 'telefone',
          };
          const erros = Object.entries(problema.errors ?? {});
          return erros.length ? erros.map(([campo, mensagens]) => ({
            kind: 'servidor', message: mensagens[0], fieldTree: campos[mapa[campo] ?? 'nome'],
          })) : { kind: 'servidor', message: mensagem };
        }
      },
    },
  });

  protected readonly salvando = computed(() =>
    this.formularioCliente().submitting() || this.formularioCondutor().submitting());

  async ngOnInit(): Promise<void> {
    await Promise.all([this.carregarClientes(), this.carregarCondutores(), this.carregarTodosClientes()]);
    await this.garantirSelecoesVinculo();
  }

  protected async carregarClientes(page = 1): Promise<void> {
    const consulta = ++this.consultaClientes;
    this.carregandoClientes.set(true);
    this.erroLista.set(null);
    try {
      const pagina = await this.api.listarClientes(page);
      if (consulta !== this.consultaClientes) return;
      this.clientes.set(pagina);
    } catch (erro) {
      if (consulta !== this.consultaClientes) return;
      this.erroLista.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-lista-pessoas');
    } finally {
      if (consulta === this.consultaClientes) this.carregandoClientes.set(false);
    }
  }

  protected async carregarCondutores(page = 1): Promise<void> {
    const consulta = ++this.consultaCondutores;
    this.carregandoCondutores.set(true);
    this.erroLista.set(null);
    try {
      const pagina = await this.api.listarCondutores(page);
      if (consulta !== this.consultaCondutores) return;
      this.condutores.set(pagina);
    } catch (erro) {
      if (consulta !== this.consultaCondutores) return;
      this.erroLista.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-lista-pessoas');
    } finally {
      if (consulta === this.consultaCondutores) this.carregandoCondutores.set(false);
    }
  }

  protected mudarAba(aba: Aba, teclado = false): void {
    this.aba.set(aba);
    this.erroAcao.set(null);
    if (aba === 'vinculos') void this.garantirSelecoesVinculo(true);
    if (teclado) document.getElementById(`aba-${aba}`)?.focus();
  }

  protected teclaAba(evento: KeyboardEvent): void {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(evento.key)) return;
    evento.preventDefault();
    const abas: Aba[] = ['clientes', 'condutores', 'vinculos'];
    const indice = abas.indexOf(this.aba());
    const nova = evento.key === 'Home' ? abas[0] : evento.key === 'End' ? abas[2]
      : abas[(indice + (evento.key === 'ArrowRight' ? 1 : -1) + abas.length) % abas.length];
    this.mudarAba(nova, true);
  }

  protected abrirNovo(): void {
    if (this.aba() === 'condutores') void this.abrirCondutor();
    else if (this.aba() === 'clientes') void this.abrirCliente();
  }

  protected async abrirCliente(cliente?: ResumoCliente): Promise<void> {
    const abertura = ++this.abertura;
    this.erroAcao.set(null);
    this.clienteAtual = null;
    this.editorTipo.set('cliente');
    this.tituloEditor.set(cliente ? 'Editar cliente' : 'Novo cliente');
    this.formularioCliente().reset(clienteVazio());
    this.prepararEditor();
    if (!cliente) {
      this.editorDisponivel.set(true);
      await this.focarPrimeiroCampo();
      return;
    }
    this.carregandoEditor.set(true);
    try {
      const atual = await this.api.consultarCliente(cliente.id);
      if (abertura !== this.abertura) return;
      this.clienteAtual = atual;
      const dados = atual.dados;
      this.formularioCliente().reset({
        tipo: dados.tipo,
        nome: dados.nome,
        documento: dados.tipo === 'PF' ? dados.cpf ?? '' : dados.cnpj ?? '',
        rg: dados.rg ?? '',
        cnh: dados.cnh ?? '',
        telefone: dados.telefone,
        email: dados.email ?? '',
        estado: dados.endereco.estado,
        cidade: dados.endereco.cidade,
        bairro: dados.endereco.bairro,
        rua: dados.endereco.rua,
        numero: dados.endereco.numero,
      });
      this.carregandoEditor.set(false);
      this.editorDisponivel.set(true);
      await this.focarPrimeiroCampo();
    } catch (erro) {
      if (abertura === this.abertura) await this.mostrarErroEditor(mensagemConflito(problemaCadastro(erro)));
    } finally {
      if (abertura === this.abertura) this.carregandoEditor.set(false);
    }
  }

  protected async abrirCondutor(condutor?: Condutor): Promise<void> {
    const abertura = ++this.abertura;
    this.erroAcao.set(null);
    this.condutorAtual = null;
    this.editorTipo.set('condutor');
    this.tituloEditor.set(condutor ? 'Editar condutor' : 'Novo condutor');
    this.formularioCondutor().reset(condutorVazio());
    this.prepararEditor();
    if (!condutor) {
      this.editorDisponivel.set(true);
      await this.focarPrimeiroCampo();
      return;
    }
    this.carregandoEditor.set(true);
    try {
      const atual = await this.api.consultarCondutor(condutor.id);
      if (abertura !== this.abertura) return;
      this.condutorAtual = atual;
      const dados = atual.dados;
      const cliente = this.todosClientes().find((item) => item.id === dados.clienteId);
      const proprio = cliente?.tipo === 'PF'
        && normalizarDocumento(cliente.cpf ?? '') === normalizarDocumento(dados.cpf)
        && normalizarCnh(cliente.cnh ?? '') === normalizarCnh(dados.cnh);
      this.formularioCondutor().reset({
        clienteId: dados.clienteId,
        proprioCliente: proprio,
        nome: dados.nome,
        email: dados.email,
        cpf: dados.cpf,
        cnh: dados.cnh,
        validadeCnh: dados.validadeCnh,
        telefone: dados.telefone,
      });
      this.carregandoEditor.set(false);
      this.editorDisponivel.set(true);
      await this.focarPrimeiroCampo();
    } catch (erro) {
      if (abertura === this.abertura) await this.mostrarErroEditor(mensagemConflito(problemaCadastro(erro)));
    } finally {
      if (abertura === this.abertura) this.carregandoEditor.set(false);
    }
  }

  protected mudarTipo(tipo: TipoCliente): void {
    if (this.modeloCliente().tipo === tipo) return;
    this.modeloCliente.update((modelo) => ({
      ...modelo, tipo, documento: '', rg: tipo === 'PF' ? modelo.rg : '', cnh: tipo === 'PF' ? modelo.cnh : '',
    }));
  }

  protected selecionarClienteCondutor(evento: Event): void {
    const clienteId = (evento.target as HTMLSelectElement).value;
    if (this.modeloCondutor().clienteId !== clienteId) {
      this.modeloCondutor.update((modelo) => ({ ...modelo, clienteId }));
    }
    if (this.modeloCondutor().proprioCliente) this.preencherProprio();
  }

  protected alternarProprio(evento: Event): void {
    const proprioCliente = (evento.target as HTMLInputElement).checked;
    this.modeloCondutor.update((modelo) => ({ ...modelo, proprioCliente }));
    if (proprioCliente) this.preencherProprio();
  }

  protected selecionarPjVinculo(evento: Event): void {
    this.pjVinculo.set((evento.target as HTMLSelectElement).value);
    void this.carregarVinculos();
  }

  protected selecionarPfVinculo(evento: Event): void {
    this.pfVinculo.set((evento.target as HTMLSelectElement).value);
  }

  protected async adicionarVinculo(): Promise<void> {
    const pj = this.pjVinculo();
    const pf = this.pfVinculo();
    this.erroVinculos.set(null);
    this.aviso.set(null);
    if (!pj || !pf) {
      this.erroVinculos.set('Selecione uma pessoa física e uma pessoa jurídica para criar o vínculo.');
      await focarResumo('erro-vinculos');
      return;
    }
    try {
      await this.api.vincular(pj, pf);
      this.aviso.set('Vínculo adicionado.');
      await this.carregarVinculos();
    } catch (erro) {
      this.erroVinculos.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-vinculos');
    }
  }

  protected async removerVinculo(vinculo: Vinculo): Promise<void> {
    this.erroVinculos.set(null);
    this.aviso.set(null);
    try {
      await this.api.desvincular(this.pjVinculo(), vinculo.id);
      this.aviso.set('Vínculo removido.');
      await this.carregarVinculos();
    } catch (erro) {
      this.erroVinculos.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-vinculos');
    }
  }

  protected async excluirCliente(cliente: ResumoCliente): Promise<void> {
    this.erroAcao.set(null);
    this.aviso.set(null);
    try {
      await this.api.excluirCliente(cliente.id);
      this.aviso.set('Cliente retirado de uso. Os vínculos PF–PJ aplicáveis foram encerrados.');
      await this.recarregarClientes();
      document.getElementById('novo-registro')?.focus();
    } catch (erro) {
      this.erroAcao.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-acao-pessoas');
    }
  }

  protected async excluirCondutor(condutor: Condutor): Promise<void> {
    this.erroAcao.set(null);
    this.aviso.set(null);
    try {
      await this.api.excluirCondutor(condutor.id);
      this.aviso.set('Condutor retirado de uso.');
      const pagina = this.condutores();
      await this.carregarCondutores(pagina.items.length === 1 && pagina.page > 1 ? pagina.page - 1 : pagina.page);
      document.getElementById('novo-registro')?.focus();
    } catch (erro) {
      this.erroAcao.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-acao-pessoas');
    }
  }

  protected fecharEditor(): void {
    if (!this.salvando()) this.editor()?.nativeElement.close();
  }

  protected cancelarEditor(evento: Event): void {
    if (this.salvando()) evento.preventDefault();
  }

  protected teclaEditor(evento: KeyboardEvent): void {
    const dialogo = this.editor()?.nativeElement;
    if (dialogo?.open) conterTabulacao(evento, dialogo);
  }

  protected editorFechado(): void {
    this.abertura++;
    if (this.origemFoco?.isConnected) this.origemFoco.focus();
    const dialogo = this.editor()?.nativeElement;
    if (dialogo?.contains(document.activeElement)) {
      document.getElementById('conteudo-pessoas')?.focus();
    }
  }

  protected invalido(campo: 'clienteId' | 'proprioCliente'): boolean {
    return this.formularioCondutor[campo]().invalid() && this.formularioCondutor[campo]().touched();
  }

  protected mascararDocumento(valor: string | null): string {
    if (!valor) return '—';
    const normalizado = normalizarDocumento(valor);
    if (normalizado.length === 11) return `***.${normalizado.slice(3, 6)}.***-**`;
    if (normalizado.length === 14) return `**.***.${normalizado.slice(5, 8)}/****-**`;
    return 'Documento protegido';
  }

  protected mascararCnh(valor: string): string {
    const normalizada = normalizarCnh(valor);
    return normalizada.length <= 3 ? '***' : `*******${normalizada.slice(-3)}`;
  }

  protected formatarData(valor: string): string {
    const [ano, mes, dia] = valor.split('-');
    return dia && mes && ano ? `${dia}/${mes}/${ano}` : valor;
  }

  protected rotuloTipo(tipo: TipoCliente): string {
    return tipo === 'PF' ? 'Pessoa física' : 'Pessoa jurídica';
  }

  private async carregarTodosClientes(): Promise<void> {
    try {
      this.todosClientes.set(await this.api.listarTodosClientes());
    } catch (erro) {
      this.erroLista.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-lista-pessoas');
    }
  }

  private async recarregarClientes(page = this.clientes().page): Promise<void> {
    await Promise.all([this.carregarClientes(page), this.carregarTodosClientes()]);
    await this.garantirSelecoesVinculo();
  }

  private async garantirSelecoesVinculo(carregar = false): Promise<void> {
    if (!this.clientesPj().some((cliente) => cliente.id === this.pjVinculo())) {
      this.pjVinculo.set(this.clientesPj()[0]?.id ?? '');
    }
    if (!this.clientesPf().some((cliente) => cliente.id === this.pfVinculo())) {
      this.pfVinculo.set(this.clientesPf()[0]?.id ?? '');
    }
    if (carregar && this.pjVinculo()) await this.carregarVinculos();
    else if (!this.pjVinculo()) this.vinculos.set([]);
  }

  private async carregarVinculos(): Promise<void> {
    const pj = this.pjVinculo();
    this.erroVinculos.set(null);
    if (!pj) {
      this.vinculos.set([]);
      return;
    }
    this.carregandoVinculos.set(true);
    try {
      this.vinculos.set(await this.api.listarVinculos(pj));
    } catch (erro) {
      this.erroVinculos.set(mensagemConflito(problemaCadastro(erro)));
      await focarResumo('erro-vinculos');
    } finally {
      this.carregandoVinculos.set(false);
    }
  }

  private preencherProprio(): void {
    const cliente = this.clienteSelecionado();
    if (cliente?.tipo !== 'PF') return;
    this.modeloCondutor.update((modelo) => ({
      ...modelo,
      nome: cliente.nome,
      cpf: cliente.cpf ?? '',
      cnh: cliente.cnh ?? '',
      telefone: cliente.telefone,
      email: cliente.email ?? modelo.email,
    }));
  }

  private prepararEditor(): void {
    this.erroEditor.set(null);
    this.editorDisponivel.set(false);
    this.carregandoEditor.set(false);
    const dialogo = this.editor()?.nativeElement;
    if (dialogo && !dialogo.open) {
      this.origemFoco = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      dialogo.showModal();
    }
  }

  private async focarPrimeiroCampo(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    this.editor()?.nativeElement.querySelector<HTMLElement>('input, select')?.focus();
  }

  private async mostrarErroEditor(mensagem: string): Promise<void> {
    this.erroEditor.set(mensagem);
    await focarResumo('erro-editor-pessoas');
  }
}

function mensagemConflito(problema: ProblemaCadastro): string {
  switch (problema.code) {
    case 'cliente_com_condutores_ativos':
      return 'Este cliente possui condutor ativo. Exclua ou transfira os condutores antes de tentar novamente. O cliente e seus vínculos foram mantidos.';
    case 'cliente_com_aluguel_aberto':
    case 'condutor_com_aluguel_aberto':
      return 'Existe um aluguel aberto relacionado a este cadastro. Os dados foram mantidos.';
    case 'vinculo_duplicado':
      return 'Este vínculo já existe entre as pessoas selecionadas.';
    default:
      return problema.title ?? 'Não foi possível concluir a operação.';
  }
}

function normalizarDocumento(valor: string): string {
  return valor.replace(/[^0-9A-Za-z]/g, '').toUpperCase();
}

function normalizarCnh(valor: string): string {
  return normalizarDocumento(valor);
}

function cpfValido(valor: string): boolean {
  const cpf = normalizarDocumento(valor);
  if (!/^\d{11}$/.test(cpf) || /^(\d)\1{10}$/.test(cpf)) return false;
  for (let tamanho = 9; tamanho <= 10; tamanho++) {
    let soma = 0;
    for (let indice = 0; indice < tamanho; indice++) soma += Number(cpf[indice]) * (tamanho + 1 - indice);
    const resto = soma % 11;
    if (Number(cpf[tamanho]) !== (resto < 2 ? 0 : 11 - resto)) return false;
  }
  return true;
}

function cnpjValido(valor: string): boolean {
  const cnpj = normalizarDocumento(valor);
  if (!/^[0-9A-Z]{12}\d{2}$/.test(cnpj) || /^(.)\1{13}$/.test(cnpj)) return false;
  for (let tamanho = 12; tamanho <= 13; tamanho++) {
    let soma = 0;
    for (let indice = tamanho - 1; indice >= 0; indice--) {
      soma += (cnpj.charCodeAt(indice) - 48) * (2 + (tamanho - 1 - indice) % 8);
    }
    const resto = soma % 11;
    if (Number(cnpj[tamanho]) !== (resto < 2 ? 0 : 11 - resto)) return false;
  }
  return true;
}

function emailValido(valor: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(valor.trim());
}

function dataValida(valor: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(valor) && !Number.isNaN(Date.parse(`${valor}T00:00:00Z`));
}
