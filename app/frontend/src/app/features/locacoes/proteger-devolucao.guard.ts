import { CanDeactivateFn } from '@angular/router';
import { DevolucaoLocacaoComponent } from './devolucao-locacao.component';

export const protegerDevolucao: CanDeactivateFn<DevolucaoLocacaoComponent> = (componente) => !componente.navegacaoBloqueada();
