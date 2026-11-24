import { CanDeactivateFn } from '@angular/router';
import type { AlugueisComponent } from './alugueis.component';

export const protegerContratacao: CanDeactivateFn<AlugueisComponent> = (componente) => componente.podeSair();
