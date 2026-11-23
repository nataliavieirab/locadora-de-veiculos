export async function focarResumo(identificador = 'resumo-erro'): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  document.getElementById(identificador)?.focus();
}

export function conterTabulacao(evento: KeyboardEvent, dialogo: HTMLDialogElement): void {
  if (evento.key !== 'Tab') return;
  const controles = [...dialogo.querySelectorAll<HTMLElement>(
    'button, input, select, textarea, a[href], [tabindex]',
  )].filter((elemento) => elemento.tabIndex >= 0 && !elemento.matches(':disabled')
    && elemento.getClientRects().length > 0);
  const primeiro = controles[0];
  const ultimo = controles.at(-1);
  if (!primeiro || !ultimo) {
    evento.preventDefault();
    return;
  }
  if (evento.shiftKey && document.activeElement === primeiro) {
    evento.preventDefault();
    ultimo.focus();
  } else if (!evento.shiftKey && document.activeElement === ultimo) {
    evento.preventDefault();
    primeiro.focus();
  }
}
