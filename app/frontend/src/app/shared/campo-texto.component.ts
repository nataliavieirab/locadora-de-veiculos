import { Component, input } from '@angular/core';
import { FieldTree, FormField } from '@angular/forms/signals';

@Component({
  selector: 'app-campo-texto',
  imports: [FormField],
  template: `
    <div class="flex flex-col gap-1.5">
      <label class="text-sm font-semibold text-stone-900" [for]="identificador()">{{ rotulo() }}</label>
      <input
        [id]="identificador()"
        [formField]="campo()"
        [type]="tipo()"
        [attr.inputmode]="modoEntrada()"
        [attr.autocomplete]="autocomplete()"
        [attr.aria-invalid]="invalido()"
        [attr.aria-describedby]="descricao()"
        class="w-full rounded-lg border border-stone-400 bg-white px-3 py-2.5 text-stone-950"
      />
      @if (ajuda()) {
        <p [id]="'ajuda-' + identificador()" class="text-sm text-stone-600">{{ ajuda() }}</p>
      }
      @if (invalido()) {
        <p [id]="'erro-' + identificador()" class="text-sm font-medium text-red-950">
          {{ campo()().errors()[0]?.message ?? 'Revise este campo.' }}
        </p>
      }
    </div>
  `,
})
export class CampoTextoComponent {
  readonly campo = input.required<FieldTree<string>>();
  readonly identificador = input.required<string>();
  readonly rotulo = input.required<string>();
  readonly tipo = input('text');
  readonly modoEntrada = input('text');
  readonly autocomplete = input('off');
  readonly ajuda = input('');

  protected invalido(): boolean {
    return this.campo()().invalid() && this.campo()().touched();
  }

  protected descricao(): string | null {
    return [
      this.ajuda() ? `ajuda-${this.identificador()}` : '',
      this.invalido() ? `erro-${this.identificador()}` : '',
    ].filter(Boolean).join(' ') || null;
  }
}
