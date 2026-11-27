import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { MemoriaSessao } from '../../core/memoria-sessao';
import { focarResumo } from '../../shared/foco';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { Recurso } from './grupos-planos.service';
import { Veiculo, VeiculosService } from './veiculos.service';

@Component({
  selector: 'app-foto-veiculo',
  imports: [ResumoErroComponent],
  templateUrl: './foto-veiculo.component.html',
})
export class FotoVeiculoComponent {
  private readonly api = inject(VeiculosService);
  private readonly memoria = inject(MemoriaSessao);
  readonly recurso = input<Recurso<Veiculo> | null>(null);
  readonly novo = input(false);
  readonly alterado = input(false);
  readonly salvando = input(false);
  readonly atualizado = output<Recurso<Veiculo>>();
  readonly reconsultado = output<Recurso<Veiculo>>();
  readonly ocupado = output<boolean>();
  readonly restrito = output<boolean>();
  protected readonly arquivo = signal<File | null>(null);
  protected readonly previa = signal<string | null>(null);
  protected readonly imagem = signal<string | null>(null);
  protected readonly carregando = signal(false);
  protected readonly enviando = signal(false);
  protected readonly erroLeitura = signal(false);
  protected readonly erro = signal<string | null>(null);
  protected readonly aviso = signal<string | null>(null);
  protected readonly bloqueio = signal<string | null>(null);
  protected readonly sessaoNegada = signal(false);
  private geracao = 0;
  private leitura = 0;
  private identidadeCriada: string | null = null;
  private intencao: { veiculoId: string; etag: string; fotoAnterior: string | null; acao: 'enviar' | 'remover' } | null = null;
  protected readonly impedido = computed(() => (!this.novo() && (!this.recurso() || this.alterado())) || this.salvando()
    || this.enviando() || this.carregando() || !!this.bloqueio() || this.sessaoNegada()
    || !!this.recurso()?.dados.possuiAluguelAberto);
  protected readonly orientacao = computed(() => this.sessaoNegada()
    ? 'Sessão encerrada. Entre novamente no sistema para continuar.'
    : this.novo() ? 'A foto é opcional. Se selecionada, será enviada ao clicar em Salvar veículo.'
    : !this.recurso() ? 'A foto é opcional.'
    : this.alterado() ? 'Salve ou descarte as alterações cadastrais antes de alterar a foto.'
    : this.recurso()?.dados.possuiAluguelAberto ? 'Aluguel em aberto. A foto pode ser consultada, mas não alterada ou removida.'
    : this.bloqueio());

  constructor() {
    effect(() => this.restrito.emit(this.sessaoNegada() || !!this.bloqueio() || !!this.recurso()?.dados.possuiAluguelAberto));
    let anterior: string | null | undefined;
    effect(() => {
      const recurso = this.recurso();
      const identidade = recurso?.dados.id ?? null;
      if (identidade === anterior) return;
      const criada = anterior === null && identidade === this.identidadeCriada;
      anterior = identidade;
      this.identidadeCriada = null;
      // Só a transição coordenada null → ID confirmado preserva a seleção.
      if (criada) return;
      this.limpar();
      this.bloqueio.set(null);
      this.aviso.set(null);
      this.erro.set(null);
      if (recurso?.dados.foto && !this.sessaoNegada()) void this.carregarFoto();
    });
    effect(() => {
      if (this.memoria.accessToken()) return;
      this.limpar();
      this.sessaoNegada.set(true);
      this.bloqueio.set('Sessão encerrada. Entre novamente no sistema para continuar.');
    });
    inject(DestroyRef).onDestroy(() => this.limpar(false));
  }

  private revogar(url: string | null): void { if (url) URL.revokeObjectURL(url); }
  private limparSelecao(): void {
    this.revogar(this.previa());
    this.previa.set(null);
    this.arquivo.set(null);
  }
  private limpar(emitir = true): void {
    this.geracao++;
    this.leitura++;
    this.limparSelecao();
    this.revogar(this.imagem());
    this.imagem.set(null);
    this.carregando.set(false);
    this.erroLeitura.set(false);
    this.enviando.set(false);
    if (emitir) this.ocupado.emit(false);
    this.intencao = null;
  }

