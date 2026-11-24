import { Component, input } from '@angular/core';
import { MoedaBrlPipe } from '../../shared/moeda-brl.pipe';
import { formatarInstanteSp } from '../../shared/horario-sao-paulo';
import { ApuracaoDevolucao } from './devolucao.contratos';

@Component({
  selector: 'app-apuracao-devolucao',
  imports: [MoedaBrlPipe],
  template: `
    <section class="overflow-hidden rounded-xl border border-stone-300 bg-white" aria-label="Demonstrativo da devolução">
      <header class="border-b border-stone-200 p-5"><p class="text-xs font-bold tracking-widest text-blue-900 uppercase">Demonstrativo</p>
        <h2 class="mt-2 text-lg font-semibold">{{ final() ? 'Demonstrativo final' : 'Prévia da devolução' }}</h2>
        <p class="text-xs text-stone-600">{{ final() ? 'Final imutável · Valores preservados no fechamento.' : 'Ainda não concluído. Revise antes de confirmar.' }}</p>
      </header>
      <div class="space-y-4 p-5">
        <dl class="grid grid-cols-2 gap-3 rounded-lg bg-stone-50 p-3 text-sm">
          <div><dt>Duração efetiva</dt><dd class="font-bold">{{ dados().diarias }} diárias</dd></div>
          <div><dt>Distância percorrida</dt><dd class="font-bold">{{ dados().quilometros }} km</dd></div>
        </dl>
        @if (dados().franquiaTotalKm !== null) { <p class="text-sm">Franquia: {{ dados().franquiaTotalKm }} km · Excedente: {{ dados().excedenteKm }} km</p> }
        @for (c of dados().componentes; track $index) {
          <div class="flex justify-between gap-3 text-sm" [class.border-t]="c.categoria === 'Combustivel'" [class.pt-4]="c.categoria === 'Combustivel'">
            <span>{{ c.nome }}
              @if (c.categoria !== 'Multa') { <small class="block text-xs text-stone-600">{{ quantidade(c.quantidade) }} × {{ c.precoUnitario | moedaBrl }}</small> }
              @if (c.origemInclusao) { <small class="block text-xs text-blue-900">{{ c.origemInclusao === 'Devolucao' ? 'Incluída na devolução' : 'Preço contratado preservado' }}</small> }
              @if (c.categoria === 'Plano') { <small class="block text-xs text-stone-600">Termos brutos: diárias {{ quantidade(dados().termoDiariasBruto) }} + km {{ quantidade(dados().termoKmBruto) }}</small> }
              @if (c.categoria === 'Multa') {
                <small class="block text-xs text-stone-600">
                  @if (dados().emAtraso) { Multa única de 10% sobre {{ subtotalPlano() | moedaBrl }} do plano arredondado. Não incide sobre adicionais. }
                  @else { Sem atraso · retorno não posterior ao previsto. }
                </small>
              }
            </span><strong class="whitespace-nowrap tabular-nums">{{ c.valorArredondado | moedaBrl }}</strong>
          </div>
        }
        <p class="text-xs text-stone-600">{{ dados().combustivel.tipo }} · {{ quantidade(dados().litrosRepostos) }} L repostos · preço {{ dados().combustivel.precoUnitario | moedaBrl }}/L
          · versão {{ dados().combustivel.id }} · vigente desde {{ formatarSp(dados().combustivel.inicioVigenciaUtc) }}</p>
        <div class="border-t border-stone-300 pt-4"><span class="text-sm font-semibold">{{ final() ? 'Total final da locação' : 'Total da prévia' }}</span>
          <p class="text-3xl font-bold tabular-nums">{{ dados().totalFinal | moedaBrl }}</p><p class="text-xs text-stone-600">Regra {{ dados().versaoRegra }}</p></div>
      </div>
      <footer class="border-t border-stone-300 bg-stone-50 p-5"><div class="flex justify-between gap-3"><span>Garantia · caução fixa</span><strong>{{ dados().caucao | moedaBrl }}</strong></div>
        <p class="mt-2 text-xs text-stone-600">Separada do custo. Não soma, não abate e não é movimentada nesta operação.</p></footer>
    </section>
  `,
})
export class ApuracaoDevolucaoComponent {
  readonly dados = input.required<ApuracaoDevolucao>();
  readonly final = input(false);
  protected readonly formatarSp = formatarInstanteSp;
  protected quantidade(valor: string): string { return valor.replace('.', ','); }
  protected subtotalPlano(): string { return this.dados().componentes.find(c => c.categoria === 'Plano')?.valorArredondado ?? '0.00'; }
}
