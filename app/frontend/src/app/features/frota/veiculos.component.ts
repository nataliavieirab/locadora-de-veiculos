import { Component, computed, effect, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { form, FormField, FormRoot, maxLength, required, validate } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { ShellCadastralComponent } from '../../shared/shell-cadastral.component';
import { CampoTextoComponent } from '../../shared/campo-texto.component';
import { problemaCadastro } from '../../shared/erro-cadastro';
import { conterTabulacao, focarResumo } from '../../shared/foco';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { Grupo, Pagina, Recurso } from './grupos-planos.service';
import { Combustivel, Veiculo, VeiculosService } from './veiculos.service';
import { FotoVeiculoComponent } from './foto-veiculo.component';

const vazio = () => ({
  placa: '', marca: '', cor: '', modelo: '', combustivel: 'Gasolina', capacidadeLitros: '', ano: '', grupoId: '',
});

@Component({
  selector: 'app-veiculos',
  imports: [RouterLink, ShellCadastralComponent, FormRoot, FormField, CampoTextoComponent, ResumoErroComponent, FotoVeiculoComponent],
  templateUrl: './veiculos.component.html',
})
export class VeiculosComponent implements OnInit {
  private readonly api = inject(VeiculosService);
  private readonly editor = viewChild<ElementRef<HTMLDialogElement>>('editor');
  private readonly fotoEditor = viewChild(FotoVeiculoComponent);
  protected readonly cadastroNovo = signal(true);
  private readonly coordenando = signal(false);
  private resultadoParcial = false;
  private atual: Recurso<Veiculo> | null = null;
  protected readonly recurso = signal<Recurso<Veiculo> | null>(null);
  protected readonly fotoOcupada = signal(false);
  protected readonly fotoRestrita = signal(false);
  protected readonly avisoCadastro = signal<string | null>(null);
  protected readonly ocupado = computed(() => this.salvando() || this.fotoOcupada());
  private origem: HTMLElement | null = null;
  private abertura = 0;
  private consulta = 0;
  private readonly focoFotoPendente = signal(false);
  private readonly modelo = signal(vazio());
  protected readonly pagina = signal<Pagina<Veiculo>>({ items: [], page: 1, pageSize: 20, total: 0 });
  protected readonly grupos = signal<Grupo[]>([]);
  protected readonly filtro = signal('');
  protected readonly agrupado = signal(false);
  protected readonly carregando = signal(true);
  protected readonly erroLista = signal<string | null>(null);
  protected readonly erroEditor = signal<string | null>(null);
  protected readonly aviso = signal<string | null>(null);
  protected readonly titulo = signal('Novo veículo');
  protected readonly carregandoEditor = signal(false);
  protected readonly editorDisponivel = signal(false);
  protected readonly combustiveis: Combustivel[] = ['Gasolina', 'Gas', 'Diesel', 'Alcool'];
  protected readonly rotulosCombustivel: Record<Combustivel, string> = { Gasolina: 'Gasolina', Gas: 'Gás', Diesel: 'Diesel', Alcool: 'Álcool' };
  protected readonly secoes = computed(() => {
    if (!this.agrupado()) return [{ id: '', nome: '', itens: this.pagina().items }];
    const porGrupo = new Map<string, Veiculo[]>();
    for (const veiculo of this.pagina().items) {
      const itens = porGrupo.get(veiculo.grupoId) ?? [];
      itens.push(veiculo);
      porGrupo.set(veiculo.grupoId, itens);
    }
    return [...porGrupo].map(([id, itens]) => ({ id, nome: this.nomeGrupo(id), itens }))
      .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR') || a.id.localeCompare(b.id));
  });
  protected readonly formulario = form(this.modelo, (caminho) => {
    for (const campo of [caminho.placa, caminho.marca, caminho.cor, caminho.modelo]) {
      required(campo, { message: 'Preencha este campo.' });
      validate(campo, ({ value }) => value().trim() ? null : { kind: 'obrigatorio', message: 'Preencha este campo.' });
    }
    maxLength(caminho.placa, 16, { message: 'A placa pode ter até 16 caracteres.' });
    for (const campo of [caminho.marca, caminho.cor, caminho.modelo]) {
      maxLength(campo, 100, { message: 'Este campo pode ter até 100 caracteres.' });
    }
    validate(caminho.placa, ({ value }) => /[a-z0-9]/i.test(value()) ? null
      : { kind: 'placa', message: 'Informe uma placa válida.' });
    required(caminho.grupoId, { message: 'Selecione um grupo.' });
    required(caminho.capacidadeLitros, { message: 'Informe a capacidade em litros.' });
    required(caminho.ano, { message: 'Informe o ano.' });
    validate(caminho.capacidadeLitros, ({ value }) => {
      const valor = value().replace(',', '.');
      if (!valor) return null;
      if (/^\d+(?:\.\d{1,3})?$/.test(valor) && BigInt(valor.split('.')[0]) <= 999999999n
        && BigInt(valor.replace('.', '')) > 0n) return null;
      return { kind: 'capacidade', message: 'Informe capacidade positiva, até nove dígitos inteiros e três casas decimais.' };
    });
    validate(caminho.ano, ({ value }) => !value() || /^\d+$/.test(value()) && BigInt(value()) > 0n && BigInt(value()) <= 2147483647n
      ? null : { kind: 'ano', message: 'Informe um ano inteiro positivo.' });
    validate(caminho.combustivel, ({ value }) => this.combustiveis.includes(value() as Combustivel)
      ? null : { kind: 'combustivel', message: 'Selecione o combustível.' });
  }, {
    submission: {
      onInvalid: () => this.mostrarErroEditor('Revise os campos indicados antes de salvar.'),
      action: async (campos) => {
        if (!this.editorDisponivel() || this.coordenando() || this.fotoOcupada() || this.fotoRestrita()) return { kind: 'carregando', message: 'Aguarde os dados ou recarregue o cadastro.' };
        this.coordenando.set(true);
        const modelo = this.modelo();
        const foto = this.fotoEditor();
        const enviarFoto = this.cadastroNovo() && !!foto?.temSelecao();
        this.erroEditor.set(null);
        this.avisoCadastro.set(null);
        this.aviso.set(null);
        try {
          const [inteiro, fracao] = modelo.capacidadeLitros.replace(',', '.').split('.');
          const atual = await this.api.salvar({
            ...modelo, combustivel: modelo.combustivel as Combustivel, ano: Number(modelo.ano),
            capacidadeLitros: BigInt(inteiro).toString() + (fracao === undefined ? '' : `.${fracao}`),
          }, this.atual);
          this.atualizarRecurso(atual);
          this.preencherCadastro(atual);
          this.titulo.set('Editar veículo');
          const fotoConfirmada = !enviarFoto || await foto!.concluirCadastro(atual);
          this.cadastroNovo.set(false);
          if (fotoConfirmada) {
            this.avisoCadastro.set(enviarFoto ? 'Veículo e foto salvos.' : 'Veículo salvo. A foto é opcional.');
            this.aviso.set(enviarFoto ? 'Veículo e foto salvos.' : 'Veículo salvo.');
          } else {
            this.resultadoParcial = true;
            await this.mostrarErroEditor('Veículo salvo, mas o envio da foto não foi concluído ou confirmado. Revise o resultado da foto abaixo antes de continuar. Não é necessário cadastrar o veículo novamente.');
          }
          await this.carregar(this.pagina().page);
          if (fotoConfirmada) this.focoFotoPendente.set(true);
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          await this.mostrarErroEditor(problema.title!);
          const erros = Object.entries(problema.errors ?? {});
          return erros.length ? erros.map(([nome, mensagens]) => ({
            kind: 'servidor', message: mensagens[0], fieldTree: campos[nome as keyof ReturnType<typeof vazio>] ?? campos,
          })) : { kind: 'servidor', message: problema.title! };
        } finally {
          this.coordenando.set(false);
        }
      },
    },
  });
  protected readonly salvando = computed(() => this.formulario().submitting() || this.coordenando());

  constructor() {
    effect(() => {
      if (!this.focoFotoPendente() || this.salvando() || this.fotoOcupada()) return;
      const abertura = this.abertura;
      this.focoFotoPendente.set(false);
      setTimeout(() => {
        if (abertura !== this.abertura) return;
        const botao = this.editor()?.nativeElement.querySelector<HTMLButtonElement>('#selecionar-foto');
        if (botao && !botao.disabled) botao.focus();
      });
    });
  }

  async ngOnInit(): Promise<void> {
    await this.carregar();
  }

  protected async carregar(page = 1): Promise<void> {
    const consulta = ++this.consulta;
    this.carregando.set(true);
    this.erroLista.set(null);
    try {
      const [pagina, grupos] = await Promise.all([this.api.listar(page, this.filtro()), this.api.listarGrupos()]);
      if (consulta !== this.consulta) return;
      this.pagina.set(pagina);
      this.grupos.set(grupos);
    } catch (erro) {
      if (consulta !== this.consulta) return;
      this.erroLista.set(problemaCadastro(erro).title!);
      await focarResumo('erro-lista-veiculos');
    } finally {
      if (consulta === this.consulta) this.carregando.set(false);
    }
  }

  protected async filtrar(evento?: Event): Promise<void> {
    this.filtro.set(evento ? (evento.target as HTMLSelectElement).value : '');
    await this.carregar();
  }

  protected alternarAgrupamento(evento: Event): void {
    this.agrupado.set((evento.target as HTMLInputElement).checked);
  }

  protected nomeGrupo(id: string): string {
    return this.grupos().find((grupo) => grupo.id === id)?.nome ?? id;
  }

  protected async abrir(veiculo?: Veiculo): Promise<void> {
    const abertura = ++this.abertura;
    this.atual = null;
    this.recurso.set(null);
    this.resultadoParcial = false;
    this.cadastroNovo.set(!veiculo);
    this.avisoCadastro.set(null);
    this.titulo.set(veiculo ? 'Editar veículo' : 'Novo veículo');
    this.erroEditor.set(null);
    this.editorDisponivel.set(false);
    this.carregandoEditor.set(!!veiculo);
    this.formulario().reset({ ...vazio(), grupoId: this.filtro() });
    this.origem = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.editor()?.nativeElement.showModal();
    try {
      if (veiculo) {
        const atual = await this.api.consultar(veiculo.id);
        if (abertura !== this.abertura) return;
        this.atualizarRecurso(atual);
        this.preencherCadastro(atual);
      }
      if (abertura !== this.abertura) return;
      this.carregandoEditor.set(false);
      this.editorDisponivel.set(true);
      await new Promise((resolve) => setTimeout(resolve));
      if (abertura === this.abertura) this.editor()?.nativeElement.querySelector<HTMLInputElement>('#placa-veiculo')?.focus();
    } catch (erro) {
      if (abertura === this.abertura) await this.mostrarErroEditor(problemaCadastro(erro).title!);
    } finally {
      if (abertura === this.abertura) this.carregandoEditor.set(false);
    }
  }

  protected async excluir(veiculo: Veiculo): Promise<void> {
    this.erroLista.set(null);
    this.aviso.set(null);
    try {
      await this.api.excluir(veiculo.id);
      this.aviso.set('Veículo retirado de uso.');
      const pagina = this.pagina();
      await this.carregar(pagina.items.length === 1 && pagina.page > 1 ? pagina.page - 1 : pagina.page);
      document.getElementById('novo-veiculo')?.focus();
    } catch (erro) {
      this.erroLista.set(problemaCadastro(erro).title!);
      await focarResumo('erro-lista-veiculos');
    }
  }

  protected fechar(): void {
    if (!this.ocupado()) this.editor()?.nativeElement.close();
  }

  protected cancelar(evento: Event): void {
    if (this.ocupado()) evento.preventDefault();
  }

  protected fechado(): void {
    this.abertura++;
    this.editorDisponivel.set(false);
    this.atual = null;
    this.recurso.set(null);
    if (this.origem?.isConnected) this.origem.focus();
    else document.getElementById('novo-veiculo')?.focus();
  }

  protected teclaEditor(evento: KeyboardEvent): void {
    const dialogo = this.editor()?.nativeElement;
    if (dialogo) conterTabulacao(evento, dialogo);
  }

  protected invalido(campo: 'grupoId' | 'combustivel'): boolean {
    return this.formulario[campo]().invalid() && this.formulario[campo]().touched();
  }

  protected atualizarRecurso(atual: Recurso<Veiculo>): void {
    this.atual = atual;
    this.recurso.set(atual);
  }

  protected atualizarFoto(atual: Recurso<Veiculo>): void {
    this.atualizarRecurso(atual);
    if (this.resultadoParcial) {
      this.resultadoParcial = false;
      this.erroEditor.set(null);
    }
  }

  protected preencherCadastro(atual: Recurso<Veiculo>): void {
    const dados = atual.dados;
    this.formulario().reset({
      placa: dados.placa, marca: dados.marca, cor: dados.cor, modelo: dados.modelo,
      combustivel: dados.combustivel, grupoId: dados.grupoId, ano: dados.ano.toString(),
      capacidadeLitros: dados.capacidadeLitros.replace('.', ','),
    });
  }

  protected descartarCadastro(): void {
    if (this.atual && !this.ocupado()) this.preencherCadastro(this.atual);
  }

  private async mostrarErroEditor(mensagem: string): Promise<void> {
    this.erroEditor.set(mensagem);
    await focarResumo('erro-editor-veiculos');
  }
}
