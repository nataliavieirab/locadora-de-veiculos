import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class MemoriaSessao {
  readonly accessToken = signal<string | null>(null);
  readonly refreshToken = signal<string | null>(null);
  readonly contaId = signal<string | null>(null);
  readonly empresaId = signal<string | null>(null);
  readonly erro = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly revogacaoNaoConfirmada = signal(false);

  limparCredenciais(): void {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.contaId.set(null);
    this.empresaId.set(null);
  }
}