  protected selecionar(evento: Event): void {
    const input = evento.target as HTMLInputElement;
    const arquivo = input.files?.[0];
    input.value = '';
    if (!arquivo || this.impedido()) return;
    this.limparSelecao();
    this.erro.set(null);
    this.aviso.set(null);
    if (!['image/jpeg', 'image/png'].includes(arquivo.type) || arquivo.size === 0 || arquivo.size > 5242880) {
      void this.falhar('Imagem inválida. Selecione JPEG ou PNG estático, até 5 MiB e 16 megapixels.');
      return;
    }
    this.arquivo.set(arquivo);
    this.previa.set(URL.createObjectURL(arquivo));
    void this.focar(this.novo() ? 'salvar-veiculo' : 'enviar-foto');
  }

  protected descartar(): void {
    if (this.enviando() || this.salvando()) return;
    this.limparSelecao();
    this.erro.set(null);
    void this.focar('selecionar-foto');
  }

  protected async carregarFoto(): Promise<boolean> {
    const recurso = this.recurso();
    if (!recurso?.dados.foto || this.sessaoNegada()) return true;
    const geracao = this.geracao;
    const leitura = ++this.leitura;
    this.carregando.set(true);
    this.erroLeitura.set(false);
    this.revogar(this.imagem());
    this.imagem.set(null);
    try {
      const blob = await this.api.lerFoto(recurso.dados.id);
      if (geracao !== this.geracao || leitura !== this.leitura || this.sessaoNegada()) return false;
      if (!['image/png', 'image/jpeg'].includes(blob.type) || blob.size === 0 || blob.size > 5242880)
        throw new Error('Imagem indisponível.');
      this.imagem.set(URL.createObjectURL(blob));
      return true;
    } catch (erro) {
      if (geracao !== this.geracao || leitura !== this.leitura) return false;
      if (erro instanceof HttpErrorResponse && erro.status === 401) this.perderSessao();
      else this.erroLeitura.set(true);
      return false;
    } finally {
      if (geracao === this.geracao && leitura === this.leitura) this.carregando.set(false);
    }
  }

  temSelecao(): boolean { return !!this.arquivo(); }

  async concluirCadastro(atual: Recurso<Veiculo>): Promise<boolean> {
    this.identidadeCriada = atual.dados.id;
    return this.alterar(false, atual);
  }

  protected async alterar(remover = false, criado?: Recurso<Veiculo>): Promise<boolean> {
    const atual = criado ?? this.recurso();
    const arquivo = this.arquivo();
    if (!atual || (!criado && this.impedido()) || this.sessaoNegada() || this.enviando() || (!remover && !arquivo)) return false;
    const geracao = this.geracao;
    this.intencao = { veiculoId: atual.dados.id, etag: atual.etag,
      fotoAnterior: atual.dados.foto?.id ?? null, acao: remover ? 'remover' : 'enviar' };
    this.enviando.set(true);
    this.ocupado.emit(true);
    this.erro.set(null);
    this.aviso.set(remover ? 'Removendo referência da foto…' : 'Enviando e processando a imagem…');
    try {
      const resposta = remover ? null : await this.api.enviarFoto(atual.dados.id, atual.etag, arquivo!);
      const etag = remover ? await this.api.removerFoto(atual.dados.id, atual.etag) : resposta!.etag;
      if (geracao !== this.geracao || this.sessaoNegada()) return false;
      const novo = { dados: { ...atual.dados, foto: resposta?.dados ?? null }, etag };
      this.atualizado.emit(novo);
      this.limparSelecao();
      this.revogar(this.imagem());
      this.imagem.set(null);
      this.aviso.set(remover ? 'Foto removida. O veículo foi mantido.'
        : atual.dados.foto ? 'Foto substituída.' : 'Foto adicionada.');
      this.intencao = null;
      // A leitura é separada: sua falha não desfaz o resultado confirmado.
      if (!remover) await this.carregarAposAtualizar();
      void this.focar('selecionar-foto');
      return true;
    } catch (erro) {
      if (geracao !== this.geracao || this.sessaoNegada()) return false;
      this.aviso.set(null);
      const status = erro instanceof HttpErrorResponse ? erro.status : 0;
      const codigo = erro instanceof HttpErrorResponse ? (erro.error as { code?: string } | null)?.code : null;
      const indisponibilidadeConfirmada = status === 503
        && ['foto_indisponivel', 'processamento_indisponivel'].includes(codigo ?? '');
      if (status === 401) { this.perderSessao(); return false; }
      if ([409, 412, 428, 404].includes(status)) {
        this.limparSelecao();
        this.bloqueio.set('O veículo foi alterado, alugado ou retirado. Recarregue o cadastro antes de alterar a foto.');
      } else if (!status || status >= 500 && !indisponibilidadeConfirmada) {
        this.limparSelecao();
        this.bloqueio.set('Resultado não confirmado. Consulte novamente antes de alterar a foto.');
      }
      const mensagem = this.bloqueio() ?? (status === 503
        ? 'Não foi possível processar ou enviar a foto. Processamento ou armazenamento temporariamente indisponível. A foto atual e o cadastro foram preservados. Tente novamente.'
        : status === 403 ? 'Acesso negado para alterar a foto.'
        : 'Imagem inválida. Selecione JPEG ou PNG estático, até 5 MiB e 16 megapixels.');
      await this.falhar(mensagem);
      return false;
    } finally {
      if (geracao === this.geracao) { this.enviando.set(false); this.ocupado.emit(false); }
    }
  }

