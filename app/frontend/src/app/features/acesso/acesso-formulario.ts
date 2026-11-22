export async function focarResumo(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  document.getElementById('resumo-erro')?.focus();
}

export function mensagemCampo(erros: readonly { message?: string }[]): string {
  return erros[0]?.message ?? 'Revise este campo.';
}
