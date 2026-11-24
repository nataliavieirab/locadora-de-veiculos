import { Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-shell-cadastral',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <a [href]="'#' + conteudoId()" class="fixed -left-full top-2 z-50 rounded-lg bg-stone-950 px-4 py-2 text-white focus:left-2">Ir para o conteúdo</a>
    <div class="min-h-screen bg-stone-100 md:grid md:grid-cols-[232px_1fr]">
      <aside class="flex flex-col gap-5 bg-stone-900 px-4 py-5 text-stone-50 md:gap-8 md:py-6">
        <a routerLink="/painel" class="flex items-center gap-3 px-2">
          <span class="grid size-9 place-items-center rounded-lg bg-stone-50 font-bold text-stone-950" aria-hidden="true">LV</span>
          <span><strong class="block">Locadora</strong><span class="text-sm text-stone-400">Administração</span></span>
        </a>
        <nav aria-label="Navegação cadastral" class="flex flex-wrap gap-2 md:flex-col">
          @for (destino of destinos; track destino.rota) {
            <a [routerLink]="destino.rota" routerLinkActive="bg-stone-700" ariaCurrentWhenActive="page"
              class="rounded-lg px-3 py-3 font-medium text-stone-100 underline">{{ destino.nome }}</a>
          }
        </nav>
        <p class="mt-auto hidden border-t border-stone-700 px-2 pt-4 text-sm text-stone-300 md:block">Sessão administrativa nesta aba</p>
      </aside>
      <div class="min-w-0">
        <header class="flex h-18 items-center justify-end border-b border-stone-300 bg-white px-5 sm:px-10">
          <a routerLink="/painel" class="font-medium text-blue-900 underline">Sua empresa</a>
        </header>
        <ng-content />
      </div>
    </div>
  `,
})
export class ShellCadastralComponent {
  readonly conteudoId = input.required<string>();
  protected readonly destinos = [
    { rota: '/grupos-planos', nome: 'Grupos e planos' },
    { rota: '/veiculos', nome: 'Veículos' },
    { rota: '/clientes', nome: 'Clientes e condutores' },
    { rota: '/precos-servicos', nome: 'Preços e serviços' },
    { rota: '/alugueis', nome: 'Aluguéis' },
    { rota: '/painel', nome: 'Sua empresa' },
  ];
}
