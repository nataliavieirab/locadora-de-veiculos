import { Component, input } from '@angular/core';
import { LucideCircleX } from '@lucide/angular';

@Component({
  selector: 'app-resumo-erro',
  imports: [LucideCircleX],
  template: `
    @if (mensagem()) {
      <div
        id="resumo-erro"
        tabindex="-1"
        role="alert"
        class="flex gap-2 rounded-md border border-red-800 bg-red-50 p-3 text-red-950"
      >
        <svg lucideCircleX [size]="20" class="mt-0.5 shrink-0" aria-hidden="true"></svg>
        <p>{{ mensagem() }}</p>
      </div>
    }
  `,
})
export class ResumoErroComponent {
  readonly mensagem = input<string | null>(null);
}
