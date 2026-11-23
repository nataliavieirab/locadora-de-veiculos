import { Component, computed, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { form, FormField, FormRoot, maxLength, required, validate } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { ShellCadastralComponent } from '../../shared/shell-cadastral.component';
import { CampoTextoComponent } from '../../shared/campo-texto.component';
import { problemaCadastro } from '../../shared/erro-cadastro';
import { conterTabulacao, focarResumo } from '../../shared/foco';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { Grupo, Pagina, Recurso } from './grupos-planos.service';
import { Combustivel, Veiculo, VeiculosService } from './veiculos.service';

const vazio = () => ({
  placa: '', marca: '', cor: '', modelo: '', combustivel: 'Gasolina', capacidadeLitros: '', ano: '', grupoId: '',
});

@Component({
  selector: 'app-veiculos',
  imports: [RouterLink, ShellCadastralComponent, FormRoot, FormField, CampoTextoComponent, ResumoErroComponent],
  templateUrl: './veiculos.component.html',
})
export class VeiculosComponent implements OnInit {
  private readonly api = inject(VeiculosService);
  private readonly editor = viewChild<ElementRef<HTMLDialogElement>>('editor');
  private atual: Recurso<Veiculo> | null = null;
  private origem: HTMLElement | null = null;
  private abertura = 0;
  private consulta = 0;
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
        if (!this.editorDisponivel()) return { kind: 'carregando', message: 'Aguarde os dados do cadastro.' };
        const modelo = this.modelo();
        try {
          const [inteiro, fracao] = modelo.capacidadeLitros.replace(',', '.').split('.');
          await this.api.salvar({
            ...modelo, combustivel: modelo.combustivel as Combustivel, ano: Number(modelo.ano),
            capacidadeLitros: BigInt(inteiro).toString() + (fracao === undefined ? '' : `.${fracao}`),
          }, this.atual);
          this.editor()?.nativeElement.close();
          this.aviso.set('Veículo salvo.');
          await this.carregar(this.pagina().page);
          return;
        } catch (erro) {
          const problema = problemaCadastro(erro);
          await this.mostrarErroEditor(problema.title!);
          const erros = Object.entries(problema.errors ?? {});
          return erros.length ? erros.map(([nome, mensagens]) => ({
            kind: 'servidor', message: mensagens[0], fieldTree: campos[nome as keyof ReturnType<typeof vazio>] ?? campos,
          })) : { kind: 'servidor', message: problema.title! };
        }
      },
    },
  });
  protected readonly salvando = computed(() => this.formulario().submitting());

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
        this.atual = atual;
        const { id: _id, ...dados } = atual.dados;
        this.formulario().reset({ ...dados, ano: dados.ano.toString(), capacidadeLitros: dados.capacidadeLitros.replace('.', ',') });
      }
      if (abertura !== this.abertura) return;
      this.carregandoEditor.set(false);
      this.editorDisponivel.set(true);
      await new Promise((resolve) => setTimeout(resolve));
      if (abertura === this.abertura) this.editor()?.nativeElement.querySelector<HTMLInputElement>('input')?.focus();
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
    if (!this.salvando()) this.editor()?.nativeElement.close();
  }

  protected cancelar(evento: Event): void {
    if (this.salvando()) evento.preventDefault();
  }

  protected fechado(): void {
    this.abertura++;
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

  private async mostrarErroEditor(mensagem: string): Promise<void> {
    this.erroEditor.set(mensagem);
    await focarResumo('erro-editor-veiculos');
  }
}