  private async carregarAposAtualizar(): Promise<void> {
    // Aguarda a atualização do input pelo pai, sem repetir multipart.
    await new Promise<void>(resolve => setTimeout(resolve));
    await this.carregarFoto();
  }

  protected async recarregar(): Promise<void> {
    const anterior = this.recurso();
    if (!anterior || this.enviando() || this.salvando() || this.alterado() || this.sessaoNegada()) return;
    const geracao = this.geracao;
    this.enviando.set(true);
    this.ocupado.emit(true);
    try {
      const atual = await this.api.consultar(anterior.dados.id);
      if (geracao !== this.geracao || this.sessaoNegada()) return;
      this.limparSelecao();
      this.reconsultado.emit(atual);
      this.atualizado.emit(atual);
      this.revogar(this.imagem());
      this.imagem.set(null);
      this.erroLeitura.set(false);
      await new Promise<void>(resolve => setTimeout(resolve));
      const lida = atual.dados.foto ? await this.carregarFoto() : true;
      if (geracao !== this.geracao || this.sessaoNegada()) return;
      if (lida) {
        this.bloqueio.set(null);
        this.intencao = null;
        this.erro.set(null);
        this.aviso.set('Cadastro consultado novamente. A foto exibida corresponde à referência atual. Para alterar, selecione uma imagem novamente.');
        void this.focar('selecionar-foto');
      } else this.bloqueio.set('Não foi possível consultar a foto atual. Consulte novamente antes de alterar a foto.');
    } catch (erro) {
      if (geracao !== this.geracao) return;
      if (erro instanceof HttpErrorResponse && erro.status === 401) this.perderSessao();
      else await this.falhar('Não foi possível consultar o cadastro. A alteração continua bloqueada; tente recarregar novamente.');
    } finally {
      if (geracao === this.geracao) { this.enviando.set(false); this.ocupado.emit(false); }
    }
  }

  private perderSessao(): void {
    this.limpar();
    this.sessaoNegada.set(true);
    this.aviso.set(null);
    this.erro.set(null);
    this.bloqueio.set('Sessão encerrada. Entre novamente no sistema para continuar.');
  }
  private async falhar(mensagem: string): Promise<void> {
    this.erro.set(mensagem);
    await focarResumo('erro-foto-veiculo');
  }
  protected imagemFalhou(): void {
    if (this.previa()) {
      this.limparSelecao();
      void this.falhar('Não foi possível apresentar a prévia. Selecione uma imagem JPEG ou PNG válida.');
    } else {
      this.revogar(this.imagem());
      this.imagem.set(null);
      this.erroLeitura.set(true);
    }
  }
  private async focar(id: string): Promise<void> {
    const geracao = this.geracao;
    await new Promise<void>(resolve => setTimeout(resolve));
    if (geracao === this.geracao && !this.sessaoNegada()) document.getElementById(id)?.focus();
  }
}
